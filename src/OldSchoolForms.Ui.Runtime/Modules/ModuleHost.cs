using Jint;
using Jint.Native;
using Jint.Native.Object;
using OldSchoolForms.Shared.Scripting;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Reports;
using OldSchoolForms.Ui.Runtime.Facade;
using OldSchoolForms.Ui.Runtime.Scripting;
using OldSchoolForms.Ui.Runtime.Serialization;
using OldSchoolForms.Ui.Runtime.Sources;

namespace OldSchoolForms.Ui.Runtime.Modules;

/// <summary>
/// Describes the form in which a module script is written.
/// </summary>
public enum ModuleMode
{
    /// <summary>Function-based: exported <c>init</c>/<c>activate</c>/<c>deactivate</c>/<c>dispose</c> functions.</summary>
    Function,

    /// <summary>Class-based: <c>export default class</c>, constructor = init, methods = lifecycle.</summary>
    Class
}

/// <summary>
/// Orchestrates the lifecycle of the JavaScript modules: opening (loading the
/// layout, importing the script, calling <c>init</c>/<c>activate</c>), switching,
/// closing, and hot reload. Each module gets its own tab in the content area.
/// Modules can be function-based or an ES6 class (<c>export default class</c>);
/// the form is detected automatically.
/// </summary>
public sealed class ModuleHost
{
    private sealed class LoadedModule
    {
        public ModuleDescriptor Descriptor = null!;
        public TabPage Tab = null!;
        public ObjectInstance Namespace = null!;
        public ObjectInstance? Instance;
        public ModuleMode Mode = ModuleMode.Function;
        public List<JsControl> Facades { get; } = new();
        public List<Facade.JsToolStripItem> StripFacades { get; } = new();
        public List<Facade.JsBindingSource> BindingSources { get; } = new();
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
    /// Creates the module host.
    /// </summary>
    /// <param name="source">The package source.</param>
    /// <param name="engine">The JS engine.</param>
    /// <param name="loader">The layout loader.</param>
    /// <param name="registry">The module registry.</param>
    /// <param name="contentTabs">The TabControl for the module tabs.</param>
    /// <param name="shellForm">The facade of the main window.</param>
    /// <param name="host">The host services for JS.</param>
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

    /// <summary>The currently open module IDs.</summary>
    public IReadOnlyCollection<string> OpenModuleIds => _open.Keys;

    /// <summary>
    /// The remote services facade exposed to modules as <c>ctx.remote</c>
    /// (set after construction when a server endpoint is configured).
    /// </summary>
    public Remote.RemoteServices? Remote { get; set; }

    /// <summary>
    /// Opens a module in a new tab (or selects the existing one).
    /// </summary>
    /// <param name="id">The module ID.</param>
    /// <exception cref="InvalidOperationException">The module is unknown.</exception>
    public void OpenModule(string id)
    {
        var descriptor = _registry.Find(id)
            ?? throw new InvalidOperationException(string.Format(LangRes.GetEnglish("Runtime_UnknownModule"), id));

        if (_open.TryGetValue(id, out var existing))
        {
            var idx = _contentTabs.TabPages.IndexOf(existing.Tab);
            if (idx >= 0) _contentTabs.SelectedIndex = idx;
            Activate(id);
            return;
        }

        var tab = new TabPage { Text = descriptor.GetTitle(_engine.I18n) };

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

        // Delegate that binds a class method <c>this.loadLayout(path)</c> to the
        // C# loader. Without an argument → the module's default layout.
        Func<JsValue, JsValue> loadLayout = pathJs =>
        {
            string? rel = null;
            if (pathJs is not null && !pathJs.IsUndefined() && !pathJs.IsNull())
                rel = pathJs.ToString();
            return LoadLayoutIntoTab(tab, descriptor, loaded, rel);
        };

        // Delegate that binds a class method <c>this.createControl(type, props)</c>
        // to the C# control factory. Creates a control, applies props, tracks the facade.
        Func<string, JsValue, JsValue> createControl = (typeName, propsJs) =>
        {
            var control = _loader.CreateControl(typeName);
            if (control == null)
            {
                _engine.ReportError($"createControl '{typeName}'",
                    new InvalidOperationException(string.Format(LangRes.GetEnglish("Runtime_UnknownControlType"), typeName)));
                return JsValue.Undefined;
            }

            if (propsJs is ObjectInstance propsObj)
            {
                var warnings = new List<string>();
                _loader.ApplyPropsFromJs(_engine.Engine, control, propsObj, warnings);
                foreach (var w in warnings)
                    _engine.ReportError($"createControl '{typeName}'", new InvalidOperationException(w));
            }

            var facade = JsFacadeRegistry.WrapControl(_engine, control);
            facade.OwnerModuleId = descriptor.Id;
            loaded.Facades.Add(facade);
            return JsValue.FromObject(_engine.Engine, facade);
        };

        if (_source.Exists(descriptor.ScriptPath))
        {
            try
            {
                using var scope = _engine.UseModule(id);
                loaded.Namespace = _engine.ImportModule(scriptKey);
                if (IsClassModule(loaded.Namespace))
                {
                    // Class mode: the host does NOT preload; the class calls
                    // this.loadLayout() in its constructor. this.controls starts empty.
                    loaded.Mode = ModuleMode.Class;
                    var context = BuildContext(tab, _engine.NewObject(), loaded, loadLayout, createControl);
                    if (_engine.TryConstructDefault(loaded.Namespace, context, out var instance))
                        loaded.Instance = instance;
                }
                else
                {
                    // Function mode (legacy): the layout is preloaded and controls are populated.
                    loaded.Mode = ModuleMode.Function;
                    var controls = LoadLayoutIntoTab(tab, descriptor, loaded, null);
                    var context = BuildContext(tab, controls, loaded, loadLayout, createControl);
                    _engine.CallExport(loaded.Namespace, "init", context);
                }
            }
            catch (Exception ex)
            {
                _engine.ReportError($"Module init '{id}'", ex);
            }
        }
        else
        {
            // No script: still load the layout so the tab shows content.
            LoadLayoutIntoTab(tab, descriptor, loaded, null);
        }

        _contentTabs.AddTabPage(tab);
        _contentTabs.SelectedIndex = _contentTabs.TabPages.Count - 1;
        _open[id] = loaded;
        Activate(id);
    }

    /// <summary>
    /// Opens a report in a new tab with a <see cref="ReportViewer"/>. The viewer
    /// provides a toolbar with print/export and a scrollable page preview.
    /// </summary>
    /// <param name="report">The report to display.</param>
    /// <param name="title">Optional tab title; defaults to the report name.</param>
    /// <returns>The created tab page.</returns>
    public TabPage OpenReport(Report report, string? title = null)
    {
        ArgumentNullException.ThrowIfNull(report);
        var tab = new TabPage { Text = title ?? report.Name };
        var viewer = new ReportViewer { Dock = DockStyle.Fill };
        viewer.Report = report;
        viewer.RefreshReport();
        tab.Controls.Add(viewer);
        _contentTabs.AddTabPage(tab);
        _contentTabs.SelectedIndex = _contentTabs.TabPages.Count - 1;
        return tab;
    }

    /// <summary>
    /// Closes a module and removes its tab. The JS <c>dispose</c> callback runs,
    /// all facade event handlers are unwired, the module's timers are cancelled and
    /// the tab's control subtree is disposed so no resources (e.g. the shared
    /// cursor-blink registration) leak.
    /// </summary>
    /// <param name="id">The module ID.</param>
    public void CloseModule(string id)
    {
        if (!_open.TryGetValue(id, out var loaded)) return;

        try
        {
            using (_engine.UseModule(id))
                CallLifecycle(loaded, "dispose");
        }
        catch (Exception ex)
        {
            _engine.ReportError($"Module dispose '{id}'", ex);
        }

        foreach (var facade in loaded.Facades)
            facade.OffAll();

        foreach (var stripFacade in loaded.StripFacades)
            stripFacade.OffAll();
        loaded.StripFacades.Clear();

        foreach (var bs in loaded.BindingSources)
            bs.Dispose();
        loaded.BindingSources.Clear();

        _engine.Timers.ClearForModule(id);

        _contentTabs.RemoveTabPage(loaded.Tab);
        DisposeControlTree(loaded.Tab);
        _open.Remove(id);
        if (_activeId == id) _activeId = null;
    }

    /// <summary>
    /// Recursively disposes a control and all of its descendants, then clears the
    /// container. Used to release the tab page subtree after the tab was removed.
    /// </summary>
    /// <param name="control">The root control to dispose.</param>
    private static void DisposeControlTree(Control control)
    {
        foreach (var child in control.Controls.ToArray())
            DisposeControlTree(child);
        control.Controls.Clear();
        control.Dispose();
    }

    /// <summary>
    /// Reloads a module (hot reload): dispose, re-import, re-init.
    /// </summary>
    /// <param name="id">The module ID.</param>
    public void ReloadModule(string id)
    {
        if (!_open.TryGetValue(id, out var loaded)) return;

        try
        {
            using (_engine.UseModule(id))
                CallLifecycle(loaded, "dispose");
        }
        catch (Exception ex)
        {
            _engine.ReportError($"Module dispose (reload) '{id}'", ex);
        }

        foreach (var facade in loaded.Facades)
            facade.OffAll();

        foreach (var stripFacade in loaded.StripFacades)
            stripFacade.OffAll();
        loaded.StripFacades.Clear();

        foreach (var bs in loaded.BindingSources)
            bs.Dispose();
        loaded.BindingSources.Clear();

        _engine.Timers.ClearForModule(id);

        _contentTabs.RemoveTabPage(loaded.Tab);
        DisposeControlTree(loaded.Tab);
        _open.Remove(id);
        if (_activeId == id) _activeId = null;

        OpenModule(id);
    }

    /// <summary>
    /// Activates an open module (<c>activate</c> callback).
    /// </summary>
    /// <param name="id">The module ID.</param>
    public void Activate(string id)
    {
        if (_activeId == id) return;
        if (_activeId != null)
            Deactivate(_activeId);

        if (_open.TryGetValue(id, out var loaded))
        {
            try
            {
                using (_engine.UseModule(id))
                    CallLifecycle(loaded, "activate");
            }
            catch (Exception ex)
            {
                _engine.ReportError($"Module activate '{id}'", ex);
            }
        }

        _activeId = id;
    }

    /// <summary>
    /// Deactivates an open module (<c>deactivate</c> callback).
    /// </summary>
    /// <param name="id">The module ID.</param>
    public void Deactivate(string id)
    {
        if (_open.TryGetValue(id, out var loaded))
        {
            try
            {
                using (_engine.UseModule(id))
                    CallLifecycle(loaded, "deactivate");
            }
            catch (Exception ex)
            {
                _engine.ReportError($"Module deactivate '{id}'", ex);
            }
        }

        if (_activeId == id) _activeId = null;
    }

    /// <summary>
    /// Wires the tab selection to the module activity callbacks and the tab close
    /// buttons to <see cref="CloseModule"/>.
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

        _contentTabs.TabCloseRequested += (s, tab) =>
        {
            var id = _open.FirstOrDefault(kv => kv.Value.Tab == tab).Key;
            if (id != null)
                CloseModule(id);
        };
    }

    /// <summary>
    /// Invokes a lifecycle callback (<c>activate</c>/<c>deactivate</c>/<c>dispose</c>)
    /// depending on the module form: for classes on the instance (with <c>this</c>
    /// binding, inherited no-ops via the prototype chain), otherwise as an exported function.
    /// </summary>
    /// <param name="loaded">The loaded module.</param>
    /// <param name="name">The callback name.</param>
    private void CallLifecycle(LoadedModule loaded, string name)
    {
        if (loaded.Mode == ModuleMode.Class)
        {
            if (loaded.Instance == null) return;
            var fn = loaded.Instance.Get(name);
            if (fn is Jint.Native.Function.Function)
                _engine.CallFunction(fn, loaded.Instance);
            return;
        }

        _engine.CallExport(loaded.Namespace, name);
    }

    /// <summary>
    /// Detects whether a module namespace has a constructible ES6 class as its
    /// <c>default</c> export (class mode) or not (function mode).
    /// </summary>
    /// <param name="moduleNamespace">The module namespace.</param>
    /// <returns><see langword="true"/> if an <c>export default class</c> is present.</returns>
    private static bool IsClassModule(ObjectInstance moduleNamespace)
    {
        var def = moduleNamespace.Get("default");
        return def is Jint.Native.Function.Function f && f.IsConstructor();
    }

    /// <summary>
    /// Loads a layout into the tab and rebuilds the JS control facade.
    /// Existing facades are detached and the tab is cleared first, so a repeated
    /// call (layout switch) replaces cleanly.
    /// </summary>
    /// <param name="tab">The target tab.</param>
    /// <param name="descriptor">The module descriptor (for the default layout path).</param>
    /// <param name="loaded">The loaded module (for facade tracking).</param>
    /// <param name="explicitRel">Explicit relative layout path, or <c>null</c> for the default layout.</param>
    /// <returns>The JS controls object (name → <see cref="JsControl"/>).</returns>
    private ObjectInstance LoadLayoutIntoTab(TabPage tab, ModuleDescriptor descriptor, LoadedModule loaded, string? explicitRel)
    {
        var rel = explicitRel is null
            ? descriptor.LayoutPath
            : ResolveLayoutPath(descriptor.Directory, explicitRel);

        // Detach previous facades and clear the tab (clean switch/reload).
        foreach (var facade in loaded.Facades)
            facade.OffAll();
        loaded.Facades.Clear();
        foreach (var stripFacade in loaded.StripFacades)
            stripFacade.OffAll();
        loaded.StripFacades.Clear();
        tab.Controls.Clear();

        var controls = _engine.NewObject();
        if (_source.Exists(rel))
        {
            var lr = _loader.LoadInto(tab, _source.ReadText(rel));
            foreach (var warning in lr.Warnings)
                _engine.ReportError($"Layout {rel}", new InvalidOperationException(warning));

            foreach (var kvp in lr.Controls)
            {
                var facade = JsFacadeRegistry.WrapControl(_engine, kvp.Value);
                facade.OwnerModuleId = descriptor.Id;
                loaded.Facades.Add(facade);
                controls.Set(kvp.Key, JsValue.FromObject(_engine.Engine, facade));
            }

            foreach (var kvp in lr.StripItems)
            {
                var facade = JsFacadeRegistry.WrapStripItem(_engine, kvp.Value);
                facade.OwnerModuleId = descriptor.Id;
                loaded.StripFacades.Add(facade);
                controls.Set(kvp.Key, JsValue.FromObject(_engine.Engine, facade));
            }
        }
        else
        {
            _engine.ReportError($"Layout {rel}", new InvalidOperationException(string.Format(LangRes.GetEnglish("Runtime_NoLayoutFound"), rel)));
        }

        return controls;
    }

    /// <summary>
    /// Resolves a relative layout path: <c>@app/…</c> and a leading <c>/</c> are
    /// relative to the package root, otherwise relative to the module directory.
    /// </summary>
    /// <param name="moduleDir">The relative module directory.</param>
    /// <param name="rel">The provided relative path.</param>
    /// <returns>The normalized package path.</returns>
    private static string ResolveLayoutPath(string moduleDir, string rel)
    {
        if (rel.StartsWith(ModulePath.AppPrefix, StringComparison.Ordinal))
            return ModulePath.Normalize(rel[ModulePath.AppPrefix.Length..]);
        if (rel.StartsWith("/", StringComparison.Ordinal))
            return ModulePath.Normalize(rel);

        var combined = string.IsNullOrEmpty(moduleDir) ? rel : moduleDir.TrimEnd('/') + "/" + rel;
        return ModulePath.Normalize(combined);
    }

    private ObjectInstance BuildContext(TabPage tab, ObjectInstance controls, LoadedModule loaded, Func<JsValue, JsValue> loadLayout, Func<string, JsValue, JsValue> createControl)
    {
        var ctx = _engine.NewObject();

        var app = _engine.NewObject();
        app.Set("name", JsValue.FromObject(_engine.Engine, _host.AppName));
        app.Set("version", JsValue.FromObject(_engine.Engine, _host.AppVersion));

        ctx.Set("form", JsValue.FromObject(_engine.Engine, _shellForm));
        ctx.Set("controls", controls);
        ctx.Set("__loadLayout", JsValue.FromObject(_engine.Engine, loadLayout));
        ctx.Set("__createControl", JsValue.FromObject(_engine.Engine, createControl));
        ctx.Set("host", JsValue.FromObject(_engine.Engine, _host));
        var tabFacade = JsFacadeRegistry.WrapControl(_engine, tab);
        tabFacade.OwnerModuleId = loaded.Descriptor.Id;
        ctx.Set("tab", JsValue.FromObject(_engine.Engine, tabFacade));
        ctx.Set("app", app);
        ctx.Set("i18n", _engine.Engine.GetValue("i18n"));
        if (Remote != null)
            ctx.Set("remote", JsValue.FromObject(_engine.Engine, Remote));
        ctx.Set("data", BuildDataFactory(loaded));
        ctx.Set("report", BuildReportFactory(loaded));

        return ctx;
    }

    /// <summary>
    /// Builds the <c>ctx.report</c> object that exposes the <c>create</c> and
    /// <c>load</c> factories for report definitions.
    /// </summary>
    /// <param name="loaded">The loaded module that owns the created reports.</param>
    /// <returns>The <c>report</c> namespace object.</returns>
    private ObjectInstance BuildReportFactory(LoadedModule loaded)
    {
        var report = _engine.NewObject();

        Func<JsValue, JsValue> create = nameJs =>
        {
            var name = nameJs.IsUndefined() || nameJs.IsNull() ? "Report" : nameJs.ToString();
            var r = new Report(name);
            return JsValue.FromObject(_engine.Engine, new Facade.Typed.JsReport(_engine, r));
        };

        Func<JsValue, JsValue> load = pathJs =>
        {
            var rel = pathJs.IsUndefined() || pathJs.IsNull()
                ? throw new InvalidOperationException("report.load(path) requires a path.")
                : pathJs.ToString();
            var path = ResolveLayoutPath(loaded.Descriptor.Directory, rel);
            var json = _source.ReadText(path);
            var r = ReportJsonSerializer.Load(json);
            return JsValue.FromObject(_engine.Engine, new Facade.Typed.JsReport(_engine, r));
        };

        report.Set("create", JsValue.FromObject(_engine.Engine, create));
        report.Set("load", JsValue.FromObject(_engine.Engine, load));
        return report;
    }

    /// <summary>
    /// Builds the <c>ctx.data</c> object that exposes the <c>createBindingSource</c>
    /// factory. Created binding sources are tracked on the module and disposed when
    /// the module closes, so their event subscriptions never outlive the module.
    /// </summary>
    /// <param name="loaded">The loaded module that owns the created sources.</param>
    /// <returns>The <c>data</c> namespace object.</returns>
    private ObjectInstance BuildDataFactory(LoadedModule loaded)
    {
        var data = _engine.NewObject();

        Func<JsValue, JsValue, JsValue> createBindingSource = (sourceJs, tableJs) =>
        {
            var source = sourceJs.ToObject() switch
            {
                JsDataTable table => new Facade.JsBindingSource(_engine, table),
                JsDataSet set => new Facade.JsBindingSource(
                    _engine,
                    set,
                    tableJs.IsUndefined() || tableJs.IsNull()
                        ? throw new InvalidOperationException("createBindingSource(dataSet, tableName) requires a table name.")
                        : tableJs.ToString()),
                _ => throw new InvalidOperationException("createBindingSource expects a table or dataset facade.")
            };
            loaded.BindingSources.Add(source);
            return JsValue.FromObject(_engine.Engine, source);
        };

        data.Set("createBindingSource", JsValue.FromObject(_engine.Engine, createBindingSource));
        return data;
    }
}
