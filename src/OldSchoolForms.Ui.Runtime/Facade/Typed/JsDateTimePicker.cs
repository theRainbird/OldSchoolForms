using Jint.Native;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// Typed JS facade for a <see cref="DateTimePicker"/>. Date values are
/// marshalled as ISO-8601 strings. Exposes range, format, check box state,
/// drop-down control and the <c>valuechanged</c>, <c>checkedchanged</c>,
/// <c>dropdown</c> and <c>closeup</c> events.
/// </summary>
public class JsDateTimePicker : JsControl
{
    /// <summary>
    /// Creates the facade around a date/time picker.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="picker">The wrapped date/time picker.</param>
    public JsDateTimePicker(JsEngineHost host, DateTimePicker picker)
        : base(host, picker)
    {
        Picker = picker;
    }

    /// <summary>The wrapped date/time picker.</summary>
    public DateTimePicker Picker { get; }

    /// <summary>The selected date/time as an ISO-8601 string.</summary>
    public JsValue Value
    {
        get => JsValue.FromObject(Host.Engine, JsMarshal.FromDateTime(Picker.Value));
        set => Picker.Value = JsMarshal.ToDateTime(value, Picker.Value);
    }

    /// <summary>The minimum selectable date as an ISO-8601 string.</summary>
    public JsValue MinDate
    {
        get => JsValue.FromObject(Host.Engine, JsMarshal.FromDateTime(Picker.MinDate));
        set => Picker.MinDate = JsMarshal.ToDateTime(value, Picker.MinDate);
    }

    /// <summary>The maximum selectable date as an ISO-8601 string.</summary>
    public JsValue MaxDate
    {
        get => JsValue.FromObject(Host.Engine, JsMarshal.FromDateTime(Picker.MaxDate));
        set => Picker.MaxDate = JsMarshal.ToDateTime(value, Picker.MaxDate);
    }

    /// <summary>Display format (Long, Short, Time, Custom).</summary>
    public string Format
    {
        get => Picker.Format.ToString();
        set
        {
            if (Enum.TryParse<DateTimePickerFormat>(value, true, out var format))
                Picker.Format = format;
        }
    }

    /// <summary>The custom format string used when <c>format</c> is <c>Custom</c>.</summary>
    public string? CustomFormat
    {
        get => Picker.CustomFormat;
        set => Picker.CustomFormat = value;
    }

    /// <summary>Whether the up/down spin buttons are shown.</summary>
    public bool ShowUpDown
    {
        get => Picker.ShowUpDown;
        set => Picker.ShowUpDown = value;
    }

    /// <summary>Whether the enable check box is shown.</summary>
    public bool ShowCheckBox
    {
        get => Picker.ShowCheckBox;
        set => Picker.ShowCheckBox = value;
    }

    /// <summary>Whether the picker is checked (requires <c>showCheckBox</c>).</summary>
    public bool Checked
    {
        get => Picker.Checked;
        set => Picker.Checked = value;
    }

    /// <summary>Opens the calendar drop-down.</summary>
    public void OpenDropDown() => Picker.OpenDropDown();

    /// <summary>Closes the calendar drop-down.</summary>
    public void CloseDropDown() => Picker.CloseDropDown();

    /// <inheritdoc />
    protected override Delegate? WireCustom(string normalized, JsValue callback)
    {
        switch (normalized)
        {
            case "valuechanged":
            {
                EventHandler handler = (s, e) => Raise("valuechanged", callback, e);
                Picker.ValueChanged += handler;
                return handler;
            }
            case "checkedchanged":
            {
                EventHandler handler = (s, e) => Raise("checkedchanged", callback, e);
                Picker.CheckedChanged += handler;
                return handler;
            }
            case "dropdown":
            {
                EventHandler handler = (s, e) => Raise("dropdown", callback, e);
                Picker.DropDown += handler;
                return handler;
            }
            case "closeup":
            {
                EventHandler handler = (s, e) => Raise("closeup", callback, e);
                Picker.CloseUp += handler;
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
            case "valuechanged":
                Picker.ValueChanged -= (EventHandler)handler;
                return true;
            case "checkedchanged":
                Picker.CheckedChanged -= (EventHandler)handler;
                return true;
            case "dropdown":
                Picker.DropDown -= (EventHandler)handler;
                return true;
            case "closeup":
                Picker.CloseUp -= (EventHandler)handler;
                return true;
            default:
                return false;
        }
    }
}
