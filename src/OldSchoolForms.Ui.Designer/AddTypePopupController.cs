using System;
using System.Collections.Generic;
using System.Linq;
using OldSchoolForms.Ui.Core;

namespace OldSchoolForms.Ui.Designer;

/// <summary>
/// Reusable state machine for the in-surface "add item" type-picker popup shown when the
/// user clicks the plus button on a tool/menu/status strip in the designer. The popup is
/// drawn by the <see cref="DesignSurface"/> (a real modal form cannot be positioned at the
/// button because both native and overlay dialogs centre on their owner), so this controller
/// only tracks the popup geometry in the owning control's local coordinates, the available
/// type entries, and the hovered row, and routes mouse/key input to it.
/// </summary>
internal sealed class AddTypePopupController
{
    private const int RowHeight = 24;
    private const int Width = 150;

    private readonly Action _onChanged;
    private readonly List<(string Label, Action Action)> _entries = new();
    private Rectangle _localBounds = Rectangle.Empty;
    private bool _isOpen;
    private int _hoveredIndex = -1;

    /// <summary>
    /// Initializes a new instance of the <see cref="AddTypePopupController"/> class.
    /// </summary>
    /// <param name="onChanged">Callback invoked whenever the popup state changes, so the owner can repaint.</param>
    public AddTypePopupController(Action onChanged)
    {
        _onChanged = onChanged;
    }

    /// <summary>
    /// Gets whether the popup is currently open.
    /// </summary>
    public bool IsOpen => _isOpen;

    /// <summary>
    /// Opens the popup anchored below <paramref name="anchorLocal"/> with the given type entries.
    /// </summary>
    /// <param name="anchorLocal">The add-button bounds in the control's local coordinates.</param>
    /// <param name="entries">The available item types, each with a display label and an action.</param>
    public void Open(Rectangle anchorLocal, IEnumerable<(string Label, Action Action)> entries)
    {
        _entries.Clear();
        _entries.AddRange(entries);
        _localBounds = new Rectangle(
            anchorLocal.X,
            anchorLocal.Bottom + 2,
            Width,
            Math.Max(1, _entries.Count) * RowHeight);
        _isOpen = true;
        _hoveredIndex = -1;
        _onChanged();
    }

    /// <summary>
    /// Closes the popup if it is open.
    /// </summary>
    public void Close()
    {
        if (!_isOpen) return;
        _isOpen = false;
        _hoveredIndex = -1;
        _onChanged();
    }

    /// <summary>
    /// Handles a mouse-down in the control's local coordinates while the popup is open.
    /// Clicking a row invokes its action; clicking anywhere else dismisses the popup.
    /// </summary>
    /// <param name="localPoint">The point in the control's local coordinates.</param>
    /// <returns>true if the popup consumed the click; otherwise false.</returns>
    public bool HandleMouseDown(Point localPoint)
    {
        if (!_isOpen) return false;

        if (_localBounds.Contains(localPoint))
        {
            int index = (localPoint.Y - _localBounds.Y) / RowHeight;
            if (index >= 0 && index < _entries.Count)
            {
                var action = _entries[index].Action;
                Close();
                action();
                return true;
            }
        }

        Close();
        return true;
    }

    /// <summary>
    /// Updates the hovered row while the popup is open.
    /// </summary>
    /// <param name="localPoint">The point in the control's local coordinates.</param>
    public void HandleMouseMove(Point localPoint)
    {
        if (!_isOpen) return;

        int index = _localBounds.Contains(localPoint)
            ? (localPoint.Y - _localBounds.Y) / RowHeight
            : -1;
        if (index < 0 || index >= _entries.Count) index = -1;

        if (index != _hoveredIndex)
        {
            _hoveredIndex = index;
            _onChanged();
        }
    }

    /// <summary>
    /// Handles a key-down while the popup is open. Escape dismisses the popup.
    /// </summary>
    /// <param name="e">The key event arguments.</param>
    /// <returns>true if the popup consumed the key; otherwise false.</returns>
    public bool HandleKeyDown(KeyEventArgs e)
    {
        if (!_isOpen) return false;
        if (e.KeyCode == Keys.Escape)
        {
            Close();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets the current popup overlay so the design surface can draw it.
    /// </summary>
    /// <param name="localBounds">Receives the popup bounds in the control's local coordinates.</param>
    /// <param name="labels">Receives the row labels.</param>
    /// <param name="hoveredIndex">Receives the hovered row index, or -1.</param>
    /// <returns>true if the popup is open; otherwise false.</returns>
    public bool TryGetOverlay(out Rectangle localBounds, out IReadOnlyList<string> labels, out int hoveredIndex)
    {
        if (!_isOpen)
        {
            localBounds = Rectangle.Empty;
            labels = Array.Empty<string>();
            hoveredIndex = -1;
            return false;
        }

        localBounds = _localBounds;
        labels = _entries.Select(e => e.Label).ToList();
        hoveredIndex = _hoveredIndex;
        return true;
    }
}
