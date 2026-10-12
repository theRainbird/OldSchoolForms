using Jint;
using Jint.Native;
using Jint.Native.Object;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Reports;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// JS facade for a <see cref="ReportChartControl"/>. Series are added via
/// <c>chart.series.add(name, points)</c> where <c>points</c> is a JS array of
/// <c>{ label, value, color? }</c> objects.
/// </summary>
public class JsReportChart : JsReportControl
{
    private readonly JsReportChartSeriesCollection _series;

    /// <summary>The wrapped chart control.</summary>
    public ReportChartControl Chart => (ReportChartControl)Control;

    /// <summary>
    /// Creates a facade around a report chart control.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="chart">The wrapped chart control.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="chart"/> is <see langword="null"/>.</exception>
    public JsReportChart(JsEngineHost host, ReportChartControl chart)
        : base(host, chart)
    {
        ArgumentNullException.ThrowIfNull(chart);
        _series = new JsReportChartSeriesCollection(host, chart.Series);
    }

    /// <summary>
    /// The chart type as a string (<c>Bar</c>, <c>StackedBar</c>, <c>Pie</c>,
    /// <c>Doughnut</c>, <c>Line</c>, <c>Area</c>, <c>Gauge</c>). Invalid values
    /// are ignored.
    /// </summary>
    public string ChartType
    {
        get => Chart.ChartType.ToString();
        set
        {
            if (Enum.TryParse<DiagramType>(value, true, out var type))
                Chart.ChartType = type;
        }
    }

    /// <summary>The chart title.</summary>
    public string Title
    {
        get => Chart.Title;
        set => Chart.Title = value ?? string.Empty;
    }

    /// <summary>Whether the legend is shown.</summary>
    public bool ShowLegend
    {
        get => Chart.ShowLegend;
        set => Chart.ShowLegend = value;
    }

    /// <summary>
    /// The legend position as a string (<c>None</c>, <c>Top</c>, <c>Bottom</c>,
    /// <c>Left</c>, <c>Right</c>). Invalid values are ignored.
    /// </summary>
    public string LegendPosition
    {
        get => Chart.LegendPosition.ToString();
        set
        {
            if (Enum.TryParse<LegendPosition>(value, true, out var pos))
                Chart.LegendPosition = pos;
        }
    }

    /// <summary>
    /// The data label style as a string (<c>None</c>, <c>Value</c>,
    /// <c>Percentage</c>, <c>Category</c>). Invalid values are ignored.
    /// </summary>
    public string DataLabelStyle
    {
        get => Chart.DataLabelStyle.ToString();
        set
        {
            if (Enum.TryParse<LabelStyle>(value, true, out var style))
                Chart.DataLabelStyle = style;
        }
    }

    /// <summary>The chart series collection facade.</summary>
    public JsReportChartSeriesCollection Series => _series;

    /// <inheritdoc />
    protected override void ApplyTypeConfig(ObjectInstance config)
    {
        if (JsMarshal.GetString(config, "chartType") is { } chartType) ChartType = chartType;
        if (JsMarshal.GetString(config, "title") is { } title) Title = title;
        if (JsMarshal.GetBool(config, "showLegend") is { } showLegend) ShowLegend = showLegend;
        if (JsMarshal.GetString(config, "legendPosition") is { } legend) LegendPosition = legend;
        if (JsMarshal.GetString(config, "dataLabelStyle") is { } labels) DataLabelStyle = labels;
    }
}
