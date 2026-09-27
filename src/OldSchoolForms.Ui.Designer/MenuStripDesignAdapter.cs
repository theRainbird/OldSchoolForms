using System;
using System.Collections.Generic;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Core;

namespace OldSchoolForms.Ui.Designer;

/// <summary>
/// Design-mode adapter for <see cref="MenuStrip"/>. Lets the designer select, add and remove
/// menu items in situ: once the menu strip itself is the primary selection, clicking an item
/// selects it (without triggering the runtime click behaviour), clicking the plus button at the
/// end of the strip opens a type-picker popup, and the Delete key removes the selected item.
/// Menu strips only support <see cref="ToolStripMenuItem"/> items.
/// </summary>
public sealed class MenuStripDesignAdapter : ControlDesignAdapter
{
    private readonly MenuStrip _menuStrip;
    private readonly DesignSurface _surface;
    private readonly AddTypePopupController _popup;
    private ToolStripMenuItem? _selectedItem;

    /// <summary>
    /// Initializes a new instance of the <see cref="MenuStripDesignAdapter"/> class.
    /// </summary>
    /// <param name="menuStrip">The menu strip this adapter provides interaction for.</param>
    /// <param name="surface">The design surface hosting the menu strip.</param>
    public MenuStripDesignAdapter(MenuStrip menuStrip, DesignSurface surface) : base(menuStrip)
    {
        _menuStrip = menuStrip;
        _surface = surface;
        _popup = new AddTypePopupController(() => _surface.Invalidate());
        _surface.Selection.SelectionChanged += OnSurfaceSelectionChanged;
    }

    /// <summary>
    /// Gets the menu item currently selected in the designer, or <see langword="null"/>.
    /// </summary>
    public ToolStripMenuItem? SelectedItem => _selectedItem;

    /// <summary>
    /// Raised when the adapter-internal item selection changes.
    /// </summary>
    public event EventHandler? SelectedItemChanged;

    /// <summary>
    /// Gets whether the adapter owns interaction because the add-item popup is open.
    /// </summary>
    public override bool IsInteracting => _popup.IsOpen;

    /// <summary>
    /// Determines whether <paramref name="localPoint"/> lies on a menu item or on the add-item
    /// button. Only intercepts while the menu strip itself is the primary selection.
    /// </summary>
    /// <param name="localPoint">The point in the control's local coordinates.</param>
    /// <returns>true if the point hits an item or the add button; otherwise false.</returns>
    public override bool TryHitRegion(Point localPoint)
    {
        if (!ReferenceEquals(_surface.Selection.PrimarySelection?.Control, _menuStrip))
            return false;

        if (_menuStrip.HitTestItemPublic(localPoint) != null)
            return true;

        var addBounds = _menuStrip.GetAddItemButtonBoundsPublic();
        return !IsEmpty(addBounds) && addBounds.Contains(localPoint);
    }

    /// <summary>
    /// Handles a mouse-down: while the add-item popup is open it consumes the click; otherwise
    /// the plus button opens the popup and a click on an item selects it. The event is never
    /// forwarded to the menu strip, which suppresses the runtime <c>PerformClick</c> behaviour.
    /// </summary>
    /// <param name="localPoint">The point in the control's local coordinates.</param>
    /// <param name="e">The mouse event arguments.</param>
    public override void OnMouseDown(Point localPoint, MouseEventArgs e)
    {
        if (_popup.HandleMouseDown(localPoint))
            return;

        var addBounds = _menuStrip.GetAddItemButtonBoundsPublic();
        if (!IsEmpty(addBounds) && addBounds.Contains(localPoint))
        {
            OpenAddPopup(addBounds);
            return;
        }

        var hit = _menuStrip.HitTestItemPublic(localPoint);
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
    /// <param name="bounds">Receives the item bounds in menu strip coordinates.</param>
    /// <returns>true if an item is selected and its bounds are known.</returns>
    public override bool TryGetSelectionHighlight(out Rectangle bounds)
    {
        if (_selectedItem != null)
        {
            bounds = _menuStrip.GetItemBoundsPublic(_selectedItem);
            return !IsEmpty(bounds);
        }
        bounds = Rectangle.Empty;
        return false;
    }

    /// <summary>
    /// Gets the bounds of the add-item button so the surface can draw it.
    /// </summary>
    /// <param name="bounds">Receives the button bounds in menu strip coordinates.</param>
    /// <returns>true if the add button is available.</returns>
    public override bool TryGetAddButtonHighlight(out Rectangle bounds)
    {
        bounds = _menuStrip.GetAddItemButtonBoundsPublic();
        return !IsEmpty(bounds);
    }

    /// <summary>
    /// Gets the open add-item type-picker popup so the surface can draw it.
    /// </summary>
    /// <param name="localBounds">Receives the popup bounds in menu strip coordinates.</param>
    /// <param name="labels">Receives the popup row labels.</param>
    /// <param name="hoveredIndex">Receives the hovered row index, or -1.</param>
    /// <returns>true if the popup is open.</returns>
    public override bool TryGetAddPopup(out Rectangle localBounds, out IReadOnlyList<string> labels, out int hoveredIndex)
        => _popup.TryGetOverlay(out localBounds, out labels, out hoveredIndex);

    private static bool IsEmpty(Rectangle bounds) => bounds.Width <= 0 || bounds.Height <= 0;

    /// <summary>
    /// Removes the selected item from the menu strip and records an undo action.
    /// </summary>
    /// <returns>true if an item was deleted; otherwise false.</returns>
    public override bool TryDeleteSelection()
    {
        if (_selectedItem == null) return false;
        var item = _selectedItem;
        int index = _menuStrip.Items.IndexOf(item);
        if (index < 0)
        {
            SelectItem(null);
            return false;
        }

        _menuStrip.Items.RemoveAt(index);
        SelectItem(null);
        _surface.Undo.PushAction(
            () => _menuStrip.Items.Insert(index, item),
            () => _menuStrip.Items.Remove(item));
        _surface.NotifyContentModified();
        _surface.Invalidate();
        return true;
    }

    /// <summary>
    /// Gets the selected menu item for property-grid binding.
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
            (LangRes.GetString("MenuStripItemsDialog_AddMenuItem"),
                () => AddItem(new ToolStripMenuItem { Text = LangRes.GetString("MenuStripItemsDialog_NewMenuItem") })),
        };
        _popup.Open(addBounds, entries);
    }

    private void AddItem(ToolStripMenuItem item)
    {
        _menuStrip.Items.Add(item);
        SelectItem(item);
        _surface.Undo.PushAction(
            () => _menuStrip.Items.Remove(item),
            () => _menuStrip.Items.Add(item));
        _surface.NotifyContentModified();
        _surface.Invalidate();
    }

    private void SelectItem(ToolStripMenuItem? item)
    {
        if (ReferenceEquals(_selectedItem, item)) return;
        _selectedItem = item;
        SelectedItemChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnSurfaceSelectionChanged(object? sender, EventArgs e)
    {
        var primary = _surface.Selection.PrimarySelection?.Control;
        if (!ReferenceEquals(primary, _menuStrip))
        {
            _popup.Close();
            SelectItem(null);
        }
    }
}
