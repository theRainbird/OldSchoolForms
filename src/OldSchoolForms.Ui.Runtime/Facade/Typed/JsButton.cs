using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// Typed JS facade for a <see cref="Button"/>. Adds <c>performClick()</c> on top
/// of the generic control members; click events are handled by the base class.
/// </summary>
public class JsButton : JsControl
{
    /// <summary>
    /// Creates the facade around a button.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="button">The wrapped button.</param>
    public JsButton(JsEngineHost host, Button button)
        : base(host, button)
    {
        Button = button;
    }

    /// <summary>The wrapped button.</summary>
    public Button Button { get; }

    /// <summary>
    /// Raises the click event programmatically, as if the user clicked the button.
    /// </summary>
    public void PerformClick() => Button.PerformClick();
}
