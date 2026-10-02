using OldSchoolForms.Ui.Controls;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Runtime.Facade;
using OldSchoolForms.Ui.Runtime.Modules;
using OldSchoolForms.Ui.Runtime.Scripting;
using OldSchoolForms.Ui.Runtime.Serialization;
using OldSchoolForms.Ui.Runtime.Sources;

namespace OldSchoolForms.Ui.Runtime.Shell;

/// <summary>
/// Ergebnis des Shell-Aufbaus: das Hauptfenster und die referenzierten
/// Shell-Steuerelemente.
/// </summary>
public sealed class ShellResult
{
    /// <summary>Das Hauptfenster.</summary>
    public required Form Form { get; init; }

    /// <summary>Der Content-TabControl.</summary>
    public required TabControl ContentTabs { get; init; }

    /// <summary>Die Navigations-Sidebar (TreeView), optional.</summary>
    public TreeView? NavTree { get; init; }

    /// <summary>Die Statusleisten-Label, optional.</summary>
    public ToolStripStatusLabel? StatusLabel { get; init; }

    /// <summary>Die MenuStrip, optional.</summary>
    public MenuStrip? Menu { get; init; }

    /// <summary>Die ToolStrip, optional.</summary>
    public ToolStrip? Toolbar { get; init; }
}

/// <summary>
/// Baut das Hauptfenster (Shell) aus dem Shell-Layout: Menü, Toolbar,
/// Navigations-Sidebar und Content-Tabbereich. Die Sidebar wird aus der
/// Modul-Registry bestückt.
/// </summary>
public static class ShellBuilder
{
    /// <summary>
    /// Baut die Shell.
    /// </summary>
    /// <param name="source">Die Paketquelle.</param>
    /// <param name="manifest">Das App-Manifest.</param>
    /// <param name="engine">Die JS-Engine.</param>
    /// <param name="loader">Der Layout-Loader.</param>
    /// <param name="registry">Die Modul-Registry.</param>
    /// <param name="host">Die Host-Services.</param>
    /// <returns>Das Shell-Ergebnis.</returns>
    /// <exception cref="InvalidOperationException">Shell-Layout fehlt oder Content-TabControl fehlt.</exception>
    public static ShellResult Build(
        IAppSource source,
        AppManifest manifest,
        JsEngineHost engine,
        RuntimeLayoutLoader loader,
        ModuleRegistry registry,
        Hosting.HostServices host)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(host);

        if (!source.Exists(manifest.Shell))
            throw new InvalidOperationException($"Shell-Layout fehlt: {manifest.Shell}");

        var form = new Form { Text = manifest.Name };
        var result = loader.LoadInto(form, source.ReadText(manifest.Shell));
        foreach (var warning in result.Warnings)
            Console.WriteLine($"[shell] Warnung: {warning}");

        var contentTabs = result.Find("contentTabs") as TabControl
            ?? throw new InvalidOperationException("Shell-Layout enthält keinen TabControl namens 'contentTabs'.");

        var navTree = result.Find("navTree") as TreeView;
        var statusStrip = result.Find("statusStrip") as StatusStrip ?? FindFirst<StatusStrip>(form);
        var statusLabel = statusStrip != null && statusStrip.Items.Count > 0 ? statusStrip.Items[0] : null;
        var menu = result.Find("menuStrip") as MenuStrip ?? FindFirst<MenuStrip>(form);
        var toolbar = result.Find("toolStrip") as ToolStrip ?? FindFirst<ToolStrip>(form);

        if (navTree != null)
            PopulateNavTree(navTree, registry);

        return new ShellResult
        {
            Form = form,
            ContentTabs = contentTabs,
            NavTree = navTree,
            StatusLabel = statusLabel,
            Menu = menu,
            Toolbar = toolbar
        };
    }

    /// <summary>
    /// Verknüppt die Navigations-Sidebar mit dem Modul-Host.
    /// </summary>
    /// <param name="navTree">Die Navigations-TreeView.</param>
    /// <param name="moduleHost">Der Modul-Host.</param>
    public static void WireNavTree(TreeView navTree, ModuleHost moduleHost)
    {
        ArgumentNullException.ThrowIfNull(navTree);
        ArgumentNullException.ThrowIfNull(moduleHost);

        navTree.AfterSelect += (s, e) =>
        {
            var node = navTree.SelectedNode;
            if (node?.Tag is string id)
                moduleHost.OpenModule(id);
        };
    }

    private static void PopulateNavTree(TreeView navTree, ModuleRegistry registry)
    {
        foreach (var module in registry.All)
        {
            var node = new TreeNode(module.Title) { Tag = module.Id };
            navTree.Nodes.Add(node);
        }
    }

    private static T? FindFirst<T>(Control root) where T : Control
    {
        foreach (Control c in root.Controls)
        {
            if (c is T match) return match;
            var nested = FindFirst<T>(c);
            if (nested != null) return nested;
        }

        return null;
    }
}
