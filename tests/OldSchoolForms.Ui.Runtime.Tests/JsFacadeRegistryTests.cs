using Jint;
using Jint.Native;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Runtime.Facade;
using OldSchoolForms.Ui.Runtime.Scripting;
using OldSchoolForms.Ui.Runtime.Sources;
using Xunit;

namespace OldSchoolForms.Ui.Runtime.Tests;

/// <summary>
/// Tests für die Facade-Infrastruktur: Registry-Auflösung mit Vererbungssuche,
/// Marshal-Konvertierungen und das Verhalten typisierter Facaden
/// (camelCase-Zugriff, Event-Vererbung, Teardown).
/// </summary>
public class JsFacadeRegistryTests : IDisposable
{
    /// <summary>Niemals registrierter Control-Typ für den Fallback-Test.</summary>
    private class PlainTestControl : Control
    {
    }

    private class BaseTestControl : Control
    {
    }

    private class DerivedTestControl : BaseTestControl
    {
    }

    /// <summary>Typisierte Test-Facade nach dem Muster der Phase-2-Facaden.</summary>
    private class JsTestCheckBox : JsControl
    {
        public JsTestCheckBox(JsEngineHost host, CheckBox checkBox)
            : base(host, checkBox)
        {
            CheckBox = checkBox;
        }

        public CheckBox CheckBox { get; }

        public bool Checked
        {
            get => CheckBox.Checked;
            set => CheckBox.Checked = value;
        }

        public override void On(string eventName, JsValue callback)
        {
            var normalized = (eventName ?? string.Empty).Trim().ToLowerInvariant();
            if (normalized == "checkedchanged")
            {
                if (callback is not Jint.Native.Function.Function)
                    throw new ArgumentException("Callback must be a function.");

                var handler = new EventHandler((s, e) => Raise("checkedchanged", callback, e));
                CheckBox.CheckedChanged += handler;
                Wired.Add((normalized, handler));
                return;
            }

            base.On(normalized, callback);
        }

        protected override void Unwire(string normalized, Delegate handler)
        {
            if (normalized == "checkedchanged")
            {
                CheckBox.CheckedChanged -= (EventHandler)handler;
                return;
            }

            base.Unwire(normalized, handler);
        }
    }

    public JsFacadeRegistryTests() => JsFacadeRegistry.Clear();

    public void Dispose() => JsFacadeRegistry.Clear();

    private static JsEngineHost CreateEngine() =>
        new JsEngineHost(new FileSystemAppSource(TestPaths.SampleApp));

    [Fact]
    public void WrapControl_UnknownType_FallsBackToBaseFacade()
    {
        using var engine = CreateEngine();
        var control = new PlainTestControl();

        var facade = JsFacadeRegistry.WrapControl(engine, control);

        Assert.IsType<JsControl>(facade);
        Assert.Same(control, facade.Control);
    }

    [Fact]
    public void WrapControl_RegisteredType_UsesFactory()
    {
        using var engine = CreateEngine();
        JsFacadeRegistry.Register<PlainTestControl>((host, c) => new JsControl(host, c));

        var facade = JsFacadeRegistry.WrapControl(engine, new PlainTestControl());

        Assert.IsType<JsControl>(facade);
    }

    [Fact]
    public void WrapControl_DerivedType_WalksTypeHierarchy()
    {
        using var engine = CreateEngine();
        JsFacadeRegistry.Register<BaseTestControl>((host, c) => new JsControl(host, c));

        var facade = JsFacadeRegistry.WrapControl(engine, new DerivedTestControl());

        Assert.NotNull(facade);
        Assert.IsType<DerivedTestControl>(facade.Control);
    }

    [Fact]
    public void WrapControl_NullArguments_Throw()
    {
        using var engine = CreateEngine();
        Assert.Throws<ArgumentNullException>(() => JsFacadeRegistry.WrapControl(null!, new Label()));
        Assert.Throws<ArgumentNullException>(() => JsFacadeRegistry.WrapControl(engine, null!));
    }

    [Fact]
    public void WrapStripItem_FallsBackToBaseFacade()
    {
        using var engine = CreateEngine();
        var item = new ToolStripSeparator();

        var facade = JsFacadeRegistry.WrapStripItem(engine, item);

        Assert.IsType<JsToolStripItem>(facade);
        Assert.Same(item, facade.Item);
    }

    [Fact]
    public void WrapStripItem_RegisteredBaseType_WalksHierarchy()
    {
        using var engine = CreateEngine();
        JsFacadeRegistry.RegisterStripItem<OldSchoolForms.Ui.Controls.Containers.ToolStripItem>(
            (host, item) => new JsToolStripItem(host, item));

        var facade = JsFacadeRegistry.WrapStripItem(engine, new ToolStripButton("x"));

        Assert.NotNull(facade);
    }

    [Fact]
    public void TypedFacade_CamelCaseAccess_ReachesClrProperty()
    {
        using var engine = CreateEngine();
        var label = new Label { Text = "initial" };
        var facade = new JsControl(engine, label);
        engine.Engine.SetValue("c", JsValue.FromObject(engine.Engine, facade));

        engine.Engine.Evaluate("c.text = 'changed'; c.visible = false;");

        Assert.Equal("changed", label.Text);
        Assert.False(label.Visible);
        Assert.Equal("changed", engine.Engine.Evaluate("c.text").ToString());
    }

    [Fact]
    public void TypedFacade_ExposesOwnAndInheritedMembers()
    {
        using var engine = CreateEngine();
        var checkBox = new CheckBox();
        var facade = new JsTestCheckBox(engine, checkBox);
        engine.Engine.SetValue("cb", JsValue.FromObject(engine.Engine, facade));

        engine.Engine.Evaluate("cb.checked = true; cb.text = 'label';");

        Assert.True(checkBox.Checked);
        Assert.Equal("label", checkBox.Text);
    }

    [Fact]
    public void TypedFacade_CustomEvent_FiresAndTearsDown()
    {
        using var engine = CreateEngine();
        var checkBox = new CheckBox();
        var facade = new JsTestCheckBox(engine, checkBox);
        engine.Engine.SetValue("cb", JsValue.FromObject(engine.Engine, facade));
        engine.Engine.Evaluate("var hits = 0; cb.on('checkedchanged', function () { hits++; });");

        checkBox.Checked = true;
        Assert.Equal(1, (int)engine.Engine.Evaluate("hits").AsNumber());

        facade.OffAll();
        checkBox.Checked = false;
        Assert.Equal(1, (int)engine.Engine.Evaluate("hits").AsNumber());
    }

    [Fact]
    public void TypedFacade_BaseEvent_StillWorksThroughOverride()
    {
        using var engine = CreateEngine();
        var button = new Button();
        var facade = new JsControl(engine, button);
        engine.Engine.SetValue("b", JsValue.FromObject(engine.Engine, facade));
        engine.Engine.Evaluate("var clicks = 0; b.on('click', function () { clicks++; });");

        button.PerformClick();
        Assert.Equal(1, (int)engine.Engine.Evaluate("clicks").AsNumber());

        facade.OffAll();
        button.PerformClick();
        Assert.Equal(1, (int)engine.Engine.Evaluate("clicks").AsNumber());
    }

    [Fact]
    public void Find_And_Children_UseRegistry()
    {
        using var engine = CreateEngine();
        JsFacadeRegistry.Register<PlainTestControl>((host, c) => new JsControl(host, c));

        var container = new GroupBox();
        var child = new PlainTestControl { Name = "kid" };
        container.Controls.Add(child);
        var facade = new JsControl(engine, container);

        var found = facade.Find("kid");
        Assert.NotNull(found);
        Assert.Same(child, found!.Control);
        Assert.Single(facade.Children());
    }
}

/// <summary>
/// Tests für die <see cref="JsMarshal"/>-Konvertierungen zwischen JS und CLR.
/// </summary>
public class JsMarshalTests
{
    private static JsEngineHost CreateEngine() =>
        new JsEngineHost(new FileSystemAppSource(TestPaths.SampleApp));

    [Fact]
    public void DateTime_IsoRoundTrip()
    {
        var value = new DateTime(2026, 10, 11, 14, 30, 0);
        var text = JsMarshal.FromDateTime(value);

        Assert.Equal("2026-10-11T14:30:00", text);
        Assert.Equal(value, JsMarshal.ToDateTime(JsValue.FromObject(new Engine(), text), DateTime.MinValue));
    }

    [Fact]
    public void ToDateTime_AcceptsJsDateObject()
    {
        using var engine = CreateEngine();
        var date = engine.Engine.Evaluate("new Date(2026, 9, 11, 8, 0, 0)");

        var converted = JsMarshal.ToNullableDateTime(date);

        Assert.NotNull(converted);
        Assert.Equal(2026, converted!.Value.Year);
        Assert.Equal(10, converted.Value.Month);
    }

    [Fact]
    public void ToDateTime_InvalidFallsBack()
    {
        Assert.Equal(DateTime.MinValue, JsMarshal.ToDateTime(JsValue.FromObject(new Engine(), "kein datum"), DateTime.MinValue));
        Assert.Null(JsMarshal.ToNullableDateTime(JsValue.Undefined));
    }

    [Fact]
    public void ToColor_ParsesHexVariants()
    {
        var engine = new Engine();

        Assert.Equal(Color.FromArgb(255, 0, 0), JsMarshal.ToColor(JsValue.FromObject(engine, "#ff0000"), Color.Empty));
        Assert.Equal(Color.FromArgb(255, 0, 0), JsMarshal.ToColor(JsValue.FromObject(engine, "ff0000"), Color.Empty));
        Assert.Equal(Color.FromArgb(255, 255, 0), JsMarshal.ToColor(JsValue.FromObject(engine, "#ff0"), Color.Empty));
        Assert.Equal(Color.FromArgb(0, 0, 255, 128), JsMarshal.ToColor(JsValue.FromObject(engine, "#800000ff"), Color.Empty));
    }

    [Fact]
    public void ToColor_ParsesObjectForm()
    {
        using var engine = CreateEngine();
        var value = engine.Engine.Evaluate("({ r: 10, g: 20, b: 30 })");

        Assert.Equal(Color.FromArgb(10, 20, 30), JsMarshal.ToColor(value, Color.Empty));
    }

    [Fact]
    public void ToColor_InvalidFallsBack()
    {
        Assert.Equal(Color.Empty, JsMarshal.ToColor(JsValue.FromObject(new Engine(), "#xyz"), Color.Empty));
        Assert.Equal(Color.Empty, JsMarshal.ToColor(JsValue.Null, Color.Empty));
    }

    [Fact]
    public void FromColor_OpaqueVsAlpha()
    {
        Assert.Equal("#ff0000", JsMarshal.FromColor(Color.FromArgb(255, 0, 0)));
        Assert.Equal("#80ff0000", JsMarshal.FromColor(Color.FromArgb(255, 0, 0, 128)));
    }

    [Fact]
    public void ToEnum_IsCaseInsensitiveWithFallback()
    {
        var engine = new Engine();

        Assert.Equal(DockStyle.Fill, JsMarshal.ToEnum(JsValue.FromObject(engine, "fill"), DockStyle.None));
        Assert.Equal(DockStyle.Top, JsMarshal.ToEnum(JsValue.FromObject(engine, "TOP"), DockStyle.None));
        Assert.Equal(DockStyle.None, JsMarshal.ToEnum(JsValue.FromObject(engine, "quatsch"), DockStyle.None));
        Assert.Equal(DockStyle.None, JsMarshal.ToEnum(JsValue.Undefined, DockStyle.None));
    }

    [Fact]
    public void ToArray_HandlesArraySingleAndNull()
    {
        using var engine = CreateEngine();

        var array = engine.Engine.Evaluate("[1, 'two', true]");
        Assert.Equal(new object?[] { 1d, "two", true }, JsMarshal.ToArray(array));

        Assert.Single(JsMarshal.ToArray(JsValue.FromObject(engine.Engine, "single")));
        Assert.Empty(JsMarshal.ToArray(JsValue.Undefined));
    }
}
