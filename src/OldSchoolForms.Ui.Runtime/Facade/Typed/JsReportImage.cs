using Jint.Native.Object;
using OldSchoolForms.Ui.Reports;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// JS facade for a <see cref="ReportImage"/>.
/// </summary>
public class JsReportImage : JsReportControl
{
    /// <summary>The wrapped image.</summary>
    public ReportImage Image => (ReportImage)Control;

    /// <summary>
    /// Creates a facade around a report image.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="image">The wrapped image.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="image"/> is <see langword="null"/>.</exception>
    public JsReportImage(JsEngineHost host, ReportImage image)
        : base(host, image)
    {
        ArgumentNullException.ThrowIfNull(image);
    }

    /// <summary>
    /// The image sizing mode as a string (<c>Normal</c>, <c>Stretch</c>, <c>Zoom</c>).
    /// Invalid values are ignored.
    /// </summary>
    public string Sizing
    {
        get => Image.Sizing.ToString();
        set
        {
            if (Enum.TryParse<ImageSizing>(value, true, out var sizing))
                Image.Sizing = sizing;
        }
    }

    /// <inheritdoc />
    protected override void ApplyTypeConfig(ObjectInstance config)
    {
        if (JsMarshal.GetString(config, "sizing") is { } sizing) Sizing = sizing;
    }
}
