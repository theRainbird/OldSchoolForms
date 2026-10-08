using System.Collections;
using System.Data;
using Jint;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;

namespace OldSchoolForms.Shared.Scripting;

/// <summary>
/// Marshals values between CLR and JavaScript. CLR → JS wraps
/// <see cref="DataTable"/> / <see cref="DataSet"/> / <see cref="DataRow"/>
/// into their facades; JS → CLR unwraps facades back into real ADO.NET
/// objects so they can go on the wire, and converts plain JS objects into
/// <see cref="Dictionary{TKey,TValue}"/>.
/// </summary>
public static class JsMarshal
{
    /// <summary>
    /// Converts a CLR value into a JS value, wrapping data objects into facades.
    /// </summary>
    /// <param name="engine">The target JS engine.</param>
    /// <param name="value">The CLR value.</param>
    /// <returns>The JS representation.</returns>
    public static JsValue ToJs(Engine engine, object? value)
    {
        ArgumentNullException.ThrowIfNull(engine);

        switch (value)
        {
            case null:
                return JsValue.Null;
            case JsValue js:
                return js;
            case DataTable table:
                return JsValue.FromObject(engine, new JsDataTable(engine, table));
            case DataSet set:
                return JsValue.FromObject(engine, new JsDataSet(engine, set));
            case DataRow row:
                return JsValue.FromObject(engine, new JsDataRow(engine, row));
            case string:
            case bool:
            case int:
            case long:
            case double:
            case DateTime:
            case Guid:
                return JsValue.FromObject(engine, value);
            case IDictionary dict:
            {
                var obj = engine.Intrinsics.Object.Construct(new JsValue[0]);
                foreach (DictionaryEntry entry in dict)
                    obj.Set(entry.Key.ToString() ?? string.Empty, ToJs(engine, entry.Value));
                return obj;
            }
            case IEnumerable enumerable:
            {
                var arr = (ArrayInstance)engine.Intrinsics.Array.Construct(new JsValue[0]);
                foreach (var item in enumerable)
                    arr.Push(ToJs(engine, item));
                return arr;
            }
            default:
                return JsValue.FromObject(engine, value);
        }
    }

    /// <summary>
    /// Converts a JS value into a CLR value. Facades are unwrapped into real
    /// <see cref="DataTable"/> / <see cref="DataSet"/> objects (for wire
    /// transfer); plain JS objects become <see cref="Dictionary{TKey,TValue}"/>.
    /// </summary>
    /// <param name="value">The JS value.</param>
    /// <returns>The CLR representation.</returns>
    public static object? ToClr(JsValue value)
    {
        if (value is null || value.IsUndefined() || value.IsNull())
            return null;

        var raw = value.ToObject();
        switch (raw)
        {
            case JsDataTable facade:
                return facade.Table;
            case JsDataSet facade:
                return facade.Set;
            case JsDataRow facade:
                return ToSingleRowTable(facade.Row);
        }

        switch (value)
        {
            case ArrayInstance arr:
            {
                var items = arr.ToArray();
                var result = new object?[items.Length];
                for (var i = 0; i < items.Length; i++)
                    result[i] = ToClr(items[i]);
                return result;
            }
            case ObjectInstance obj:
            {
                var dict = new Dictionary<string, object?>();
                foreach (var prop in obj.GetOwnProperties())
                    dict[prop.Key.ToString()] = ToClr(prop.Value.Value);
                return dict;
            }
            default:
                return value.ToObject();
        }
    }

    /// <summary>
    /// Converts an array of CLR values into JS values (for engine calls).
    /// </summary>
    /// <param name="engine">The target JS engine.</param>
    /// <param name="args">The CLR arguments.</param>
    /// <returns>The JS argument array.</returns>
    public static JsValue[] ToJsArgs(Engine engine, object?[]? args)
    {
        if (args is null || args.Length == 0) return Array.Empty<JsValue>();

        var jsArgs = new JsValue[args.Length];
        for (var i = 0; i < args.Length; i++)
            jsArgs[i] = ToJs(engine, args[i]);
        return jsArgs;
    }

    /// <summary>
    /// Coerces a JS-originated value to a target CLR column type, handling
    /// the common number/string/date conversions.
    /// </summary>
    /// <param name="value">The value to coerce.</param>
    /// <param name="target">The target column type.</param>
    /// <returns>The coerced value (or <see cref="DBNull.Value"/> for null).</returns>
    /// <exception cref="InvalidOperationException">The value cannot be converted.</exception>
    public static object Coerce(object? value, Type target)
    {
        if (value is null || (value is JsValue jsv && (jsv.IsUndefined() || jsv.IsNull())))
            return DBNull.Value;

        var underlying = Nullable.GetUnderlyingType(target) ?? target;
        if (underlying.IsInstanceOfType(value))
            return value;

        try
        {
            if (underlying == typeof(string))
                return value.ToString()!;

            if (value is string s)
            {
                if (underlying == typeof(DateTime))
                    return DateTime.Parse(s);
                if (underlying == typeof(Guid))
                    return Guid.Parse(s);
                if (underlying == typeof(bool))
                    return bool.Parse(s);
                return Convert.ChangeType(s, underlying);
            }

            if (value is double d)
            {
                if (underlying == typeof(long) || underlying == typeof(int) || underlying == typeof(short))
                    return Convert.ChangeType(d, underlying);
                if (underlying == typeof(decimal))
                    return Convert.ChangeType(d, underlying);
                if (underlying == typeof(DateTime))
                    return DateTime.UnixEpoch.AddMilliseconds(d);
            }

            if (value is DateTime dt && underlying == typeof(double))
                return dt.Ticks;

            if (value is bool b && underlying == typeof(string))
                return b ? "true" : "false";

            return Convert.ChangeType(value, underlying);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Cannot convert value '{value}' ({value.GetType().Name}) to target type '{underlying.Name}'.", ex);
        }
    }

    private static DataTable ToSingleRowTable(DataRow row)
    {
        var table = row.Table.Clone();
        table.ImportRow(row);
        return table;
    }
}
