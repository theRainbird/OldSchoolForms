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
}
