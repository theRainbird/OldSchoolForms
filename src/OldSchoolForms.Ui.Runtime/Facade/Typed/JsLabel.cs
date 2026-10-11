using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// Typed JS facade for a <see cref="Label"/>. Exposes text alignment on top of
/// the generic control members.
/// </summary>
public class JsLabel : JsControl
{
    /// <summary>
    /// Creates the facade around a label.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="label">The wrapped label.</param>
    public JsLabel(JsEngineHost host, Label label)
        : base(host, label)
    {
        Label = label;
    }

    /// <summary>The wrapped label.</summary>
    public Label Label { get; }

    /// <summary>
    /// Text alignment (TopLeft, TopCenter, TopRight, MiddleLeft, MiddleCenter,
    /// MiddleRight, BottomLeft, BottomCenter, BottomRight).
    /// </summary>
    public string TextAlign
    {
        get => Label.TextAlign.ToString();
        set
        {
            if (Enum.TryParse<Core.ContentAlignment>(value, true, out var alignment))
                Label.TextAlign = alignment;
        }
    }
}
