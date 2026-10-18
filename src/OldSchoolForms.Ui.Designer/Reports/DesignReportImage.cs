using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Reports;
using Graphics = OldSchoolForms.Ui.Rendering.Graphics;

namespace OldSchoolForms.Ui.Designer.Reports;

/// <summary>
/// Design-time proxy for a <see cref="ReportImage"/>.
/// </summary>
public class DesignReportImage : DesignReportControl
{
    /// <summary>
    /// Creates a new image proxy with a default report image.
    /// </summary>
    public DesignReportImage()
        : this(new ReportImage())
    {
    }

    /// <summary>
    /// Wraps an existing report image.
    /// </summary>
    /// <param name="image">The report image to edit.</param>
    internal DesignReportImage(ReportImage image)
        : base(image)
    {
    }

    /// <summary>
    /// Gets the wrapped report image.
    /// </summary>
    public new ReportImage Image => (ReportImage)Wrapped;

    /// <summary>
    /// Draws a dashed placeholder with a caption when no image content is assigned,
    /// since image content is not persisted in the report format.
    /// </summary>
    /// <param name="g">The graphics context positioned at the proxy's origin.</param>
    protected override void RenderDesignHints(Graphics g)
    {
        if (Image.Image != null) return;

        var placeholder = Color.FromArgb(160, 128, 128, 128);
        var dash = new[] { 4f, 3f };
        g.DrawLine(placeholder, 1, 1, Width - 1, 1, 1, dash);
        g.DrawLine(placeholder, Width - 1, 1, Width - 1, Height - 1, 1, dash);
        g.DrawLine(placeholder, Width - 1, Height - 1, 1, Height - 1, 1, dash);
        g.DrawLine(placeholder, 1, Height - 1, 1, 1, 1, dash);

        string caption = LangRes.GetString("ReportDesigner_ImagePlaceholder");
        var font = OldSchoolForms.Ui.Theming.ThemeManager.CurrentTheme.DefaultFont;
        if (g.MeasureText != null)
        {
            var (w, _) = g.MeasureText(caption, font, 1f);
            g.DrawString(caption, font, placeholder,
                (Width - w) / 2f, (Height - 14f) / 2f);
        }
        else
        {
            g.DrawString(caption, font, placeholder, Width / 2f - 12f, Height / 2f - 7f);
        }
    }
}
