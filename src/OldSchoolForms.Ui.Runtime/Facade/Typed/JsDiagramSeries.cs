using Jint;
using Jint.Native;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// JS facade for a <see cref="DiagramViewSeries"/>.
/// </summary>
public class JsDiagramSeries
{
    private readonly JsEngineHost _host;

    /// <summary>The wrapped series.</summary>
    public DiagramViewSeries Series { get; }

    /// <summary>
    /// Creates a facade around a diagram series.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="series">The wrapped series.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="series"/> is <see langword="null"/>.</exception>
    public JsDiagramSeries(JsEngineHost host, DiagramViewSeries series)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(series);
        _host = host;
        Series = series;
    }

    /// <summary>The series name.</summary>
    public string Name
    {
        get => Series.Name;
        set => Series.Name = value ?? string.Empty;
    }

    /// <summary>The series color as a hex string, or <c>null</c> for the default palette color.</summary>
    public JsValue Color
    {
        get => Series.Color == null
            ? JsValue.Null
            : JsValue.FromObject(_host.Engine, JsMarshal.FromColor(Series.Color.Value));
        set
        {
            if (value is null || value.IsNull() || value.IsUndefined())
                Series.Color = null;
            else
                Series.Color = JsMarshal.ToColor(value, Series.Color ?? default);
        }
    }

    /// <summary>The number of data points in the series.</summary>
    public int PointCount => Series.Points.Count;

    /// <summary>
    /// Gets a data point as a JS object with <c>label</c> and <c>value</c> members.
    /// </summary>
    /// <param name="index">The zero-based point index.</param>
    /// <returns>The data point object.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside the series.</exception>
    public JsValue GetPoint(int index)
    {
        if (index < 0 || index >= Series.Points.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        var point = Series.Points[index];
        return JsValue.FromObject(_host.Engine, new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["label"] = point.Label,
            ["value"] = point.Value
        });
    }

    /// <summary>
    /// Appends a data point to the series.
    /// </summary>
    /// <param name="label">The point label.</param>
    /// <param name="value">The point value.</param>
    /// <param name="color">Optional per-point color as a hex string.</param>
    public void Add(string? label, double value, JsValue? color = null)
    {
        var point = new DiagramViewDataPoint(label, value);
        if (color is not null && !color.IsNull() && !color.IsUndefined())
            point.Color = JsMarshal.ToColor(color, point.Color ?? default);
        Series.Add(point);
    }

    /// <summary>Removes all data points from the series.</summary>
    public void Clear() => Series.Clear();
}
