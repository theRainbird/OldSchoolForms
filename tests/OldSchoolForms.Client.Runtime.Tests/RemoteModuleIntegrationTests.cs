using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Runtime.Facade;
using OldSchoolForms.Ui.Runtime.Hosting;
using OldSchoolForms.Ui.Runtime.Localization;
using OldSchoolForms.Ui.Runtime.Modules;
using OldSchoolForms.Ui.Runtime.Remote;
using OldSchoolForms.Ui.Runtime.Scripting;
using OldSchoolForms.Ui.Runtime.Serialization;
using OldSchoolForms.Ui.Runtime.Sources;
using Xunit;

namespace OldSchoolForms.Client.Runtime.Tests;

/// <summary>
/// Decisive end-to-end test that opens the REAL <c>remote</c> sample module
/// (<c>samples/RuntimeExampleApp/modules/remote</c>) through the full
/// <see cref="ModuleHost"/> against a real <c>ServerRuntime</c> (serving
/// <c>samples/ServerSampleApp</c>), then fires a real <see cref="Control.Click"/>
/// on the wired buttons and asserts the data-binding effects occur.
/// This mirrors exactly what the user does in the GUI.
/// </summary>
public sealed class RemoteModuleIntegrationTests : IDisposable
{
    private readonly int _port;
    private readonly Server.Runtime.ServerRuntime _server;
    private readonly RemoteGatewayClient _gateway;
    private readonly SharedCodeCache _sharedCache;
    private readonly Fixture _fx;

    private static string FindSampleApp()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "samples", "RuntimeExampleApp");
            if (File.Exists(Path.Combine(candidate, "app.json")))
                return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("samples/RuntimeExampleApp not found.");
    }

    public RemoteModuleIntegrationTests()
    {
        var sampleApp = FindSampleApp();
        var repoRoot = Directory.GetParent(sampleApp)!.Parent!.FullName;
        var serverRoot = Path.Combine(repoRoot, "samples", "ServerSampleApp");

        _port = 27000 + Random.Shared.Next(1000);
        _server = new Server.Runtime.ServerRuntime(serverRoot, devMode: false, portOverride: _port);
        _server.Start();

        _gateway = new RemoteGatewayClient("127.0.0.1", _port, encryption: false);
        _gateway.Connect();

        _sharedCache = new SharedCodeCache("remote-int-" + Guid.NewGuid().ToString("n"));
        _sharedCache.Sync(_gateway);

        _fx = BuildHost();
    }

    private sealed class Fixture : IDisposable
    {
        public JsEngineHost Engine = null!;
        public ModuleHost ModuleHost = null!;
        public TabControl Tabs = null!;
        public ToolStripStatusLabel Status = null!;
        public void Dispose() => Engine.Dispose();
    }

    private Fixture BuildHost()
    {
        var source = new FileSystemAppSource(FindSampleApp());
        var loader = new CompositeModuleLoader(new AppSourceModuleLoader(source), _sharedCache);
        var engine = new JsEngineHost(source, devMode: false, moduleLoader: loader);
        var layoutLoader = new RuntimeLayoutLoader();
        var manifest = AppManifest.Load(source);
        var i18n = new RuntimeI18n(source, manifest.I18nPath, manifest.DefaultCulture,
            manifest.Cultures.Count > 0 ? manifest.Cultures : null);
        engine.I18n = i18n;
        layoutLoader.I18n = i18n;
        var registry = new ModuleRegistry(manifest.Modules);
        var tabs = new TabControl();
        var shellForm = new JsForm(engine, new Form());
        var host = new HostServices(source, manifest.Name, manifest.Version);
        var moduleHost = new ModuleHost(source, engine, layoutLoader, registry, tabs, shellForm, host);

        host.ModuleHost = moduleHost;
        var status = new ToolStripStatusLabel();
        host.AttachStatusLabel(status);
        moduleHost.AttachTabActivation();

        // Wire the remote services so ctx.remote is available to the module.
        moduleHost.Remote = new RemoteServices(engine, _gateway);

        return new Fixture { Engine = engine, ModuleHost = moduleHost, Tabs = tabs, Status = status };
    }

    private static void FireClick(Control control)
    {
        var onClick = typeof(Control)
            .GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        onClick.Invoke(control, new object[] { EventArgs.Empty });
    }

    [Fact]
    public void RemoteModule_OpensAndConstructsWithoutError()
    {
        var errors = new List<string>();
        _fx.Engine.Error += (ctx, ex) => errors.Add(ctx + ": " + ex.Message);

        _fx.ModuleHost.OpenModule("remote");

        Assert.True(_fx.ModuleHost.OpenModuleIds.Contains("remote"), "remote module should be open");
        Assert.Empty(errors);
    }

    [Fact]
    public void RemoteModule_LoadButton_PopulatesGridViaBindingSource()
    {
        _fx.ModuleHost.OpenModule("remote");
        var tab = _fx.Tabs.TabPages[_fx.Tabs.TabPages.Count - 1];

        var grid = tab.Controls.FindControl("grid", true) as DataGridView;
        Assert.NotNull(grid);
        Assert.Equal(0, grid!.Rows.Count); // empty before load

        var btnLoad = tab.Controls.FindControl("btnLoad", true);
        Assert.NotNull(btnLoad);

        var clickErrors = new List<string>();
        _fx.Engine.Error += (ctx, ex) => clickErrors.Add(ctx + ": " + ex.Message);

        FireClick(btnLoad!);

        var statusText = _fx.Status.Text;
        Assert.True(grid.Rows.Count >= 2,
            $"expected grid populated, got {grid.Rows.Count}; status='{statusText}'; clickErrors=[{string.Join(" || ", clickErrors)}]");
    }

    [Fact]
    public void RemoteModule_NavButton_ChangesBoundTextBoxViaCurrentRow()
    {
        _fx.ModuleHost.OpenModule("remote");
        var tab = _fx.Tabs.TabPages[_fx.Tabs.TabPages.Count - 1];

        var btnLoad = tab.Controls.FindControl("btnLoad", true)!;
        FireClick(btnLoad);

        var nameBox = tab.Controls.FindControl("nameBox", true) as TextBox;
        Assert.NotNull(nameBox);
        var first = nameBox!.Text;
        Assert.False(string.IsNullOrEmpty(first), "nameBox should show current row's name after load");

        var btnNext = tab.Controls.FindControl("btnNext", true)!;
        FireClick(btnNext);

        // Navigation moves the currency; the bound TextBox must reflect a different row.
        Assert.NotEqual(first, nameBox.Text);
    }

    [Fact]
    public void RemoteModule_AddButton_AppendsRowToGrid()
    {
        _fx.ModuleHost.OpenModule("remote");
        var tab = _fx.Tabs.TabPages[_fx.Tabs.TabPages.Count - 1];

        FireClick(tab.Controls.FindControl("btnLoad", true)!);
        var grid = (DataGridView)tab.Controls.FindControl("grid", true)!;
        var before = grid.Rows.Count;

        var nameBox = (TextBox)tab.Controls.FindControl("nameBox", true)!;
        var emailBox = (TextBox)tab.Controls.FindControl("emailBox", true)!;
        var ageBox = (TextBox)tab.Controls.FindControl("ageBox", true)!;
        nameBox.Text = "Zoe";
        Console.WriteLine($"TESTDBG after nameBox set: CLR nameBox.Text='{nameBox.Text}'");
        emailBox.Text = "zoe@example.com";
        ageBox.Text = "41";
        Console.WriteLine($"TESTDBG before click: CLR nameBox.Text='{nameBox.Text}' emailBox.Text='{emailBox.Text}' ageBox.Text='{ageBox.Text}'");

        var clickErrors = new List<string>();
        _fx.Engine.Error += (ctx, ex) => clickErrors.Add(ctx + ": " + ex.Message);

        FireClick(tab.Controls.FindControl("btnAdd", true)!);

        Console.WriteLine($"TESTDBG after click: CLR nameBox.Text='{nameBox.Text}'");
        var statusText = _fx.Status.Text;
        Assert.True(grid.Rows.Count == before + 1,
            $"expected {before + 1} rows after add, got {grid.Rows.Count}; status='{statusText}'; clickErrors=[{string.Join(" || ", clickErrors)}]");
    }

    public void Dispose()
    {
        _fx.Dispose();
        _gateway.Dispose();
        _server.Stop();
    }
}
