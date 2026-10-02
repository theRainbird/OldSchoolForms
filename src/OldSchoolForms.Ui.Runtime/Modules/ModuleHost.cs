using Jint.Native;
using Jint.Native.Object;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Runtime.Facade;
using OldSchoolForms.Ui.Runtime.Scripting;
using OldSchoolForms.Ui.Runtime.Serialization;
using OldSchoolForms.Ui.Runtime.Sources;

namespace OldSchoolForms.Ui.Runtime.Modules;

/// <summary>
/// Orchestriert den Lebenszyklus der JavaScript-Module: Öffnen (Layout laden,
/// Skript importieren, <c>init</c>/<c>activate</c> aufruf), Wechsel, Schließen
/// und Hot-Reload. Jedes Modul erhält einen eigenen Tab im Content-Bereich.
/// </summary>
public sealed class ModuleHost
{
    private sealed class LoadedModule
    {
        public ModuleDescriptor Descriptor = null!;
        public TabPage Tab = null!;
        public ObjectInstance Namespace = null!;
        public List<JsControl> Facades { get; } = new();
        public string ScriptKey = null!;
    }

    private readonly IAppSource _source;
    private readonly JsEngineHost _engine;
    private readonly RuntimeLayoutLoader _loader;
    private readonly ModuleRegistry _registry;
    private readonly TabControl _contentTabs;
    private readonly JsForm _shellForm;
    private readonly Hosting.HostServices _host;
    private readonly Dictionary<string, LoadedModule> _open = new(StringComparer.OrdinalIgnoreCase);
    private string? _activeId;
    private int _reloadCounter;

    /// <summary>
    /// Erzeugt den Modul-Host.
    /// </summary>
    /// <param name="source">Die Paketquelle.</param>
    /// <param name="engine">Die JS-Engine.</param>
    /// <param name="loader">Der Layout-Loader.</param>
    /// <param name="registry">Die Modul-Registry.</param>
    /// <param name="contentTabs">Der TabControl für die Modul-Tabs.</param>
    /// <param name="shellForm">Die Fassade des Hauptfensters.</param>
    /// <param name="host">Die Host-Services für JS.</param>
    public ModuleHost(
        IAppSource source,
        JsEngineHost engine,
        RuntimeLayoutLoader loader,
        ModuleRegistry registry,
        TabControl contentTabs,
        JsForm shellForm,
        Hosting.HostServices host)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(contentTabs);
        ArgumentNullException.ThrowIfNull(shellForm);
        ArgumentNullException.ThrowIfNull(host);

        _source = source;
        _engine = engine;
        _loader = loader;
        _registry = registry;
        _contentTabs = contentTabs;
        _shellForm = shellForm;
        _host = host;
    }

    /// <summary>Die aktuell geöffneten Modul-IDs.</summary>
    public IReadOnlyCollection<string> OpenModuleIds => _open.Keys;

    /// <summary>
    /// Öffnet ein Modul in einem neuen Tab (oder wählt den vorhandenen).
    /// </summary>
    /// <param name="id">Die Modul-ID.</param>
    /// <exception cref="InvalidOperationException">Das Modul ist unbekannt.</exception>
    public void OpenModule(string id)
    {
        var descriptor = _registry.Find(id)
            ?? throw new InvalidOperationException($"Unbekanntes Modul: {id}");

        if (_open.TryGetValue(id, out var existing))
        {
            var idx = _contentTabs.TabPages.IndexOf(existing.Tab);
            if (idx >= 0) _contentTabs.SelectedIndex = idx;
            Activate(id);
            return;
        }

        var tab = new TabPage { Text = descriptor.Title };

        LayoutLoadResult layoutResult;
        if (_source.Exists(descriptor.LayoutPath))
        {
            layoutResult = _loader.LoadInto(tab, _source.ReadText(descriptor.LayoutPath));
            foreach (var warning in layoutResult.Warnings)
                _engine.ReportError($"Layout {descriptor.LayoutPath}", new InvalidOperationException(warning));
        }
        else
        {
            layoutResult = new LayoutLoadResult(
                new Dictionary<string, Control>(),
                new[] { $"Kein Layout gefunden: {descriptor.LayoutPath}" });
        }

        var scriptKey = descriptor.ScriptPath;
        if (_engine.DevMode)
        {
            _reloadCounter++;
            scriptKey = descriptor.ScriptPath + "?v=" + _reloadCounter;
        }

        var loaded = new LoadedModule
        {
            Descriptor = descriptor,
            Tab = tab,
            ScriptKey = scriptKey
        };

        var context = BuildContext(tab, layoutResult, loaded);

        if (_source.Exists(descriptor.ScriptPath))
        {
            try
            {
                loaded.Namespace = _engine.ImportModule(scriptKey);
                _engine.CallExport(loaded.Namespace, "init", context);
            }
            catch (Exception ex)
            {
                _engine.ReportError($"Modul-Init '{id}'", ex);
            }
        }

        _contentTabs.AddTabPage(tab);
        _contentTabs.SelectedIndex = _contentTabs.TabPages.Count - 1;
        _open[id] = loaded;
        Activate(id);
    }

    /// <summary>
    /// Schließt ein Modul und entfernt seinen Tab.
    /// </summary>
    /// <param name="id">Die Modul-ID.</param>
    public void CloseModule(string id)
    {
        if (!_open.TryGetValue(id, out var loaded)) return;

        try
        {
            _engine.CallExport(loaded.Namespace, "dispose");
        }
        catch (Exception ex)
        {
            _engine.ReportError($"Modul-Dispose '{id}'", ex);
        }

        foreach (var facade in loaded.Facades)
            facade.OffAll();

        _contentTabs.RemoveTabPage(loaded.Tab);
        _open.Remove(id);
        if (_activeId == id) _activeId = null;
    }

    /// <summary>
    /// Lädt ein Modul neu (Hot Reload): Dispose, Re-Import, Re-Init.
    /// </summary>
    /// <param name="id">Die Modul-ID.</param>
    public void ReloadModule(string id)
    {
        if (!_open.TryGetValue(id, out var loaded)) return;

        try
        {
            _engine.CallExport(loaded.Namespace, "dispose");
        }
        catch (Exception ex)
        {
            _engine.ReportError($"Modul-Dispose (Reload) '{id}'", ex);
        }

        foreach (var facade in loaded.Facades)
            facade.OffAll();

        _contentTabs.RemoveTabPage(loaded.Tab);
        _open.Remove(id);
        if (_activeId == id) _activeId = null;

        OpenModule(id);
    }

    /// <summary>
    /// Aktiviert ein geöffnetes Modul (Callback <c>activate</c>).
    /// </summary>
    /// <param name="id">Die Modul-ID.</param>
    public void Activate(string id)
    {
        if (_activeId == id) return;
        if (_activeId != null)
            Deactivate(_activeId);

        if (_open.TryGetValue(id, out var loaded))
        {
            try
            {
                _engine.CallExport(loaded.Namespace, "activate");
            }
            catch (Exception ex)
            {
                _engine.ReportError($"Modul-Activate '{id}'", ex);
            }
        }

        _activeId = id;
    }

    /// <summary>
    /// Deaktiviert ein geöffnetes Modul (Callback <c>deactivate</c>).
    /// </summary>
    /// <param name="id">Die Modul-ID.</param>
    public void Deactivate(string id)
    {
        if (_open.TryGetValue(id, out var loaded))
        {
            try
            {
                _engine.CallExport(loaded.Namespace, "deactivate");
            }
            catch (Exception ex)
            {
                _engine.ReportError($"Modul-Deactivate '{id}'", ex);
            }
        }

        if (_activeId == id) _activeId = null;
    }

    /// <summary>
    /// Verknüpft die Tab-Auswahl mit den Modul-Aktivitäts-Callbacks.
    /// </summary>
    public void AttachTabActivation()
    {
        _contentTabs.SelectedIndexChanged += (s, e) =>
        {
            var selected = _contentTabs.SelectedTab;
            if (selected == null) return;
            var id = _open.FirstOrDefault(kv => kv.Value.Tab == selected).Key;
            if (id != null)
                Activate(id);
        };
    }

    private ObjectInstance BuildContext(TabPage tab, LayoutLoadResult layout, LoadedModule loaded)
    {
        var ctx = _engine.NewObject();

        var controls = _engine.NewObject();
        foreach (var kvp in layout.Controls)
        {
            var facade = new JsControl(_engine, kvp.Value);
            loaded.Facades.Add(facade);
            controls.Set(kvp.Key, JsValue.FromObject(_engine.Engine, facade));
        }

        var app = _engine.NewObject();
        app.Set("name", JsValue.FromObject(_engine.Engine, _host.AppName));
        app.Set("version", JsValue.FromObject(_engine.Engine, _host.AppVersion));

        ctx.Set("form", JsValue.FromObject(_engine.Engine, _shellForm));
        ctx.Set("controls", controls);
        ctx.Set("host", JsValue.FromObject(_engine.Engine, _host));
        ctx.Set("tab", JsValue.FromObject(_engine.Engine, new JsControl(_engine, tab)));
        ctx.Set("app", app);

        return ctx;
    }
}
