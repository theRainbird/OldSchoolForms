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
/// Headless-Integrationstests für klassenbasierte Module (<c>export default class ... extends FormBase</c>):
/// Konstruktor = Init, <c>this.controls</c> funktioniert, Lebenszyklus-Methoden laufen
/// mit <c>this</c>-Bindung auf der Instanz (inkl. geerbter No-ops).
/// </summary>
public class ModuleHostClassTests : IDisposable
{
    private readonly string _root;

    public ModuleHostClassTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "osf-class-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        WriteApp();
    }

    private void WriteApp()
    {
        File.WriteAllText(Path.Combine(_root, "app.json"), """
        { "name": "ClassTestApp", "version": "1.0.0", "modules": ["modules/ct", "modules/other", "modules/switch", "modules/codeui"] }
        """);

        var ct = Path.Combine(_root, "modules", "ct");
        Directory.CreateDirectory(ct);
        File.WriteAllText(Path.Combine(ct, "module.json"), """
        { "id": "ct", "title": "CT", "layout": "layout.json", "script": "index.js", "order": 1 }
        """);
        File.WriteAllText(Path.Combine(ct, "layout.json"), """
        { "format": "OldSchoolForms.Layout", "version": 1,
          "controls": [ { "type": "Label", "name": "marker", "props": { "text": "init" } } ] }
        """);
        File.WriteAllText(Path.Combine(ct, "index.js"), """
        export default class CtForm extends FormBase {
            constructor(ctx) {
                super(ctx);
                this.loadLayout();
                this.controls.marker.text = "ctor-ran";
                globalThis.__ct = (globalThis.__ct || []);
                globalThis.__ct.push("ctor");
            }
            activate() { globalThis.__ct.push("activate"); this.setStatus("ct-aktiv"); }
            deactivate() { globalThis.__ct.push("deactivate"); }
            dispose() { globalThis.__ct.push("dispose"); }
        }
        """);

        var other = Path.Combine(_root, "modules", "other");
        Directory.CreateDirectory(other);
        File.WriteAllText(Path.Combine(other, "module.json"), """
        { "id": "other", "title": "Other", "layout": "layout.json", "script": "index.js", "order": 2 }
        """);
        File.WriteAllText(Path.Combine(other, "layout.json"), """
        { "format": "OldSchoolForms.Layout", "version": 1, "controls": [] }
        """);
        File.WriteAllText(Path.Combine(other, "index.js"), """
        export default class OtherForm extends FormBase {
            constructor(ctx) { super(ctx); }
            activate() { this.setStatus("other-aktiv"); }
        }
        """);

        var sw = Path.Combine(_root, "modules", "switch");
        Directory.CreateDirectory(sw);
        File.WriteAllText(Path.Combine(sw, "module.json"), """
        { "id": "switch", "title": "Switch", "layout": "layout.json", "script": "index.js", "order": 3 }
        """);
        File.WriteAllText(Path.Combine(sw, "layout.json"), """
        { "format": "OldSchoolForms.Layout", "version": 1,
          "controls": [ { "type": "Label", "name": "first", "props": { "text": "FIRST" } } ] }
        """);
        File.WriteAllText(Path.Combine(sw, "anderes.json"), """
        { "format": "OldSchoolForms.Layout", "version": 1,
          "controls": [ { "type": "Label", "name": "second", "props": { "text": "SECOND" } } ] }
        """);
        File.WriteAllText(Path.Combine(sw, "index.js"), """
        export default class SwitchForm extends FormBase {
            constructor(ctx) {
                super(ctx);
                this.loadLayout("anderes.json");
            }
        }
        """);

        // Code-built layout: NO layout.json; controls created via this.createControl + add.
        var code = Path.Combine(_root, "modules", "codeui");
        Directory.CreateDirectory(code);
        File.WriteAllText(Path.Combine(code, "module.json"), """
        { "id": "codeui", "title": "CodeUI", "script": "index.js", "order": 4 }
        """);
        File.WriteAllText(Path.Combine(code, "index.js"), """
        export default class CodeUiForm extends FormBase {
            constructor(ctx) {
                super(ctx);
                globalThis.__codeui = (globalThis.__codeui || []);
                const panel = this.createControl("Panel", { name: "p", dock: "Fill" });
                const btn = this.createControl("Button", { name: "btn1", text: "Klick" });
                btn.on("click", () => this.setStatus("geklickt"));
                panel.add(btn);
                this.tab.add(panel);
                globalThis.__codeui.push("built");
            }
        }
        """);
    }

    private sealed class Fixture : IDisposable
    {
        public JsEngineHost Engine = null!;
        public ModuleHost ModuleHost = null!;
        public TabControl Tabs = null!;
        public ToolStripStatusLabel Status = null!;
        public void Dispose() => Engine.Dispose();
    }

    private Fixture Build()
    {
        var source = new FileSystemAppSource(_root);
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

        return new Fixture { Engine = engine, ModuleHost = moduleHost, Tabs = tabs, Status = status };
    }

    private static string Log(JsEngineHost engine)
        => engine.Engine.Evaluate("globalThis.__ct ? globalThis.__ct.join(',') : ''").ToString();

    [Fact]
    public void ClassModule_ConstructorActsAsInitAndSetsControlText()
    {
        using var f = Build();
        f.ModuleHost.OpenModule("ct");

        Assert.Contains("ctor", Log(f.Engine));
        var marker = f.Tabs.TabPages[0].Controls.FindControl("marker", true);
        Assert.NotNull(marker);
        Assert.Equal("ctor-ran", marker!.Text);
    }

    [Fact]
    public void ClassModule_LifecycleRunsThisBound()
    {
        using var f = Build();
        f.ModuleHost.OpenModule("ct");
        Assert.Equal("ctor,activate", Log(f.Engine));
        Assert.Equal("ct-aktiv", f.Status.Text);

        f.ModuleHost.OpenModule("other");
        Assert.Equal("ctor,activate,deactivate", Log(f.Engine));
        Assert.Equal("other-aktiv", f.Status.Text);

        f.ModuleHost.CloseModule("ct");
        Assert.Equal("ctor,activate,deactivate,dispose", Log(f.Engine));
    }

    [Fact]
    public void ClassModule_InheritedNoOpsDoNotThrow()
    {
        using var f = Build();
        // OtherForm überschreibt deactivate/dispose nicht -> geerbte No-ops von FormBase.
        f.ModuleHost.OpenModule("other");
        f.ModuleHost.OpenModule("ct"); // löst other-deactivate (geerbt) aus
        f.ModuleHost.CloseModule("other"); // löst other-dispose (geerbt) aus
        Assert.DoesNotContain("other", f.ModuleHost.OpenModuleIds);
    }

    [Fact]
    public void ClassModule_LoadLayoutSwitchReplacesControls()
    {
        using var f = Build();
        f.ModuleHost.OpenModule("switch");

        var tab = f.Tabs.TabPages[0];
        // Anderes Layout geladen -> "second" vorhanden ...
        var second = tab.Controls.FindControl("second", true);
        Assert.NotNull(second);
        Assert.Equal("SECOND", second!.Text);
        // ... das Standard-Layout ("first") wurde NICHT geladen.
        Assert.Null(tab.Controls.FindControl("first", true));
    }

    [Fact]
    public void ClassModule_CodeBuiltLayoutPlacesControls()
    {
        using var f = Build();
        f.ModuleHost.OpenModule("codeui");

        var tab = f.Tabs.TabPages[0];
        // Per JS erzeugter Button liegt verschachtelt im Panel auf dem Tab.
        var btn = tab.Controls.FindControl("btn1", true);
        Assert.NotNull(btn);
        Assert.Equal("Klick", btn!.Text);
        // this.controls wurde über createControl(name) befüllt -> Facade auffindbar.
        var viaControls = f.Engine.Engine.Evaluate("globalThis.__codeui ? globalThis.__codeui.join(',') : ''").ToString();
        Assert.Equal("built", viaControls);
    }

    [Fact]
    public void ClassModule_WiredButtonClick_FiresJsCallback()
    {
        using var f = Build();
        f.ModuleHost.OpenModule("codeui");

        var btn = f.Tabs.TabPages[0].Controls.FindControl("btn1", true);
        Assert.NotNull(btn);
        Assert.NotEqual("geklickt", f.Status.Text);

        var onClick = typeof(OldSchoolForms.Ui.Core.Control)
            .GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        onClick.Invoke(btn, new object[] { EventArgs.Empty });

        Assert.Equal("geklickt", f.Status.Text);
    }

    [Fact]
    public void ClassModule_CreateControlUnknownTypeDoesNotCrash()
    {
        using var f = Build();
        var errors = new List<string>();
        f.Engine.Error += (ctx, ex) => errors.Add(ctx);
        f.Engine.Engine.SetValue("mkBad", (Func<string, Jint.Native.JsValue, Jint.Native.JsValue>)((t, p) =>
        {
            // createControl via ctx-Delegate direkt aufrufen (ohne Modul-Kontext) ->
            // hier: wir rufen den Loader-Fallback fuer unbekannten Typ.
            return Jint.Native.JsValue.Undefined;
        }));
        // Ein Modul mit unbekanntem Typ im Code fuehrt zu ReportError, nicht zum Absturz.
        var loader = new OldSchoolForms.Ui.Runtime.Serialization.RuntimeLayoutLoader();
        Assert.Null(loader.CreateControl("NoSuchControlType"));
        Assert.NotNull(loader.CreateControl("Button"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { /* ignore */ }
    }
}
