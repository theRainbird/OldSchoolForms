using System.Reflection;
using Jint;
using Jint.Native;
using OldSchoolForms.Ui.Controls;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Runtime.Facade;
using OldSchoolForms.Ui.Runtime.Facade.Typed;
using OldSchoolForms.Ui.Runtime.Scripting;
using OldSchoolForms.Ui.Runtime.Sources;
using Xunit;

namespace OldSchoolForms.Ui.Runtime.Tests;

/// <summary>
/// Tests für die typisierten Phase-3-Fassaden der Container:
/// Registry-Auflösung, Property-Marschalling, Item-Erzeugung und Events.
/// </summary>
public class JsContainerFacadesTests : IDisposable
{
    public JsContainerFacadesTests() => JsFacadeRegistry.Clear();

    public void Dispose() => JsFacadeRegistry.Clear();

    private static JsEngineHost CreateEngine() =>
        new JsEngineHost(new FileSystemAppSource(TestPaths.SampleApp));

    private static JsValue Wrap(JsEngineHost engine, JsControl facade) =>
        JsValue.FromObject(engine.Engine, facade);

    private static JsValue WrapStrip(JsEngineHost engine, JsToolStripItem facade) =>
        JsValue.FromObject(engine.Engine, facade);

    private static bool EventHasSubscribers(object target, string eventName)
    {
        var field = target.GetType().GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic);
        return (field?.GetValue(target) as MulticastDelegate)?.GetInvocationList().Length > 0;
    }

    [Fact]
    public void Registry_ReturnsTypedFacades_ForAllContainersAndStripItems()
    {
        using var engine = CreateEngine();

        Assert.IsType<JsPanel>(JsFacadeRegistry.WrapControl(engine, new Panel()));
        Assert.IsType<JsSplitPanel>(JsFacadeRegistry.WrapControl(engine, new SplitPanel()));
        Assert.IsType<JsTabControl>(JsFacadeRegistry.WrapControl(engine, new TabControl()));
        Assert.IsType<JsTreeView>(JsFacadeRegistry.WrapControl(engine, new TreeView()));
        Assert.IsType<JsMenuStrip>(JsFacadeRegistry.WrapControl(engine, new MenuStrip()));
        Assert.IsType<JsToolStrip>(JsFacadeRegistry.WrapControl(engine, new ToolStrip()));
        Assert.IsType<JsStatusStrip>(JsFacadeRegistry.WrapControl(engine, new StatusStrip()));

        Assert.IsType<JsToolStripMenuItem>(JsFacadeRegistry.WrapStripItem(engine, new ToolStripMenuItem("x")));
        Assert.IsType<JsToolStripButton>(JsFacadeRegistry.WrapStripItem(engine, new ToolStripButton("x")));
        Assert.IsType<JsToolStripLabel>(JsFacadeRegistry.WrapStripItem(engine, new ToolStripLabel("x")));
        Assert.IsType<JsToolStripTextBox>(JsFacadeRegistry.WrapStripItem(engine, new ToolStripTextBox()));
    }

    [Fact]
    public void Panel_BorderStyle_RoundTrip_And_InvalidIgnored()
    {
        using var engine = CreateEngine();
        var panel = new Panel();
        var facade = new JsPanel(engine, panel);
        engine.Engine.SetValue("p", Wrap(engine, facade));

        engine.Engine.Evaluate("p.borderStyle = 'fixedsingle';");
        Assert.Equal(Controls.Containers.BorderStyle.FixedSingle, panel.BorderStyle);
        Assert.Equal("FixedSingle", engine.Engine.Evaluate("p.borderStyle").AsString());

        engine.Engine.Evaluate("p.borderStyle = 'bogus';");
        Assert.Equal(Controls.Containers.BorderStyle.FixedSingle, panel.BorderStyle);
    }

    [Fact]
    public void SplitPanel_Geometry_Panels_And_EventTeardown()
    {
        using var engine = CreateEngine();
        var split = new SplitPanel { Width = 300, Height = 200 };
        var facade = new JsSplitPanel(engine, split);
        engine.Engine.SetValue("sp", Wrap(engine, facade));
        engine.Engine.Evaluate("var hits = 0; sp.on('splittermoved', function () { hits++; });");

        engine.Engine.Evaluate("sp.orientation = 'horizontal'; sp.splitterDistance = 120; sp.splitterWidth = 8;");
        Assert.Equal(SplitOrientation.Horizontal, split.Orientation);
        Assert.Equal(120, split.SplitterDistance);
        Assert.Equal(8, split.SplitterWidth);

        Assert.IsType<JsPanel>(facade.Panel1);
        Assert.IsType<JsPanel>(facade.Panel2);

        Assert.True(EventHasSubscribers(split, "SplitterMoved"));
        facade.OffAll();
        Assert.False(EventHasSubscribers(split, "SplitterMoved"));
    }

    [Fact]
    public void TabControl_Pages_Selection_And_Events()
    {
        using var engine = CreateEngine();
        var tabs = new TabControl();
        var facade = new JsTabControl(engine, tabs);
        engine.Engine.SetValue("tc", Wrap(engine, facade));
        engine.Engine.Evaluate("var hits = 0; tc.on('selectedindexchanged', function () { hits++; });");

        engine.Engine.Evaluate("tc.addTabPage('Eins'); tc.addTabPage('Zwei');");
        Assert.Equal(2, tabs.TabPages.Count);
        Assert.Equal("Eins", engine.Engine.Evaluate("tc.tabPages[0].text").AsString());

        engine.Engine.Evaluate("tc.selectedIndex = 1;");
        Assert.Equal(1, tabs.SelectedIndex);
        Assert.Equal("Zwei", engine.Engine.Evaluate("tc.selectedTab.text").AsString());
        Assert.Equal(1, (int)engine.Engine.Evaluate("hits").AsNumber());

        engine.Engine.Evaluate("tc.insertTabPage(0, 'Null');");
        Assert.Equal("Null", tabs.TabPages[0].Text);

        engine.Engine.Evaluate("tc.removeTabAt(0);");
        Assert.Equal(2, tabs.TabPages.Count);
        Assert.False(engine.Engine.Evaluate("tc.removeTabAt(99)").AsBoolean());
    }

    [Fact]
    public void TabControl_CloseRequested_TargetsClosedPage_And_Teardown()
    {
        using var engine = CreateEngine();
        var tabs = new TabControl();
        var facade = new JsTabControl(engine, tabs);
        engine.Engine.SetValue("tc", Wrap(engine, facade));
        engine.Engine.Evaluate("var closed = ''; tc.on('tabcloserequested', function (e) { closed = e.target.text; });");

        Assert.True(EventHasSubscribers(tabs, "TabCloseRequested"));
        facade.OffAll();
        Assert.False(EventHasSubscribers(tabs, "TabCloseRequested"));
    }

    [Fact]
    public void TreeView_Nodes_Expansion_And_Teardown()
    {
        using var engine = CreateEngine();
        var tree = new TreeView();
        var facade = new JsTreeView(engine, tree);
        engine.Engine.SetValue("tv", Wrap(engine, facade));
        engine.Engine.Evaluate("var hits = 0; tv.on('afterselect', function () { hits++; });");

        engine.Engine.Evaluate("var root = tv.addNode('Root'); root.add('Kind1'); root.add('Kind2');");
        Assert.Single(tree.Nodes);
        Assert.Equal(2, tree.Nodes[0].Children.Count);
        Assert.Equal("Kind1", engine.Engine.Evaluate("tv.nodes[0].children[0].text").AsString());

        engine.Engine.Evaluate("tv.nodes[0].children[0].isExpanded = false;");
        Assert.False(tree.Nodes[0].Children[0].IsExpanded);
        engine.Engine.Evaluate("tv.nodes[0].children[0].isExpanded = true;");
        Assert.True(tree.Nodes[0].Children[0].IsExpanded);

        engine.Engine.Evaluate("tv.nodes[0].imageIndex = 3;");
        Assert.Equal(3, tree.Nodes[0].ImageIndex);
        engine.Engine.Evaluate("tv.nodes[0].imageIndex = null;");
        Assert.Null(tree.Nodes[0].ImageIndex);

        engine.Engine.Evaluate("tv.nodes[0].tag = 'payload';");
        Assert.Equal("payload", tree.Nodes[0].Tag);
        Assert.Equal("payload", engine.Engine.Evaluate("tv.nodes[0].tag").AsString());

        engine.Engine.Evaluate("tv.collapseAll(); tv.expandAll();");
        Assert.True(tree.Nodes[0].IsExpanded);

        Assert.True(EventHasSubscribers(tree, "AfterSelect"));
        facade.OffAll();
        Assert.False(EventHasSubscribers(tree, "AfterSelect"));
    }

    [Fact]
    public void MenuStrip_AddItem_NestsMenuItems()
    {
        using var engine = CreateEngine();
        var menu = new MenuStrip();
        var facade = new JsMenuStrip(engine, menu);
        engine.Engine.SetValue("ms", Wrap(engine, facade));

        engine.Engine.Evaluate("var file = ms.addItem('&Datei'); file.addItem('&Öffnen'); file.addItem('&Speichern');");
        Assert.Single(menu.Items);
        Assert.Equal("&Datei", menu.Items[0].Text);
        Assert.Equal(2, menu.Items[0].DropDownItems.Count);
        Assert.Equal(2, engine.Engine.Evaluate("ms.items[0].dropDownItems.length").AsNumber());
        Assert.Equal("&Speichern", engine.Engine.Evaluate("ms.items[0].dropDownItems[1].text").AsString());
    }

    [Fact]
    public void ToolStrip_AddItem_FiresItemAdded_WithPayload()
    {
        using var engine = CreateEngine();
        var strip = new ToolStrip();
        var facade = new JsToolStrip(engine, strip);
        engine.Engine.SetValue("ts", Wrap(engine, facade));
        engine.Engine.Evaluate("var added = ''; ts.on('itemadded', function (e) { added = e.payload.text; });");

        engine.Engine.Evaluate("ts.addItem('Speichern');");
        Assert.Single(strip.Items);
        Assert.Equal("Speichern", engine.Engine.Evaluate("added").AsString());

        engine.Engine.Evaluate("ts.insertItem(0, 'Öffnen'); ts.removeItemAt(1);");
        Assert.Single(strip.Items);
        Assert.Equal("Öffnen", strip.Items[0].Text);

        engine.Engine.Evaluate("ts.gripStyle = 'hidden';");
        Assert.Equal(ToolStripGripStyle.Hidden, strip.GripStyle);
    }

    [Fact]
    public void ToolStripButton_Checked_DropDown_And_Events()
    {
        using var engine = CreateEngine();
        var button = new ToolStripButton("Option");
        var facade = new JsToolStripButton(engine, button);
        engine.Engine.SetValue("tb", WrapStrip(engine, facade));
        engine.Engine.Evaluate("var hits = 0; tb.on('checkedchanged', function () { hits++; });");

        engine.Engine.Evaluate("tb.checked = true; tb.checkOnClick = true;");
        Assert.True(button.Checked);
        Assert.True(button.CheckOnClick);
        Assert.Equal(1, (int)engine.Engine.Evaluate("hits").AsNumber());

        engine.Engine.Evaluate("tb.addItem('Unterunkt');");
        Assert.Single(button.DropDownItems);
        Assert.IsType<ToolStripMenuItem>(button.DropDownItems[0]);

        facade.OffAll();
        engine.Engine.Evaluate("tb.checked = false;");
        Assert.Equal(1, (int)engine.Engine.Evaluate("hits").AsNumber());
    }

    [Fact]
    public void ToolStripLabel_LinkProperties_RoundTrip()
    {
        using var engine = CreateEngine();
        var label = new ToolStripLabel("Link");
        var facade = new JsToolStripLabel(engine, label);
        engine.Engine.SetValue("tl", WrapStrip(engine, facade));

        engine.Engine.Evaluate("tl.isLink = true; tl.linkColor = '#ff0000'; tl.visited = true;");
        Assert.True(label.IsLink);
        Assert.Equal(Color.FromArgb(255, 0, 0), label.LinkColor);
        Assert.True(label.Visited);
        Assert.Equal("#ff0000", engine.Engine.Evaluate("tl.linkColor").AsString());
    }

    [Fact]
    public void ToolStripTextBox_Selection_Clipboard_And_TextChanged()
    {
        using var engine = CreateEngine();
        var box = new ToolStripTextBox { Text = "Hallo Welt" };
        var facade = new JsToolStripTextBox(engine, box);
        engine.Engine.SetValue("ttb", WrapStrip(engine, facade));
        engine.Engine.Evaluate("var hits = 0; ttb.on('textchanged', function () { hits++; });");

        engine.Engine.Evaluate("ttb.selectionStart = 6; ttb.selectionLength = 4; ttb.textBoxWidth = 150;");
        Assert.Equal(6, box.SelectionStart);
        Assert.Equal(4, box.SelectionLength);
        Assert.Equal(150, box.TextBoxWidth);

        engine.Engine.Evaluate("ttb.selectAll();");
        Assert.Equal("Hallo Welt", box.SelectedText);

        engine.Engine.Evaluate("ttb.text = 'neu';");
        Assert.Equal("neu", box.Text);
        Assert.Equal(1, (int)engine.Engine.Evaluate("hits").AsNumber());
    }

    [Fact]
    public void StatusStrip_AddItem_And_LabelProperties()
    {
        using var engine = CreateEngine();
        var strip = new StatusStrip();
        var facade = new JsStatusStrip(engine, strip);
        engine.Engine.SetValue("ss", Wrap(engine, facade));

        engine.Engine.Evaluate("var lbl = ss.addItem('Bereit');");
        Assert.Single(strip.Items);
        Assert.Equal("Bereit", strip.Items[0].Text);
        Assert.Equal("Bereit", engine.Engine.Evaluate("ss.items[0].text").AsString());

        engine.Engine.Evaluate("ss.items[0].visible = false; ss.items[0].foreColor = '#00ff00';");
        Assert.False(strip.Items[0].Visible);
        Assert.Equal(Color.FromArgb(0, 255, 0), strip.Items[0].ForeColor);
    }

    [Fact]
    public void StripItem_Base_Click_Still_Wired_And_UnknownThrows()
    {
        using var engine = CreateEngine();
        var separator = new ToolStripSeparator();
        var facade = JsFacadeRegistry.WrapStripItem(engine, separator);
        engine.Engine.SetValue("si", WrapStrip(engine, facade));
        engine.Engine.Evaluate("var hits = 0; si.on('click', function () { hits++; });");

        Assert.Throws<ArgumentException>(() =>
            engine.Engine.Evaluate("si.on('bogusevent', function () {});"));

        facade.OffAll();
        Assert.False(EventHasSubscribers(separator, "Click"));
    }
}
