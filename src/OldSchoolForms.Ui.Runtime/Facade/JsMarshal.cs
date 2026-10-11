using System.Globalization;
using Jint;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using OldSchoolForms.Ui.Core;

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
