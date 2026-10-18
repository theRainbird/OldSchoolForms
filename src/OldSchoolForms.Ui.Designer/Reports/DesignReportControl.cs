using System;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Reports;
using Graphics = OldSchoolForms.Ui.Rendering.Graphics;

namespace OldSchoolForms.Ui.Designer.Reports;

/// <summary>
/// Design-time proxy for a <see cref="ReportControl"/>. The proxy is a real
/// <see cref="Control"/> so the designer's selection, move, resize and snap
/// machinery works unchanged, while all edits write through to the wrapped
/// report model in centimeters.
/// <para>
/// The proxy renders its wrapped control WYSIWYG: the report control is rendered
/// into an offscreen command list at 96 DPI and imported with the page zoom
/// applied, so text, shapes and layout match the final report preview.
/// </para>
/// </summary>
public abstract class DesignReportControl : Control
{
    private bool _syncingBounds;

    /// <summary>
    /// Initializes the proxy around the given report control.
    /// </summary>
    /// <param name="wrapped">The report control this proxy edits. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="wrapped"/> is null.</exception>
    protected DesignReportControl(ReportControl wrapped)
    {
        Wrapped = wrapped ?? throw new ArgumentNullException(nameof(wrapped));
        Name = DefaultName(wrapped);
    }

    /// <summary>
    /// Gets the wrapped report control this proxy edits.
    /// </summary>
    public ReportControl Wrapped { get; }

    /// <summary>
    /// Gets the band container this proxy currently lives in, or <see langword="null"/>
    /// when the proxy is not parented under a <see cref="DesignBand"/>.
    /// </summary>
    public DesignBand? OwnerBand
    {
        get
        {
            var current = Parent;
            while (current != null)
            {
                if (current is DesignBand band) return band;
                current = current.Parent;
            }
            return null;
        }
    }

    /// <summary>
    /// Gets the report root this proxy belongs to, or <see langword="null"/> when the
    /// proxy is not parented under a <see cref="DesignReport"/>.
    /// </summary>
    public DesignReport? OwnerReport
    {
        get
        {
            var current = Parent;
            while (current != null)
            {
                if (current is DesignReport root) return root;
                current = current.Parent;
            }
            return null;
        }
    }

    /// <summary>
    /// Writes the proxy's pixel bounds back to the wrapped control's centimeter
    /// geometry whenever the user moves or resizes it.
    /// </summary>
    protected override void OnBoundsChanged()
    {
        base.OnBoundsChanged();
        if (_syncingBounds) return;
        if (OwnerReport == null) return;
        WriteBoxToWrapped(Bounds);
    }

    /// <summary>
    /// Sets the proxy's pixel bounds from the wrapped control's centimeter geometry
    /// without triggering the write-back.
    /// </summary>
    internal void SetBoundsFromWrapped()
    {
        _syncingBounds = true;
        try
        {
            Bounds = ComputeBox();
        }
        finally
        {
            _syncingBounds = false;
        }
    }

    /// <summary>
    /// Gets whether the proxy's current bounds match the wrapped control's geometry
    /// within one pixel of rounding tolerance.
    /// </summary>
    /// <returns><see langword="true"/> when the bounds are in sync.</returns>
    internal bool BoundsMatchWrapped()
    {
        var box = ComputeBox();
        return Math.Abs(box.X - Bounds.X) <= 1
            && Math.Abs(box.Y - Bounds.Y) <= 1
            && Math.Abs(box.Width - Bounds.Width) <= 1
            && Math.Abs(box.Height - Bounds.Height) <= 1;
    }

    /// <summary>
    /// Writes the proxy's current pixel bounds back to the wrapped control's centimeter
    /// geometry. Used when a freshly placed proxy is the source of truth for its position.
    /// </summary>
    internal void PushBoxToWrapped()
    {
        if (OwnerReport == null) return;
        WriteBoxToWrapped(Bounds);
    }

    /// <summary>
    /// Removes the wrapped control from every band of the report it currently belongs to.
    /// Must be called while the proxy is still attached to the design tree.
    /// </summary>
    internal void DetachWrappedFromBands()
    {
        var report = OwnerReport;
        if (report == null) return;
        foreach (var band in report.GetBands())
            band.Controls.Remove(Wrapped);
    }

    /// <summary>
    /// Computes the proxy's pixel box from the wrapped control's geometry at the
    /// current page zoom.
    /// </summary>
    /// <returns>The pixel rectangle for the proxy bounds.</returns>
    protected virtual Rectangle ComputeBox()
    {
        var report = OwnerReport;
        if (report == null) return Bounds;
        return new Rectangle(
            (int)Math.Round(report.CmToPx(Wrapped.Left)),
            (int)Math.Round(report.CmToPx(Wrapped.Top)),
            Math.Max(2, (int)Math.Round(report.CmToPx(Wrapped.Width))),
            Math.Max(2, (int)Math.Round(report.CmToPx(Wrapped.Height))));
    }

    /// <summary>
    /// Writes the proxy's pixel box back to the wrapped control's centimeter geometry.
    /// </summary>
    /// <param name="box">The pixel rectangle the proxy was moved or resized to.</param>
    protected virtual void WriteBoxToWrapped(Rectangle box)
    {
        var report = OwnerReport;
        if (report == null) return;
        Wrapped.Left = report.PxToCm(box.X);
        Wrapped.Top = report.PxToCm(box.Y);
        Wrapped.Width = report.PxToCm(box.Width);
        Wrapped.Height = report.PxToCm(box.Height);
    }

    /// <summary>
    /// Renders the wrapped report control WYSIWYG by rendering it into an offscreen
    /// command list at 96 DPI and importing the commands scaled by the page zoom.
    /// </summary>
    /// <param name="g">The graphics context positioned at the proxy's origin.</param>
    public override void Render(Graphics g)
    {
        var report = OwnerReport;
        if (report != null)
        {
            var context = report.GetDesignContext();
            var offscreen = new Graphics { MeasureText = g.MeasureText };

            // Guarantee a text-measure callback before rendering. The wrapped control's
            // word-wrapping relies on it; a null callback would throw and leave the
            // control silently blank. Prefer the live graphics' measure function so the
            // design surface measures text exactly as the final render will.
            if (context.MeasureTextCallback == null && offscreen.MeasureText != null)
                context.MeasureTextCallback = offscreen.MeasureText;

            string? renderError = null;
            try
            {
                Wrapped.Render(offscreen, context, Wrapped.Top, Wrapped.Height);
            }
            catch (Exception ex)
            {
                // Design-time render errors must not break the designer surface, but they
                // must not be silent either: surface them for diagnosis and mark the proxy
                // with a visible border so a failed control is not mistaken for an empty one.
                renderError = ex.Message;
                System.Diagnostics.Debug.WriteLine(
                    $"[DesignReportControl] WYSIWYG render failed for '{Name}' ({Wrapped.GetType().Name}): {ex}");
            }
            g.Save();
            g.SetClip(new Rectangle(0, 0, Width, Height));
            g.ImportCommands(offscreen.GetCommands(), report.PageZoom, -Bounds.X, -Bounds.Y);
            // Restore() only restores transforms - the clip must be popped explicitly,
            // otherwise every proxy render leaks a clip region that later corrupts the
            // rendering of everything drawn afterwards on the same graphics object
            // (e.g. the property grid texts).
            g.ResetClip();
            g.Restore();

            if (renderError != null)
                RenderRenderError(g);
        }

        RenderDesignHints(g);
    }

    /// <summary>
    /// Draws a visible red border on a proxy whose WYSIWYG render threw an exception, so a
    /// failed control is distinguishable from an intentionally empty one on the surface.
    /// </summary>
    /// <param name="g">The graphics context positioned at the proxy's origin.</param>
    private void RenderRenderError(Graphics g)
    {
        g.DrawRectangle(Color.Red, 0.5f, 0.5f, Math.Max(1, Width - 1), Math.Max(1, Height - 1), 1.5f);
    }

    /// <summary>
    /// Renders design-time-only hints (placeholders, markers) on top of the WYSIWYG
    /// content. The base implementation draws nothing.
    /// </summary>
    /// <param name="g">The graphics context positioned at the proxy's origin.</param>
    protected virtual void RenderDesignHints(Graphics g)
    {
    }

    /// <summary>
    /// Builds the default control name from the wrapped control's type
    /// (e.g. <c>ReportLabel</c> becomes <c>reportLabel</c>).
    /// </summary>
    /// <param name="wrapped">The wrapped report control.</param>
    /// <returns>The camel-cased default name.</returns>
    protected static string DefaultName(ReportControl wrapped)
    {
        string baseName = wrapped.GetType().Name;
        return char.ToLowerInvariant(baseName[0]) + baseName[1..];
    }
}
