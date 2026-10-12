using OldSchoolForms.Ui.Reports;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// JS facade for a <see cref="ReportPageSetup"/>. All dimensions are exchanged
/// as plain numbers in centimeters.
/// </summary>
public class JsReportPageSetup
{
    private readonly ReportPageSetup _setup;

    /// <summary>
    /// Creates a facade around a page setup.
    /// </summary>
    /// <param name="setup">The wrapped page setup.</param>
    /// <exception cref="ArgumentNullException"><paramref name="setup"/> is <see langword="null"/>.</exception>
    public JsReportPageSetup(ReportPageSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        _setup = setup;
    }

    /// <summary>
    /// The paper size name (<c>A3</c>, <c>A4</c>, <c>A5</c>, <c>Letter</c>,
    /// <c>Legal</c>). Setting an unknown name is ignored; getting returns
    /// <c>custom</c> for non-standard portrait dimensions.
    /// </summary>
    public string PaperSize
    {
        get
        {
            var w = _setup.PortraitWidth.Value;
            var h = _setup.PortraitHeight.Value;
            if (Matches(w, h, ReportPaperSize.A3)) return "A3";
            if (Matches(w, h, ReportPaperSize.A4)) return "A4";
            if (Matches(w, h, ReportPaperSize.A5)) return "A5";
            if (Matches(w, h, ReportPaperSize.Letter)) return "Letter";
            if (Matches(w, h, ReportPaperSize.Legal)) return "Legal";
            return "custom";
        }
        set
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "a3": _setup.SetPaperSize(ReportPaperSize.A3); break;
                case "a4": _setup.SetPaperSize(ReportPaperSize.A4); break;
                case "a5": _setup.SetPaperSize(ReportPaperSize.A5); break;
                case "letter": _setup.SetPaperSize(ReportPaperSize.Letter); break;
                case "legal": _setup.SetPaperSize(ReportPaperSize.Legal); break;
            }
        }
    }

    /// <summary>Whether the page is rendered in landscape orientation.</summary>
    public bool Landscape
    {
        get => _setup.Landscape;
        set => _setup.Landscape = value;
    }

    /// <summary>The paper width in centimeters (orientation-aware).</summary>
    public double PaperWidth => _setup.PaperWidth.Value;

    /// <summary>The paper height in centimeters (orientation-aware).</summary>
    public double PaperHeight => _setup.PaperHeight.Value;

    /// <summary>The printable width in centimeters.</summary>
    public double PrintableWidth => _setup.PrintableWidth.Value;

    /// <summary>The printable height in centimeters.</summary>
    public double PrintableHeight => _setup.PrintableHeight.Value;

    /// <summary>The left margin in centimeters.</summary>
    public double LeftMargin
    {
        get => _setup.LeftMargin.Value;
        set => _setup.LeftMargin = value;
    }

    /// <summary>The right margin in centimeters.</summary>
    public double RightMargin
    {
        get => _setup.RightMargin.Value;
        set => _setup.RightMargin = value;
    }

    /// <summary>The top margin in centimeters.</summary>
    public double TopMargin
    {
        get => _setup.TopMargin.Value;
        set => _setup.TopMargin = value;
    }

    /// <summary>The bottom margin in centimeters.</summary>
    public double BottomMargin
    {
        get => _setup.BottomMargin.Value;
        set => _setup.BottomMargin = value;
    }

    /// <summary>
    /// Sets all four margins to the same value.
    /// </summary>
    /// <param name="cm">The margin in centimeters.</param>
    public void SetMargins(double cm) => _setup.SetMargins(cm);

    /// <summary>
    /// Sets the four margins individually.
    /// </summary>
    /// <param name="left">The left margin in centimeters.</param>
    /// <param name="right">The right margin in centimeters.</param>
    /// <param name="top">The top margin in centimeters.</param>
    /// <param name="bottom">The bottom margin in centimeters.</param>
    public void SetMargins(double left, double right, double top, double bottom)
        => _setup.SetMargins(left, right, top, bottom);

    private static bool Matches(double w, double h, (Cm Width, Cm Height) paper)
        => Math.Abs(w - paper.Width.Value) < 0.001 && Math.Abs(h - paper.Height.Value) < 0.001;
}
