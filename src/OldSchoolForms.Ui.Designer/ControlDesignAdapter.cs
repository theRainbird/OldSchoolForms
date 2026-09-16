using System;
using OldSchoolForms.Ui.Core;

namespace OldSchoolForms.Ui.Designer;

/// <summary>
/// Base class for <see cref="IControlDesignAdapter"/> implementations.
/// Provides a default no-op behaviour so derived adapters only override what they need.
/// </summary>
public abstract class ControlDesignAdapter : IControlDesignAdapter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ControlDesignAdapter"/> class.
    /// </summary>
    /// <param name="control">The control this adapter provides interaction for.</param>
    protected ControlDesignAdapter(Control control)
    {
        Control = control;
    }

    /// <summary>
    /// Gets the control this adapter provides interaction for.
    /// </summary>
    public Control Control { get; }

    /// <summary>
    /// Gets whether the adapter currently owns design-mode interaction.
    /// The default implementation returns <see langword="false"/>.
    /// </summary>
    public virtual bool IsInteracting => false;

    /// <summary>
    /// Determines whether <paramref name="localPoint"/> lies within a design-mode interaction
    /// region of the control. The default implementation returns <see langword="false"/>.
    /// </summary>
    /// <param name="localPoint">The point in the control's local coordinates.</param>
    /// <returns>true if the point is in an interaction region; otherwise false.</returns>
    public virtual bool TryHitRegion(Point localPoint) => false;

    /// <summary>
    /// Handles a mouse-down event for design-mode interaction. The default implementation does nothing.
    /// </summary>
    /// <param name="localPoint">The point in the control's local coordinates.</param>
    /// <param name="e">The mouse event arguments.</param>
    public virtual void OnMouseDown(Point localPoint, MouseEventArgs e) { }

    /// <summary>
    /// Handles a mouse-move event for design-mode interaction. The default implementation does nothing.
    /// </summary>
    /// <param name="localPoint">The point in the control's local coordinates.</param>
    /// <param name="e">The mouse event arguments.</param>
    public virtual void OnMouseMove(Point localPoint, MouseEventArgs e) { }

    /// <summary>
    /// Handles a mouse-up event for design-mode interaction. The default implementation does nothing.
    /// </summary>
    /// <param name="localPoint">The point in the control's local coordinates.</param>
    /// <param name="e">The mouse event arguments.</param>
    public virtual void OnMouseUp(Point localPoint, MouseEventArgs e) { }

    /// <summary>
    /// Handles a mouse-wheel event for design-mode interaction. The default implementation does nothing.
    /// </summary>
    /// <param name="localPoint">The point in the control's local coordinates.</param>
    /// <param name="e">The mouse event arguments.</param>
    public virtual void OnMouseWheel(Point localPoint, MouseEventArgs e) { }

    /// <summary>
    /// Handles a key-down event for design-mode interaction. The default implementation
    /// returns <see langword="false"/> so the surface can process the key.
    /// </summary>
    /// <param name="e">The key event arguments.</param>
    /// <returns>true if the adapter consumed the key; otherwise false.</returns>
    public virtual bool OnKeyDown(KeyEventArgs e) => false;
}
