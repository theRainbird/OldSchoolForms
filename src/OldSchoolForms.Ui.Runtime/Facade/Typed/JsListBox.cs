using Jint.Native;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// Typed JS facade for a <see cref="ListBox"/>. Exposes the item list,
/// selection (<c>selectedIndex</c> / <c>selectedItem</c> / <c>selectedValue</c>),
/// display/value members and the <c>selectedindexchanged</c> event.
/// </summary>
public class JsListBox : JsControl
{
    /// <summary>
    /// Creates the facade around a list box.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="listBox">The wrapped list box.</param>
    public JsListBox(JsEngineHost host, ListBox listBox)
        : base(host, listBox)
    {
        ListBox = listBox;
    }

    /// <summary>The wrapped list box.</summary>
    public ListBox ListBox { get; }

    /// <summary>
    /// The items as a JS array. Assigning replaces the whole list.
    /// </summary>
    public JsValue Items
    {
        get
        {
            var values = new JsValue[ListBox.Items.Count];
            for (var i = 0; i < values.Length; i++)
                values[i] = JsValue.FromObject(Host.Engine, ListBox.Items[i]);
            return JsValue.FromObject(Host.Engine, values);
        }
        set
        {
            ListBox.Items.Clear();
            foreach (var item in JsMarshal.ToArray(value))
                ListBox.Items.Add(item ?? string.Empty);
        }
    }

    /// <summary>Appends a single item to the list.</summary>
    /// <param name="value">The item to add.</param>
    public void AddItem(JsValue value) => ListBox.Items.Add(value.ToObject() ?? string.Empty);

    /// <summary>Removes the item at the given index.</summary>
    /// <param name="index">The index of the item to remove.</param>
    public void RemoveItemAt(int index) => ListBox.Items.RemoveAt(index);

    /// <summary>Removes all items from the list.</summary>
    public void ClearItems() => ListBox.Items.Clear();

    /// <summary>The index of the selected item, or <c>-1</c> when none is selected.</summary>
    public int SelectedIndex
    {
        get => ListBox.SelectedIndex;
        set => ListBox.SelectedIndex = value;
    }

    /// <summary>The selected item, or <c>null</c> when none is selected.</summary>
    public object? SelectedItem => ListBox.SelectedItem;

    /// <summary>
    /// The value of the selected item resolved through <c>valueMember</c>,
    /// or <c>null</c> when unavailable.
    /// </summary>
    public object? SelectedValue => ListBox.SelectedValue;

    /// <summary>The display member used to render items.</summary>
    public string DisplayMember
    {
        get => ListBox.DisplayMember;
        set => ListBox.DisplayMember = value ?? string.Empty;
    }

    /// <summary>The value member used for <c>selectedValue</c>.</summary>
    public string ValueMember
    {
        get => ListBox.ValueMember;
        set => ListBox.ValueMember = value ?? string.Empty;
    }

    /// <inheritdoc />
    protected override Delegate? WireCustom(string normalized, JsValue callback)
    {
        if (normalized != "selectedindexchanged")
            return null;

        EventHandler handler = (s, e) => Raise("selectedindexchanged", callback, e);
        ListBox.SelectedIndexChanged += handler;
        return handler;
    }

    /// <inheritdoc />
    protected override bool UnwireCustom(string normalized, Delegate handler)
    {
        if (normalized != "selectedindexchanged")
            return false;

        ListBox.SelectedIndexChanged -= (EventHandler)handler;
        return true;
    }
}
