using Jint.Native;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// Typed JS facade for a <see cref="TextBox"/>. Exposes selection, clipboard
/// operations and the control's own <c>textchanged</c> event.
/// </summary>
public class JsTextBox : JsControl
{
    /// <summary>
    /// Creates the facade around a text box.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="textBox">The wrapped text box.</param>
    public JsTextBox(JsEngineHost host, TextBox textBox)
        : base(host, textBox)
    {
        TextBox = textBox;
    }

    /// <summary>The wrapped text box.</summary>
    public TextBox TextBox { get; }

    /// <summary>Start index of the current selection.</summary>
    public int SelectionStart
    {
        get => TextBox.SelectionStart;
        set => TextBox.SelectionStart = value;
    }

    /// <summary>Length of the current selection.</summary>
    public int SelectionLength
    {
        get => TextBox.SelectionLength;
        set => TextBox.SelectionLength = value;
    }

    /// <summary>The currently selected text (read-only).</summary>
    public string SelectedText => TextBox.SelectedText;

    /// <summary>Whether the input is masked with the system password character.</summary>
    public bool UseSystemPasswordChar
    {
        get => TextBox.UseSystemPasswordChar;
        set => TextBox.UseSystemPasswordChar = value;
    }

    /// <summary>Copies the current selection to the clipboard.</summary>
    public void CopyToClipboard() => TextBox.CopyToClipboard();

    /// <summary>Cuts the current selection to the clipboard.</summary>
    public void Cut() => TextBox.Cut();

    /// <summary>Pastes the clipboard content at the caret.</summary>
    public void Paste() => TextBox.Paste();

    /// <summary>Selects all text in the box.</summary>
    public void SelectAll() => TextBox.SelectAll();

    /// <inheritdoc />
    protected override Delegate? WireCustom(string normalized, JsValue callback)
    {
        if (normalized != "textchanged")
            return null;

        EventHandler handler = (s, e) => Raise("textchanged", callback, e);
        TextBox.TextChanged += handler;
        return handler;
    }

    /// <inheritdoc />
    protected override bool UnwireCustom(string normalized, Delegate handler)
    {
        if (normalized != "textchanged")
            return false;

        TextBox.TextChanged -= (EventHandler)handler;
        return true;
    }
}
