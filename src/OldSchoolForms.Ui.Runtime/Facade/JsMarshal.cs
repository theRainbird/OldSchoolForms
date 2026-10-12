using System.Globalization;
using Jint;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade;

/// <summary>
/// Value marshalling helpers between JavaScript and the CLR types used by the
/// typed facades: ISO-8601 date/time strings, hex colors, case-insensitive
/// enums and JavaScript arrays.
/// </summary>
public static class JsMarshal
{
    /// <summary>The canonical ISO-8601 format used for date/time values.</summary>
    public const string IsoFormat = "yyyy-MM-ddTHH:mm:ss";

    /// <summary>
    /// Converts a JS value (ISO string or <c>Date</c> object) to a <see cref="DateTime"/>.
    /// </summary>
    /// <param name="value">The JS value.</param>
    /// <param name="fallback">Returned when the value is null/undefined or unparsable.</param>
    /// <returns>The converted date/time or the fallback.</returns>
    public static DateTime ToDateTime(JsValue value, DateTime fallback)
        => ToNullableDateTime(value) ?? fallback;

    /// <summary>
    /// Converts a JS value (ISO string or <c>Date</c> object) to a nullable <see cref="DateTime"/>.
    /// </summary>
    /// <param name="value">The JS value.</param>
    /// <returns>The converted date/time, or <see langword="null"/> when null/undefined or unparsable.</returns>
    public static DateTime? ToNullableDateTime(JsValue value)
    {
        if (value is null || value.IsUndefined() || value.IsNull())
            return null;

        if (value.ToObject() is DateTime dt)
            return dt;

        var text = value.ToString();
        if (string.IsNullOrWhiteSpace(text))
            return null;

        if (DateTime.TryParseExact(text, IsoFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
            return exact;
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return parsed;

        return null;
    }

    /// <summary>
    /// Formats a <see cref="DateTime"/> as an ISO-8601 string for JavaScript.
    /// </summary>
    /// <param name="value">The date/time value.</param>
    /// <returns>The ISO-8601 representation.</returns>
    public static string FromDateTime(DateTime value)
        => value.ToString(IsoFormat, CultureInfo.InvariantCulture);

    /// <summary>
    /// Converts a JS value to a <see cref="Color"/>. Accepts hex strings
    /// (<c>#rgb</c>, <c>#rrggbb</c>, <c>#aarrggbb</c>, with or without <c>#</c>)
    /// and objects with numeric <c>r</c>, <c>g</c>, <c>b</c> and optional <c>a</c> members.
    /// </summary>
    /// <param name="value">The JS value.</param>
    /// <param name="fallback">Returned when the value is null/undefined or unparsable.</param>
    /// <returns>The converted color or the fallback.</returns>
    public static Color ToColor(JsValue value, Color fallback)
    {
        if (value is null || value.IsUndefined() || value.IsNull())
            return fallback;

        if (value is ObjectInstance obj)
        {
            var r = GetByteMember(obj, "r");
            var g = GetByteMember(obj, "g");
            var b = GetByteMember(obj, "b");
            var a = GetByteMember(obj, "a") ?? 255;
            if (r.HasValue && g.HasValue && b.HasValue)
                return Color.FromArgb(r.Value, g.Value, b.Value, a);
            return fallback;
        }

        var text = value.ToString()?.Trim();
        if (string.IsNullOrEmpty(text))
            return fallback;
        if (text[0] == '#')
            text = text[1..];

        try
        {
            switch (text.Length)
            {
                case 3:
                    return Color.FromArgb(
                        Convert.ToInt32(new string(text[0], 2), 16),
                        Convert.ToInt32(new string(text[1], 2), 16),
                        Convert.ToInt32(new string(text[2], 2), 16));
                case 6:
                    return Color.FromArgb(
                        Convert.ToInt32(text[..2], 16),
                        Convert.ToInt32(text[2..4], 16),
                        Convert.ToInt32(text[4..6], 16));
                case 8:
                    return Color.FromArgb(
                        Convert.ToInt32(text[2..4], 16),
                        Convert.ToInt32(text[4..6], 16),
                        Convert.ToInt32(text[6..8], 16),
                        Convert.ToInt32(text[..2], 16));
            }
        }
        catch (FormatException)
        {
            return fallback;
        }

        return fallback;
    }

    /// <summary>
    /// Formats a <see cref="Color"/> as a hex string for JavaScript
    /// (<c>#rrggbb</c>, or <c>#aarrggbb</c> when not fully opaque).
    /// </summary>
    /// <param name="color">The color value.</param>
    /// <returns>The hex representation.</returns>
    public static string FromColor(Color color)
        => color.A == 255
            ? $"#{color.R:x2}{color.G:x2}{color.B:x2}"
            : $"#{color.A:x2}{color.R:x2}{color.G:x2}{color.B:x2}";

    /// <summary>
    /// Converts a JS string to a case-insensitive enum value.
    /// </summary>
    /// <typeparam name="T">The enum type.</typeparam>
    /// <param name="value">The JS value.</param>
    /// <param name="fallback">Returned when the value is null/undefined or not a member of the enum.</param>
    /// <returns>The parsed enum value or the fallback.</returns>
    public static T ToEnum<T>(JsValue value, T fallback)
        where T : struct, Enum
    {
        if (value is null || value.IsUndefined() || value.IsNull())
            return fallback;

        var text = value.ToString();
        return !string.IsNullOrWhiteSpace(text) && Enum.TryParse<T>(text, true, out var parsed)
            ? parsed
            : fallback;
    }

    /// <summary>
    /// Converts an enum value to its string name for JavaScript.
    /// </summary>
    /// <typeparam name="T">The enum type.</typeparam>
    /// <param name="value">The enum value.</param>
    /// <returns>The enum member name.</returns>
    public static string FromEnum<T>(T value)
        where T : struct, Enum
        => value.ToString();

    /// <summary>
    /// Converts a JS value to a CLR array. A JS array becomes an array of its
    /// element CLR values; any other value becomes a single-element array;
    /// null/undefined becomes an empty array.
    /// </summary>
    /// <param name="value">The JS value.</param>
    /// <returns>The CLR array.</returns>
    public static object?[] ToArray(JsValue value)
    {
        if (value is null || value.IsUndefined() || value.IsNull())
            return Array.Empty<object?>();

        if (value is JsArray array)
        {
            var result = new object?[array.Length];
            for (var i = 0; i < array.Length; i++)
                result[i] = array.Get((uint)i).ToObject();
            return result;
        }

        return new object?[] { value.ToObject() };
    }

    /// <summary>
    /// Reads the raw member value from a JS config object.
    /// Returns <see cref="JsValue.Undefined"/> when the member is absent.
    /// </summary>
    /// <param name="obj">The JS config object.</param>
    /// <param name="name">The member name.</param>
    /// <returns>The member value.</returns>
    public static JsValue GetMember(ObjectInstance obj, string name)
        => obj.Get(name) ?? JsValue.Undefined;

    /// <summary>
    /// Reads a string member from a JS config object.
    /// </summary>
    /// <param name="obj">The JS config object.</param>
    /// <param name="name">The member name.</param>
    /// <returns>The string value, or <see langword="null"/> when absent or not a string.</returns>
    public static string? GetString(ObjectInstance obj, string name)
    {
        var value = obj.Get(name);
        return value.IsString() ? value.ToString() : null;
    }

    /// <summary>
    /// Reads a numeric member from a JS config object as <see cref="int"/>.
    /// </summary>
    /// <param name="obj">The JS config object.</param>
    /// <param name="name">The member name.</param>
    /// <returns>The integer value, or <see langword="null"/> when absent or not a finite number.</returns>
    public static int? GetInt(ObjectInstance obj, string name)
    {
        var value = obj.Get(name);
        if (value is null || !value.IsNumber())
            return null;
        var number = value.AsNumber();
        return double.IsNaN(number) || double.IsInfinity(number) ? null : (int)number;
    }

    /// <summary>
    /// Reads a numeric member from a JS config object as <see cref="double"/>.
    /// </summary>
    /// <param name="obj">The JS config object.</param>
    /// <param name="name">The member name.</param>
    /// <returns>The double value, or <see langword="null"/> when absent or not a finite number.</returns>
    public static double? GetDouble(ObjectInstance obj, string name)
    {
        var value = obj.Get(name);
        if (value is null || !value.IsNumber())
            return null;
        var number = value.AsNumber();
        return double.IsNaN(number) || double.IsInfinity(number) ? null : number;
    }

    /// <summary>
    /// Reads a boolean member from a JS config object.
    /// </summary>
    /// <param name="obj">The JS config object.</param>
    /// <param name="name">The member name.</param>
    /// <returns>The boolean value, or <see langword="null"/> when absent or not a boolean.</returns>
    public static bool? GetBool(ObjectInstance obj, string name)
    {
        var value = obj.Get(name);
        return value.IsBoolean() ? value.AsBoolean() : null;
    }

    /// <summary>
    /// Converts a JS object into a CLR record map whose values are recursively
    /// converted through <see cref="ToClr"/>. Non-object values yield an empty map.
    /// Keys are compared case-insensitively.
    /// </summary>
    /// <param name="value">The JS value.</param>
    /// <returns>The record map.</returns>
    public static Dictionary<string, object?> ToRecord(JsValue value)
    {
        var map = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (value is ObjectInstance obj and not JsArray)
        {
            foreach (var pair in obj.GetOwnProperties())
            {
                var key = pair.Key.ToString();
                if (!string.IsNullOrEmpty(key))
                    map[key] = ToClr(obj.Get(key));
            }
        }
        return map;
    }

    /// <summary>
    /// Converts a JS array of objects (or a single object) into a list of record maps.
    /// </summary>
    /// <param name="value">The JS value.</param>
    /// <returns>The list of record maps.</returns>
    public static List<Dictionary<string, object?>> ToRecordList(JsValue value)
    {
        var list = new List<Dictionary<string, object?>>();
        if (value is JsArray array)
        {
            for (var i = 0; i < array.Length; i++)
            {
                var item = array.Get((uint)i);
                if (item is ObjectInstance and not JsArray)
                    list.Add(ToRecord(item));
            }
        }
        else if (value is ObjectInstance and not JsArray)
        {
            list.Add(ToRecord(value));
        }
        return list;
    }

    /// <summary>
    /// Converts a JS value into a plain CLR value: primitives pass through,
    /// <c>Date</c> becomes <see cref="DateTime"/>, arrays become
    /// <see cref="List{T}"/> of converted elements and plain objects become
    /// case-insensitive record dictionaries.
    /// </summary>
    /// <param name="value">The JS value.</param>
    /// <returns>The converted CLR value.</returns>
    public static object? ToClr(JsValue value)
    {
        if (value is null || value.IsUndefined() || value.IsNull())
            return null;
        if (value.IsBoolean())
            return value.AsBoolean();
        if (value.IsString())
            return value.ToString();
        if (value is JsArray array)
        {
            var list = new List<object?>((int)array.Length);
            for (var i = 0; i < array.Length; i++)
                list.Add(ToClr(array.Get((uint)i)));
            return list;
        }
        if (value.ToObject() is DateTime date)
            return date;
        if (value.IsNumber())
            return value.AsNumber();
        if (value is ObjectInstance)
            return ToRecord(value);
        return value.ToObject();
    }

    /// <summary>
    /// Converts a <see cref="JsEvent"/> into a plain JS object so that callbacks
    /// can both read all members and write back <c>handled</c> and <c>value</c>
    /// (the CLR member accessor of the engine only intercepts reads).
    /// </summary>
    /// <param name="host">The JS engine.</param>
    /// <param name="jsEvent">The event to convert.</param>
    /// <returns>The JS event object.</returns>
    public static JsValue ToJsEventObject(JsEngineHost host, JsEvent jsEvent)
    {
        var obj = (ObjectInstance)host.Engine.Evaluate("({})");
        obj.Set("type", JsValue.FromObject(host.Engine, jsEvent.Type));
        obj.Set("target", jsEvent.Target == null ? JsValue.Null : JsValue.FromObject(host.Engine, jsEvent.Target));
        obj.Set("x", JsValue.FromObject(host.Engine, jsEvent.X));
        obj.Set("y", JsValue.FromObject(host.Engine, jsEvent.Y));
        obj.Set("button", JsValue.FromObject(host.Engine, jsEvent.Button));
        obj.Set("delta", JsValue.FromObject(host.Engine, jsEvent.Delta));
        obj.Set("keyCode", JsValue.FromObject(host.Engine, jsEvent.KeyCode));
        obj.Set("keyChar", JsValue.FromObject(host.Engine, jsEvent.KeyChar));
        obj.Set("shift", JsValue.FromObject(host.Engine, jsEvent.Shift));
        obj.Set("control", JsValue.FromObject(host.Engine, jsEvent.Control));
        obj.Set("alt", JsValue.FromObject(host.Engine, jsEvent.Alt));
        obj.Set("handled", JsValue.FromObject(host.Engine, jsEvent.Handled));
        obj.Set("row", JsValue.FromObject(host.Engine, jsEvent.Row));
        obj.Set("column", JsValue.FromObject(host.Engine, jsEvent.Column));
        obj.Set("value", JsValue.FromObject(host.Engine, jsEvent.Value));
        obj.Set("payload", JsValue.FromObject(host.Engine, jsEvent.Payload));
        return obj;
    }

    /// <summary>
    /// Reads the callback-writable members (<c>handled</c>, <c>value</c>) back
    /// from the JS event object into the CLR event.
    /// </summary>
    /// <param name="jsEvent">The CLR event to update.</param>
    /// <param name="eventObj">The JS event object passed to the callback.</param>
    public static void ReadBackJsEvent(JsEvent jsEvent, JsValue eventObj)
    {
        if (eventObj is not ObjectInstance obj)
            return;
        var handled = obj.Get("handled");
        if (handled.IsBoolean())
            jsEvent.Handled = handled.AsBoolean();
        var value = obj.Get("value");
        if (!value.IsUndefined())
            jsEvent.Value = value.ToObject();
    }

    /// <summary>
    /// Converts a JS value into a data source object for data-bound controls.
    /// A <c>JsBindingSource</c> passes through as its underlying binding source,
    /// a <c>JsDataTable</c> as its default view, a JS array of objects as a list
    /// of record maps; any other value is unwrapped via <c>ToObject</c>.
    /// </summary>
    /// <param name="value">The JS value.</param>
    /// <returns>The CLR data source, or <see langword="null"/>.</returns>
    public static object? ToDataSource(JsValue value)
    {
        if (value is null || value.IsUndefined() || value.IsNull())
            return null;
        if (value.ToObject() is JsBindingSource bindingSource)
            return bindingSource.Source;
        if (value.ToObject() is OldSchoolForms.Shared.Scripting.JsDataTable dataTable)
            return dataTable.Table.DefaultView;
        if (value is JsArray)
            return ToRecordList(value);
        return value.ToObject();
    }

    private static byte? GetByteMember(ObjectInstance obj, string name)
    {
        var member = obj.Get(name);
        if (member is null || member.IsUndefined() || member.IsNull())
            return null;
        try
        {
            var number = member.AsNumber();
            if (double.IsNaN(number) || number < 0 || number > 255)
                return null;
            return (byte)number;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
