using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// Typed JS facade for a <see cref="ToolStripMenuItem"/>. Exposes the
/// sub-items and allows building nested menus programmatically.
/// </summary>
public class JsToolStripMenuItem : JsToolStripItem
{
    /// <summary>
    /// Creates the facade around a menu item.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="menuItem">The wrapped menu item.</param>
    public JsToolStripMenuItem(JsEngineHost host, ToolStripMenuItem menuItem)
        : base(host, menuItem)
    {
        MenuItem = menuItem;
    }

    /// <summary>The wrapped menu item.</summary>
    public ToolStripMenuItem MenuItem { get; }

    /// <summary>The sub-item facades, in order.</summary>
    public JsToolStripMenuItem[] DropDownItems
    {
        get
        {
            var list = new List<JsToolStripMenuItem>();
            foreach (var item in MenuItem.DropDownItems)
                list.Add((JsToolStripMenuItem)JsFacadeRegistry.WrapStripItem(Host, item));
            return list.ToArray();
        }
    }

    /// <summary>
    /// Creates a sub menu item with the given caption (mnemonics via <c>&amp;</c>).
    /// </summary>
    /// <param name="text">The menu caption.</param>
    /// <returns>The facade of the new sub menu item.</returns>
    public JsToolStripMenuItem AddItem(string text)
    {
        var item = new ToolStripMenuItem(text ?? string.Empty);
        MenuItem.DropDownItems.Add(item);
        return (JsToolStripMenuItem)JsFacadeRegistry.WrapStripItem(Host, item);
    }

    /// <summary>Whether the item is currently highlighted in the menu.</summary>
    public bool IsSelected => MenuItem.IsSelected;

    /// <summary>Whether the item's dropdown is currently open.</summary>
    public bool IsDropDownVisible => MenuItem.IsDropDownVisible;
}
