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
/// Result of the shell construction: the main window and the referenced
/// shell controls.
/// </summary>
public sealed class ShellResult
{
    /// <summary>The main window.</summary>
    public required Form Form { get; init; }

    /// <summary>The content TabControl.</summary>
    public required TabControl ContentTabs { get; init; }

    /// <summary>The navigation sidebar (TreeView), optional.</summary>
    public TreeView? NavTree { get; init; }

    /// <summary>The status bar label, optional.</summary>
    public ToolStripStatusLabel? StatusLabel { get; init; }

    /// <summary>The MenuStrip, optional.</summary>
    public MenuStrip? Menu { get; init; }

    /// <summary>The ToolStrip, optional.</summary>
    public ToolStrip? Toolbar { get; init; }
}

/// <summary>
/// Builds the main window (shell) from the shell layout: menu, toolbar,
/// navigation sidebar and content tab area. The sidebar is populated from
/// the module registry.
/// </summary>
public static class ShellBuilder
{
    /// <summary>
    /// Builds the shell.
    /// </summary>
    /// <param name="source">The package source.</param>
    /// <param name="manifest">The app manifest.</param>
    /// <param name="engine">The JS engine.</param>
    /// <param name="loader">The layout loader.</param>
    /// <param name="registry">The module registry.</param>
    /// <param name="host">The host services.</param>
    /// <returns>The shell result.</returns>
    /// <exception cref="InvalidOperationException">The shell layout or the content TabControl is missing.</exception>
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
            throw new InvalidOperationException(string.Format(LangRes.GetString("Runtime_ShellLayoutMissing"), manifest.Shell));

        var form = new Form { Text = manifest.Name };
        var result = loader.LoadInto(form, source.ReadText(manifest.Shell));
        foreach (var warning in result.Warnings)
            Console.WriteLine($"[shell] Warning: {warning}");

        var contentTabs = result.Find("contentTabs") as TabControl
            ?? throw new InvalidOperationException(LangRes.GetString("Runtime_ShellMissingContentTabs"));

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
    /// Wires the navigation sidebar to the module host.
    /// </summary>
    /// <param name="navTree">The navigation TreeView.</param>
    /// <param name="moduleHost">The module host.</param>
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
