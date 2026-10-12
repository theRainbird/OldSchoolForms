using Jint;
using Jint.Native;
using Jint.Native.Object;
using OldSchoolForms.Ui.Reports;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// JS facade for a <see cref="ReportLine"/>.
/// </summary>
public class JsReportLine : JsReportControl
{
    /// <summary>The wrapped line.</summary>
    public ReportLine Line => (ReportLine)Control;

    /// <summary>
    /// Creates a facade around a report line.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="line">The wrapped line.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="line"/> is <see langword="null"/>.</exception>
    public JsReportLine(JsEngineHost host, ReportLine line)
        : base(host, line)
    {
        ArgumentNullException.ThrowIfNull(line);
    }

    /// <summary>The end X coordinate in centimeters.</summary>
    public double X2
    {
        get => Line.X2.Value;
        set => Line.X2 = value;
    }

    /// <summary>The end Y coordinate in centimeters.</summary>
    public double Y2
    {
        get => Line.Y2.Value;
        set => Line.Y2 = value;
    }

    /// <summary>The line width in centimeters.</summary>
    public double LineWidth
    {
        get => Line.LineWidth.Value;
        set => Line.LineWidth = value;
    }

    /// <summary>The line color as a hex string.</summary>
    public JsValue LineColor
    {
        get => JsValue.FromObject(Host.Engine, JsMarshal.FromColor(Line.LineColor));
        set => Line.LineColor = JsMarshal.ToColor(value, Line.LineColor);
    }

    /// <inheritdoc />
    protected override void ApplyTypeConfig(ObjectInstance config)
    {
        if (JsMarshal.GetDouble(config, "x2") is { } x2) X2 = x2;
        if (JsMarshal.GetDouble(config, "y2") is { } y2) Y2 = y2;
        if (JsMarshal.GetDouble(config, "lineWidth") is { } lineWidth) LineWidth = lineWidth;
        var color = JsMarshal.GetMember(config, "lineColor");
        if (!color.IsUndefined()) LineColor = color;
    }
}
