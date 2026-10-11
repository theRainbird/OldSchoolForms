using Jint;
using Jint.Native;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Runtime.Facade;
using OldSchoolForms.Ui.Runtime.Facade.Typed;
using OldSchoolForms.Ui.Runtime.Scripting;
using OldSchoolForms.Ui.Runtime.Sources;
using Xunit;

namespace OldSchoolForms.Ui.Runtime.Tests;

/// <summary>
/// Tests für die typisierten Phase-2-Fassaden der Basis-Controls:
/// Registry-Auflösung, Property-Marschalling und Control-spezifische Events.
/// </summary>
public class JsBasicControlFacadesTests : IDisposable
{
    private record TestItem(string Code, string Name);

    public JsBasicControlFacadesTests() => JsFacadeRegistry.Clear();

    public void Dispose() => JsFacadeRegistry.Clear();

    private static JsEngineHost CreateEngine() =>
        new JsEngineHost(new FileSystemAppSource(TestPaths.SampleApp));

    private static JsValue Wrap(JsEngineHost engine, JsControl facade) =>
        JsValue.FromObject(engine.Engine, facade);

    [Fact]
    public void Registry_ReturnsTypedFacades_ForAllBasicControls()
    {
        using var engine = CreateEngine();

        Assert.IsType<JsButton>(JsFacadeRegistry.WrapControl(engine, new Button()));
        Assert.IsType<JsCheckBox>(JsFacadeRegistry.WrapControl(engine, new CheckBox()));
        Assert.IsType<JsRadioButton>(JsFacadeRegistry.WrapControl(engine, new RadioButton()));
        Assert.IsType<JsTextBox>(JsFacadeRegistry.WrapControl(engine, new TextBox()));
        Assert.IsType<JsMemoBox>(JsFacadeRegistry.WrapControl(engine, new MemoBox()));
        Assert.IsType<JsComboBox>(JsFacadeRegistry.WrapControl(engine, new ComboBox()));
        Assert.IsType<JsListBox>(JsFacadeRegistry.WrapControl(engine, new ListBox()));
        Assert.IsType<JsProgressBar>(JsFacadeRegistry.WrapControl(engine, new ProgressBar()));
        Assert.IsType<JsSpinner>(JsFacadeRegistry.WrapControl(engine, new Spinner()));
        Assert.IsType<JsFontPicker>(JsFacadeRegistry.WrapControl(engine, new FontPicker()));
        Assert.IsType<JsLabel>(JsFacadeRegistry.WrapControl(engine, new Label()));
        Assert.IsType<JsPictureBox>(JsFacadeRegistry.WrapControl(engine, new PictureBox()));
        Assert.IsType<JsDateTimePicker>(JsFacadeRegistry.WrapControl(engine, new DateTimePicker()));
    }

    [Fact]
    public void Button_PerformClick_FiresClickHandler()
    {
        using var engine = CreateEngine();
        var button = new Button();
        var facade = new JsButton(engine, button);
        engine.Engine.SetValue("b", Wrap(engine, facade));
        engine.Engine.Evaluate("var hits = 0; b.on('click', function () { hits++; });");

        engine.Engine.Evaluate("b.performClick();");

        Assert.Equal(1, (int)engine.Engine.Evaluate("hits").AsNumber());
    }

    [Fact]
    public void CheckBox_CheckedRoundTrip_And_Event()
    {
        using var engine = CreateEngine();
        var checkBox = new CheckBox();
        var facade = new JsCheckBox(engine, checkBox);
        engine.Engine.SetValue("cb", Wrap(engine, facade));
        engine.Engine.Evaluate("var hits = 0; cb.on('checkedchanged', function () { hits++; });");

        engine.Engine.Evaluate("cb.checked = true;");
        Assert.True(checkBox.Checked);
        Assert.Equal(1, (int)engine.Engine.Evaluate("hits").AsNumber());
        Assert.True(engine.Engine.Evaluate("cb.checked").AsBoolean());

        facade.OffAll();
        engine.Engine.Evaluate("cb.checked = false;");
        Assert.False(checkBox.Checked);
        Assert.Equal(1, (int)engine.Engine.Evaluate("hits").AsNumber());
    }

    [Fact]
    public void RadioButton_CheckedRoundTrip_And_Event()
    {
        using var engine = CreateEngine();
        var radio = new RadioButton();
        var facade = new JsRadioButton(engine, radio);
        engine.Engine.SetValue("rb", Wrap(engine, facade));
        engine.Engine.Evaluate("var hits = 0; rb.on('checkedchanged', function () { hits++; });");

        engine.Engine.Evaluate("rb.checked = true;");
        Assert.True(radio.Checked);
        Assert.Equal(1, (int)engine.Engine.Evaluate("hits").AsNumber());

        facade.OffAll();
        engine.Engine.Evaluate("rb.checked = false;");
        Assert.Equal(1, (int)engine.Engine.Evaluate("hits").AsNumber());
    }

    [Fact]
    public void TextBox_Selection_PasswordChar_And_TextChanged()
    {
        using var engine = CreateEngine();
        var textBox = new TextBox { Text = "Hello World" };
        var facade = new JsTextBox(engine, textBox);
        engine.Engine.SetValue("tb", Wrap(engine, facade));
        engine.Engine.Evaluate("var hits = 0; tb.on('textchanged', function () { hits++; });");

        engine.Engine.Evaluate("tb.selectionStart = 6; tb.selectionLength = 5; tb.useSystemPasswordChar = true;");
        Assert.Equal(6, textBox.SelectionStart);
        Assert.Equal(5, textBox.SelectionLength);
        Assert.True(textBox.UseSystemPasswordChar);
        Assert.Equal("World", textBox.SelectedText);

        engine.Engine.Evaluate("tb.selectAll(); tb.text = 'abc';");
        Assert.Equal("abc", textBox.Text);
        Assert.Equal(1, (int)engine.Engine.Evaluate("hits").AsNumber());

        facade.OffAll();
        engine.Engine.Evaluate("tb.text = 'xyz';");
        Assert.Equal(1, (int)engine.Engine.Evaluate("hits").AsNumber());
    }

    [Fact]
    public void MemoBox_WordWrap_And_TextChanged()
    {
        using var engine = CreateEngine();
        var memo = new MemoBox { Text = "line one" };
        var facade = new JsMemoBox(engine, memo);
        engine.Engine.SetValue("mb", Wrap(engine, facade));
        engine.Engine.Evaluate("var hits = 0; mb.on('textchanged', function () { hits++; });");

        engine.Engine.Evaluate("mb.wordWrap = true; mb.text = 'changed';");
        Assert.True(memo.WordWrap);
        Assert.Equal("changed", memo.Text);
        Assert.Equal(1, (int)engine.Engine.Evaluate("hits").AsNumber());
    }

    [Fact]
    public void ComboBox_ItemsSelection_And_Events()
    {
        using var engine = CreateEngine();
        var combo = new ComboBox();
        var facade = new JsComboBox(engine, combo);
        engine.Engine.SetValue("co", Wrap(engine, facade));
        engine.Engine.Evaluate("var hits = 0; co.on('selectedindexchanged', function () { hits++; });");

        engine.Engine.Evaluate("co.items = ['a', 'b', 'c']; co.addItem('d');");
        Assert.Equal(4, combo.Items.Count);
        Assert.Equal(4, (int)engine.Engine.Evaluate("co.items.length").AsNumber());
        Assert.Equal("b", engine.Engine.Evaluate("co.items[1]").ToString());

        engine.Engine.Evaluate("co.selectedIndex = 2;");
        Assert.Equal(2, combo.SelectedIndex);
        Assert.Equal("c", combo.SelectedItem);
        Assert.Equal(1, (int)engine.Engine.Evaluate("hits").AsNumber());

        engine.Engine.Evaluate("co.removeItemAt(3); co.clearItems();");
        Assert.Empty(combo.Items);

        engine.Engine.Evaluate("co.dropDownStyle = 'dropdownlist';");
        Assert.Equal(DropDownStyle.DropDownList, combo.DropDownStyle);
        Assert.Equal("DropDownList", engine.Engine.Evaluate("co.dropDownStyle").ToString());
    }

    [Fact]
    public void ComboBox_DisplayValueMember_ResolvesSelectedValue()
    {
        using var engine = CreateEngine();
        var combo = new ComboBox
        {
            DisplayMember = "Name",
            ValueMember = "Code"
        };
        combo.Items.Add(new TestItem("A", "Alpha"));
        combo.Items.Add(new TestItem("B", "Beta"));
        var facade = new JsComboBox(engine, combo);
        engine.Engine.SetValue("co", Wrap(engine, facade));

        engine.Engine.Evaluate("co.selectedIndex = 1;");

        Assert.Equal("B", combo.SelectedValue);
        Assert.Equal("B", engine.Engine.Evaluate("co.selectedValue").ToString());
    }

    [Fact]
    public void ListBox_SelectedIndex_And_Event()
    {
        using var engine = CreateEngine();
        var listBox = new ListBox();
        var facade = new JsListBox(engine, listBox);
        engine.Engine.SetValue("lb", Wrap(engine, facade));
        engine.Engine.Evaluate("var hits = 0; lb.on('selectedindexchanged', function () { hits++; });");

        engine.Engine.Evaluate("lb.items = ['x', 'y']; lb.selectedIndex = 1;");
        Assert.Equal(1, listBox.SelectedIndex);
        Assert.Equal("y", listBox.SelectedItem);
        Assert.Equal(1, (int)engine.Engine.Evaluate("hits").AsNumber());

        facade.OffAll();
        engine.Engine.Evaluate("lb.selectedIndex = 0;");
        Assert.Equal(1, (int)engine.Engine.Evaluate("hits").AsNumber());
    }

    [Fact]
    public void ProgressBar_RangeValueOrientation_And_Event()
    {
        using var engine = CreateEngine();
        var bar = new ProgressBar();
        var facade = new JsProgressBar(engine, bar);
        engine.Engine.SetValue("pb", Wrap(engine, facade));
        engine.Engine.Evaluate("var hits = 0; pb.on('valuechanged', function () { hits++; });");

        engine.Engine.Evaluate("pb.minimum = 10; pb.maximum = 90; pb.value = 42; pb.orientation = 'vertical';");
        Assert.Equal(10, bar.Minimum);
        Assert.Equal(90, bar.Maximum);
        Assert.Equal(42, bar.Value);
        Assert.Equal(Orientation.Vertical, bar.Orientation);
        Assert.Equal(1, (int)engine.Engine.Evaluate("hits").AsNumber());
        Assert.Equal("Vertical", engine.Engine.Evaluate("pb.orientation").ToString());
    }

    [Fact]
    public void Spinner_ActiveColor_And_Event()
    {
        using var engine = CreateEngine();
        var spinner = new Spinner();
        var facade = new JsSpinner(engine, spinner);
        engine.Engine.SetValue("sp", Wrap(engine, facade));
        engine.Engine.Evaluate("var hits = 0; sp.on('activechanged', function () { hits++; });");

        engine.Engine.Evaluate("sp.dotCount = 12; sp.animationInterval = 50; sp.dotColor = '#ff0000'; sp.active = true;");
        Assert.Equal(12, spinner.DotCount);
        Assert.Equal(50, spinner.AnimationInterval);
        Assert.Equal(Color.FromArgb(255, 0, 0), spinner.DotColor);
        Assert.True(spinner.Active);
        Assert.Equal(1, (int)engine.Engine.Evaluate("hits").AsNumber());
        Assert.Equal("#ff0000", engine.Engine.Evaluate("sp.dotColor").ToString());
    }

    [Fact]
    public void FontPicker_Properties_And_Event()
    {
        using var engine = CreateEngine();
        var picker = new FontPicker();
        var facade = new JsFontPicker(engine, picker);
        engine.Engine.SetValue("fp", Wrap(engine, facade));
        engine.Engine.Evaluate("var hits = 0; fp.on('selectedfontchanged', function () { hits++; });");

        engine.Engine.Evaluate("fp.selectedFontFamily = 'Sans'; fp.fontSize = 14; fp.previewText = 'Preview';");
        Assert.Equal("Sans", picker.SelectedFontFamily);
        Assert.Equal(14f, picker.FontSize);
        Assert.Equal("Preview", picker.PreviewText);
        Assert.Equal(1, (int)engine.Engine.Evaluate("hits").AsNumber());
        Assert.False(facade.DroppedDown);
    }

    [Fact]
    public void Label_TextAlign_And_PictureBox_SizeMode()
    {
        using var engine = CreateEngine();
        var label = new Label();
        var labelFacade = new JsLabel(engine, label);
        var pic = new PictureBox();
        var picFacade = new JsPictureBox(engine, pic);
        engine.Engine.SetValue("la", Wrap(engine, labelFacade));
        engine.Engine.SetValue("pi", Wrap(engine, picFacade));

        engine.Engine.Evaluate("la.textAlign = 'middlecenter'; pi.sizeMode = 'zoom';");
        Assert.Equal(ContentAlignment.MiddleCenter, label.TextAlign);
        Assert.Equal(PictureBoxSizeMode.Zoom, pic.SizeMode);
        Assert.Equal("MiddleCenter", engine.Engine.Evaluate("la.textAlign").ToString());
        Assert.Equal("Zoom", engine.Engine.Evaluate("pi.sizeMode").ToString());

        engine.Engine.Evaluate("la.textAlign = 'unsinn'; pi.sizeMode = 'unsinn';");
        Assert.Equal(ContentAlignment.MiddleCenter, label.TextAlign);
        Assert.Equal(PictureBoxSizeMode.Zoom, pic.SizeMode);
    }

    [Fact]
    public void DateTimePicker_ValueIsoMinMaxFormatChecked_And_Events()
    {
        using var engine = CreateEngine();
        var picker = new DateTimePicker();
        var facade = new JsDateTimePicker(engine, picker);
        engine.Engine.SetValue("dtp", Wrap(engine, facade));
        engine.Engine.Evaluate(
            "var vc = 0; var cc = 0; dtp.on('valuechanged', function () { vc++; }); dtp.on('checkedchanged', function () { cc++; });");

        engine.Engine.Evaluate("dtp.value = '2026-03-15T10:30:00';");
        Assert.Equal(new DateTime(2026, 3, 15, 10, 30, 0), picker.Value);
        Assert.Equal("2026-03-15T10:30:00", engine.Engine.Evaluate("dtp.value").ToString());
        Assert.Equal(1, (int)engine.Engine.Evaluate("vc").AsNumber());

        engine.Engine.Evaluate("dtp.minDate = '2026-01-01T00:00:00'; dtp.maxDate = '2026-12-31T23:59:59';");
        Assert.Equal(new DateTime(2026, 1, 1), picker.MinDate);
        Assert.Equal(new DateTime(2026, 12, 31, 23, 59, 59), picker.MaxDate);

        engine.Engine.Evaluate("dtp.format = 'short'; dtp.showUpDown = true; dtp.showCheckBox = true; dtp.checked = false;");
        Assert.Equal(DateTimePickerFormat.Short, picker.Format);
        Assert.True(picker.ShowUpDown);
        Assert.True(picker.ShowCheckBox);
        Assert.False(picker.Checked);
        Assert.Equal(1, (int)engine.Engine.Evaluate("cc").AsNumber());

        facade.OffAll();
        engine.Engine.Evaluate("dtp.value = '2026-04-01T00:00:00'; dtp.checked = true;");
        Assert.Equal(1, (int)engine.Engine.Evaluate("vc").AsNumber());
        Assert.Equal(1, (int)engine.Engine.Evaluate("cc").AsNumber());
    }
}
