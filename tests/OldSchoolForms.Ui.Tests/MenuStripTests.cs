using System.Reflection;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Controls.Containers;
using Xunit;

namespace OldSchoolForms.Ui.Tests;

/// <summary>
/// Tests for MenuStrip top-level item mouse behavior, verifying that clicking a
/// second top-level item while a dropdown is open switches the open dropdown.
/// </summary>
public class MenuStripTests
{
    private static bool GetDropDownVisible(MenuStrip menuStrip)
    {
        return (bool)typeof(MenuStrip)
            .GetField("_dropDownVisible", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(menuStrip)!;
    }

    private static ToolStripMenuItem? GetOpenItem(MenuStrip menuStrip)
    {
        return typeof(MenuStrip)
            .GetField("_openItem", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(menuStrip) as ToolStripMenuItem;
    }

    private static MenuStrip CreateMenuStripWithTwoTopItems(
        out ToolStripMenuItem fileItem,
        out ToolStripMenuItem viewItem)
    {
        var menuStrip = new MenuStrip { Size = new Size(300, 24) };
        fileItem = new ToolStripMenuItem("File");
        fileItem.DropDownItems.Add(new ToolStripMenuItem("New"));
        viewItem = new ToolStripMenuItem("View");
        viewItem.DropDownItems.Add(new ToolStripMenuItem("Zoom"));
        menuStrip.Items.Add(fileItem);
        menuStrip.Items.Add(viewItem);
        return menuStrip;
    }

    [Fact]
    public void ClickFirstTopLevel_OpensDropdown()
    {
        var menuStrip = CreateMenuStripWithTwoTopItems(out var fileItem, out _);
        var fileBounds = menuStrip.GetItemBoundsPublic(fileItem);

        menuStrip.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, fileBounds.X + 2, fileBounds.Y + 2, 0));

        Assert.True(GetDropDownVisible(menuStrip));
        Assert.Same(fileItem, GetOpenItem(menuStrip));
    }

    [Fact]
    public void ClickSecondTopLevel_SwitchesOpenDropdown()
    {
        var menuStrip = CreateMenuStripWithTwoTopItems(out var fileItem, out var viewItem);
        var fileBounds = menuStrip.GetItemBoundsPublic(fileItem);
        var viewBounds = menuStrip.GetItemBoundsPublic(viewItem);

        menuStrip.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, fileBounds.X + 2, fileBounds.Y + 2, 0));
        Assert.Same(fileItem, GetOpenItem(menuStrip));

        menuStrip.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, viewBounds.X + 2, viewBounds.Y + 2, 0));

        Assert.True(GetDropDownVisible(menuStrip));
        Assert.Same(viewItem, GetOpenItem(menuStrip));
    }

    [Fact]
    public void FormRouting_ClickSecondTopLevel_SwitchesOpenDropdown()
    {
        var form = new Form { Size = new Size(600, 400) };
        var menuStrip = CreateMenuStripWithTwoTopItems(out var fileItem, out var viewItem);
        menuStrip.Location = new Point(0, 0);
        form.Controls.Add(menuStrip);

        var fileBounds = menuStrip.GetItemBoundsPublic(fileItem);
        var viewBounds = menuStrip.GetItemBoundsPublic(viewItem);

        form.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, fileBounds.X + 2, fileBounds.Y + 2, 0));
        form.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, fileBounds.X + 2, fileBounds.Y + 2, 0));
        Assert.Same(fileItem, GetOpenItem(menuStrip));

        form.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, viewBounds.X + 2, viewBounds.Y + 2, 0));
        form.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, viewBounds.X + 2, viewBounds.Y + 2, 0));

        Assert.True(GetDropDownVisible(menuStrip));
        Assert.Same(viewItem, GetOpenItem(menuStrip));
    }

    private static void ClearDirty(Control control)
    {
        typeof(Control)
            .GetField("_dirty", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(control, false);
        if (control is Form form)
        {
            typeof(Form)
                .GetField("_requiresRender", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(form, false);
        }

        foreach (Control child in control.Controls)
        {
            ClearDirty(child);
        }
    }

    [Fact]
    public void SwitchSecondTopLevel_MarksFormRequiresRender()
    {
        var form = new Form { Size = new Size(600, 400) };
        var menuStrip = CreateMenuStripWithTwoTopItems(out var fileItem, out var viewItem);
        menuStrip.Location = new Point(0, 0);
        form.Controls.Add(menuStrip);

        var fileBounds = menuStrip.GetItemBoundsPublic(fileItem);
        var viewBounds = menuStrip.GetItemBoundsPublic(viewItem);

        form.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, fileBounds.X + 2, fileBounds.Y + 2, 0));
        form.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, fileBounds.X + 2, fileBounds.Y + 2, 0));
        Assert.Same(fileItem, GetOpenItem(menuStrip));

        // Clear every dirty flag so only the switch's own Invalidate can mark the form.
        ClearDirty(form);
        Assert.False(form.RequiresRender);

        form.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, viewBounds.X + 2, viewBounds.Y + 2, 0));

        Assert.Same(viewItem, GetOpenItem(menuStrip));
        Assert.True(form.RequiresRender);
    }
}
