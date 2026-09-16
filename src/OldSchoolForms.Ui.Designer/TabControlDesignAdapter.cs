using System;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Core;

namespace OldSchoolForms.Ui.Designer;

/// <summary>
/// Design adapter for <see cref="TabControl"/>.
/// Enables tab switching and the overflow dropdown while designing by forwarding
/// events to the control's own handlers and tracking the dropdown state.
/// </summary>
public sealed class TabControlDesignAdapter : ControlDesignAdapter
{
    private readonly TabControl _tabControl;

    /// <summary>
    /// Initializes a new instance of the <see cref="TabControlDesignAdapter"/> class.
    /// </summary>
    /// <param name="tabControl">The tab control this adapter provides interaction for.</param>
    public TabControlDesignAdapter(TabControl tabControl) : base(tabControl)
    {
        _tabControl = tabControl;
    }

    /// <summary>
    /// Gets whether the overflow dropdown is currently displayed.
    /// </summary>
    public override bool IsInteracting => _tabControl.OverflowDroppedDown;

    /// <summary>
    /// Determines whether <paramref name="localPoint"/> lies within the tab header band.
    /// </summary>
    /// <param name="localPoint">The point in the control's local coordinates.</param>
    /// <returns>true if the point is in the tab header band; otherwise false.</returns>
    public override bool TryHitRegion(Point localPoint)
        => _tabControl.TryHitTabHeaderAreaPublic(localPoint, out _);

    /// <summary>
    /// Handles a mouse-down event for tab header clicks and the overflow dropdown.
    /// </summary>
    /// <param name="localPoint">The point in the control's local coordinates.</param>
    /// <param name="e">The mouse event arguments.</param>
    public override void OnMouseDown(Point localPoint, MouseEventArgs e)
        => _tabControl.DesignerMouseDown(new MouseEventArgs(e.Button, e.Clicks, localPoint.X, localPoint.Y, e.Delta));

    /// <summary>
    /// Handles a mouse-move event for hover tracking in the overflow dropdown.
    /// </summary>
    /// <param name="localPoint">The point in the control's local coordinates.</param>
    /// <param name="e">The mouse event arguments.</param>
    public override void OnMouseMove(Point localPoint, MouseEventArgs e)
        => _tabControl.DesignerMouseMove(new MouseEventArgs(e.Button, e.Clicks, localPoint.X, localPoint.Y, e.Delta));

    /// <summary>
    /// Handles a mouse-up event.
    /// </summary>
    /// <param name="localPoint">The point in the control's local coordinates.</param>
    /// <param name="e">The mouse event arguments.</param>
    public override void OnMouseUp(Point localPoint, MouseEventArgs e)
        => _tabControl.DesignerMouseUp(new MouseEventArgs(e.Button, e.Clicks, localPoint.X, localPoint.Y, e.Delta));

    /// <summary>
    /// Handles a mouse-wheel event for scrolling the overflow dropdown.
    /// </summary>
    /// <param name="localPoint">The point in the control's local coordinates.</param>
    /// <param name="e">The mouse event arguments.</param>
    public override void OnMouseWheel(Point localPoint, MouseEventArgs e)
        => _tabControl.DesignerMouseWheel(new MouseEventArgs(e.Button, e.Clicks, localPoint.X, localPoint.Y, e.Delta));

    /// <summary>
    /// Handles a key-down event for the overflow dropdown (F4 opens, arrows navigate,
    /// Escape/Enter close).
    /// </summary>
    /// <param name="e">The key event arguments.</param>
    /// <returns>true if the adapter consumed the key; otherwise false.</returns>
    public override bool OnKeyDown(KeyEventArgs e)
    {
        if (_tabControl.OverflowDroppedDown || e.KeyCode == Keys.F4)
        {
            _tabControl.DesignerKeyDown(e);
            return true;
        }
        return false;
    }
}
