using System.Data;
using Jint;
using Jint.Native;
using Jint.Native.Object;

namespace OldSchoolForms.Shared.Scripting;

/// <summary>
/// JS-facing facade over a <see cref="System.Data.DataRow"/>. Exposes a
/// small, ES6-friendly surface (<c>get</c>/<c>set</c>/<c>state</c>) instead
/// of the raw CLR object, so no ADO.NET types leak into JavaScript.
/// Instances are created by <see cref="JsDataTable"/> and
/// <see cref="JsMarshal"/>; the underlying row is only reachable from
/// trusted (C#) code via the internal <see cref="Row"/> property.
/// </summary>
public sealed class JsDataRow
{
    private readonly Engine _engine;

    /// <summary>
    /// Creates a facade for the given row.
    /// </summary>
    /// <param name="engine">The JS engine this facade belongs to.</param>
    /// <param name="row">The underlying data row.</param>
    public JsDataRow(Engine engine, DataRow row)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(row);
        _engine = engine;
        Row = row;
    }

    /// <summary>The underlying CLR row. Trusted code only (not visible to JS).</summary>
    internal DataRow Row { get; }

    /// <summary>
    /// Gets the current value of a column.
    /// </summary>
    /// <param name="column">The column name.</param>
    /// <returns>The current value, or <see langword="null"/> for <c>DBNull</c>.</returns>
    public object? Get(string column)
    {
        var value = Row[column];
        return value == DBNull.Value ? null : value;
    }

    /// <summary>
    /// Gets the original value of a column (the value before the last
    /// accept-changes, as preserved by the DiffGram transfer).
    /// </summary>
    /// <param name="column">The column name.</param>
    /// <returns>The original value, or <see langword="null"/> for <c>DBNull</c>.</returns>
    public object? GetOriginal(string column)
    {
        var value = Row[column, DataRowVersion.Original];
        return value == DBNull.Value ? null : value;
    }

    /// <summary>
    /// Sets the current value of a column, coercing common JS types
    /// (number, string, boolean, date) to the column's CLR type.
    /// </summary>
    /// <param name="column">The column name.</param>
    /// <param name="value">The new value.</param>
    public void Set(string column, object? value)
    {
        var col = Row.Table.Columns[column]
            ?? throw new InvalidOperationException($"Unknown column '{column}' in table '{Row.Table.TableName}'.");
        Row[column] = JsMarshal.Coerce(value, col.DataType);
    }

    /// <summary>
    /// The row state as a lowercase string: <c>unchanged</c>, <c>added</c>,
    /// <c>modified</c> or <c>deleted</c> — preserved across the wire.
    /// </summary>
    public string State => Row.RowState switch
    {
        DataRowState.Added => "added",
        DataRowState.Deleted => "deleted",
        DataRowState.Modified => "modified",
        _ => "unchanged"
    };

    /// <summary>
    /// Converts the row's current values into a plain JS object
    /// (column name → value).
    /// </summary>
    /// <returns>A JS object with one property per column.</returns>
    public JsValue ToObject()
    {
        var obj = _engine.Intrinsics.Object.Construct(new JsValue[0]);
        foreach (DataColumn col in Row.Table.Columns)
        {
            var value = Row[col];
            obj.Set(col.ColumnName, JsMarshal.ToJs(_engine, value == DBNull.Value ? null : value));
        }

        return obj;
    }
}
