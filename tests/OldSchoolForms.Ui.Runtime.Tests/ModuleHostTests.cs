using System.Globalization;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Runtime.Facade;
using OldSchoolForms.Ui.Runtime.Hosting;
using OldSchoolForms.Ui.Runtime.Localization;
using OldSchoolForms.Ui.Runtime.Modules;
using OldSchoolForms.Ui.Runtime.Scripting;
using OldSchoolForms.Ui.Runtime.Serialization;
using OldSchoolForms.Ui.Runtime.Sources;
using Xunit;

namespace OldSchoolForms.Ui.Runtime.Tests;

/// <summary>
/// Headless integration tests for the <see cref="ModuleHost"/>: modules are
/// opened/closed against a real <see cref="TabControl"/> without creating a
/// window (no display needed). Runs with a fixed English culture so localized
/// titles and status texts are deterministic.
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
        var i18n = new RuntimeI18n(source, "i18n", "en");
        LocalizationManager.SetCulture(new CultureInfo("en"));
        var engine = new JsEngineHost(source) { I18n = i18n };
        var loader = new RuntimeLayoutLoader { I18n = i18n };
        var manifest = AppManifest.Load(source);
        var registry = new ModuleRegistry(manifest.Modules);
        var tabs = new TabControl();
        var shellForm = new JsForm(engine, new Form());
        var host = new HostServices(source, manifest.Name, manifest.Version) { I18n = i18n };
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
            f.ModuleHost.OpenModule("dashboard");
        }
        finally
        {
            Console.SetOut(original);
        }

        Assert.Contains("dashboard", f.ModuleHost.OpenModuleIds);
        Assert.Single(f.Tabs.TabPages);
        Assert.Equal("Dashboard", f.Tabs.TabPages[0].Text);
        Assert.Contains("Dashboard initialized", buffer.ToString());
        Assert.Equal("Dashboard active", f.Status.Text);
    }

    [Fact]
    public void OpenModule_SecondModuleSwitchesActive()
    {
        using var f = Build();
        f.ModuleHost.OpenModule("dashboard");
        f.ModuleHost.OpenModule("counter");

        Assert.Equal(2, f.Tabs.TabPages.Count);
        Assert.Equal("Counter active", f.Status.Text);
    }

    [Fact]
    public void OpenModule_ExistingSelectsAndActivates()
    {
        using var f = Build();
        f.ModuleHost.OpenModule("dashboard");
        f.ModuleHost.OpenModule("counter");
        f.ModuleHost.OpenModule("dashboard");

        Assert.Equal(2, f.Tabs.TabPages.Count);
        Assert.Equal("Dashboard active", f.Status.Text);
    }

    [Fact]
    public void CloseModule_RemovesTabAndUntracks()
    {
        using var f = Build();
        f.ModuleHost.OpenModule("dashboard");
        f.ModuleHost.CloseModule("dashboard");

        Assert.DoesNotContain("dashboard", f.ModuleHost.OpenModuleIds);
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
        f.ModuleHost.OpenModule("dashboard");
        f.ModuleHost.ReloadModule("dashboard");

        Assert.Contains("dashboard", f.ModuleHost.OpenModuleIds);
        Assert.Single(f.Tabs.TabPages);
        Assert.Equal("Dashboard active", f.Status.Text);
    }
}
