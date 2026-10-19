using System;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Reports;
using Graphics = OldSchoolForms.Ui.Rendering.Graphics;

namespace OldSchoolForms.Ui.Designer.Reports;

/// <summary>
/// Design-time proxy for a <see cref="ReportLine"/>. The proxy's bounding box maps
/// to the line's start point (Left/Top) and end point (X2/Y2); horizontal and
/// vertical lines keep their orientation when resized.
/// </summary>
[ToolboxIcon("OldSchoolForms.Ui.Resources.Icons.Controls.ctrl_report_line.svg")]
public class DesignReportLine : DesignReportControl
{
    private bool _horizontal = true;
    private bool _vertical;

    /// <summary>
    /// Creates a new line proxy with a default horizontal report line.
    /// </summary>
    public DesignReportLine()
        : this(new ReportLine { X2 = 5, Y2 = 0 })
    {
    }

    /// <summary>
    /// Wraps an existing report line.
    /// </summary>
    /// <param name="line">The report line to edit.</param>
    internal DesignReportLine(ReportLine line)
        : base(line)
    {
    }

    /// <summary>
    /// Gets the wrapped report line.
    /// </summary>
    public new ReportLine Line => (ReportLine)Wrapped;

    /// <summary>
    /// Computes the bounding box from the line's start and end points and captures
    /// the line's orientation so subsequent resizes preserve it.
    /// </summary>
    /// <returns>The pixel rectangle enclosing the line.</returns>
    protected override Rectangle ComputeBox()
    {
        var line = Line;
        _horizontal = line.Y2 == line.Top;
        _vertical = line.X2 == line.Left;

        var report = OwnerReport;
        if (report == null) return Bounds;

        float x1 = report.CmToPx(line.Left);
        float y1 = report.CmToPx(line.Top);
        float x2 = report.CmToPx(line.X2);
        float y2 = report.CmToPx(line.Y2);

        return new Rectangle(
            (int)Math.Round(Math.Min(x1, x2)),
            (int)Math.Round(Math.Min(y1, y2)),
            Math.Max(2, (int)Math.Round(Math.Abs(x2 - x1))),
            Math.Max(2, (int)Math.Round(Math.Abs(y2 - y1))));
    }

    /// <summary>
    /// Writes the proxy's box back to the line's start/end points, preserving a
    /// purely horizontal or vertical orientation when it had one before.
    /// </summary>
    /// <param name="box">The pixel rectangle the proxy was moved or resized to.</param>
    protected override void WriteBoxToWrapped(Rectangle box)
    {
        var report = OwnerReport;
        if (report == null) return;
        var line = Line;

        Cm left = report.PxToCm(box.X);
        Cm top = report.PxToCm(box.Y);
        Cm width = report.PxToCm(box.Width);
        Cm height = report.PxToCm(box.Height);

        line.Left = left;
        line.Top = top;
        line.X2 = _vertical ? left : left + width;
        line.Y2 = _horizontal ? top : top + height;
        line.Width = line.X2 - line.Left;
        line.Height = line.Y2 - line.Top;
    }

    /// <summary>
    /// Renders the line. Purely horizontal or vertical lines are drawn at the center
    /// of the proxy's thin bounding box so the stroke is never clipped by the proxy
    /// bounds (a line at the box edge would lose half its width to the clip).
    /// Diagonal lines use the standard offscreen WYSIWYG render.
    /// </summary>
    /// <param name="g">The graphics context positioned at the proxy's origin.</param>
    public override void Render(Graphics g)
    {
        var report = OwnerReport;
        if (report == null)
        {
            RenderDesignHints(g);
            return;
        }

        var line = Line;
        float lineWidth = Math.Max(1f, report.CmToPx(line.LineWidth));

        if (_horizontal || _vertical)
        {
            g.Save();
            g.SetClip(new Rectangle(0, 0, Width, Height));
            if (_vertical)
                g.DrawLine(line.LineColor, Width / 2f, 0f, Width / 2f, Height, lineWidth);
            else
                g.DrawLine(line.LineColor, 0f, Height / 2f, Width, Height / 2f, lineWidth);
            // Restore() only restores transforms - pop the clip explicitly so the
            // proxy render does not leak a clip region into subsequent rendering.
            g.ResetClip();
            g.Restore();
            RenderDesignHints(g);
            return;
        }

        base.Render(g);
    }
}
