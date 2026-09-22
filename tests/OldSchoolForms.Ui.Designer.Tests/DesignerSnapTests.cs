using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Designer.Services;
using Xunit;

namespace OldSchoolForms.Ui.Designer.Tests;

/// <summary>
/// Snap-line behavior: controls align to sibling edges, to the client edges of their
/// container, and to the client edges of the form, both for single and multi-select drags.
/// </summary>
public class DesignerSnapTests
{
    private sealed class TestableDesignSurface : DesignSurface
    {
        public void RaiseMouseDown(MouseEventArgs e) => OnMouseDown(e);
        public void RaiseMouseMove(MouseEventArgs e) => OnMouseMove(e);
        public void RaiseMouseUp(MouseEventArgs e) => OnMouseUp(e);
    }

    private static MouseEventArgs LeftMouseDown(int x, int y)
        => new(MouseButtons.Left, 1, x, y, 0f);

    [Fact]
    public void Drag_SingleControl_SnapsToFormClientEdge()
    {
        var surface = new TestableDesignSurface { SnapToGrid = false };
        var button = new Button { Size = new Size(120, 40) };
        var item = surface.AddControl(button, new Point(250, 50));
        surface.Selection.Select(item);

        surface.Drag.BeginMove(new Point(250, 50));
        // Move the button's top-left near the form's client top-left (1,31).
        surface.Drag.ContinueDrag(new Point(2, 32));

        // Left and top edges snap to the form's client area origin.
        Assert.Equal(new Point(1, 31), button.PointToScreen(Point.Empty));
        Assert.Contains(surface.Drag.CurrentSnap.GuideLines, g => g.Kind == GuideKind.Container);
    }

    [Fact]
    public void Drag_SingleControl_SnapsToContainerClientEdge()
    {
        var surface = new TestableDesignSurface { SnapToGrid = false };
        var panel = new Panel { BorderStyle = BorderStyle.None, Size = new Size(200, 200) };
        surface.AddControl(panel, new Point(30, 30));

        var button = new Button { Size = new Size(120, 40) };
        var buttonItem = surface.AddControl(button, panel, new Point(70, 70));
        surface.Selection.Select(buttonItem);

        // Button surface top-left: panel (30,30) + (70,70) = (100,100).
        surface.Drag.BeginMove(new Point(100, 100));
        // Drag so the button's right edge approaches the panel client right edge (230).
        surface.Drag.ContinueDrag(new Point(108, 197));

        // The panel client area spans surface (30,30,200,200), so its right edge is 230.
        Assert.Equal(230, button.PointToScreen(Point.Empty).X + button.Width);
    }

    [Fact]
    public void Drag_MultiSelect_SnapsAsGroup()
    {
        var surface = new TestableDesignSurface { SnapToGrid = false };
        var a = new Button { Size = new Size(100, 40) };
        var b = new Button { Size = new Size(100, 40) };
        var aItem = surface.AddControl(a, new Point(200, 100));
        var bItem = surface.AddControl(b, new Point(350, 100));
        surface.Selection.Select(aItem);
        surface.Selection.ToggleSelection(bItem);

        surface.Drag.BeginMove(new Point(200, 100));
        // Move the group so its left edge approaches the form client left edge (1).
        surface.Drag.ContinueDrag(new Point(4, 100));

        // Both controls shift left by the same delta so the group left edge snaps to 1.
        Assert.Equal(new Point(1, 100), a.PointToScreen(Point.Empty));
        Assert.Equal(new Point(151, 100), b.PointToScreen(Point.Empty));
    }

    [Fact]
    public void Drag_SnapsIndependentlyOfSnapToGrid()
    {
        var surface = new TestableDesignSurface { SnapToGrid = false };
        var button = new Button { Size = new Size(120, 40) };
        var item = surface.AddControl(button, new Point(250, 50));
        surface.Selection.Select(item);

        surface.Drag.BeginMove(new Point(250, 50));
        surface.Drag.ContinueDrag(new Point(2, 32));

        Assert.Equal(new Point(1, 31), button.PointToScreen(Point.Empty));
    }

    [Fact]
    public void Drag_NoSnapWhenFarFromAnyEdge()
    {
        var surface = new TestableDesignSurface { SnapToGrid = false };
        var button = new Button { Size = new Size(120, 40) };
        var item = surface.AddControl(button, new Point(250, 50));
        surface.Selection.Select(item);

        surface.Drag.BeginMove(new Point(250, 50));
        surface.Drag.ContinueDrag(new Point(200, 150));

        // Well inside the form and far from any edge: no snap delta is applied.
        Assert.Equal(new Point(200, 150), button.PointToScreen(Point.Empty));
        Assert.False(surface.Drag.CurrentSnap.HasSnap);
    }

    [Fact]
    public void Drag_FullMouseFlow_SnapsToFormClientEdge()
    {
        var surface = new TestableDesignSurface { SnapToGrid = false };
        var button = new Button { Size = new Size(120, 40) };
        var item = surface.AddControl(button, new Point(250, 50));
        surface.Selection.Select(item);

        // Click the button center (off the resize handles), then drag the top-left near
        // the form client top-left. The first move past the threshold starts the drag;
        // the second applies it.
        surface.RaiseMouseDown(LeftMouseDown(310, 70));
        surface.RaiseMouseMove(LeftMouseDown(63, 53));
        surface.RaiseMouseMove(LeftMouseDown(64, 54));

        // Button top-left: (250,50) + ((64,54) - (310,70)) = (4,32), snaps to (1,31).
        Assert.Equal(new Point(1, 31), button.PointToScreen(Point.Empty));
        Assert.Contains(surface.Drag.CurrentSnap.GuideLines, g => g.Kind == GuideKind.Container);
    }

    [Fact]
    public void Drag_FullMouseFlow_SnapsToContainerClientEdge()
    {
        var surface = new TestableDesignSurface { SnapToGrid = false };
        var panel = new Panel { BorderStyle = BorderStyle.None, Size = new Size(200, 200) };
        surface.AddControl(panel, new Point(30, 30));

        var button = new Button { Size = new Size(120, 40) };
        var buttonItem = surface.AddControl(button, panel, new Point(70, 70));
        surface.Selection.Select(buttonItem);

        // Button surface top-left: panel (30,30) + (70,70) = (100,100), center (160,120).
        // Click the center (off the resize handles) and drag so the right edge nears (but does
        // not yet reach) the panel client right edge 230, then snaps to it.
        surface.RaiseMouseDown(LeftMouseDown(160, 120));
        surface.RaiseMouseMove(LeftMouseDown(170, 130));  // past threshold, starts the drag
        surface.RaiseMouseMove(LeftMouseDown(168, 170));  // right edge at 228 -> snaps to 230

        // Button top-left: (100,100) + ((168,170) - (160,120)) = (108,150), right edge 228,
        // then snaps +2 to the panel client right edge 230.
        Assert.Equal(230, button.PointToScreen(Point.Empty).X + button.Width);
        Assert.Contains(surface.Drag.CurrentSnap.GuideLines, g => g.Kind == GuideKind.Container);
    }
}
