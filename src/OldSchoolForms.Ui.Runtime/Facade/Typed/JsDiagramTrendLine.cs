using Jint;
using Jint.Native;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// JS facade for a <see cref="DiagramViewTrendLine"/>.
/// </summary>
public class JsDiagramTrendLine
{
    private readonly JsEngineHost _host;

    /// <summary>The wrapped trend line configuration.</summary>
    public DiagramViewTrendLine TrendLine { get; }

    /// <summary>
    /// Creates a facade around a trend line configuration.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="trendLine">The wrapped trend line.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="trendLine"/> is <see langword="null"/>.</exception>
    public JsDiagramTrendLine(JsEngineHost host, DiagramViewTrendLine trendLine)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(trendLine);
        _host = host;
        TrendLine = trendLine;
    }

    /// <summary>
    /// The trend line type as a string (<c>None</c>, <c>LinearRegression</c>,
    /// <c>MovingAverage</c>). Invalid values are ignored.
    /// </summary>
    public string Type
    {
        get => TrendLine.Type.ToString();
        set
        {
            if (Enum.TryParse<TrendLineType>(value, true, out var type))
                TrendLine.Type = type;
        }
    }

    /// <summary>The trend line color as a hex string.</summary>
    public JsValue Color
    {
        get => JsValue.FromObject(_host.Engine, JsMarshal.FromColor(TrendLine.Color));
        set => TrendLine.Color = JsMarshal.ToColor(value, TrendLine.Color);
    }

    /// <summary>The trend line width in pixels.</summary>
    public float LineWidth
    {
        get => TrendLine.LineWidth;
        set => TrendLine.LineWidth = value;
    }

    /// <summary>The period for a moving-average trend line.</summary>
    public int MovingAveragePeriod
    {
        get => TrendLine.MovingAveragePeriod;
        set => TrendLine.MovingAveragePeriod = value;
    }
}
