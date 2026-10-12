using Jint;
using Jint.Native;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// JS facade for a <see cref="DiagramViewAxisConfig"/> (X or Y axis of a diagram).
/// </summary>
public class JsDiagramAxis
{
    private readonly JsEngineHost _host;

    /// <summary>The wrapped axis configuration.</summary>
    public DiagramViewAxisConfig Axis { get; }

    /// <summary>
    /// Creates a facade around an axis configuration.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="axis">The wrapped axis configuration.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="axis"/> is <see langword="null"/>.</exception>
    public JsDiagramAxis(JsEngineHost host, DiagramViewAxisConfig axis)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(axis);
        _host = host;
        Axis = axis;
    }

    /// <summary>Whether the axis is visible.</summary>
    public bool Visible
    {
        get => Axis.Visible;
        set => Axis.Visible = value;
    }

    /// <summary>
    /// The axis scale type as a string (<c>Linear</c>, <c>Logarithmic</c>).
    /// Invalid values are ignored.
    /// </summary>
    public string Type
    {
        get => Axis.Type.ToString();
        set
        {
            if (Enum.TryParse<AxisType>(value, true, out var type))
                Axis.Type = type;
        }
    }

    /// <summary>The axis title, or <c>null</c>.</summary>
    public string Title
    {
        get => Axis.Title ?? string.Empty;
        set => Axis.Title = string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>The explicit minimum axis value, or <c>null</c> for auto-scaling.</summary>
    public JsValue MinValue
    {
        get => Axis.MinValue == null ? JsValue.Null : JsValue.FromObject(_host.Engine, Axis.MinValue.Value);
        set
        {
            if (value is null || value.IsNull() || value.IsUndefined())
                Axis.MinValue = null;
            else if (value.IsNumber())
                Axis.MinValue = value.AsNumber();
        }
    }

    /// <summary>The explicit maximum axis value, or <c>null</c> for auto-scaling.</summary>
    public JsValue MaxValue
    {
        get => Axis.MaxValue == null ? JsValue.Null : JsValue.FromObject(_host.Engine, Axis.MaxValue.Value);
        set
        {
            if (value is null || value.IsNull() || value.IsUndefined())
                Axis.MaxValue = null;
            else if (value.IsNumber())
                Axis.MaxValue = value.AsNumber();
        }
    }

    /// <summary>The tick interval; <c>0</c> means auto.</summary>
    public double TickInterval
    {
        get => Axis.TickInterval;
        set => Axis.TickInterval = value;
    }

    /// <summary>Whether grid lines are drawn for this axis.</summary>
    public bool ShowGridLines
    {
        get => Axis.ShowGridLines;
        set => Axis.ShowGridLines = value;
    }

    /// <summary>Whether tick labels are shown.</summary>
    public bool ShowLabels
    {
        get => Axis.ShowLabels;
        set => Axis.ShowLabels = value;
    }

    /// <summary>Whether the axis line itself is drawn.</summary>
    public bool ShowAxisLine
    {
        get => Axis.ShowAxisLine;
        set => Axis.ShowAxisLine = value;
    }
}
