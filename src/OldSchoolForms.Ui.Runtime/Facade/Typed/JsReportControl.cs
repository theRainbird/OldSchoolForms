using Jint;
using Jint.Native;
using Jint.Native.Object;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Reports;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// Base JS facade for a <see cref="ReportControl"/>. Exposes the common
/// geometry (centimeter numbers), visibility, colors, font and formatting.
/// Type-specific members are provided by the derived facades.
/// </summary>
public class JsReportControl
{
    /// <summary>The JS engine used for callbacks.</summary>
    protected readonly JsEngineHost Host;

    /// <summary>The wrapped report control.</summary>
    public ReportControl Control { get; }

    /// <summary>
    /// Creates a facade around a report control.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="control">The wrapped control.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="control"/> is <see langword="null"/>.</exception>
    public JsReportControl(JsEngineHost host, ReportControl control)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(control);
        Host = host;
        Control = control;
    }

    /// <summary>The control name.</summary>
    public string Name
    {
        get => Control.Name;
        set => Control.Name = value ?? string.Empty;
    }

    /// <summary>The left position in centimeters.</summary>
    public double Left
    {
        get => Control.Left.Value;
        set => Control.Left = value;
    }

    /// <summary>The top position in centimeters.</summary>
    public double Top
    {
        get => Control.Top.Value;
        set => Control.Top = value;
    }

    /// <summary>The width in centimeters.</summary>
    public double Width
    {
        get => Control.Width.Value;
        set => Control.Width = value;
    }

    /// <summary>The height in centimeters.</summary>
    public double Height
    {
        get => Control.Height.Value;
        set => Control.Height = value;
    }

    /// <summary>Whether the control is rendered.</summary>
    public bool Visible
    {
        get => Control.Visible;
        set => Control.Visible = value;
    }

    /// <summary>Whether the control may grow vertically at render time.</summary>
    public bool CanGrow
    {
        get => Control.CanGrow;
        set => Control.CanGrow = value;
    }

    /// <summary>Whether the control may shrink vertically at render time.</summary>
    public bool CanShrink
    {
        get => Control.CanShrink;
        set => Control.CanShrink = value;
    }

    /// <summary>The background color as a hex string.</summary>
    public JsValue BackColor
    {
        get => JsValue.FromObject(Host.Engine, JsMarshal.FromColor(Control.BackColor));
        set => Control.BackColor = JsMarshal.ToColor(value, Control.BackColor);
    }

    /// <summary>The foreground (text) color as a hex string.</summary>
    public JsValue ForeColor
    {
        get => JsValue.FromObject(Host.Engine, JsMarshal.FromColor(Control.ForeColor));
        set => Control.ForeColor = JsMarshal.ToColor(value, Control.ForeColor);
    }

    /// <summary>
    /// The horizontal text alignment as a string (<c>Left</c>, <c>Center</c>,
    /// <c>Right</c>). Invalid values are ignored.
    /// </summary>
    public string TextAlign
    {
        get => Control.TextAlign.ToString();
        set
        {
            if (Enum.TryParse<TextAlignment>(value, true, out var align))
                Control.TextAlign = align;
        }
    }

    /// <summary>The format string applied to the rendered value.</summary>
    public string Format
    {
        get => Control.Format ?? string.Empty;
        set => Control.Format = string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>
    /// Applies the common members plus the type-specific members of a JS
    /// configuration object to this control.
    /// </summary>
    /// <param name="config">The JS configuration object.</param>
    internal void ApplyConfig(ObjectInstance config)
    {
        if (JsMarshal.GetString(config, "name") is { } name) Name = name;
        if (JsMarshal.GetDouble(config, "left") is { } left) Left = left;
        if (JsMarshal.GetDouble(config, "top") is { } top) Top = top;
        if (JsMarshal.GetDouble(config, "width") is { } width) Width = width;
        if (JsMarshal.GetDouble(config, "height") is { } height) Height = height;
        if (JsMarshal.GetBool(config, "visible") is { } visible) Visible = visible;
        if (JsMarshal.GetBool(config, "canGrow") is { } canGrow) CanGrow = canGrow;
        if (JsMarshal.GetBool(config, "canShrink") is { } canShrink) CanShrink = canShrink;
        var back = JsMarshal.GetMember(config, "backColor");
        if (!back.IsUndefined()) BackColor = back;
        var fore = JsMarshal.GetMember(config, "foreColor");
        if (!fore.IsUndefined()) ForeColor = fore;
        if (JsMarshal.GetString(config, "textAlign") is { } align) TextAlign = align;
        if (JsMarshal.GetString(config, "format") is { } format) Format = format;
        ApplyTypeConfig(config);
    }

    /// <summary>
    /// Applies the type-specific members of a JS configuration object.
    /// </summary>
    /// <param name="config">The JS configuration object.</param>
    protected virtual void ApplyTypeConfig(ObjectInstance config)
    {
    }
}
