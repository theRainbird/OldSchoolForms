using Jint.Native;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// Typed JS facade for a <see cref="MemoBox"/>. Exposes word wrap, selection,
/// clipboard operations and the control's own <c>textchanged</c> event.
/// </summary>
public class JsMemoBox : JsControl
{
    /// <summary>
    /// Creates the facade around a memo box.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="memoBox">The wrapped memo box.</param>
    public JsMemoBox(JsEngineHost host, MemoBox memoBox)
        : base(host, memoBox)
    {
        MemoBox = memoBox;
    }

    /// <summary>The wrapped memo box.</summary>
    public MemoBox MemoBox { get; }

    /// <summary>Whether long lines wrap inside the box.</summary>
    public bool WordWrap
    {
        get => MemoBox.WordWrap;
        set => MemoBox.WordWrap = value;
    }

    /// <summary>Start index of the current selection.</summary>
    public int SelectionStart
    {
        get => MemoBox.SelectionStart;
        set => MemoBox.SelectionStart = value;
    }

    /// <summary>Length of the current selection.</summary>
    public int SelectionLength
    {
        get => MemoBox.SelectionLength;
        set => MemoBox.SelectionLength = value;
    }

    /// <summary>Copies the current selection to the clipboard.</summary>
    public void CopyToClipboard() => MemoBox.CopyToClipboard();

    /// <summary>Cuts the current selection to the clipboard.</summary>
    public void Cut() => MemoBox.Cut();

    /// <summary>Pastes the clipboard content at the caret.</summary>
    public void Paste() => MemoBox.Paste();

    /// <summary>Selects all text in the box.</summary>
    public void SelectAll() => MemoBox.SelectAll();

    /// <inheritdoc />
    protected override Delegate? WireCustom(string normalized, JsValue callback)
    {
        if (normalized != "textchanged")
            return null;

        EventHandler handler = (s, e) => Raise("textchanged", callback, e);
        MemoBox.TextChanged += handler;
        return handler;
    }

    /// <inheritdoc />
    protected override bool UnwireCustom(string normalized, Delegate handler)
    {
        if (normalized != "textchanged")
            return false;

        MemoBox.TextChanged -= (EventHandler)handler;
        return true;
    }
}
