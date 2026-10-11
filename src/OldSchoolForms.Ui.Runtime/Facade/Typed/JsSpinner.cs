using Jint.Native;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// Typed JS facade for a <see cref="Spinner"/>. Exposes animation settings,
/// dot color and the <c>activechanged</c> event.
/// </summary>
public class JsSpinner : JsControl
{
    /// <summary>
    /// Creates the facade around a spinner.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="spinner">The wrapped spinner.</param>
    public JsSpinner(JsEngineHost host, Spinner spinner)
        : base(host, spinner)
    {
        Spinner = spinner;
    }

    /// <summary>The wrapped spinner.</summary>
    public Spinner Spinner { get; }

    /// <summary>Whether the spinner animation is running.</summary>
    public bool Active
    {
        get => Spinner.Active;
        set => Spinner.Active = value;
    }

    /// <summary>Whether the animation starts automatically when shown.</summary>
    public bool AutoStart
    {
        get => Spinner.AutoStart;
        set => Spinner.AutoStart = value;
    }

    /// <summary>Number of dots in the spinner.</summary>
    public int DotCount
    {
        get => Spinner.DotCount;
        set => Spinner.DotCount = value;
    }

    /// <summary>Radius of each dot in pixels.</summary>
    public int DotRadius
    {
        get => Spinner.DotRadius;
        set => Spinner.DotRadius = value;
    }

    /// <summary>Radius of the spinner circle in pixels.</summary>
    public int SpinnerRadius
    {
        get => Spinner.SpinnerRadius;
        set => Spinner.SpinnerRadius = value;
    }

    /// <summary>Interval between animation frames in milliseconds.</summary>
    public int AnimationInterval
    {
        get => Spinner.AnimationInterval;
        set => Spinner.AnimationInterval = value;
    }

    /// <summary>Color of the dots as a hex string or <c>{r,g,b,a}</c> object.</summary>
    public JsValue DotColor
    {
        get => JsValue.FromObject(Host.Engine, JsMarshal.FromColor(Spinner.DotColor));
        set => Spinner.DotColor = JsMarshal.ToColor(value, Spinner.DotColor);
    }

    /// <inheritdoc />
    protected override Delegate? WireCustom(string normalized, JsValue callback)
    {
        if (normalized != "activechanged")
            return null;

        EventHandler handler = (s, e) => Raise("activechanged", callback, e);
        Spinner.ActiveChanged += handler;
        return handler;
    }

    /// <inheritdoc />
    protected override bool UnwireCustom(string normalized, Delegate handler)
    {
        if (normalized != "activechanged")
            return false;

        Spinner.ActiveChanged -= (EventHandler)handler;
        return true;
    }
}
