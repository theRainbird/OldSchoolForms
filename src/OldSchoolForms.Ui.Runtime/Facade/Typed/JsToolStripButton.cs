using Jint.Native;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// Typed JS facade for a <see cref="ToolStripButton"/>. Exposes the checked
/// state, the dropdown items and the <c>checkedchanged</c>,
/// <c>dropdownopened</c> / <c>dropdownclosed</c> events.
/// </summary>
public class JsToolStripButton : JsToolStripItem
{
    /// <summary>
    /// Creates the facade around a tool strip button.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="button">The wrapped tool strip button.</param>
    public JsToolStripButton(JsEngineHost host, ToolStripButton button)
        : base(host, button)
    {
        Button = button;
    }

    /// <summary>The wrapped tool strip button.</summary>
    public ToolStripButton Button { get; }

    /// <summary>The checked state of the button.</summary>
    public bool Checked
    {
        get => Button.Checked;
        set => Button.Checked = value;
    }

    /// <summary>Whether clicking the button toggles its checked state.</summary>
    public bool CheckOnClick
    {
        get => Button.CheckOnClick;
        set => Button.CheckOnClick = value;
    }

    /// <summary>The dropdown item facades, in order.</summary>
    public JsToolStripItem[] DropDownItems
    {
        get
        {
            var list = new List<JsToolStripItem>();
            foreach (var item in Button.DropDownItems)
                list.Add(JsFacadeRegistry.WrapStripItem(Host, item));
            return list.ToArray();
        }
    }

    /// <summary>
    /// Creates a menu item with the given caption and adds it to the dropdown.
    /// </summary>
    /// <param name="text">The menu caption (mnemonics via <c>&amp;</c>).</param>
    /// <returns>The facade of the new dropdown item.</returns>
    public JsToolStripItem AddItem(string text)
    {
        var item = new ToolStripMenuItem(text ?? string.Empty);
        Button.DropDownItems.Add(item);
        return JsFacadeRegistry.WrapStripItem(Host, item);
    }

    /// <inheritdoc />
    protected override Delegate? WireCustom(string normalized, JsValue callback)
    {
        switch (normalized)
        {
            case "checkedchanged":
            {
                EventHandler handler = (s, e) => Raise("checkedchanged", callback, e);
                Button.CheckedChanged += handler;
                return handler;
            }
            case "dropdownopened":
            {
                EventHandler handler = (s, e) => Raise("dropdownopened", callback, e);
                Button.DropDownOpened += handler;
                return handler;
            }
            case "dropdownclosed":
            {
                EventHandler handler = (s, e) => Raise("dropdownclosed", callback, e);
                Button.DropDownClosed += handler;
                return handler;
            }
            default:
                return null;
        }
    }

    /// <inheritdoc />
    protected override bool UnwireCustom(string normalized, Delegate handler)
    {
        switch (normalized)
        {
            case "checkedchanged":
                Button.CheckedChanged -= (EventHandler)handler;
                return true;
            case "dropdownopened":
                Button.DropDownOpened -= (EventHandler)handler;
                return true;
            case "dropdownclosed":
                Button.DropDownClosed -= (EventHandler)handler;
                return true;
            default:
                return false;
        }
    }
}
