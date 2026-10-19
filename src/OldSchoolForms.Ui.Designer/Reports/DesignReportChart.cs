using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Reports;

namespace OldSchoolForms.Ui.Designer.Reports;

/// <summary>
/// Design-time proxy for a <see cref="ReportChartControl"/>. Renders the live chart via the
/// shared WYSIWYG offscreen render pipeline and exposes the chart model for editing.
/// </summary>
[ToolboxIcon("OldSchoolForms.Ui.Resources.Icons.Controls.ctrl_chart.svg")]
public class DesignReportChart : DesignReportControl
{
    /// <summary>
    /// Initializes a new chart proxy with a default bar chart containing one sample series.
    /// </summary>
    public DesignReportChart()
        : this(new ReportChartControl())
    {
        Chart.Series.Add(new DiagramViewSeries { Name = LangRes.GetString("ReportDesigner_DefaultSeriesName") });
        Chart.Series[0].Add("A", 10);
        Chart.Series[0].Add("B", 20);
        Chart.Series[0].Add("C", 15);
    }

    /// <summary>
    /// Initializes a new chart proxy wrapping an existing chart model.
    /// </summary>
    /// <param name="chart">The chart model to wrap.</param>
    internal DesignReportChart(ReportChartControl chart)
        : base(chart)
    {
    }

    /// <summary>
    /// Gets the wrapped chart model.
    /// </summary>
    public ReportChartControl Chart => (ReportChartControl)Wrapped;
}
