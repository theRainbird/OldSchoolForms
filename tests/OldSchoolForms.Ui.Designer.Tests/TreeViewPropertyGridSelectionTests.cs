using System.Linq;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Controls;
using OldSchoolForms.Ui.Designer.Services;
using OldSchoolForms.Ui.Designer.PropertyGrid;
using Xunit;

namespace OldSchoolForms.Ui.Designer.Tests;

/// <summary>
/// Regression test for the PropertyGrid staying empty (white) when a TreeView placed in the
/// designer is selected. The hit-test used the control's parent-relative <see cref="Control.Bounds"/>
/// with a point expressed in the control's own local space, so a control dropped away from the
/// surface origin was never selected and the grid stayed in its no-selection state.
/// </summary>
public class TreeViewPropertyGridSelectionTests
{
    private sealed class TestableDesignSurface : DesignSurface
    {
        public void RaiseMouseDown(MouseEventArgs e) => OnMouseDown(e);
        public void RaiseMouseUp(MouseEventArgs e) => OnMouseUp(e);
    }

    private static MouseEventArgs LeftMouseDown(int x, int y)
        => new(MouseButtons.Left, 1, x, y, 0f);

    private static DesignItem DropTreeViewAt(TestableDesignSurface surface, Point location)
    {
        var treeItem = surface.Toolbox.Items.First(i => i.ControlType == typeof(TreeView));
        surface.BeginExternalDrop(treeItem);
        surface.RaiseMouseDown(LeftMouseDown(location.X, location.Y));
        surface.RaiseMouseUp(LeftMouseDown(location.X, location.Y));
        return surface.Items.Single(i => i.Control is TreeView);
    }

    [Fact]
    public void FindItemAt_SelectsTreeView_DroppedAwayFromOrigin()
    {
        var surface = new TestableDesignSurface { SnapToGrid = false };
        var item = DropTreeViewAt(surface, new Point(150, 150));

        // The TreeView default size is 200x200, so its center is (250, 250) in surface space.
        var hit = surface.FindItemAt(new Point(250, 250));

        Assert.Same(item, hit);
        Assert.Same(surface.RootControl, item.Control.Parent);
    }

    [Fact]
    public void PropertyGrid_Populates_ForSelectedTreeView()
    {
        var surface = new TestableDesignSurface { SnapToGrid = false };
        var item = DropTreeViewAt(surface, new Point(150, 150));

        var selection = surface.Selection;
        selection.Select(item);
        Assert.Same(item.Control, selection.PrimarySelection?.Control);

        var properties = new PropertyService().GetProperties(item.Control);

        Assert.NotEmpty(properties);
    }
}
