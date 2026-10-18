using System;
using System.Collections.Generic;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Reports;

namespace OldSchoolForms.Ui.Designer.Reports;

/// <summary>
/// Design-mode adapter for <see cref="DesignBand"/> containers. Binds the property grid
/// to the wrapped <see cref="ReportBand"/> and provides group management: the detail
/// band and group bands show a plus button that opens a popup for creating groups, and
/// group bands additionally support deleting and reordering their group.
/// </summary>
public sealed class BandDesignAdapter : ControlDesignAdapter
{
    private const int AddButtonSize = 16;
    private const int AddButtonMargin = 2;

    private readonly DesignBand _band;
    private readonly DesignSurface _surface;
    private readonly AddTypePopupController _popup;

    /// <summary>
    /// Initializes a new instance of the <see cref="BandDesignAdapter"/> class.
    /// </summary>
    /// <param name="band">The band container this adapter provides interaction for.</param>
    /// <param name="surface">The design surface hosting the band.</param>
    public BandDesignAdapter(DesignBand band, DesignSurface surface) : base(band)
    {
        _band = band;
        _surface = surface;
        _popup = new AddTypePopupController(() => _surface.Invalidate());
        _surface.Selection.SelectionChanged += OnSurfaceSelectionChanged;
    }

    /// <summary>
    /// Gets whether the adapter owns interaction because the group popup is open.
    /// </summary>
    public override bool IsInteracting => _popup.IsOpen;

    /// <summary>
    /// Gets the bounds of the group-management plus button at the right edge of the band.
    /// </summary>
    private Rectangle AddButtonBounds =>
        new(_band.Width - AddButtonSize - AddButtonMargin, AddButtonMargin, AddButtonSize, AddButtonSize);

    /// <summary>
    /// Determines whether <paramref name="localPoint"/> lies on the group-management plus
    /// button. Only intercepts while the band itself is the primary selection.
    /// </summary>
    /// <param name="localPoint">The point in the band's local coordinates.</param>
    /// <returns>true if the plus button was hit.</returns>
    public override bool TryHitRegion(Point localPoint)
    {
        if (!ReferenceEquals(_surface.Selection.PrimarySelection?.Control, _band))
            return false;
        if (!SupportsGroupPopup(_band.Band))
            return false;
        return AddButtonBounds.Contains(localPoint);
    }

    /// <summary>
    /// Handles a mouse-down: dismisses or opens the group management popup.
    /// </summary>
    /// <param name="localPoint">The point in the band's local coordinates.</param>
    /// <param name="e">The mouse event arguments.</param>
    public override void OnMouseDown(Point localPoint, MouseEventArgs e)
    {
        if (_popup.HandleMouseDown(localPoint))
            return;

        if (AddButtonBounds.Contains(localPoint))
            OpenAddPopup();
    }

    /// <summary>
    /// Handles a mouse-move to update the group popup hover state.
    /// </summary>
    /// <param name="localPoint">The point in the band's local coordinates.</param>
    /// <param name="e">The mouse event arguments.</param>
    public override void OnMouseMove(Point localPoint, MouseEventArgs e)
    {
        _popup.HandleMouseMove(localPoint);
    }

    /// <summary>
    /// Handles a key-down; Escape dismisses the group popup.
    /// </summary>
    /// <param name="e">The key event arguments.</param>
    /// <returns>true if the popup consumed the key; otherwise false.</returns>
    public override bool OnKeyDown(KeyEventArgs e) => _popup.HandleKeyDown(e);

    /// <summary>
    /// Gets the bounds of the plus button so the surface can draw it.
    /// </summary>
    /// <param name="bounds">Receives the button bounds in band coordinates.</param>
    /// <returns>true if the add button is available.</returns>
    public override bool TryGetAddButtonHighlight(out Rectangle bounds)
    {
        bounds = AddButtonBounds;
        return SupportsGroupPopup(_band.Band);
    }

    /// <summary>
    /// Gets the open group popup so the surface can draw it.
    /// </summary>
    /// <param name="localBounds">Receives the popup bounds in band coordinates.</param>
    /// <param name="labels">Receives the popup row labels.</param>
    /// <param name="hoveredIndex">Receives the hovered row index, or -1.</param>
    /// <returns>true if the popup is open.</returns>
    public override bool TryGetAddPopup(out Rectangle localBounds, out IReadOnlyList<string> labels, out int hoveredIndex)
        => _popup.TryGetOverlay(out localBounds, out labels, out hoveredIndex);

    /// <summary>
    /// Deletes the group of a selected group band. Fixed report bands cannot be deleted.
    /// </summary>
    /// <returns>true if a group was deleted; otherwise false.</returns>
    public override bool TryDeleteSelection()
    {
        if (_band.OwnerGroup == null) return false;
        DeleteGroup(_band.OwnerGroup);
        return true;
    }

    /// <summary>
    /// Gets the wrapped band for property-grid binding.
    /// </summary>
    /// <returns>The report band.</returns>
    public override object? GetSelectedValue() => _band.Band;

    /// <summary>
    /// Clears the popup when the band selection moves away.
    /// </summary>
    /// <returns>true if the popup was open and got closed.</returns>
    public override bool TryClearSelection()
    {
        if (!_popup.IsOpen) return false;
        _popup.Close();
        _surface.Invalidate();
        return true;
    }

    private static bool SupportsGroupPopup(ReportBand band)
        => band.BandType is ReportBandType.Detail
            or ReportBandType.GroupHeader
            or ReportBandType.GroupFooter;

    private void OpenAddPopup()
    {
        var entries = new List<(string Label, Action Action)>
        {
            (LangRes.GetString("ReportDesigner_GroupAdd"), AddGroup)
        };

        if (_band.OwnerGroup != null)
        {
            entries.Add((LangRes.GetString("ReportDesigner_GroupDelete"), () => DeleteGroup(_band.OwnerGroup!)));
            entries.Add((LangRes.GetString("ReportDesigner_GroupMoveUp"), () => MoveGroup(_band.OwnerGroup!, -1)));
            entries.Add((LangRes.GetString("ReportDesigner_GroupMoveDown"), () => MoveGroup(_band.OwnerGroup!, 1)));
        }

        _popup.Open(AddButtonBounds, entries);
    }

    private void AddGroup()
    {
        var report = _band.BandOwner.Report;
        int index = report.Groups.Count + 1;
        var group = new ReportGroup(string.Empty, string.Format(LangRes.GetString("ReportDesigner_GroupDefaultName"), index));
        report.Groups.Add(group);
        _band.BandOwner.RefreshGeometry();

        var container = _band.BandOwner.GetBandContainer(group.Header);
        if (container != null)
        {
            var item = _surface.FindItem(container);
            if (item != null)
                _surface.Selection.Select(item);
        }

        _surface.Undo.PushAction(
            () =>
            {
                int at = report.Groups.IndexOf(group);
                if (at >= 0) report.Groups.RemoveAt(at);
                _band.BandOwner.RefreshGeometry();
            },
            () =>
            {
                report.Groups.Add(group);
                _band.BandOwner.RefreshGeometry();
            });
        _surface.NotifyContentModified();
        _surface.Invalidate();
    }

    private void DeleteGroup(ReportGroup group)
    {
        var parent = _band.BandOwner.FindParentCollection(group);
        if (parent == null) return;
        int index = parent.IndexOf(group);
        if (index < 0) return;

        parent.RemoveAt(index);
        _band.BandOwner.RefreshGeometry();
        _surface.Selection.DeselectAll();

        _surface.Undo.PushAction(
            () =>
            {
                parent.Insert(index, group);
                _band.BandOwner.RefreshGeometry();
            },
            () =>
            {
                int at = parent.IndexOf(group);
                if (at >= 0) parent.RemoveAt(at);
                _band.BandOwner.RefreshGeometry();
            });
        _surface.NotifyContentModified();
        _surface.Invalidate();
    }

    private void MoveGroup(ReportGroup group, int direction)
    {
        var parent = _band.BandOwner.FindParentCollection(group);
        if (parent == null) return;
        int index = parent.IndexOf(group);
        int target = index + direction;
        if (index < 0 || target < 0 || target >= parent.Count) return;

        parent.RemoveAt(index);
        parent.Insert(target, group);
        _band.BandOwner.RefreshGeometry();

        _surface.Undo.PushAction(
            () =>
            {
                int at = parent.IndexOf(group);
                if (at >= 0) parent.RemoveAt(at);
                parent.Insert(index, group);
                _band.BandOwner.RefreshGeometry();
            },
            () =>
            {
                int at = parent.IndexOf(group);
                if (at >= 0) parent.RemoveAt(at);
                parent.Insert(target, group);
                _band.BandOwner.RefreshGeometry();
            });
        _surface.NotifyContentModified();
        _surface.Invalidate();
    }

    private void OnSurfaceSelectionChanged(object? sender, EventArgs e)
    {
        var primary = _surface.Selection.PrimarySelection?.Control;
        if (!ReferenceEquals(primary, _band))
            _popup.Close();
    }
}
