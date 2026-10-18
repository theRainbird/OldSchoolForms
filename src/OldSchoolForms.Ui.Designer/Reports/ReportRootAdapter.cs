using System;
using System.Collections.Generic;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Reports;

namespace OldSchoolForms.Ui.Designer.Reports;

/// <summary>
/// Design-mode adapter for the <see cref="DesignReport"/> root. Exposes the wrapped
/// <see cref="Report"/> to the property grid when the root is selected, and lets the
/// user select individual bands by clicking their header labels in the left margin
/// gutter — including bands that currently have zero height.
/// </summary>
public sealed class ReportRootAdapter : ControlDesignAdapter
{
    private readonly DesignReport _root;
    private readonly DesignSurface _surface;
    private ReportBand? _pendingGutterBand;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportRootAdapter"/> class.
    /// </summary>
    /// <param name="root">The design report root this adapter provides interaction for.</param>
    /// <param name="surface">The design surface hosting the root.</param>
    public ReportRootAdapter(DesignReport root, DesignSurface surface) : base(root)
    {
        _root = root;
        _surface = surface;
    }

    /// <summary>
    /// Gets whether the adapter owns interaction. Always <see langword="false"/>.
    /// </summary>
    public override bool IsInteracting => false;

    /// <summary>
    /// Determines whether <paramref name="localPoint"/> lies on a band gutter label.
    /// </summary>
    /// <param name="localPoint">The point in the root's local coordinates.</param>
    /// <returns>true if a band gutter label was hit.</returns>
    public override bool TryHitRegion(Point localPoint)
    {
        _pendingGutterBand = null;
        if (localPoint.X >= _root.CmToPx(_root.Report.LeftMargin))
            return false;

        foreach (var (band, rect) in _root.GetGutterHitAreas())
        {
            if (rect.Contains(localPoint))
            {
                _pendingGutterBand = band;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Handles a mouse-down on a gutter label by selecting the corresponding band.
    /// </summary>
    /// <param name="localPoint">The point in the root's local coordinates.</param>
    /// <param name="e">The mouse event arguments.</param>
    public override void OnMouseDown(Point localPoint, MouseEventArgs e)
    {
        if (_pendingGutterBand == null) return;
        var container = _root.GetBandContainer(_pendingGutterBand);
        if (container == null) return;
        var item = _surface.FindItem(container);
        if (item != null)
            _surface.Selection.Select(item);
        _pendingGutterBand = null;
    }

    /// <summary>
    /// Gets the wrapped report for property-grid binding when the root is selected.
    /// </summary>
    /// <returns>The report model.</returns>
    public override object? GetSelectedValue() => _root.Report;
}
