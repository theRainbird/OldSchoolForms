using Jint;
using Jint.Native;
using Jint.Native.Object;
using OldSchoolForms.Ui.Reports;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// JS facade for a <see cref="ReportBand"/>. Controls are added via
/// <c>band.controls.add({ type: 'label', text: 'Hello', left: 1, top: 0.5, width: 8, height: 0.8 })</c>.
/// </summary>
public class JsReportBand
{
    private readonly JsEngineHost _host;
    private readonly JsReportControlCollection _controls;

    /// <summary>The wrapped band.</summary>
    public ReportBand Band { get; }

    /// <summary>
    /// Creates a facade around a report band.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="band">The wrapped band.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="band"/> is <see langword="null"/>.</exception>
    public JsReportBand(JsEngineHost host, ReportBand band)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(band);
        _host = host;
        Band = band;
        _controls = new JsReportControlCollection(host, band);
    }

    /// <summary>The band height in centimeters.</summary>
    public double Height
    {
        get => Band.Height.Value;
        set => Band.Height = value;
    }

    /// <summary>The band background color as a hex string.</summary>
    public JsValue BackColor
    {
        get => JsValue.FromObject(_host.Engine, JsMarshal.FromColor(Band.BackColor));
        set => Band.BackColor = JsMarshal.ToColor(value, Band.BackColor);
    }

    /// <summary>Whether the band is rendered.</summary>
    public bool Visible
    {
        get => Band.Visible;
        set => Band.Visible = value;
    }

    /// <summary>Whether the band may grow to fit its content.</summary>
    public bool CanGrow
    {
        get => Band.CanGrow;
        set => Band.CanGrow = value;
    }

    /// <summary>Whether the band repeats at the top of each page.</summary>
    public bool RepeatOnNewPage
    {
        get => Band.RepeatOnNewPage;
        set => Band.RepeatOnNewPage = value;
    }

    /// <summary>Whether the band is kept together with the following band.</summary>
    public bool KeepTogether
    {
        get => Band.KeepTogether;
        set => Band.KeepTogether = value;
    }

    /// <summary>The band's control collection facade.</summary>
    public JsReportControlCollection Controls => _controls;
}
