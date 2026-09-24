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

    /// <summary>
    /// Gets the bounds (in the control's local coordinates) of the adapter-internal selection
    /// that should be highlighted by the design surface. The default implementation returns
    /// <see langword="false"/>.
    /// </summary>
    /// <param name="bounds">Receives the highlight bounds when this method returns <see langword="true"/>.</param>
    /// <returns>true if the adapter has a selection to highlight; otherwise false.</returns>
    public virtual bool TryGetSelectionHighlight(out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        return false;
    }

    /// <summary>
    /// Gets the bounds (in the control's local coordinates) of an "add item" affordance the
    /// design surface should draw. The default implementation returns <see langword="false"/>.
    /// </summary>
    /// <param name="bounds">Receives the button bounds when this method returns <see langword="true"/>.</param>
    /// <returns>true if an add affordance is available; otherwise false.</returns>
    public virtual bool TryGetAddButtonHighlight(out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        return false;
    }

    /// <summary>
    /// Deletes the adapter-internal selection. The default implementation returns
    /// <see langword="false"/> so the surface can delete the control itself.
    /// </summary>
    /// <returns>true if a selection was deleted; otherwise false.</returns>
    public virtual bool TryDeleteSelection() => false;

    /// <summary>
    /// Gets the adapter-internal selected object so the property grid can bind to it.
    /// The default implementation returns <see langword="null"/>.
    /// </summary>
    /// <returns>The selected object, or <see langword="null"/>.</returns>
    public virtual object? GetSelectedValue() => null;

    /// <summary>
    /// Clears the adapter-internal selection. The default implementation returns
    /// <see langword="false"/>.
    /// </summary>
    /// <returns>true if a selection was cleared; otherwise false.</returns>
    public virtual bool TryClearSelection() => false;
}
