using Jint.Native;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// Typed JS facade for a <see cref="ProgressBar"/>. Exposes range, value,
/// orientation and the <c>valuechanged</c> event.
/// </summary>
public class JsProgressBar : JsControl
{
    /// <summary>
    /// Creates the facade around a progress bar.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="progressBar">The wrapped progress bar.</param>
    public JsProgressBar(JsEngineHost host, ProgressBar progressBar)
        : base(host, progressBar)
    {
        ProgressBar = progressBar;
    }

    /// <summary>The wrapped progress bar.</summary>
    public ProgressBar ProgressBar { get; }

    /// <summary>Lower bound of the range.</summary>
    public int Minimum
    {
        get => ProgressBar.Minimum;
        set => ProgressBar.Minimum = value;
    }

    /// <summary>Upper bound of the range.</summary>
    public int Maximum
    {
        get => ProgressBar.Maximum;
        set => ProgressBar.Maximum = value;
    }

    /// <summary>The current value within the range.</summary>
    public int Value
    {
        get => ProgressBar.Value;
        set => ProgressBar.Value = value;
    }

    /// <summary>Orientation of the bar (Horizontal or Vertical).</summary>
    public string Orientation
    {
        get => ProgressBar.Orientation.ToString();
        set
        {
            if (Enum.TryParse<Controls.Basic.Orientation>(value, true, out var orientation))
                ProgressBar.Orientation = orientation;
        }
    }

    /// <inheritdoc />
    protected override Delegate? WireCustom(string normalized, JsValue callback)
    {
        if (normalized != "valuechanged")
            return null;

        EventHandler handler = (s, e) => Raise("valuechanged", callback, e);
        ProgressBar.ValueChanged += handler;
        return handler;
    }

    /// <inheritdoc />
    protected override bool UnwireCustom(string normalized, Delegate handler)
    {
        if (normalized != "valuechanged")
            return false;

        ProgressBar.ValueChanged -= (EventHandler)handler;
        return true;
    }
}
