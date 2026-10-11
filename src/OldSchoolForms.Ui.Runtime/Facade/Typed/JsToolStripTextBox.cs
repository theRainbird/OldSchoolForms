using Jint.Native;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// Typed JS facade for a <see cref="ToolStripTextBox"/>. Exposes selection,
/// clipboard operations and the <c>textchanged</c> event.
/// </summary>
public class JsToolStripTextBox : JsToolStripItem
{
    /// <summary>
    /// Creates the facade around a tool strip text box.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="textBox">The wrapped tool strip text box.</param>
    public JsToolStripTextBox(JsEngineHost host, ToolStripTextBox textBox)
        : base(host, textBox)
    {
        TextBox = textBox;
    }

    /// <summary>The wrapped tool strip text box.</summary>
    public ToolStripTextBox TextBox { get; }

    /// <summary>
    /// The text of the text box. Shadows the base facade member because
    /// <see cref="ToolStripTextBox.Text"/> hides (not overrides) the base item text.
    /// </summary>
    public new string Text
    {
        get => TextBox.Text;
        set => TextBox.Text = value ?? string.Empty;
    }

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

    /// <summary>Whether the text is masked with the system password character.</summary>
    public bool UseSystemPasswordChar
    {
        get => TextBox.UseSystemPasswordChar;
        set => TextBox.UseSystemPasswordChar = value;
    }

    /// <summary>Whether the text box currently has focus (read-only).</summary>
    public bool Focused => TextBox.Focused;

    /// <summary>Width of the editable text area in pixels.</summary>
    public int TextBoxWidth
    {
        get => TextBox.TextBoxWidth;
        set => TextBox.TextBoxWidth = value;
    }

    /// <summary>Copies the current selection to the clipboard.</summary>
    public void CopyToClipboard() => TextBox.CopyToClipboard();

    /// <summary>Cuts the current selection to the clipboard.</summary>
    public void Cut() => TextBox.Cut();

    /// <summary>Pastes the clipboard content at the caret.</summary>
    public void Paste() => TextBox.Paste();

    /// <summary>Selects the entire text.</summary>
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
