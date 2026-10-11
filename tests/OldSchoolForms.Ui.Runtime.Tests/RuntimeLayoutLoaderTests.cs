using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Runtime.Serialization;
using Xunit;

namespace OldSchoolForms.Ui.Runtime.Tests;

/// <summary>
/// Headless-Tests für den <see cref="RuntimeLayoutLoader"/>: Laden in einen
/// gewöhnlichen Container ohne Fenster/Display.
/// </summary>
public class RuntimeLayoutLoaderTests
{
    [Fact]
    public void LoadInto_ParsesNamedControlsAndProperties()
    {
        const string json = """
        {
          "format": "OldSchoolForms.Layout",
          "version": 1,
          "rootSize": { "width": 400, "height": 300 },
          "controls": [
            { "type": "Label", "name": "title", "props": { "text": "Hallo" } },
            { "type": "Button", "name": "ok", "props": { "text": "OK" } }
          ]
        }
        """;

        var target = new Panel();
        var result = new RuntimeLayoutLoader().LoadInto(target, json);

        Assert.Empty(result.Warnings);
        Assert.Equal(2, result.Controls.Count);

        var label = Assert.IsType<Label>(result.Find("title"));
        Assert.Equal("Hallo", label.Text);

        var button = Assert.IsType<Button>(result.Find("ok"));
        Assert.Equal("OK", button.Text);

        Assert.Equal(400, target.Width);
        Assert.Equal(300, target.Height);
        Assert.Equal(2, target.Controls.Count);
    }

    [Fact]
    public void LoadInto_CaseInsensitiveNameLookup()
    {
        const string json = """
        { "format": "OldSchoolForms.Layout", "version": 1,
          "controls": [ { "type": "TextBox", "name": "searchBox", "props": { "text": "x" } } ] }
        """;

        var result = new RuntimeLayoutLoader().LoadInto(new Panel(), json);
        Assert.NotNull(result.Find("SEARCHBOX"));
        Assert.NotNull(result.Find("searchBox"));
    }

    [Fact]
    public void LoadInto_NestedChildren()
    {
        const string json = """
        { "format": "OldSchoolForms.Layout", "version": 1,
          "controls": [
            { "type": "Panel", "name": "outer", "children": [
              { "type": "Button", "name": "inner", "props": { "text": "In" } }
            ] }
          ] }
        """;

        var result = new RuntimeLayoutLoader().LoadInto(new Panel(), json);
        var inner = Assert.IsType<Button>(result.Find("inner"));
        Assert.Equal("In", inner.Text);
        Assert.IsType<Panel>(inner.Parent);
    }

    [Fact]
    public void LoadInto_TabPageUnderTabControl()
    {
        const string json = """
        { "format": "OldSchoolForms.Layout", "version": 1,
          "controls": [
            { "type": "TabControl", "name": "tabs", "children": [
              { "type": "TabPage", "name": "page1", "props": { "text": "Erster" } }
            ] }
          ] }
        """;

        var result = new RuntimeLayoutLoader().LoadInto(new Panel(), json);
        var tabs = Assert.IsType<TabControl>(result.Find("tabs"));
        Assert.Single(tabs.TabPages);
        Assert.Equal("Erster", tabs.TabPages[0].Text);
    }

    [Fact]
    public void LoadInto_DataGridViewColumns()
    {
        const string json = """
        { "format": "OldSchoolForms.Layout", "version": 1,
          "controls": [
            { "type": "DataGridView", "name": "grid", "columns": [
              { "name": "id", "headerText": "ID", "width": 80, "valueType": "int" },
              { "name": "name", "headerText": "Name", "width": 160, "valueType": "string" }
            ] }
          ] }
        """;

        var result = new RuntimeLayoutLoader().LoadInto(new Panel(), json);
        var grid = Assert.IsType<DataGridView>(result.Find("grid"));
        Assert.Equal(2, grid.Columns.Count);
        Assert.Equal(typeof(int), grid.Columns[0].ValueType);
        Assert.Equal("Name", grid.Columns[1].HeaderText);
    }

    [Fact]
    public void LoadInto_UnknownTypeSkippedWithWarning()
    {
        const string json = """
        { "format": "OldSchoolForms.Layout", "version": 1,
          "controls": [ { "type": "Frobnicator", "name": "x" } ] }
        """;

        var result = new RuntimeLayoutLoader().LoadInto(new Panel(), json);
        Assert.Single(result.Warnings);
        Assert.Empty(result.Controls);
    }

    [Fact]
    public void LoadInto_UnknownPropertySkippedWithWarning()
    {
        const string json = """
        { "format": "OldSchoolForms.Layout", "version": 1,
          "controls": [ { "type": "Button", "name": "b", "props": { "nonexistent": 1 } } ] }
        """;

        var result = new RuntimeLayoutLoader().LoadInto(new Panel(), json);
        Assert.Single(result.Warnings);
        Assert.NotNull(result.Find("b"));
    }

    [Fact]
    public void LoadInto_MalformedJsonThrows()
    {
        Assert.Throws<InvalidOperationException>(
            () => new RuntimeLayoutLoader().LoadInto(new Panel(), "{ not json"));
    }

    [Fact]
    public void LoadInto_WrongFormatWarns()
    {
        const string json = """{ "format": "Something.Else", "version": 1, "controls": [] }""";
        var result = new RuntimeLayoutLoader().LoadInto(new Panel(), json);
        Assert.Contains(result.Warnings, w => w.Contains("Unexpected document format"));
    }

    [Fact]
    public void Register_CustomTypeResolves()
    {
        var loader = new RuntimeLayoutLoader();
        loader.Register("MyPanel", typeof(Panel));
        Assert.Same(typeof(Panel), loader.ResolveType("mypanel"));
    }

    [Theory]
    [InlineData("nav_first")]
    [InlineData("NavFirst")]
    [InlineData("nav-first")]
    [InlineData("NAVNEXT")]
    public void ResolveIcon_ResolvesNavigationIconsCaseAndSeparatorInsensitive(string key)
    {
        var icon = RuntimeLayoutLoader.ResolveIcon(key);
        Assert.NotNull(icon);
        Assert.True(icon!.Width > 0);
        Assert.True(icon.Height > 0);
    }

    [Fact]
    public void ResolveIcon_UnknownOrEmptyReturnsNull()
    {
        Assert.Null(RuntimeLayoutLoader.ResolveIcon("does_not_exist"));
        Assert.Null(RuntimeLayoutLoader.ResolveIcon(""));
        Assert.Null(RuntimeLayoutLoader.ResolveIcon(null));
    }

    [Fact]
    public void LoadInto_StripItemIconSetsImageAndDisplayStyle()
    {
        const string json = """
        { "format": "OldSchoolForms.Layout", "version": 1,
          "controls": [
            { "type": "ToolStrip", "name": "strip", "props": { "dock": "Top" }, "items": [
              { "type": "ToolStripButton", "name": "navFirst", "icon": "nav_first",
                "props": { "text": "First", "displayStyle": "Image" } }
            ] }
          ] }
        """;

        var result = new RuntimeLayoutLoader().LoadInto(new Panel(), json);
        Assert.Empty(result.Warnings);

        var item = result.StripItems["navFirst"];
        Assert.NotNull(item.Image);
        Assert.Equal(ToolStripItemDisplayStyle.Image, item.DisplayStyle);
    }

    [Fact]
    public void LoadInto_UnknownStripIconWarnsAndLeavesImageNull()
    {
        const string json = """
        { "format": "OldSchoolForms.Layout", "version": 1,
          "controls": [
            { "type": "ToolStrip", "name": "strip", "items": [
              { "type": "ToolStripButton", "name": "b", "icon": "no_such_icon", "props": { "text": "X" } }
            ] }
          ] }
        """;

        var result = new RuntimeLayoutLoader().LoadInto(new Panel(), json);
        Assert.Contains(result.Warnings, w => w.Contains("no_such_icon"));
        Assert.Null(result.StripItems["b"].Image);
    }
}
