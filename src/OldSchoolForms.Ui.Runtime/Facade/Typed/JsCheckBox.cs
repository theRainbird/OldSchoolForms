using Jint.Native;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// Typed JS facade for a <see cref="CheckBox"/>. Exposes <c>checked</c> and the
/// <c>checkedchanged</c> event in addition to the generic control members.
/// </summary>
public class JsCheckBox : JsControl
{
    /// <summary>
    /// Creates the facade around a check box.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="checkBox">The wrapped check box.</param>
    public JsCheckBox(JsEngineHost host, CheckBox checkBox)
        : base(host, checkBox)
    {
        CheckBox = checkBox;
    }

    /// <summary>The wrapped check box.</summary>
    public CheckBox CheckBox { get; }

    /// <summary>The checked state of the check box.</summary>
    public bool Checked
    {
        get => CheckBox.Checked;
        set => CheckBox.Checked = value;
    }

    /// <inheritdoc />
    protected override Delegate? WireCustom(string normalized, JsValue callback)
    {
        if (normalized != "checkedchanged")
            return null;

        EventHandler handler = (s, e) => Raise("checkedchanged", callback, e);
        CheckBox.CheckedChanged += handler;
        return handler;
    }

    /// <inheritdoc />
    protected override bool UnwireCustom(string normalized, Delegate handler)
    {
        if (normalized != "checkedchanged")
            return false;

        CheckBox.CheckedChanged -= (EventHandler)handler;
        return true;
    }
}
