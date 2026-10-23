using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Printing;

namespace OldSchoolForms.Ui.Reports;

/// <summary>
/// Defines a printable banded report with sections (ReportHeader, PageHeader,
/// Detail, PageFooter, ReportFooter), optional grouping, and a data source.
/// All measurements are in centimeters (<see cref="Cm"/>).
/// Use <see cref="PageSetup"/> for convenient configuration of paper size,
/// orientation, and margins with German defaults (A4 portrait, 2.0 cm margins).
/// </summary>
public class Report
{
    private readonly ReportPageSetup _pageSetup = new();

    /// <summary>
    /// Gets or sets the name of this report.
    /// </summary>
    public string Name { get; set; } = "Report";

    /// <summary>
    /// Gets or sets the <see cref="ReportPageSetup"/> which provides paper size,
    /// orientation (portrait/landscape), and margin configuration.
    /// German default: A4 portrait, 2.0 cm margins.
    /// </summary>
    public ReportPageSetup PageSetup => _pageSetup;

    /// <summary>
    /// Gets or sets the total page width in cm (accounts for <see cref="ReportPageSetup.Landscape"/>).
    /// </summary>
    public Cm PageWidth
    {
        get => _pageSetup.PaperWidth;
        set
        {
            if (_pageSetup.Landscape)
                _pageSetup.SetPaperSize(_pageSetup.PortraitWidth, value);
            else
                _pageSetup.SetPaperSize(value, _pageSetup.PortraitHeight);
        }
    }

    /// <summary>
    /// Gets or sets the total page height in cm (accounts for <see cref="ReportPageSetup.Landscape"/>).
    /// </summary>
    public Cm PageHeight
    {
        get => _pageSetup.PaperHeight;
        set
        {
            if (_pageSetup.Landscape)
                _pageSetup.SetPaperSize(value, _pageSetup.PortraitWidth);
            else
                _pageSetup.SetPaperSize(_pageSetup.PortraitWidth, value);
        }
    }

    /// <summary>
    /// Gets or sets the left margin in cm (German default: 2.0).
    /// </summary>
    public Cm LeftMargin
    {
        get => _pageSetup.LeftMargin;
        set => _pageSetup.LeftMargin = value;
    }

    /// <summary>
    /// Gets or sets the right margin in cm (German default: 2.0).
    /// </summary>
    public Cm RightMargin
    {
        get => _pageSetup.RightMargin;
        set => _pageSetup.RightMargin = value;
    }

    /// <summary>
    /// Gets or sets the top margin in cm (German default: 2.0).
    /// </summary>
    public Cm TopMargin
    {
        get => _pageSetup.TopMargin;
        set => _pageSetup.TopMargin = value;
    }

    /// <summary>
    /// Gets or sets the bottom margin in cm (German default: 2.0).
    /// </summary>
    public Cm BottomMargin
    {
        get => _pageSetup.BottomMargin;
        set => _pageSetup.BottomMargin = value;
    }

    /// <summary>
    /// Gets the printable width (page width minus left and right margins).
    /// </summary>
    public Cm PrintableWidth => _pageSetup.PrintableWidth;

    /// <summary>
    /// Gets the printable height (page height minus top and bottom margins).
    /// </summary>
    public Cm PrintableHeight => _pageSetup.PrintableHeight;

    /// <summary>
    /// Gets or sets the PageSettings for this report (paper size, orientation, printer margins).
    /// Used when printing; the margins from PageSettings can override the report margins.
    /// </summary>
    public PageSettings? PageSettings { get; set; }

    /// <summary>
    /// Gets the named data source definitions of this report. Each source carries a
    /// schema (<see cref="ReportDataSource.Fields"/>) plus runtime data
    /// (<see cref="ReportDataSource.Data"/>). Bands select their source via
    /// <see cref="ReportBand.DataSourceName"/>. Persisted as the optional
    /// <c>dataSources</c> member of the <c>OldSchoolForms.Report</c> format.
    /// </summary>
    public ReportDataSourceCollection DataSources { get; } = new();

    /// <summary>
    /// Gets or sets the legacy single data source for this report.
    /// Must be an IEnumerable (list, array, or any enumerable collection of records).
    /// Kept as a migration path: when a band has no <see cref="ReportBand.DataSourceName"/>
    /// and no named source is defined in <see cref="DataSources"/>, the render engine
    /// falls back to this property. New reports should define explicit
    /// <see cref="DataSources"/> entries instead.
    /// </summary>
    public object? DataSource { get; set; }

    /// <summary>
    /// Gets the design-time list of data field names available for data binding.
    /// This list is a designer aid for the field picker and the WYSIWYG sample-data
    /// preview; the render engine ignores it. Persisted as the optional
    /// <c>designFields</c> member of the <c>OldSchoolForms.Report</c> format.
    /// </summary>
    public List<string> DesignFields { get; } = new();

    /// <summary>
    /// Gets the report header band (rendered once at the beginning of the report).
    /// </summary>
    public ReportBand ReportHeader { get; } = new(ReportBandType.ReportHeader);

    /// <summary>
    /// Gets the page header band (rendered at the top of every page).
    /// </summary>
    public ReportBand PageHeader { get; } = new(ReportBandType.PageHeader);

    /// <summary>
    /// Gets the detail band (rendered once for each record in the data source).
    /// </summary>
    public ReportBand Detail { get; } = new(ReportBandType.Detail);

    /// <summary>
    /// Gets the page footer band (rendered at the bottom of every page).
    /// </summary>
    public ReportBand PageFooter { get; } = new(ReportBandType.PageFooter);

    /// <summary>
    /// Gets the report footer band (rendered once at the end of the report, after the last page footer).
    /// </summary>
    public ReportBand ReportFooter { get; } = new(ReportBandType.ReportFooter);

    /// <summary>
    /// Gets the groups defined for this report.
    /// Groups are rendered in order and can be nested via <see cref="ReportGroup.Groups"/>.
    /// </summary>
    public ReportGroupCollection Groups { get; } = new();

    /// <summary>
    /// Initializes a new Report with German defaults: A4 portrait, 2.0 cm margins.
    /// </summary>
    public Report()
    {
    }

    /// <summary>
    /// Initializes a new Report with the specified name.
    /// </summary>
    /// <param name="name">The report name.</param>
    public Report(string name)
    {
        Name = name;
    }
}
