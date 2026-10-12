using Jint;
using Jint.Native;
using Jint.Native.Object;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// JS facade for the series collection of a <see cref="DiagramView"/>.
/// Series are added with a name and an array of points, e.g.
/// <c>chart.series.add('Q1', [{ label: 'Jan', value: 120 }, { label: 'Feb', value: 140 }])</c>.
/// </summary>
public class JsDiagramSeriesCollection
{
    private readonly JsEngineHost _host;
    private readonly DiagramView _view;

    /// <summary>
    /// Creates the facade over the diagram's series collection.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="view">The owning diagram view.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="view"/> is <see langword="null"/>.</exception>
    public JsDiagramSeriesCollection(JsEngineHost host, DiagramView view)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(view);
        _host = host;
        _view = view;
    }

    /// <summary>The number of series.</summary>
    public int Count => _view.Series.Count;

    /// <summary>
    /// Gets the series facade at the given index.
    /// </summary>
    /// <param name="index">The zero-based series index.</param>
    /// <returns>The series facade.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside the collection.</exception>
    public JsDiagramSeries GetAt(int index) => new(_host, _view.Series[index]);

    /// <summary>
    /// Adds a series with the given name and points. Each point is an object with
    /// <c>label</c>, <c>value</c> and optional <c>color</c> (hex string) members.
    /// </summary>
    /// <param name="name">The series name.</param>
    /// <param name="points">The JS array of point objects.</param>
    /// <returns>The new series facade.</returns>
    public JsDiagramSeries Add(string name, JsValue points)
    {
        var series = new DiagramViewSeries { Name = name ?? string.Empty };
        foreach (var pointValue in EnumeratePoints(points))
        {
            if (pointValue is not ObjectInstance point)
                continue;
            var label = JsMarshal.GetString(point, "label");
            var value = JsMarshal.GetDouble(point, "value") ?? 0d;
            var dataPoint = new DiagramViewDataPoint(label, value);
            var color = JsMarshal.GetMember(point, "color");
            if (!color.IsUndefined() && !color.IsNull())
                dataPoint.Color = JsMarshal.ToColor(color, dataPoint.Color ?? default);
            series.Add(dataPoint);
        }
        _view.Series.Add(series);
        return new JsDiagramSeries(_host, series);
    }

    private static IEnumerable<JsValue> EnumeratePoints(JsValue points)
    {
        if (points is JsArray array)
        {
            for (var i = 0; i < array.Length; i++)
                yield return array.Get((uint)i);
        }
        else if (points is ObjectInstance)
        {
            yield return points;
        }
    }

    /// <summary>Removes all series.</summary>
    public void Clear() => _view.Series.Clear();
}
