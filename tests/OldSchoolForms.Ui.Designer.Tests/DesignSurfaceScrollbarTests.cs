using System.Linq;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Rendering;
using Xunit;

namespace OldSchoolForms.Ui.Designer.Tests;

/// <summary>
/// Verifies that the design surface shows scrollbars whenever the root control
/// (design form or user control) is larger than the visible surface area, that the
/// scrollbars are painted, and that the mouse wheel pans the root so off-screen
/// content becomes reachable.
/// </summary>
public class DesignSurfaceScrollbarTests
{
    private sealed class TestableDesignSurface : DesignSurface
    {
        public void RaiseMouseWheel(float delta)
            => OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, 0, 0, delta));
    }

    [Fact]
    public void ScrollbarsAppearWhenFormBiggerThanSurface()
    {
        // The default design form is 800x600; a 400x400 surface overflows on both axes.
        var surface = new TestableDesignSurface { SnapToGrid = false, Size = new Size(400, 400) };
        surface.Render(new Graphics()); // apply layout so scrollbar ranges reflect content size

        Assert.True(surface._vscroll.NeedsScrollbar);
        Assert.True(surface._hscroll.NeedsScrollbar);
    }

    [Fact]
    public void NoScrollbarsWhenFormSmallerThanSurface()
    {
        // A 1000x800 surface fully contains the 800x600 form.
        var surface = new TestableDesignSurface { SnapToGrid = false, Size = new Size(1000, 800) };
        surface.Render(new Graphics());

        Assert.False(surface._vscroll.NeedsScrollbar);
        Assert.False(surface._hscroll.NeedsScrollbar);
    }

    [Fact]
    public void ScrollbarsAreDrawnWhenOverflowing()
    {
        var surface = new TestableDesignSurface { SnapToGrid = false, Size = new Size(400, 400) };

        var gfx = new Graphics();
        surface.Render(gfx);

        int sbw = ScrollBarEngine.DefaultScrollBarSize;
        bool hasVertical = gfx.GetCommands()
            .Any(c => c.Type == DrawCommandType.FillRectangle && c.X >= surface.Width - sbw);
        bool hasHorizontal = gfx.GetCommands()
            .Any(c => c.Type == DrawCommandType.FillRectangle && c.Y >= surface.Height - sbw);

        Assert.True(hasVertical, "A vertical scrollbar track should be drawn along the right edge.");
        Assert.True(hasHorizontal, "A horizontal scrollbar track should be drawn along the bottom edge.");
    }

    [Fact]
    public void MouseWheel_ScrollsRootOutOfOrigin_WhenOverflowing()
    {
        var surface = new TestableDesignSurface { SnapToGrid = false, Size = new Size(400, 400) };
        var root = surface.RootControl!;
        Assert.Equal(new Point(0, 0), root.Location);

        // A negative wheel delta scrolls the content down, which pans the (larger than
        // the surface) root upward, so its Location becomes negative.
        surface.RaiseMouseWheel(-100f);
        surface.Render(new Graphics());

        Assert.True(root.Location.Y < 0,
            "Scrolling down should move the root up (negative Y) to reveal lower content.");
    }

    [Fact]
    public void MouseWheel_WhenContentFits_DoesNotMoveRoot()
    {
        var surface = new TestableDesignSurface { SnapToGrid = false, Size = new Size(1000, 800) };
        var root = surface.RootControl!;

        surface.RaiseMouseWheel(-500f);
        surface.Render(new Graphics());

        Assert.Equal(new Point(0, 0), root.Location);
    }
}
