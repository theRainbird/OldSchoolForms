using System;
using System.Linq;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Layout;
using OldSchoolForms.Ui.Designer.Services;
using OldSchoolForms.Ui.Rendering;
using Xunit;

namespace OldSchoolForms.Ui.Designer.Tests;

public class DesignerContainerTests
{
    private static MouseEventArgs LeftMouseDown(int x, int y)
        => new(MouseButtons.Left, 1, x, y, 0f);

    /// <summary>
    /// Exposes the protected mouse-event handlers so the designer's mouse routing
    /// can be exercised headlessly without a real input stack.
    /// </summary>
    private sealed class TestableDesignSurface : DesignSurface
    {
        public void RaiseMouseDown(MouseEventArgs e) => OnMouseDown(e);
        public void RaiseMouseMove(MouseEventArgs e) => OnMouseMove(e);
        public void RaiseMouseUp(MouseEventArgs e) => OnMouseUp(e);
    }

    private static Panel CreatePanel(Point location, Size size)
    {
        var panel = new Panel { Location = location, Size = size };
        return panel;
    }

    [Fact]
    public void AddControl_IntoContainer_SetsHierarchy_AndTracksSingleItem()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        var panel = CreatePanel(new Point(30, 30), new Size(200, 200));
        var panelItem = surface.AddControl(panel, new Point(30, 30));

        var button = new Button { Size = new Size(120, 40) };
        var buttonItem = surface.AddControl(button, panel, new Point(70, 70));

        Assert.Same(surface.RootControl, panel.Parent);
        Assert.Same(surface.RootItem, panelItem.ParentItem);
        Assert.Same(panel, button.Parent);
        Assert.Same(panelItem, buttonItem.ParentItem);
        Assert.Equal(new Point(70, 70), button.Location);

        // The control is parented to the container exactly once and tracked once.
        // The root (DesignForm), the panel, and the button are all tracked.
        Assert.Single(panel.Controls);
        Assert.Single(surface.Items, i => i.Control == panel);
        Assert.Single(surface.Items, i => i.Control == button);
        Assert.Equal(3, surface.Items.Count);
        Assert.Same(buttonItem, surface.FindItem(button));
    }

    [Fact]
    public void AddControl_IntoContainer_SnapsWhenGridEnabled()
    {
        var surface = new DesignSurface { SnapToGrid = true };
        var panel = CreatePanel(new Point(30, 30), new Size(200, 200));
        surface.AddControl(panel, new Point(30, 30));

        var button = new Button { Size = new Size(120, 40) };
        surface.AddControl(button, panel, new Point(73, 41));

        // Grid size is 8: (73, 41) snaps to (72, 40).
        Assert.Equal(new Point(72, 40), button.Location);
    }

    [Fact]
    public void FindItemAt_ReturnsNestedControl_AndContainerForEmptyArea()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        var panel = CreatePanel(new Point(30, 30), new Size(200, 200));
        surface.AddControl(panel, new Point(30, 30));
        var button = new Button { Size = new Size(120, 40) };
        surface.AddControl(button, panel, new Point(70, 70));

        // Button occupies surface rectangle (100,100)-(220,140).
        var hit = surface.FindItemAt(new Point(150, 115));
        Assert.Same(button, hit?.Control);

        // An empty area inside the panel resolves to the container itself.
        var empty = surface.FindItemAt(new Point(40, 40));
        Assert.Same(panel, empty?.Control);
    }

    [Fact]
    public void FindItemAt_ReturnsTopLevelControl_OnSurface()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        var button = new Button { Location = new Point(50, 50), Size = new Size(120, 40) };
        surface.AddControl(button, new Point(50, 50));

        var hit = surface.FindItemAt(new Point(60, 60));
        Assert.Same(button, hit?.Control);

        Assert.Same(surface.RootControl, surface.FindItemAt(new Point(5, 5))?.Control);
    }

    [Fact]
    public void FindDropContainerAt_ReturnsInnermostContainer_AndNullOutside()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        var panel = CreatePanel(new Point(30, 30), new Size(200, 200));
        surface.AddControl(panel, new Point(30, 30));

        // Inside the panel -> panel.
        Assert.Same(panel, surface.FindDropContainerAt(new Point(100, 100)));

        // Outside the panel (but inside the root) -> the root itself.
        Assert.Same(surface.RootControl, surface.FindDropContainerAt(new Point(5, 5)));
    }

    [Fact]
    public void DeleteSelected_RemovesNestedControl_FromContainer()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        var panel = CreatePanel(new Point(30, 30), new Size(200, 200));
        surface.AddControl(panel, new Point(30, 30));
        var button = new Button { Size = new Size(120, 40) };
        var buttonItem = surface.AddControl(button, panel, new Point(70, 70));

        surface.Selection.Select(buttonItem);
        surface.DeleteSelected();

        Assert.Empty(panel.Controls);
        Assert.Null(surface.FindItem(button));
        // The root (DesignForm) and the surviving panel remain tracked after removing the button.
        Assert.Equal(2, surface.Items.Count);
        Assert.Same(panel, surface.Items.Single(i => i.Control is Panel).Control);
    }

    [Fact]
    public void Drag_WithinSurface_FollowsMouse_WithoutReparenting()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        var button = new Button { Location = new Point(250, 50), Size = new Size(120, 40) };
        var item = surface.AddControl(button, new Point(250, 50));
        surface.Selection.Select(item);

        var reparented = false;
        surface.Drag.DragReparented += (_, _) => reparented = true;

        surface.Drag.BeginMove(new Point(250, 50));
        surface.Drag.ContinueDrag(new Point(200, 80));

        Assert.Same(surface.RootControl, button.Parent);
        Assert.Equal(new Point(200, 80), button.PointToScreen(Point.Empty));
        Assert.False(reparented);

        surface.Drag.EndDrag();
    }

    [Fact]
    public void Drag_Root_IsNotMoved()
    {
        var surface = new TestableDesignSurface { SnapToGrid = false };
        var rootItem = surface.RootItem;
        surface.Selection.Select(rootItem!);

        var start = rootItem!.Control.PointToScreen(Point.Empty);

        // Drag on the empty form area past the move threshold.
        surface.RaiseMouseDown(LeftMouseDown(10, 10));
        surface.RaiseMouseMove(LeftMouseDown(250, 200));
        surface.RaiseMouseUp(LeftMouseDown(250, 200));

        // The root (design form) cannot be moved.
        Assert.Equal(start, rootItem.Control.PointToScreen(Point.Empty));
        Assert.False(surface.Drag.IsDragging);
    }

    [Fact]
    public void Resize_Root_GrowsViaHandle()
    {
        var surface = new TestableDesignSurface { SnapToGrid = false };
        var rootItem = surface.RootItem;
        surface.Selection.Select(rootItem!);

        var bounds = surface.RootControl!.Bounds;
        var topLeft = surface.RootControl.PointToScreen(Point.Empty);

        surface.Drag.BeginResize(topLeft, ResizeHandle.BottomRight);
        surface.Drag.ContinueDrag(new Point(topLeft.X + bounds.Width + 100,
                                            topLeft.Y + bounds.Height + 100));

        Assert.True(surface.Drag.IsDragging);
        Assert.True(surface.RootControl!.Width > bounds.Width);
        Assert.True(surface.RootControl.Height > bounds.Height);

        surface.Drag.EndDrag();
    }

    [Fact]
    public void Resize_Control_GrowsViaHandle_InteractivePath()
    {
        var surface = new TestableDesignSurface { SnapToGrid = false };
        var button = new Button { Size = new Size(120, 40) };
        var item = surface.AddControl(button, new Point(250, 50));
        surface.Selection.Select(item);

        // The handles are rendered in surface coordinates, so the bottom-right handle sits at
        // the control's surface bottom-right corner. Clicking it and dragging past the move
        // threshold must start a resize (regression: hit-testing used parent-relative bounds).
        var surfaceTL = surface.PointToClient(button.PointToScreen(Point.Empty));
        int half = SelectionService.GetHandleSize() / 2;
        int handleX = surfaceTL.X + button.Width - half;
        int handleY = surfaceTL.Y + button.Height - half;
        int originalWidth = button.Width;

        surface.RaiseMouseDown(new MouseEventArgs(MouseButtons.Left, 1, handleX, handleY, 0f));
        // First move past the threshold starts the resize on the detected handle; the
        // second move actually applies the new size via ContinueDrag.
        surface.RaiseMouseMove(new MouseEventArgs(MouseButtons.Left, 1, handleX + 20, handleY + 20, 0f));
        surface.RaiseMouseMove(new MouseEventArgs(MouseButtons.Left, 1, handleX + 40, handleY + 40, 0f));

        Assert.True(surface.Drag.IsDragging);
        Assert.True(button.Width > originalWidth);

        surface.Drag.EndDrag();
    }

    [Fact]
    public void Drag_LiveReparentsIntoContainer_PreservingSurfacePosition()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        var panel = CreatePanel(new Point(30, 30), new Size(200, 200));
        var panelItem = surface.AddControl(panel, new Point(30, 30));

        var button = new Button { Size = new Size(120, 40) };
        surface.AddControl(button, new Point(250, 50));
        var buttonItem = surface.FindItem(button)!;
        surface.Selection.Select(buttonItem);

        var reparentCount = 0;
        surface.Drag.DragReparented += (_, _) => reparentCount++;

        // Start the drag on the surface, then drag into the panel.
        surface.Drag.BeginMove(new Point(250, 50));
        surface.Drag.ContinueDrag(new Point(100, 100));

        Assert.Same(panel, button.Parent);
        Assert.Same(panelItem, buttonItem.ParentItem);
        Assert.Equal(new Point(70, 70), button.Location);
        Assert.Equal(new Point(100, 100), button.PointToScreen(Point.Empty));
        Assert.Equal(1, reparentCount);

        surface.Drag.EndDrag();
    }

    [Fact]
    public void Drag_WithinSameContainer_DoesNotReparent()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        var panel = CreatePanel(new Point(30, 30), new Size(200, 200));
        surface.AddControl(panel, new Point(30, 30));

        var button = new Button { Size = new Size(120, 40) };
        surface.AddControl(button, panel, new Point(70, 70));
        var buttonItem = surface.FindItem(button)!;
        surface.Selection.Select(buttonItem);

        var reparentCount = 0;
        surface.Drag.DragReparented += (_, _) => reparentCount++;

        // Drag entirely within the panel, grabbing the button's top-left.
        surface.Drag.BeginMove(new Point(100, 100));
        surface.Drag.ContinueDrag(new Point(120, 110));

        Assert.Same(panel, button.Parent);
        Assert.Equal(new Point(90, 80), button.Location);
        Assert.Equal(new Point(120, 110), button.PointToScreen(Point.Empty));
        Assert.Equal(0, reparentCount);

        surface.Drag.EndDrag();
    }

    [Fact]
    public void ToolboxDrop_IntoContainer_PlacesControlOnce()
    {
        var surface = new TestableDesignSurface { SnapToGrid = false };
        var panel = CreatePanel(new Point(30, 30), new Size(200, 200));
        surface.AddControl(panel, new Point(30, 30));

        var buttonItem = surface.Toolbox.Items.First(i => i.ControlType == typeof(Button));
        surface.BeginExternalDrop(buttonItem);
        Assert.True(surface.IsPendingDrop);

        // Click inside the panel at surface coordinates (100,100).
        surface.RaiseMouseDown(LeftMouseDown(100, 100));

        Assert.False(surface.IsPendingDrop);
        Assert.Equal(3, surface.Items.Count);

        var placed = surface.Items.Single(i => i.Control is Button);
        Assert.Same(panel, placed.Control.Parent);
        Assert.Single(panel.Controls);
    }

    [Fact]
    public void ToolboxDrop_IntoGroupBox_IsGeneric_ForAnyContainer()
    {
        var surface = new TestableDesignSurface { SnapToGrid = false };
        var groupBox = new GroupBox { Location = new Point(30, 30), Size = new Size(300, 200) };
        surface.AddControl(groupBox, new Point(30, 30));

        var buttonItem = surface.Toolbox.Items.First(i => i.ControlType == typeof(Button));
        surface.BeginExternalDrop(buttonItem);

        // Click well inside the group box content area.
        surface.RaiseMouseDown(LeftMouseDown(120, 100));

        Assert.False(surface.IsPendingDrop);
        var placed = surface.Items.Single(i => i.Control is Button);
        Assert.Same(groupBox, placed.Control.Parent);
    }

    [Fact]
    public void SurfaceDrop_PlaceAtClickLocation()
    {
        var surface = new TestableDesignSurface { SnapToGrid = false };

        var drop = new Point(100, 100);
        var buttonItem = surface.Toolbox.Items.First(i => i.ControlType == typeof(Button));
        surface.BeginExternalDrop(buttonItem);
        surface.RaiseMouseDown(LeftMouseDown(drop.X, drop.Y));

        var ctrl = surface.Items.Single(i => i.Control is Button).Control;
        // The click point is in surface-local space, so the control lands exactly under the cursor.
        Assert.Equal(drop, ctrl.PointToScreen(Point.Empty));
    }

    [Fact]
    public void ContainerDrop_PlaceNestedAtClickLocation()
    {
        var surface = new TestableDesignSurface { SnapToGrid = false };
        var panel = CreatePanel(new Point(30, 30), new Size(200, 200));
        surface.AddControl(panel, new Point(30, 30));

        var drop = new Point(100, 100);
        var buttonItem = surface.Toolbox.Items.First(i => i.ControlType == typeof(Button));
        surface.BeginExternalDrop(buttonItem);
        surface.RaiseMouseDown(LeftMouseDown(drop.X, drop.Y));

        var placed = surface.Items.Single(i => i.Control is Button);
        Assert.Same(panel, placed.Control.Parent);
        // Surface click (100,100) minus the panel's surface offset (30,30) => panel-local (70,70).
        Assert.Equal(new Point(70, 70), placed.Control.Location);
    }

    [Fact]
    public void Drag_OuterContainer_OverInnerContainer_DoesNotReparentIntoDescendant()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        var outer = CreatePanel(new Point(30, 30), new Size(300, 300));
        surface.AddControl(outer, new Point(30, 30));

        var inner = CreatePanel(new Point(50, 50), new Size(100, 100));
        surface.AddControl(inner, outer, new Point(50, 50));
        var outerItem = surface.FindItem(outer)!;
        surface.Selection.Select(outerItem);

        var reparentCount = 0;
        surface.Drag.DragReparented += (_, _) => reparentCount++;

        // Start the drag on the outer panel (over its empty area, not the inner),
        // then move the pointer over the inner panel. The innermost container under
        // the pointer is the inner panel, which is a descendant of the dragged one.
        surface.Drag.BeginMove(new Point(40, 40));
        surface.Drag.ContinueDrag(new Point(100, 100));

        // The outer panel must never become a child of its own descendant, which
        // would create a parent/child cycle and infinite recursion.
        Assert.Same(surface.RootControl, outer.Parent);
        Assert.Equal(1, reparentCount);

        surface.Drag.EndDrag();
    }

    /// <summary>
    /// Reproduces the real-app scenario where the design surface is nested inside
    /// other controls (so it carries a non-zero offset from the form origin). A
    /// toolbox drop inside a container must still land in that container rather than
    /// silently falling back to the surface because surface-local click coordinates
    /// were treated as form coordinates.
    /// </summary>
    [Fact]
    public void ContainerDrop_WithSurfaceOffset_LayersUnderCursor()
    {
        var host = new Panel { Location = new Point(250, 200), Size = new Size(500, 500) };
        var surface = new TestableDesignSurface { Location = Point.Empty, Size = new Size(400, 400), SnapToGrid = false };
        host.Controls.Add(surface);

        var panel = CreatePanel(new Point(30, 30), new Size(200, 200));
        surface.AddControl(panel, new Point(30, 30));

        var drop = new Point(100, 100);
        var buttonItem = surface.Toolbox.Items.First(i => i.ControlType == typeof(Button));
        surface.BeginExternalDrop(buttonItem);
        surface.RaiseMouseDown(LeftMouseDown(drop.X, drop.Y));

        var placed = surface.Items.Single(i => i.Control is Button);
        Assert.Same(panel, placed.Control.Parent);
        Assert.Equal(new Point(70, 70), placed.Control.Location);
    }

    /// <summary>
    /// Dragging a surface-level control onto a container that sits under a real offset
    /// must reparent the control into that container.
    /// </summary>
    [Fact]
    public void Drag_LiveReparentsIntoOffsetContainer_PreservingSurfacePosition()
    {
        var host = new Panel { Location = new Point(250, 200), Size = new Size(500, 500) };
        var surface = new DesignSurface { Location = Point.Empty, Size = new Size(400, 400), SnapToGrid = false };
        host.Controls.Add(surface);

        var panel = CreatePanel(new Point(30, 30), new Size(200, 200));
        surface.AddControl(panel, new Point(30, 30));

        var button = new Button { Size = new Size(120, 40) };
        surface.AddControl(button, new Point(320, 320));
        var buttonItem = surface.FindItem(button)!;
        surface.Selection.Select(buttonItem);

        var reparentCount = 0;
        surface.Drag.DragReparented += (_, _) => reparentCount++;

        surface.Drag.BeginMove(new Point(320, 320));
        surface.Drag.ContinueDrag(new Point(100, 100));

        Assert.Same(panel, button.Parent);
        Assert.Equal(new Point(70, 70), button.Location);
        Assert.Equal(1, reparentCount);

        surface.Drag.EndDrag();
    }

    /// <summary>
    /// Placing a new control inside a container must not change the container's own
    /// location or size. Reproduces the report that dropping a button into a panel
    /// unexpectedly moved and resized the panel.
    /// </summary>
    [Fact]
    public void Drop_IntoContainer_DoesNotAlterPanelBounds()
    {
        var host = new Panel { Location = new Point(250, 200), Size = new Size(500, 500) };
        var surface = new TestableDesignSurface { Location = Point.Empty, Size = new Size(400, 400), SnapToGrid = false };
        host.Controls.Add(surface);

        var panel = CreatePanel(new Point(30, 30), new Size(200, 150));
        surface.AddControl(panel, new Point(30, 30));

        var beforeLocation = panel.Location;
        var beforeSize = panel.Size;

        var buttonItem = surface.Toolbox.Items.First(i => i.ControlType == typeof(Button));
        surface.BeginExternalDrop(buttonItem);
        surface.RaiseMouseDown(LeftMouseDown(100, 100));

        Assert.Equal(beforeLocation, panel.Location);
        Assert.Equal(beforeSize, panel.Size);

        var placed = surface.Items.Single(i => i.Control is Button).Control;
        Assert.Same(panel, placed.Parent);
        // Surface click (100,100) minus panel surface offset (30,30) => panel-local (70,70).
        Assert.Equal(new Point(70, 70), placed.Location);
        // The button must render under the cursor in form coords: surface (100,100) + host (250,200).
        Assert.Equal(new Point(350, 300), placed.PointToScreen(Point.Empty));
    }

    /// <summary>
    /// The selection chrome for a control nested inside a container must be drawn in
    /// this surface's coordinate space, not in the parent's local space. Without the
    /// conversion the box and handles were offset by the container's surface position,
    /// which read like the container itself moving or resizing.
    /// </summary>
    [Fact]
    public void DrawSelectionChrome_ForNestedControl_DrawnAtSurfacePosition()
    {
        var surface = new DesignSurface { SnapToGrid = false, Size = new Size(400, 400) };
        var panel = CreatePanel(new Point(30, 30), new Size(200, 150));
        surface.AddControl(panel, new Point(30, 30));

        var button = new Button { Location = new Point(70, 70), Size = new Size(120, 40) };
        var buttonItem = surface.AddControl(button, panel, new Point(70, 70));
        surface.Selection.Select(buttonItem);

        using var g = new Graphics();
        g.Zoom = 1f;
        surface.Render(g);

        // Primary chrome: outline inset by 1 with lineWidth 2 and size + 2.
        var cmd = g.GetCommands()
            .FirstOrDefault(c => c.Type == DrawCommandType.DrawRectangle && Math.Abs(c.LineWidth - 2f) < 0.001f);
        Assert.NotNull(cmd);
        Assert.Equal(DrawCommandType.DrawRectangle, cmd!.Type);

        // Button surface position: panel (30,30) + button (70,70) = (100,100,120,40).
        var chrome = new Rectangle((int)(cmd.X / g.Zoom), (int)(cmd.Y / g.Zoom),
                                   (int)(cmd.Width / g.Zoom), (int)(cmd.Height / g.Zoom));
        Assert.Equal(new Rectangle(99, 99, 122, 42), chrome);
    }

    /// <summary>
    /// When the design form is moved off the origin, the selection chrome must still
    /// frame the form border. The form chrome is drawn at the form's own origin and the
    /// surface translates by the form's Location, so the border must land at
    /// Location * zoom (not 2 * Location * zoom). Previously the border was drawn at the
    /// form's Location *and* translated by that same Location, so it landed at 2x while
    /// the chrome used the single Location and the box looked offset by Location * zoom.
    /// </summary>
    [Fact]
    public void DrawSelectionChrome_AlignsWithFormBorder_WhenFormMoved()
    {
        var form = new Form();
        form.Zoom = 1.25f;
        var surface = new DesignSurface { SnapToGrid = false, Size = new Size(1400, 1000) };
        form.Controls.Add(surface);
        surface.BeginDesignForm();

        var designForm = (DesignForm) surface.RootControl!;
        designForm.Location = new Point(100, 50);
        surface.Selection.Select(surface.RootItem!);

        using var g = new Graphics();
        surface.Render(g);

        float zoom = g.Zoom;
        Assert.Equal(1.25f, zoom, 3);

        // Form border (grey 200) is drawn at the form origin and translated by Location,
        // so it must land at Location * zoom, not 2 * Location * zoom.
        var border = g.GetCommands()
            .FirstOrDefault(c => c.Type == DrawCommandType.DrawRectangle
                && c.Color.R == 200 && c.Color.G == 200 && c.Color.B == 200);
        Assert.NotNull(border);
        Assert.Equal(100f * zoom, border!.X, 3);
        Assert.Equal(50f * zoom, border.Y, 3);

        // Primary selection box (inset by 1px, lineWidth 2) must frame the border.
        var frame = g.GetCommands()
            .FirstOrDefault(c => c.Type == DrawCommandType.DrawRectangle
                && Math.Abs(c.LineWidth - 2f * zoom) < 0.001f);
        Assert.NotNull(frame);
        Assert.Equal((100f - 1) * zoom, frame!.X, 3);
        Assert.Equal((50f - 1) * zoom, frame.Y, 3);
    }

    /// <summary>
    /// The selection chrome for a Dock=Fill control on the design form must frame the
    /// form's client area (below the title bar), not the whole form. Previously the Fill
    /// layout sized the control to the parent's full bounds, so the box was taller than
    /// the visible client area by the title-bar height.
    /// </summary>
    [Fact]
    public void DrawSelectionChrome_ForFillDockedControl_FramesClientArea_NotWholeForm()
    {
        var surface = new DesignSurface { SnapToGrid = false, Size = new Size(400, 400) };
        var panel = new Panel { Dock = DockStyle.Fill };
        var panelItem = surface.AddControl(panel, new Point(0, 0));
        surface.Selection.Select(panelItem);

        Assert.Equal(new Rectangle(0, 0, 798, 568), panel.Bounds);

        using var g = new Graphics();
        g.Zoom = 1f;
        surface.Render(g);

        var cmd = g.GetCommands()
            .FirstOrDefault(c => c.Type == DrawCommandType.DrawRectangle && Math.Abs(c.LineWidth - 2f) < 0.001f);
        Assert.NotNull(cmd);
        var chrome = new Rectangle((int)(cmd!.X / g.Zoom), (int)(cmd.Y / g.Zoom),
                                   (int)(cmd.Width / g.Zoom), (int)(cmd.Height / g.Zoom));

        // Client area maps to (1,31,798,568) in surface space; the primary box is inset by 1
        // with a +2 size, giving (0,30,800,570) - not the whole form (0,30,800,602).
        Assert.Equal(new Rectangle(0, 30, 800, 570), chrome);
    }

    /// <summary>
    /// Placing a control inside a FlowLayoutPanel must lay it out immediately by the
    /// flow layout, not leave it at the drop point until the next layout pass.
    /// </summary>
    [Fact]
    public void ToolboxDrop_IntoFlowLayoutPanel_LaysOutImmediately()
    {
        var surface = new TestableDesignSurface { SnapToGrid = false };
        var flow = new FlowLayoutPanel { Location = new Point(30, 30), Size = new Size(200, 200) };
        surface.AddControl(flow, new Point(30, 30));

        var buttonItem = surface.Toolbox.Items.First(i => i.ControlType == typeof(Button));
        surface.BeginExternalDrop(buttonItem);

        // Click inside the flow panel. The flow layout (LeftToRight, no padding)
        // places the first control at its padding origin (0,0), overriding the drop point.
        surface.RaiseMouseDown(LeftMouseDown(100, 100));

        var placed = surface.Items.Single(i => i.Control is Button).Control;
        Assert.Same(flow, placed.Parent);
        Assert.Equal(new Point(0, 0), placed.Location);
    }
}
