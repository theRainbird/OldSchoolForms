using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// Typed JS facade for a <see cref="Panel"/>. Exposes <c>borderStyle</c>
/// in addition to the generic container members.
/// </summary>
public class JsPanel : JsControl
{
    /// <summary>
    /// Creates the facade around a panel.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="panel">The wrapped panel.</param>
    public JsPanel(JsEngineHost host, Panel panel)
        : base(host, panel)
    {
        Panel = panel;
    }

    /// <summary>The wrapped panel.</summary>
    public Panel Panel { get; }

    /// <summary>
    /// The border style as a string (<c>None</c>, <c>FixedSingle</c>, <c>Fixed3D</c>).
    /// Invalid values are ignored.
    /// </summary>
    public string BorderStyle
    {
        get => Panel.BorderStyle.ToString();
        set
        {
            if (Enum.TryParse<Controls.Containers.BorderStyle>(value, true, out var style))
                Panel.BorderStyle = style;
        }
    }
}
