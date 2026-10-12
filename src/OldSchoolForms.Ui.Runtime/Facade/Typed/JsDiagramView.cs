using Jint;
using Jint.Native;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// JS facade for a <see cref="DiagramView"/>. Provides chart type, series,
/// axes, trend line and legend configuration.
/// </summary>
public class JsDiagramView : JsControl
{
    private readonly JsDiagramSeriesCollection _series;
    private readonly JsDiagramAxis _xAxis;
    private readonly JsDiagramAxis _yAxis;
    private readonly JsDiagramTrendLine _trendLine;

    /// <summary>The wrapped diagram view.</summary>
    public DiagramView Chart { get; }

    /// <summary>
    /// Creates a facade around a diagram view.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="chart">The wrapped diagram view.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="chart"/> is <see langword="null"/>.</exception>
    public JsDiagramView(JsEngineHost host, DiagramView chart)
        : base(host, chart)
    {
        ArgumentNullException.ThrowIfNull(chart);
        Chart = chart;
        _series = new JsDiagramSeriesCollection(host, chart);
        _xAxis = new JsDiagramAxis(host, chart.XAxis);
        _yAxis = new JsDiagramAxis(host, chart.YAxis);
        _trendLine = new JsDiagramTrendLine(host, chart.TrendLine);
    }

    /// <summary>
    /// The chart type as a string (<c>Bar</c>, <c>StackedBar</c>, <c>Pie</c>,
    /// <c>Doughnut</c>, <c>Line</c>, <c>Area</c>, <c>Gauge</c>). Invalid values are ignored.
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
            if (Enum.TryParse<LegendPosition>(value, true, out var position))
                Chart.LegendPosition = position;
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

    /// <summary>The series collection facade.</summary>
    public JsDiagramSeriesCollection Series => _series;

    /// <summary>The X axis configuration facade.</summary>
    public JsDiagramAxis XAxis => _xAxis;

    /// <summary>The Y axis configuration facade.</summary>
    public JsDiagramAxis YAxis => _yAxis;

    /// <summary>The trend line configuration facade.</summary>
    public JsDiagramTrendLine TrendLine => _trendLine;

    /// <summary>The current zoom factor.</summary>
    public float ZoomLevel
    {
        get => Chart.ZoomLevel;
        set => Chart.ZoomLevel = value;
    }

    /// <summary>The horizontal pan offset.</summary>
    public float PanX
    {
        get => Chart.PanX;
        set => Chart.PanX = value;
    }

    /// <summary>The vertical pan offset.</summary>
    public float PanY
    {
        get => Chart.PanY;
        set => Chart.PanY = value;
    }

    /// <summary>Data member used for point labels.</summary>
    public string DisplayMember
    {
        get => Chart.DisplayMember;
        set => Chart.DisplayMember = value ?? string.Empty;
    }

    /// <summary>Data member used for point values.</summary>
    public string ValueMember
    {
        get => Chart.ValueMember;
        set => Chart.ValueMember = value ?? string.Empty;
    }

    /// <summary>Data member used to split points into categories.</summary>
    public string CategoryMember
    {
        get => Chart.CategoryMember;
        set => Chart.CategoryMember = value ?? string.Empty;
    }

    /// <summary>
    /// The diagram's data source. Accepts a <c>JsBindingSource</c>, a
    /// <c>JsDataTable</c>, an array of plain JS objects, or any CLR
    /// <see cref="IEnumerable{T}"/>.
    /// </summary>
    public JsValue DataSource
    {
        get => JsValue.FromObject(Host.Engine, Chart.DataSource);
        set => Chart.DataSource = JsMarshal.ToDataSource(value);
    }

    /// <summary>Rebuilds the series from the bound data source.</summary>
    public void PopulateFromDataSource() => Chart.PopulateFromDataSource();

    /// <summary>Resets zoom and pan to the default view.</summary>
    public void ResetView() => Chart.ResetView();

    /// <summary>Fits the chart content to the visible bounds.</summary>
    public void FitToBounds() => Chart.FitToBounds();
}
