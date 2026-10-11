using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// Typed JS facade for a <see cref="MenuStrip"/>. Exposes the menu items and
/// allows creating top-level menu entries programmatically.
/// </summary>
public class JsMenuStrip : JsControl
{
    /// <summary>
    /// Creates the facade around a menu strip.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="menuStrip">The wrapped menu strip.</param>
    public JsMenuStrip(JsEngineHost host, MenuStrip menuStrip)
        : base(host, menuStrip)
    {
        MenuStrip = menuStrip;
    }

    /// <summary>The wrapped menu strip.</summary>
    public MenuStrip MenuStrip { get; }

    /// <summary>The top-level menu item facades, in order.</summary>
    public JsToolStripMenuItem[] Items
    {
        get
        {
            var list = new List<JsToolStripMenuItem>();
            foreach (var item in MenuStrip.Items)
                list.Add((JsToolStripMenuItem)JsFacadeRegistry.WrapStripItem(Host, item));
            return list.ToArray();
        }
    }

    /// <summary>
    /// Creates a top-level menu item with the given caption (mnemonics via <c>&amp;</c>).
    /// </summary>
    /// <param name="text">The menu caption.</param>
    /// <returns>The facade of the new menu item.</returns>
    public JsToolStripMenuItem AddItem(string text)
    {
        var item = new ToolStripMenuItem(text ?? string.Empty);
        MenuStrip.Items.Add(item);
        return (JsToolStripMenuItem)JsFacadeRegistry.WrapStripItem(Host, item);
    }

    /// <summary>Whether the strip operates in keyboard menu mode.</summary>
    public bool MenuMode
    {
        get => MenuStrip.MenuMode;
        set => MenuStrip.MenuMode = value;
    }
}
