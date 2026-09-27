using System.Linq;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Designer;
using OldSchoolForms.Ui.Designer.Dialogs;
using OldSchoolForms.Ui.Designer.PropertyGrid;
using OldSchoolForms.Ui.Designer.Roslyn;
using Xunit;

namespace OldSchoolForms.Ui.Designer.Tests;

/// <summary>
/// Verifies designer support for <see cref="MenuStrip"/> and <see cref="StatusStrip"/>:
/// code generation of items as inline initializers (incl. nested dropdowns), parsing them
/// back onto a fresh surface, in-situ selection/add/delete via the design adapters, the
/// type-picker popup, editor registration, and status-label cloning.
/// </summary>
public class MenuStripStatusStripDesignerTests
{
    private static MouseEventArgs LeftMouseDown(int x, int y)
        => new(MouseButtons.Left, 1, x, y, 0f);

    [Fact]
    public void Generate_MenuStripWithItems_EmitsInlineInitializers()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        var menu = new MenuStrip { Name = "menuStrip1", Size = new Size(300, 29) };
        menu.Items.Add(new ToolStripMenuItem("File"));
        menu.Items.Add(new ToolStripMenuItem("Edit"));
        surface.AddControl(menu, new Point(0, 0));

        string code = new CodeGenerator(surface).Generate("Demo", "MainForm");

        Assert.Contains("menuStrip1.Items.Add(new ToolStripMenuItem { Text = \"File\" });", code);
        Assert.Contains("menuStrip1.Items.Add(new ToolStripMenuItem { Text = \"Edit\" });", code);
    }

    [Fact]
    public void Generate_MenuStripNestedDropDown_EmitsNestedCollectionInitializer()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        var menu = new MenuStrip { Name = "menuStrip1", Size = new Size(300, 29) };
        var file = new ToolStripMenuItem("File");
        file.DropDownItems.Add(new ToolStripMenuItem("Open"));
        menu.Items.Add(file);
        surface.AddControl(menu, new Point(0, 0));

        string code = new CodeGenerator(surface).Generate("Demo", "MainForm");

        Assert.Contains("DropDownItems = {", code);
        Assert.Contains("Text = \"Open\"", code);
    }

    [Fact]
    public void Generate_StatusStripWithLabels_EmitsInlineInitializers()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        var status = new StatusStrip { Name = "statusStrip1", Size = new Size(300, 25) };
        status.Items.Add(new ToolStripStatusLabel { Text = "Ready" });
        surface.AddControl(status, new Point(0, 0));

        string code = new CodeGenerator(surface).Generate("Demo", "MainForm");

        Assert.Contains("statusStrip1.Items.Add(new ToolStripStatusLabel { Text = \"Ready\" });", code);
    }

    [Fact]
    public void RoundTrip_MenuStripItems_ReconstructsItemsAndNesting()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        var menu = new MenuStrip { Name = "menuStrip1", Size = new Size(300, 29) };
        var file = new ToolStripMenuItem("File");
        file.DropDownItems.Add(new ToolStripMenuItem("Open"));
        menu.Items.Add(file);
        menu.Items.Add(new ToolStripMenuItem("Edit"));
        surface.AddControl(menu, new Point(0, 0));
        string first = new CodeGenerator(surface).Generate("Demo", "MainForm");

        var loaded = new DesignSurface { SnapToGrid = false };
        new FormParser().Parse(first, loaded);

        var parsed = loaded.Items.Select(i => i.Control).OfType<MenuStrip>().Single();
        Assert.Equal(2, parsed.Items.Count);
        Assert.Equal("File", parsed.Items[0].Text);
        Assert.Single(parsed.Items[0].DropDownItems);
        Assert.Equal("Open", parsed.Items[0].DropDownItems[0].Text);
        Assert.Equal("Edit", parsed.Items[1].Text);
    }

    [Fact]
    public void RoundTrip_StatusStripLabels_StableAcrossRegenerate()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        var status = new StatusStrip { Name = "statusStrip1", Size = new Size(300, 25) };
        status.Items.Add(new ToolStripStatusLabel { Text = "Ready" });
        status.Items.Add(new ToolStripStatusLabel { Text = "Hidden", Visible = false });
        surface.AddControl(status, new Point(0, 0));
        string first = new CodeGenerator(surface).Generate("Demo", "MainForm");

        var loaded = new DesignSurface { SnapToGrid = false };
        new FormParser().Parse(first, loaded);
        string second = new CodeGenerator(loaded).Generate("Demo", "MainForm");

        Assert.Equal(first.Replace("\r", "").Trim(), second.Replace("\r", "").Trim());
    }

    [Fact]
    public void Parse_StatusStripLabels_ReconstructsVisible()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        var status = new StatusStrip { Name = "statusStrip1", Size = new Size(300, 25) };
        status.Items.Add(new ToolStripStatusLabel { Text = "A" });
        status.Items.Add(new ToolStripStatusLabel { Text = "B", Visible = false });
        surface.AddControl(status, new Point(0, 0));
        string code = new CodeGenerator(surface).Generate("Demo", "MainForm");

        var loaded = new DesignSurface { SnapToGrid = false };
        new FormParser().Parse(code, loaded);

        var parsed = loaded.Items.Select(i => i.Control).OfType<StatusStrip>().Single();
        Assert.Equal(2, parsed.Items.Count);
        Assert.True(parsed.Items[0].Visible);
        Assert.False(parsed.Items[1].Visible);
    }

    [Fact]
    public void MenuStripAdapter_ClickItem_SelectsItem()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        var menu = new MenuStrip { Name = "menuStrip1", Size = new Size(300, 29) };
        menu.Items.Add(new ToolStripMenuItem("File"));
        surface.AddControl(menu, new Point(0, 0));
        surface.Selection.Select(surface.FindItem(menu)!);

        var adapter = new MenuStripDesignAdapter(menu, surface);
        var bounds = menu.GetItemBoundsPublic(menu.Items[0]);
        adapter.OnMouseDown(new Point(bounds.X + 2, 5), LeftMouseDown(bounds.X + 2, 5));

        Assert.Same(menu.Items[0], adapter.SelectedItem);
        Assert.Same(menu.Items[0], adapter.GetSelectedValue());
    }

    [Fact]
    public void MenuStripAdapter_DeleteSelection_RemovesWithUndo()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        var menu = new MenuStrip { Name = "menuStrip1", Size = new Size(300, 29) };
        menu.Items.Add(new ToolStripMenuItem("File"));
        surface.AddControl(menu, new Point(0, 0));
        surface.Selection.Select(surface.FindItem(menu)!);

        var adapter = new MenuStripDesignAdapter(menu, surface);
        var item = menu.Items[0];
        var bounds = menu.GetItemBoundsPublic(item);
        adapter.OnMouseDown(new Point(bounds.X + 2, 5), LeftMouseDown(bounds.X + 2, 5));

        Assert.True(adapter.TryDeleteSelection());
        Assert.Empty(menu.Items);
        surface.Undo.Undo();
        Assert.Single(menu.Items);
    }

    [Fact]
    public void MenuStripAdapter_AddPopup_SingleMenuItemEntry()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        var menu = new MenuStrip { Name = "menuStrip1", Size = new Size(300, 29) };
        surface.AddControl(menu, new Point(0, 0));
        surface.Selection.Select(surface.FindItem(menu)!);

        var adapter = new MenuStripDesignAdapter(menu, surface);
        var addBounds = menu.GetAddItemButtonBoundsPublic();
        adapter.OnMouseDown(new Point(addBounds.X + 2, addBounds.Y + 2),
            LeftMouseDown(addBounds.X + 2, addBounds.Y + 2));

        Assert.True(adapter.TryGetAddPopup(out var popupBounds, out var labels, out _));
        Assert.Single(labels);
        adapter.OnMouseDown(new Point(popupBounds.X + 2, popupBounds.Y + 12),
            LeftMouseDown(popupBounds.X + 2, popupBounds.Y + 12));
        Assert.Single(menu.Items);
        Assert.IsType<ToolStripMenuItem>(menu.Items[0]);
    }

    [Fact]
    public void MenuStripAdapter_TryHitRegion_FalseWhenNotPrimary()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        var menu = new MenuStrip { Name = "menuStrip1", Size = new Size(300, 29) };
        menu.Items.Add(new ToolStripMenuItem("File"));
        surface.AddControl(menu, new Point(0, 0));
        surface.Selection.DeselectAll();

        var adapter = new MenuStripDesignAdapter(menu, surface);
        Assert.False(adapter.TryHitRegion(new Point(10, 5)));
    }

    [Fact]
    public void StatusStripAdapter_ClickLabel_SelectsAndDeletes()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        var status = new StatusStrip { Name = "statusStrip1", Size = new Size(300, 25) };
        status.Items.Add(new ToolStripStatusLabel { Text = "Ready" });
        surface.AddControl(status, new Point(0, 0));
        surface.Selection.Select(surface.FindItem(status)!);

        var adapter = new StatusStripDesignAdapter(status, surface);
        var bounds = status.GetItemBoundsPublic(status.Items[0]);
        adapter.OnMouseDown(new Point(bounds.X + 2, 5), LeftMouseDown(bounds.X + 2, 5));
        Assert.Same(status.Items[0], adapter.SelectedItem);

        Assert.True(adapter.TryDeleteSelection());
        Assert.Empty(status.Items);
        surface.Undo.Undo();
        Assert.Single(status.Items);
    }

    [Fact]
    public void StatusStripAdapter_AddPopup_SingleLabelEntry()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        var status = new StatusStrip { Name = "statusStrip1", Size = new Size(300, 25) };
        surface.AddControl(status, new Point(0, 0));
        surface.Selection.Select(surface.FindItem(status)!);

        var adapter = new StatusStripDesignAdapter(status, surface);
        var addBounds = status.GetAddItemButtonBoundsPublic();
        adapter.OnMouseDown(new Point(addBounds.X + 2, addBounds.Y + 2),
            LeftMouseDown(addBounds.X + 2, addBounds.Y + 2));
        Assert.True(adapter.TryGetAddPopup(out var popupBounds, out var labels, out _));
        Assert.Single(labels);
        adapter.OnMouseDown(new Point(popupBounds.X + 2, popupBounds.Y + 12),
            LeftMouseDown(popupBounds.X + 2, popupBounds.Y + 12));
        Assert.Single(status.Items);
        Assert.IsType<ToolStripStatusLabel>(status.Items[0]);
    }

    [Fact]
    public void MenuStripItemsEditor_Handles_MenuStripItems()
    {
        var editor = new MenuStripItemsEditor();
        var prop = typeof(MenuStrip).GetProperty(nameof(MenuStrip.Items))!;
        Assert.True(editor.Handles(prop));
        var other = typeof(MenuStrip).GetProperty(nameof(MenuStrip.Name))!;
        Assert.False(editor.Handles(other));
    }

    [Fact]
    public void StatusStripItemsEditor_Handles_StatusStripItems()
    {
        var editor = new StatusStripItemsEditor();
        var prop = typeof(StatusStrip).GetProperty(nameof(StatusStrip.Items))!;
        Assert.True(editor.Handles(prop));
    }

    [Fact]
    public void CloneStatusLabel_CopiesAllProperties()
    {
        var source = new ToolStripStatusLabel { Text = "X", Visible = false };
        var clone = StatusStripItemsEditorDialog.CloneStatusLabel(source);
        Assert.Equal("X", clone.Text);
        Assert.False(clone.Visible);
        Assert.NotSame(source, clone);
    }
}
