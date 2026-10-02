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
/// Headless-Integrationstests für den <see cref="ModuleHost"/>: Module werden
/// gegen einen echten <see cref="TabControl"/> geöffnet/geschlossen, ohne ein
/// Fenster zu erzeugen (kein Display nötig).
/// </summary>
public class ModuleHostTests
{
    private sealed class Fixture : IDisposable
    {
        public JsEngineHost Engine = null!;
        public ModuleHost ModuleHost = null!;
        public TabControl Tabs = null!;
        public ToolStripStatusLabel Status = null!;

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
        var status = new ToolStripStatusLabel();
        host.AttachStatusLabel(status);
        moduleHost.AttachTabActivation();

        return new Fixture
        {
            Engine = engine,
            ModuleHost = moduleHost,
            Tabs = tabs,
            Status = status
        };
    }

    [Fact]
    public void OpenModule_CreatesTabAndRunsInit()
    {
        using var f = Build();
        var buffer = new StringWriter();
        var original = Console.Out;
        try
        {
            Console.SetOut(buffer);
            f.ModuleHost.OpenModule("customers");
        }
        finally
        {
            Console.SetOut(original);
        }

        Assert.Contains("customers", f.ModuleHost.OpenModuleIds);
        Assert.Single(f.Tabs.TabPages);
        Assert.Equal("Kunden", f.Tabs.TabPages[0].Text);
        Assert.Contains("Hallo, Kunden-Modul!", buffer.ToString());
        Assert.Equal("Kunden-Modul aktiv", f.Status.Text);
    }

    [Fact]
    public void OpenModule_SecondModuleSwitchesActive()
    {
        using var f = Build();
        f.ModuleHost.OpenModule("customers");
        f.ModuleHost.OpenModule("orders");

        Assert.Equal(2, f.Tabs.TabPages.Count);
        Assert.Equal("Aufträge-Modul aktiv", f.Status.Text);
    }

    [Fact]
    public void OpenModule_ExistingSelectsAndActivates()
    {
        using var f = Build();
        f.ModuleHost.OpenModule("customers");
        f.ModuleHost.OpenModule("orders");
        f.ModuleHost.OpenModule("customers");

        Assert.Equal(2, f.Tabs.TabPages.Count);
        Assert.Equal("Kunden-Modul aktiv", f.Status.Text);
    }

    [Fact]
    public void CloseModule_RemovesTabAndUntracks()
    {
        using var f = Build();
        f.ModuleHost.OpenModule("customers");
        f.ModuleHost.CloseModule("customers");

        Assert.DoesNotContain("customers", f.ModuleHost.OpenModuleIds);
        Assert.Empty(f.Tabs.TabPages);
    }

    [Fact]
    public void OpenModule_UnknownThrows()
    {
        using var f = Build();
        Assert.Throws<InvalidOperationException>(() => f.ModuleHost.OpenModule("nonexistent"));
    }

    [Fact]
    public void ReloadModule_KeepsTabAndReinitializes()
    {
        using var f = Build();
        f.ModuleHost.OpenModule("customers");
        f.ModuleHost.ReloadModule("customers");

        Assert.Contains("customers", f.ModuleHost.OpenModuleIds);
        Assert.Single(f.Tabs.TabPages);
        Assert.Equal("Kunden-Modul aktiv", f.Status.Text);
    }
}
