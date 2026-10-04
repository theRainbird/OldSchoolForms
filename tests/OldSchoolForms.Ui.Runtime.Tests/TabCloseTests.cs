using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Runtime.Facade;
using OldSchoolForms.Ui.Runtime.Hosting;
using OldSchoolForms.Ui.Runtime.Modules;
using OldSchoolForms.Ui.Runtime.Scripting;
using OldSchoolForms.Ui.Runtime.Serialization;
using OldSchoolForms.Ui.Runtime.Sources;
using Xunit;

namespace OldSchoolForms.Ui.Runtime.Tests;

/// <summary>
/// Headless-Tests für das Schließen der Modul-Reiter per X-Button: Event,
/// sauberes Entladen (Dispose der Tab-Subtree) und Timer-Bereinigung.
/// </summary>
public class TabCloseTests
{
    private sealed class Fixture : IDisposable
    {
        public JsEngineHost Engine = null!;
        public ModuleHost ModuleHost = null!;
        public TabControl Tabs = null!;

        public void Dispose() => Engine.Dispose();
    }

    private static Fixture Build()
    {
        var source = new FileSystemAppSource(TestPaths.SampleApp);
        var engine = new JsEngineHost(source);
        var loader = new RuntimeLayoutLoader();
        var manifest = AppManifest.Load(source);
        var registry = new ModuleRegistry(manifest.Modules);
        var tabs = new TabControl();
        var shellForm = new JsForm(engine, new Form());
        var host = new HostServices(source, manifest.Name, manifest.Version);
        var moduleHost = new ModuleHost(source, engine, loader, registry, tabs, shellForm, host);

        host.ModuleHost = moduleHost;
        moduleHost.AttachTabActivation();

        return new Fixture
        {
            Engine = engine,
            ModuleHost = moduleHost,
            Tabs = tabs
        };
    }

    private static void ClickCloseButton(TabControl tabs, int tabIndex)
    {
        var bounds = tabs.GetTabCloseButtonBounds(tabIndex);
        tabs.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1,
            bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2, 0));
    }

    [Fact]
    public void CloseButtonClick_FiresTabCloseRequested_WithoutChangingSelection()
    {
        using var f = Build();
        f.ModuleHost.OpenModule("customers");
        f.ModuleHost.OpenModule("orders");
        Assert.Equal(1, f.Tabs.SelectedIndex);

        var firstTab = f.Tabs.TabPages[0];
        TabPage? closed = null;
        f.Tabs.TabCloseRequested += (s, tab) => closed = tab;

        ClickCloseButton(f.Tabs, 0);

        Assert.NotNull(closed);
        Assert.Same(firstTab, closed);
        Assert.Equal(0, f.Tabs.SelectedIndex);
        Assert.Single(f.Tabs.TabPages);
    }

    [Fact]
    public void CloseButtonOnUnselectedTab_DoesNotChangeSelection()
    {
        var tabs = new TabControl();
        tabs.AddTabPage(new TabPage { Text = "A" });
        tabs.AddTabPage(new TabPage { Text = "B" });
        tabs.SelectedIndex = 1;

        TabPage? closed = null;
        tabs.TabCloseRequested += (s, tab) => closed = tab;

        ClickCloseButton(tabs, 0);

        Assert.NotNull(closed);
        Assert.Equal("A", closed.Text);
        Assert.Equal(1, tabs.SelectedIndex);
    }

    [Fact]
    public void CloseButtonClick_ClosesModuleViaHost()
    {
        using var f = Build();
        f.ModuleHost.OpenModule("customers");
        f.ModuleHost.OpenModule("orders");

        ClickCloseButton(f.Tabs, 0);

        Assert.DoesNotContain("customers", f.ModuleHost.OpenModuleIds);
        Assert.Single(f.Tabs.TabPages);
        Assert.Contains("orders", f.ModuleHost.OpenModuleIds);
    }

    [Fact]
    public void CloseModule_DisposesTabPageAndChildControls()
    {
        using var f = Build();
        f.ModuleHost.OpenModule("customers");

        var tab = f.Tabs.TabPages[0];
        Assert.NotEmpty(tab.Controls);
        var child = tab.Controls.First();

        f.ModuleHost.CloseModule("customers");

        Assert.True(tab.IsDisposed);
        Assert.True(child.IsDisposed);
    }

    [Fact]
    public void CloseModule_ClearsModuleOwnedTimers()
    {
        using var f = Build();
        f.ModuleHost.OpenModule("customers");

        using (f.Engine.UseModule("customers"))
            f.Engine.Engine.Evaluate("globalThis.__interval = setInterval(function () { }, 100000);");

        Assert.True(f.Engine.Timers.Count >= 1);

        f.ModuleHost.CloseModule("customers");

        Assert.Equal(0, f.Engine.Timers.Count);
    }

    [Fact]
    public void CloseModule_KeepsTimersOfOtherModules()
    {
        using var f = Build();
        f.ModuleHost.OpenModule("customers");
        f.ModuleHost.OpenModule("orders");

        using (f.Engine.UseModule("orders"))
            f.Engine.Engine.Evaluate("globalThis.__interval = setInterval(function () { }, 100000);");

        var ordersTimers = f.Engine.Timers.Count;
        Assert.True(ordersTimers >= 1);

        f.ModuleHost.CloseModule("customers");

        Assert.Equal(ordersTimers, f.Engine.Timers.Count);
    }

    [Fact]
    public void CloseLastTab_LeavesEmptyShell()
    {
        using var f = Build();
        f.ModuleHost.OpenModule("customers");

        ClickCloseButton(f.Tabs, 0);

        Assert.Empty(f.Tabs.TabPages);
        Assert.Empty(f.ModuleHost.OpenModuleIds);
        Assert.Null(f.Tabs.SelectedTab);
    }

    [Fact]
    public void ClearForModule_RemovesOnlyOwnedTimers()
    {
        var source = new FileSystemAppSource(TestPaths.SampleApp);
        using var engine = new JsEngineHost(source);
        var callback = engine.Engine.Evaluate("(function () { })");

        engine.Timers.SetTimeout(callback, 1000, "alpha");
        var keptId = engine.Timers.SetTimeout(callback, 1000, "beta");
        engine.Timers.SetTimeout(callback, 1000, null);

        engine.Timers.ClearForModule("alpha");

        Assert.Equal(2, engine.Timers.Count);
        engine.Timers.Clear(keptId);
        engine.Timers.ClearForModule("beta");
        Assert.Equal(1, engine.Timers.Count);
    }
}
