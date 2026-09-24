using System;
using OldSchoolForms.Ui.Core;

namespace OldSchoolForms.Ui.Designer;

/// <summary>
/// Provides design-mode interaction for a specific control type.
/// Design adapters let placed controls expose special behaviour (tab headers,
/// dropdowns, ...) without the <see cref="DesignSurface"/> hardcoding individual controls.
/// </summary>
public interface IControlDesignAdapter
{
    /// <summary>
    /// Gets the control this adapter provides interaction for.
    /// </summary>
    Control Control { get; }

    /// <summary>
    /// Gets whether the adapter currently owns design-mode interaction, for example because
    /// a dropdown is open. While this is <see langword="true"/> the <see cref="DesignSurface"/>
    /// keeps routing input to this adapter regardless of the pointer position.
    /// </summary>
    bool IsInteracting { get; }

    /// <summary>
    /// Determines whether <paramref name="localPoint"/> lies within a design-mode interaction
    /// region of the control (for example a tab header).
    /// </summary>
    /// <param name="localPoint">The point in the control's local coordinates.</param>
    /// <returns>true if the point is in an interaction region; otherwise false.</returns>
    bool TryHitRegion(Point localPoint);

    /// <summary>
    /// Handles a mouse-down event for design-mode interaction.
    /// </summary>
    /// <param name="localPoint">The point in the control's local coordinates.</param>
    /// <param name="e">The mouse event arguments.</param>
    void OnMouseDown(Point localPoint, MouseEventArgs e);

    /// <summary>
    /// Handles a mouse-move event for design-mode interaction.
    /// </summary>
    /// <param name="localPoint">The point in the control's local coordinates.</param>
    /// <param name="e">The mouse event arguments.</param>
    void OnMouseMove(Point localPoint, MouseEventArgs e);

    /// <summary>
    /// Handles a mouse-up event for design-mode interaction.
    /// </summary>
    /// <param name="localPoint">The point in the control's local coordinates.</param>
    /// <param name="e">The mouse event arguments.</param>
    void OnMouseUp(Point localPoint, MouseEventArgs e);

    /// <summary>
    /// Handles a mouse-wheel event for design-mode interaction.
    /// </summary>
    /// <param name="localPoint">The point in the control's local coordinates.</param>
    /// <param name="e">The mouse event arguments.</param>
    void OnMouseWheel(Point localPoint, MouseEventArgs e);

    /// <summary>
    /// Handles a key-down event for design-mode interaction.
    /// </summary>
    /// <param name="e">The key event arguments.</param>
    /// <returns>true if the adapter consumed the key; otherwise false so the surface can process it.</returns>
    bool OnKeyDown(KeyEventArgs e);

    /// <summary>
    /// Gets the bounds (in the control's local coordinates) of the adapter-internal selection
    /// that should be highlighted by the design surface, for example a selected tool strip item.
    /// </summary>
    /// <param name="bounds">Receives the highlight bounds when this method returns <see langword="true"/>.</param>
    /// <returns>true if the adapter has a selection to highlight; otherwise false.</returns>
    bool TryGetSelectionHighlight(out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        return false;
    }

    /// <summary>
    /// Gets the bounds (in the control's local coordinates) of an "add item" affordance the
    /// design surface should draw, for example a plus button at the end of a tool strip.
    /// </summary>
    /// <param name="bounds">Receives the button bounds when this method returns <see langword="true"/>.</param>
    /// <returns>true if an add affordance is available; otherwise false.</returns>
    bool TryGetAddButtonHighlight(out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        return false;
    }

    /// <summary>
    /// Deletes the adapter-internal selection (for example a selected tool strip item).
    /// </summary>
    /// <returns>true if a selection was deleted; otherwise false so the surface can delete the control itself.</returns>
    bool TryDeleteSelection() => false;

    /// <summary>
    /// Gets the adapter-internal selected object (for example a selected tool strip item) so the
    /// property grid can bind to it, or <see langword="null"/> to bind to the control instead.
    /// </summary>
    /// <returns>The selected object, or <see langword="null"/>.</returns>
    object? GetSelectedValue() => null;

    /// <summary>
    /// Clears the adapter-internal selection (for example when the user presses Escape).
    /// </summary>
    /// <returns>true if a selection was cleared; otherwise false.</returns>
    bool TryClearSelection() => false;
}
