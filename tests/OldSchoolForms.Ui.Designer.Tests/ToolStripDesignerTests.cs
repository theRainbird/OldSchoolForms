using System.Linq;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Designer;
using OldSchoolForms.Ui.Designer.PropertyGrid;
using OldSchoolForms.Ui.Designer.Roslyn;
using Xunit;

namespace OldSchoolForms.Ui.Designer.Tests;

/// <summary>
/// Verifies designer support for <see cref="ToolStrip"/>: code generation of items as inline
/// initializers, parsing them back onto a fresh surface, in-situ item selection/add/delete via
/// <see cref="ToolStripDesignAdapter"/>, and property-grid exposure of the items collection.
/// </summary>
public class ToolStripDesignerTests
{
    private static MouseEventArgs LeftMouseDown(int x, int y)
        => new(MouseButtons.Left, 1, x, y, 0f);

    private static ToolStrip CreateStrip(string name = "toolStrip1")
    {
        var strip = new ToolStrip { Name = name, Size = new Size(300, 29) };
        strip.Items.Add(new ToolStripButton { Text = "Save" });
        strip.Items.Add(new ToolStripLabel { Text = "Status" });
        strip.Items.Add(new ToolStripSeparator());
        return strip;
    }

    [Fact]
    public void Generate_ToolStripWithItems_EmitsInlineInitializers()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        var strip = CreateStrip();
        surface.AddControl(strip, new Point(0, 0));

        string code = new CodeGenerator(surface).Generate("Demo", "MainForm");

        Assert.Contains("toolStrip1.Items.Add(new ToolStripButton { Text = \"Save\" });", code);
        Assert.Contains("toolStrip1.Items.Add(new ToolStripLabel { Text = \"Status\" });", code);
        Assert.Contains("toolStrip1.Items.Add(new ToolStripSeparator());", code);
    }

    [Fact]
    public void Generate_ToolStripMenuItemWithDropDown_EmitsNestedCollectionInitializer()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        var strip = new ToolStrip { Name = "toolStrip1", Size = new Size(300, 29) };
        var file = new ToolStripMenuItem { Text = "File" };
        file.DropDownItems.Add(new ToolStripMenuItem { Text = "Open" });
        strip.Items.Add(file);
        surface.AddControl(strip, new Point(0, 0));

        string code = new CodeGenerator(surface).Generate("Demo", "MainForm");

        Assert.Contains("Text = \"File\"", code);
        Assert.Contains("DropDownItems = {", code);
        Assert.Contains("Text = \"Open\"", code);
    }

    [Fact]
    public void Parse_ToolStripItems_ReconstructsTypesAndText()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        surface.AddControl(CreateStrip(), new Point(0, 0));
        string code = new CodeGenerator(surface).Generate("Demo", "MainForm");

        var loaded = new DesignSurface { SnapToGrid = false };
        new FormParser().Parse(code, loaded);

        var strip = loaded.Items.Select(i => i.Control).OfType<ToolStrip>().Single();
        Assert.Equal(3, strip.Items.Count);
        Assert.IsType<ToolStripButton>(strip.Items[0]);
        Assert.Equal("Save", strip.Items[0].Text);
        Assert.IsType<ToolStripLabel>(strip.Items[1]);
        Assert.Equal("Status", strip.Items[1].Text);
        Assert.IsType<ToolStripSeparator>(strip.Items[2]);
    }

    [Fact]
    public void Parse_NestedDropDownItems_Reconstructed()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        var strip = new ToolStrip { Name = "toolStrip1", Size = new Size(300, 29) };
        var file = new ToolStripMenuItem { Text = "File" };
        file.DropDownItems.Add(new ToolStripMenuItem { Text = "Open" });
        file.DropDownItems.Add(new ToolStripMenuItem { Text = "Save" });
        strip.Items.Add(file);
        surface.AddControl(strip, new Point(0, 0));
        string code = new CodeGenerator(surface).Generate("Demo", "MainForm");

        var loaded = new DesignSurface { SnapToGrid = false };
        new FormParser().Parse(code, loaded);

        var parsedStrip = loaded.Items.Select(i => i.Control).OfType<ToolStrip>().Single();
        var parsedFile = Assert.IsType<ToolStripMenuItem>(parsedStrip.Items[0]);
        Assert.Equal(2, parsedFile.DropDownItems.Count);
        Assert.Equal("Open", parsedFile.DropDownItems[0].Text);
        Assert.Equal("Save", parsedFile.DropDownItems[1].Text);
    }

    [Fact]
    public void RoundTrip_ToolStripItems_StableAcrossRegenerate()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        var strip = CreateStrip();
        var menu = new ToolStripMenuItem { Text = "Edit" };
        menu.DropDownItems.Add(new ToolStripMenuItem { Text = "Copy" });
        strip.Items.Add(menu);
        surface.AddControl(strip, new Point(0, 0));
        string first = new CodeGenerator(surface).Generate("Demo", "MainForm");

        var loaded = new DesignSurface { SnapToGrid = false };
        new FormParser().Parse(first, loaded);
        string second = new CodeGenerator(loaded).Generate("Demo", "MainForm");

        Assert.Equal(
            first.Replace("\r", "").Trim(),
            second.Replace("\r", "").Trim());
    }

    [Fact]
    public void Adapter_TryHitRegion_FalseWhenStripNotPrimarySelection()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        var strip = CreateStrip();
        surface.AddControl(strip, new Point(0, 0));
        surface.Selection.DeselectAll();

        var adapter = new ToolStripDesignAdapter(strip, surface);

        Assert.False(adapter.TryHitRegion(new Point(15, 5)));
    }

    [Fact]
    public void Adapter_ClickItem_SelectsItem()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        var strip = CreateStrip();
        surface.AddControl(strip, new Point(0, 0));
        surface.Selection.Select(surface.FindItem(strip)!);

        var adapter = new ToolStripDesignAdapter(strip, surface);
        var bounds = strip.GetItemBoundsPublic(strip.Items[0]);
        Assert.False(bounds.Width <= 0);

        adapter.OnMouseDown(new Point(bounds.X + 2, 5), LeftMouseDown(bounds.X + 2, 5));

        Assert.Same(strip.Items[0], adapter.SelectedItem);
        Assert.Same(strip.Items[0], adapter.GetSelectedValue());
    }

    [Fact]
    public void Adapter_DeleteSelection_RemovesItemAndSupportsUndo()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        var strip = CreateStrip();
        surface.AddControl(strip, new Point(0, 0));
        surface.Selection.Select(surface.FindItem(strip)!);

        var adapter = new ToolStripDesignAdapter(strip, surface);
        var item = strip.Items[1];
        var bounds = strip.GetItemBoundsPublic(item);
        adapter.OnMouseDown(new Point(bounds.X + 2, 5), LeftMouseDown(bounds.X + 2, 5));
        Assert.Same(item, adapter.SelectedItem);

        Assert.True(adapter.TryDeleteSelection());
        Assert.Equal(2, strip.Items.Count);
        Assert.DoesNotContain(item, strip.Items);
        Assert.Null(adapter.SelectedItem);

        surface.Undo.Undo();
        Assert.Equal(3, strip.Items.Count);
        Assert.Same(item, strip.Items[1]);

        surface.Undo.Redo();
        Assert.Equal(2, strip.Items.Count);
    }

    [Fact]
    public void Adapter_ClickAddButton_AppendsButtonWithUndo()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        var strip = CreateStrip();
        surface.AddControl(strip, new Point(0, 0));
        surface.Selection.Select(surface.FindItem(strip)!);

        var adapter = new ToolStripDesignAdapter(strip, surface);
        var addBounds = strip.GetAddItemButtonBoundsPublic();
        Assert.False(addBounds.Width <= 0);

        adapter.OnMouseDown(new Point(addBounds.X + 2, addBounds.Y + 2),
            LeftMouseDown(addBounds.X + 2, addBounds.Y + 2));

        Assert.Equal(4, strip.Items.Count);
        Assert.IsType<ToolStripButton>(strip.Items[3]);
        Assert.Same(strip.Items[3], adapter.SelectedItem);

        surface.Undo.Undo();
        Assert.Equal(3, strip.Items.Count);
    }

    [Fact]
    public void Adapter_ClearSelection_OnEscapeLikeClear()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        var strip = CreateStrip();
        surface.AddControl(strip, new Point(0, 0));
        surface.Selection.Select(surface.FindItem(strip)!);

        var adapter = new ToolStripDesignAdapter(strip, surface);
        var bounds = strip.GetItemBoundsPublic(strip.Items[0]);
        adapter.OnMouseDown(new Point(bounds.X + 2, 5), LeftMouseDown(bounds.X + 2, 5));
        Assert.NotNull(adapter.SelectedItem);

        Assert.True(adapter.TryClearSelection());
        Assert.Null(adapter.SelectedItem);
        Assert.False(adapter.TryClearSelection());
    }

    [Fact]
    public void Adapter_MovingSelectionAwayFromStrip_ClearsItemSelection()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        var strip = CreateStrip();
        surface.AddControl(strip, new Point(0, 0));
        var button = new Button { Size = new Size(80, 30) };
        surface.AddControl(button, new Point(100, 100));
        surface.Selection.Select(surface.FindItem(strip)!);

        var adapter = new ToolStripDesignAdapter(strip, surface);
        var bounds = strip.GetItemBoundsPublic(strip.Items[0]);
        adapter.OnMouseDown(new Point(bounds.X + 2, 5), LeftMouseDown(bounds.X + 2, 5));
        Assert.NotNull(adapter.SelectedItem);

        surface.Selection.Select(surface.FindItem(button)!);
        Assert.Null(adapter.SelectedItem);
    }

    [Fact]
    public void PropertyService_ToolStripItems_VisibleWithEditorButton()
    {
        var service = new PropertyService();
        var strip = CreateStrip();

        var props = service.GetProperties(strip);

        var itemsProp = props.FirstOrDefault(p => p.Name == "Items");
        Assert.NotNull(itemsProp);
    }

    [Fact]
    public void PropertyService_ToolStripItem_EmitsBaseAndTypeProperties()
    {
        var service = new PropertyService();

        var props = service.GetPropertiesForObject(new ToolStripButton { Text = "X" });
        var names = props.Select(p => p.Name).ToList();

        Assert.Contains("Text", names);
        Assert.Contains("Enabled", names);
        Assert.Contains("Visible", names);
        Assert.Contains("DisplayStyle", names);
        Assert.Contains("CheckOnClick", names);
        Assert.DoesNotContain("IsHovered", names);
        Assert.DoesNotContain("DisplayText", names);
    }
}
