using Jint;
using Jint.Native;
using Jint.Native.Array;
using OldSchoolForms.Shared.Scripting;
using OldSchoolForms.Ui.Reports;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// JS facade for a <see cref="Report"/>. Provides the report name, page setup,
/// the five bands (reportHeader/pageHeader/detail/pageFooter/reportFooter),
/// grouping, a data source and headless PDF export.
/// </summary>
public class JsReport
{
    private readonly JsEngineHost _host;
    private readonly JsReportPageSetup _pageSetup;
    private readonly JsReportBand _reportHeader;
    private readonly JsReportBand _pageHeader;
    private readonly JsReportBand _detail;
    private readonly JsReportBand _pageFooter;
    private readonly JsReportBand _reportFooter;
    private readonly JsReportGroupCollection _groups;

    /// <summary>The wrapped report.</summary>
    public Report Report { get; }

    /// <summary>
    /// Creates a facade around a report.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="report">The wrapped report.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="report"/> is <see langword="null"/>.</exception>
    public JsReport(JsEngineHost host, Report report)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(report);
        _host = host;
        Report = report;
        _pageSetup = new JsReportPageSetup(report.PageSetup);
        _reportHeader = new JsReportBand(host, report.ReportHeader);
        _pageHeader = new JsReportBand(host, report.PageHeader);
        _detail = new JsReportBand(host, report.Detail);
        _pageFooter = new JsReportBand(host, report.PageFooter);
        _reportFooter = new JsReportBand(host, report.ReportFooter);
        _groups = new JsReportGroupCollection(host, report.Groups);
    }

    /// <summary>The report name.</summary>
    public string Name
    {
        get => Report.Name;
        set => Report.Name = value ?? string.Empty;
    }

    /// <summary>The page setup facade.</summary>
    public JsReportPageSetup PageSetup => _pageSetup;

    /// <summary>The report header band facade.</summary>
    public JsReportBand ReportHeader => _reportHeader;

    /// <summary>The page header band facade.</summary>
    public JsReportBand PageHeader => _pageHeader;

    /// <summary>The detail band facade.</summary>
    public JsReportBand Detail => _detail;

    /// <summary>The page footer band facade.</summary>
    public JsReportBand PageFooter => _pageFooter;

    /// <summary>The report footer band facade.</summary>
    public JsReportBand ReportFooter => _reportFooter;

    /// <summary>The group collection facade.</summary>
    public JsReportGroupCollection Groups => _groups;

    /// <summary>
    /// The report data source. Accepts a <c>JsDataTable</c> (uses its default
    /// view), a <c>JsDataSet</c> (uses the first table's default view), a JS
    /// array of objects (converted to record maps) or any CLR object.
    /// </summary>
    public JsValue DataSource
    {
        get => JsValue.FromObject(_host.Engine, Report.DataSource);
        set
        {
            if (value is null || value.IsUndefined() || value.IsNull())
            {
                Report.DataSource = null;
                return;
            }

            if (value.ToObject() is JsDataTable table)
            {
                Report.DataSource = table.Table.DefaultView;
                return;
            }

            if (value.ToObject() is JsDataSet set)
            {
                Report.DataSource = set.Set.Tables.Count > 0 ? set.Set.Tables[0].DefaultView : null;
                return;
            }

            if (value is JsArray)
            {
                Report.DataSource = JsMarshal.ToRecordList(value);
                return;
            }

            Report.DataSource = value.ToObject();
        }
    }

    /// <summary>
    /// Exports the report to a PDF file at the given path.
    /// </summary>
    /// <param name="path">The target file path.</param>
    public void ExportPdf(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        new ReportExportPdf().Export(Report, path);
    }

    /// <summary>
    /// Exports the report to PDF bytes and returns them as a base64 string.
    /// </summary>
    /// <returns>The base64-encoded PDF bytes.</returns>
    public string ExportPdfBytes()
        => Convert.ToBase64String(new ReportExportPdf().ExportToBytes(Report));

    /// <summary>
    /// Renders the report headlessly and returns the number of pages.
    /// </summary>
    /// <returns>The page count.</returns>
    public int PageCount()
        => new ReportRenderEngine().Render(Report).Count;
}
