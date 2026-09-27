using System;
using System.Collections.Generic;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Core;

namespace OldSchoolForms.Ui.Designer;

/// <summary>
/// Design-mode adapter for <see cref="ToolStrip"/>. Lets the designer select, add and remove
/// tool strip items in situ: once the strip itself is the primary selection, clicking an item
/// selects it (without triggering the runtime click behaviour), clicking the plus button at the
/// end of the strip opens a type-picker popup, and the Delete key removes the selected item.
/// </summary>
public sealed class ToolStripDesignAdapter : ControlDesignAdapter
{
    private readonly ToolStrip _toolStrip;
    private readonly DesignSurface _surface;
    private readonly AddTypePopupController _popup;
    private ToolStripItem? _selectedItem;

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolStripDesignAdapter"/> class.
    /// </summary>
    /// <param name="toolStrip">The tool strip this adapter provides interaction for.</param>
    /// <param name="surface">The design surface hosting the tool strip.</param>
    public ToolStripDesignAdapter(ToolStrip toolStrip, DesignSurface surface) : base(toolStrip)
    {
        _toolStrip = toolStrip;
        _surface = surface;
        _popup = new AddTypePopupController(() => _surface.Invalidate());
        _surface.Selection.SelectionChanged += OnSurfaceSelectionChanged;
    }

    /// <summary>
    /// Gets the tool strip item currently selected in the designer, or <see langword="null"/>.
    /// </summary>
    public ToolStripItem? SelectedItem => _selectedItem;

    /// <summary>
    /// Raised when the adapter-internal item selection changes.
    /// </summary>
    public event EventHandler? SelectedItemChanged;

    /// <summary>
    /// Gets whether the adapter owns interaction because the add-item popup is open.
    /// </summary>
    public override bool IsInteracting => _popup.IsOpen;

    /// <summary>
    /// Determines whether <paramref name="localPoint"/> lies on a tool strip item or on the
    /// add-item button. Only intercepts while the tool strip itself is the primary selection,
    /// so the first click still selects the strip via the regular surface logic.
    /// </summary>
    /// <param name="localPoint">The point in the control's local coordinates.</param>
    /// <returns>true if the point hits an item or the add button; otherwise false.</returns>
    public override bool TryHitRegion(Point localPoint)
    {
        if (!ReferenceEquals(_surface.Selection.PrimarySelection?.Control, _toolStrip))
            return false;

        if (_toolStrip.HitTestItemPublic(localPoint) != null)
            return true;

        var addBounds = _toolStrip.GetAddItemButtonBoundsPublic();
        return !IsEmpty(addBounds) && addBounds.Contains(localPoint);
    }

    /// <summary>
    /// Handles a mouse-down: while the add-item popup is open it consumes the click; otherwise
    /// the plus button opens the popup and a click on an item selects it. The event is never
    /// forwarded to the tool strip, which suppresses the runtime <c>PerformClick</c> behaviour.
    /// </summary>
    /// <param name="localPoint">The point in the control's local coordinates.</param>
    /// <param name="e">The mouse event arguments.</param>
    public override void OnMouseDown(Point localPoint, MouseEventArgs e)
    {
        if (_popup.HandleMouseDown(localPoint))
            return;

        var addBounds = _toolStrip.GetAddItemButtonBoundsPublic();
        if (!IsEmpty(addBounds) && addBounds.Contains(localPoint))
        {
            OpenAddPopup(addBounds);
            return;
        }

        var hit = _toolStrip.HitTestItemPublic(localPoint);
        if (hit != null)
            SelectItem(hit);
    }

    /// <summary>
    /// Handles a mouse-move to update the add-item popup hover state.
    /// </summary>
    /// <param name="localPoint">The point in the control's local coordinates.</param>
    /// <param name="e">The mouse event arguments.</param>
    public override void OnMouseMove(Point localPoint, MouseEventArgs e)
    {
        _popup.HandleMouseMove(localPoint);
    }

    /// <summary>
    /// Handles a key-down; Escape dismisses the add-item popup.
    /// </summary>
    /// <param name="e">The key event arguments.</param>
    /// <returns>true if the popup consumed the key; otherwise false.</returns>
    public override bool OnKeyDown(KeyEventArgs e)
    {
        return _popup.HandleKeyDown(e);
    }

    /// <summary>
    /// Gets the bounds of the selected item so the surface can highlight it.
    /// </summary>
    /// <param name="bounds">Receives the item bounds in tool strip coordinates.</param>
    /// <returns>true if an item is selected and its bounds are known.</returns>
    public override bool TryGetSelectionHighlight(out Rectangle bounds)
    {
        if (_selectedItem != null)
        {
            bounds = _toolStrip.GetItemBoundsPublic(_selectedItem);
            return !IsEmpty(bounds);
        }
        bounds = Rectangle.Empty;
        return false;
    }

    /// <summary>
    /// Gets the bounds of the add-item button so the surface can draw it.
    /// </summary>
    /// <param name="bounds">Receives the button bounds in tool strip coordinates.</param>
    /// <returns>true if the add button is available (no overflow).</returns>
    public override bool TryGetAddButtonHighlight(out Rectangle bounds)
    {
        bounds = _toolStrip.GetAddItemButtonBoundsPublic();
        return !IsEmpty(bounds);
    }

    /// <summary>
    /// Gets the open add-item type-picker popup so the surface can draw it.
    /// </summary>
    /// <param name="localBounds">Receives the popup bounds in tool strip coordinates.</param>
    /// <param name="labels">Receives the popup row labels.</param>
    /// <param name="hoveredIndex">Receives the hovered row index, or -1.</param>
    /// <returns>true if the popup is open.</returns>
    public override bool TryGetAddPopup(out Rectangle localBounds, out IReadOnlyList<string> labels, out int hoveredIndex)
        => _popup.TryGetOverlay(out localBounds, out labels, out hoveredIndex);

    private static bool IsEmpty(Rectangle bounds) => bounds.Width <= 0 || bounds.Height <= 0;

    /// <summary>
    /// Removes the selected item from the tool strip and records an undo action.
    /// </summary>
    /// <returns>true if an item was deleted; otherwise false.</returns>
    public override bool TryDeleteSelection()
    {
        if (_selectedItem == null) return false;
        var item = _selectedItem;
        int index = _toolStrip.Items.IndexOf(item);
        if (index < 0)
        {
            SelectItem(null);
            return false;
        }

        _toolStrip.Items.RemoveAt(index);
        SelectItem(null);
        _surface.Undo.PushAction(
            () => _toolStrip.Items.Insert(index, item),
            () => _toolStrip.Items.Remove(item));
        _surface.NotifyContentModified();
        _surface.Invalidate();
        return true;
    }

    /// <summary>
    /// Gets the selected tool strip item for property-grid binding.
    /// </summary>
    /// <returns>The selected item, or <see langword="null"/>.</returns>
    public override object? GetSelectedValue() => _selectedItem;

    /// <summary>
    /// Clears the item selection when Escape is pressed.
    /// </summary>
    /// <returns>true if a selection was cleared.</returns>
    public override bool TryClearSelection()
    {
        if (_selectedItem == null) return false;
        SelectItem(null);
        _surface.Invalidate();
        return true;
    }

    private void OpenAddPopup(Rectangle addBounds)
    {
        var entries = new List<(string Label, Action Action)>
        {
            (LangRes.GetString("ToolStripItemsDialog_AddButton"),
                () => AddItem(new ToolStripButton { Text = LangRes.GetString("ToolStripItemsDialog_NewButton") })),
            (LangRes.GetString("ToolStripItemsDialog_AddLabel"),
                () => AddItem(new ToolStripLabel { Text = LangRes.GetString("ToolStripItemsDialog_NewLabel") })),
            (LangRes.GetString("ToolStripItemsDialog_AddSeparator"),
                () => AddItem(new ToolStripSeparator())),
            (LangRes.GetString("ToolStripItemsDialog_AddTextBox"),
                () => AddItem(new ToolStripTextBox())),
        };
        _popup.Open(addBounds, entries);
    }

    private void AddItem(ToolStripItem item)
    {
        _toolStrip.Items.Add(item);
        SelectItem(item);
        _surface.Undo.PushAction(
            () => _toolStrip.Items.Remove(item),
            () => _toolStrip.Items.Add(item));
        _surface.NotifyContentModified();
        _surface.Invalidate();
    }

    private void SelectItem(ToolStripItem? item)
    {
        if (ReferenceEquals(_selectedItem, item)) return;
        _selectedItem = item;
        SelectedItemChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnSurfaceSelectionChanged(object? sender, EventArgs e)
    {
        var primary = _surface.Selection.PrimarySelection?.Control;
        if (!ReferenceEquals(primary, _toolStrip))
        {
            _popup.Close();
            SelectItem(null);
        }
    }
}
