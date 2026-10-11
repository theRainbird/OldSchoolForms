using Jint.Native;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// Typed JS facade for a <see cref="ComboBox"/>. Exposes the item list,
/// selection (<c>selectedIndex</c> / <c>selectedItem</c> / <c>selectedValue</c>),
/// display/value members, drop-down style and the <c>selectedindexchanged</c>
/// and <c>textchanged</c> events.
/// </summary>
public class JsComboBox : JsControl
{
    /// <summary>
    /// Creates the facade around a combo box.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="comboBox">The wrapped combo box.</param>
    public JsComboBox(JsEngineHost host, ComboBox comboBox)
        : base(host, comboBox)
    {
        ComboBox = comboBox;
    }

    /// <summary>The wrapped combo box.</summary>
    public ComboBox ComboBox { get; }

    /// <summary>
    /// The items as a JS array. Assigning replaces the whole list.
    /// </summary>
    public JsValue Items
    {
        get
        {
            var values = new JsValue[ComboBox.Items.Count];
            for (var i = 0; i < values.Length; i++)
                values[i] = JsValue.FromObject(Host.Engine, ComboBox.Items[i]);
            return JsValue.FromObject(Host.Engine, values);
        }
        set
        {
            ComboBox.Items.Clear();
            foreach (var item in JsMarshal.ToArray(value))
                ComboBox.Items.Add(item ?? string.Empty);
        }
    }

    /// <summary>Appends a single item to the list.</summary>
    /// <param name="value">The item to add.</param>
    public void AddItem(JsValue value) => ComboBox.Items.Add(value.ToObject() ?? string.Empty);

    /// <summary>Removes the item at the given index.</summary>
    /// <param name="index">The index of the item to remove.</param>
    public void RemoveItemAt(int index) => ComboBox.Items.RemoveAt(index);

    /// <summary>Removes all items from the list.</summary>
    public void ClearItems() => ComboBox.Items.Clear();

    /// <summary>The index of the selected item, or <c>-1</c> when none is selected.</summary>
    public int SelectedIndex
    {
        get => ComboBox.SelectedIndex;
        set => ComboBox.SelectedIndex = value;
    }

    /// <summary>The selected item, or <c>null</c> when none is selected.</summary>
    public object? SelectedItem => ComboBox.SelectedItem;

    /// <summary>
    /// The value of the selected item resolved through <c>valueMember</c>,
    /// or <c>null</c> when unavailable.
    /// </summary>
    public object? SelectedValue => ComboBox.SelectedValue;

    /// <summary>The display member used to render items.</summary>
    public string DisplayMember
    {
        get => ComboBox.DisplayMember;
        set => ComboBox.DisplayMember = value ?? string.Empty;
    }

    /// <summary>The value member used for <c>selectedValue</c>.</summary>
    public string ValueMember
    {
        get => ComboBox.ValueMember;
        set => ComboBox.ValueMember = value ?? string.Empty;
    }

    /// <summary>The drop-down style (Simple, DropDown, DropDownList).</summary>
    public string DropDownStyle
    {
        get => ComboBox.DropDownStyle.ToString();
        set
        {
            if (Enum.TryParse<Controls.Basic.DropDownStyle>(value, true, out var style))
                ComboBox.DropDownStyle = style;
        }
    }

    /// <summary>Maximum height of the drop-down list in pixels.</summary>
    public int DropDownHeight
    {
        get => ComboBox.DropDownHeight;
        set => ComboBox.DropDownHeight = value;
    }

    /// <inheritdoc />
    protected override Delegate? WireCustom(string normalized, JsValue callback)
    {
        switch (normalized)
        {
            case "selectedindexchanged":
            {
                EventHandler handler = (s, e) => Raise("selectedindexchanged", callback, e);
                ComboBox.SelectedIndexChanged += handler;
                return handler;
            }
            case "textchanged":
            {
                EventHandler handler = (s, e) => Raise("textchanged", callback, e);
                ComboBox.TextChanged += handler;
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
            case "selectedindexchanged":
                ComboBox.SelectedIndexChanged -= (EventHandler)handler;
                return true;
            case "textchanged":
                ComboBox.TextChanged -= (EventHandler)handler;
                return true;
            default:
                return false;
        }
    }
}
