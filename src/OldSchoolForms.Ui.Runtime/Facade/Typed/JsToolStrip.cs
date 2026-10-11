using Jint.Native;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// Typed JS facade for a <see cref="ToolStrip"/>. Exposes the item collection,
/// allows creating buttons programmatically and raises <c>itemadded</c> /
/// <c>itemremoved</c> events whose <c>payload</c> is the affected item facade.
/// </summary>
public class JsToolStrip : JsControl
{
    /// <summary>
    /// Creates the facade around a tool strip.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="toolStrip">The wrapped tool strip.</param>
    public JsToolStrip(JsEngineHost host, ToolStrip toolStrip)
        : base(host, toolStrip)
    {
        ToolStrip = toolStrip;
    }

    /// <summary>The wrapped tool strip.</summary>
    public ToolStrip ToolStrip { get; }

    /// <summary>The item facades, in order.</summary>
    public JsToolStripItem[] Items
    {
        get
        {
            var list = new List<JsToolStripItem>();
            foreach (var item in ToolStrip.Items)
                list.Add(JsFacadeRegistry.WrapStripItem(Host, item));
            return list.ToArray();
        }
    }

    /// <summary>
    /// Creates a tool strip button with the given caption and appends it.
    /// </summary>
    /// <param name="text">The button caption.</param>
    /// <returns>The facade of the new button.</returns>
    public JsToolStripItem AddItem(string text)
    {
        var item = new ToolStripButton(text ?? string.Empty);
        ToolStrip.Items.Add(item);
        return JsFacadeRegistry.WrapStripItem(Host, item);
    }

    /// <summary>
    /// Creates a tool strip button with the given caption and inserts it at the index.
    /// </summary>
    /// <param name="index">The zero-based insertion index.</param>
    /// <param name="text">The button caption.</param>
    /// <returns>The facade of the new button.</returns>
    public JsToolStripItem InsertItem(int index, string text)
    {
        var item = new ToolStripButton(text ?? string.Empty);
        ToolStrip.Items.Insert(index, item);
        return JsFacadeRegistry.WrapStripItem(Host, item);
    }

    /// <summary>
    /// Removes the item at the given index.
    /// </summary>
    /// <param name="index">The zero-based index of the item to remove.</param>
    /// <returns><see langword="true"/> when an item was removed.</returns>
    public bool RemoveItemAt(int index)
    {
        if (index < 0 || index >= ToolStrip.Items.Count)
            return false;
        ToolStrip.Items.RemoveAt(index);
        return true;
    }

    /// <summary>
    /// The grip style as a string (<c>Visible</c> or <c>Hidden</c>).
    /// Invalid values are ignored.
    /// </summary>
    public string GripStyle
    {
        get => ToolStrip.GripStyle.ToString();
        set
        {
            if (Enum.TryParse<ToolStripGripStyle>(value, true, out var style))
                ToolStrip.GripStyle = style;
        }
    }

    /// <inheritdoc />
    protected override Delegate? WireCustom(string normalized, JsValue callback)
    {
        switch (normalized)
        {
            case "itemadded":
            {
                EventHandler<ToolStripItemEventArgs> handler = (s, e) =>
                {
                    var jsEvent = BuildEvent("itemadded", EventArgs.Empty);
                    jsEvent.Payload = e.Item == null ? null : JsFacadeRegistry.WrapStripItem(Host, e.Item);
                    RaiseEvent("itemadded", callback, jsEvent);
                };
                ToolStrip.ItemAdded += handler;
                return handler;
            }
            case "itemremoved":
            {
                EventHandler<ToolStripItemEventArgs> handler = (s, e) =>
                {
                    var jsEvent = BuildEvent("itemremoved", EventArgs.Empty);
                    jsEvent.Payload = e.Item == null ? null : JsFacadeRegistry.WrapStripItem(Host, e.Item);
                    RaiseEvent("itemremoved", callback, jsEvent);
                };
                ToolStrip.ItemRemoved += handler;
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
            case "itemadded":
                ToolStrip.ItemAdded -= (EventHandler<ToolStripItemEventArgs>)handler;
                return true;
            case "itemremoved":
                ToolStrip.ItemRemoved -= (EventHandler<ToolStripItemEventArgs>)handler;
                return true;
            default:
                return false;
        }
    }
}
