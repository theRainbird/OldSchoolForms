using Jint.Native;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// Typed JS facade for a <see cref="RadioButton"/>. Exposes <c>checked</c> and
/// the <c>checkedchanged</c> event in addition to the generic control members.
/// </summary>
public class JsRadioButton : JsControl
{
    /// <summary>
    /// Creates the facade around a radio button.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="radioButton">The wrapped radio button.</param>
    public JsRadioButton(JsEngineHost host, RadioButton radioButton)
        : base(host, radioButton)
    {
        RadioButton = radioButton;
    }

    /// <summary>The wrapped radio button.</summary>
    public RadioButton RadioButton { get; }

    /// <summary>The checked state of the radio button.</summary>
    public bool Checked
    {
        get => RadioButton.Checked;
        set => RadioButton.Checked = value;
    }

    /// <inheritdoc />
    protected override Delegate? WireCustom(string normalized, JsValue callback)
    {
        if (normalized != "checkedchanged")
            return null;

        EventHandler handler = (s, e) => Raise("checkedchanged", callback, e);
        RadioButton.CheckedChanged += handler;
        return handler;
    }

    /// <inheritdoc />
    protected override bool UnwireCustom(string normalized, Delegate handler)
    {
        if (normalized != "checkedchanged")
            return false;

        RadioButton.CheckedChanged -= (EventHandler)handler;
        return true;
    }
}
