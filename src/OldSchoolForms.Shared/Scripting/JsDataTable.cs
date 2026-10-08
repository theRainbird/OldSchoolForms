using System.Data;
using Jint;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;

namespace OldSchoolForms.Shared.Scripting;

/// <summary>
/// JS-facing facade over a <see cref="System.Data.DataTable"/>. Provides an
/// ES6-friendly surface (<c>rowCount</c>, <c>row(i)</c>, <c>add(...)</c>,
/// <c>getChanges()</c>, ...) while keeping the raw ADO.NET object out of
/// JavaScript. The same facade is used on the client and on the server, so
/// shared validation modules work identically on both sides.
/// </summary>
public sealed class JsDataTable
{
    private readonly Engine _engine;

    /// <summary>
    /// Creates a facade for the given table.
    /// </summary>
    /// <param name="engine">The JS engine this facade belongs to.</param>
    /// <param name="table">The underlying data table.</param>
    public JsDataTable(Engine engine, DataTable table)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(table);
        _engine = engine;
        Table = table;
    }

    /// <summary>The underlying CLR table. Trusted code only (not visible to JS).</summary>
    internal DataTable Table { get; }

    /// <summary>Gets or sets the table name.</summary>
    public string Name
    {
        get => Table.TableName;
        set => Table.TableName = value ?? string.Empty;
    }

    /// <summary>The column names in schema order.</summary>
    public string[] ColumnNames
    {
        get
        {
            var names = new string[Table.Columns.Count];
            for (var i = 0; i < names.Length; i++)
                names[i] = Table.Columns[i].ColumnName;
            return names;
        }
    }

    /// <summary>Number of columns.</summary>
    public int ColumnCount => Table.Columns.Count;

    /// <summary>Number of rows (excluding physically deleted ones).</summary>
    public int RowCount => Table.Rows.Count;

    /// <summary>
    /// Gets a row facade by index.
    /// </summary>
    /// <param name="index">The zero-based row index.</param>
    /// <returns>The row facade.</returns>
    public JsDataRow GetRow(int index) => new(_engine, Table.Rows[index]);

    /// <summary>
    /// Adds a column to the table schema. The <paramref name="type"/> is a simple
    /// type name (<c>string</c>, <c>long</c>, <c>int</c>, <c>double</c>,
    /// <c>bool</c>, <c>decimal</c>, <c>datetime</c>, <c>guid</c>).
    /// </summary>
    /// <param name="name">The column name.</param>
    /// <param name="type">The simple type name.</param>
    public void AddColumn(string name, string type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var clrType = MapType(type);
        Table.Columns.Add(new DataColumn(name, clrType));
    }

    private static Type MapType(string type) => (type ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "string" => typeof(string),
        "long" => typeof(long),
        "int" => typeof(int),
        "double" => typeof(double),
        "bool" or "boolean" => typeof(bool),
        "decimal" => typeof(decimal),
        "datetime" or "date" => typeof(DateTime),
        "guid" => typeof(Guid),
        _ => throw new InvalidOperationException($"Unknown column type '{type}'.")
    };

    /// <summary>
    /// Appends a new row. Accepts either a JS array (positional values) or a
    /// plain JS object (column name → value).
    /// </summary>
    /// <param name="values">A JS array of positional values or an object map.</param>
    /// <returns>The facade of the newly added row.</returns>
    public JsDataRow Add(JsValue values)
    {
        var row = Table.NewRow();

        if (values is ArrayInstance arr)
        {
            var items = arr.ToArray();
            if (items.Length > Table.Columns.Count)
                throw new InvalidOperationException(
                    $"Too many values for table '{Table.TableName}': {items.Length} for {Table.Columns.Count} columns.");

            for (var i = 0; i < items.Length; i++)
                row[i] = JsMarshal.Coerce(JsMarshal.ToClr(items[i]), Table.Columns[i].DataType);
        }
        else if (values is ObjectInstance obj)
        {
            foreach (var prop in obj.GetOwnProperties())
            {
                var name = prop.Key.ToString();
                var col = Table.Columns[name]
                    ?? throw new InvalidOperationException($"Unknown column '{name}' in table '{Table.TableName}'.");
                row[name] = JsMarshal.Coerce(JsMarshal.ToClr(prop.Value.Value), col.DataType);
            }
        }
        else
        {
            throw new InvalidOperationException(
                "add() expects an array of values or an object with column names.");
        }

        Table.Rows.Add(row);
        return new JsDataRow(_engine, row);
    }

    /// <summary>
    /// Marks a row as deleted (logical delete; the row state is preserved for
    /// <see cref="GetChanges"/>).
    /// </summary>
    /// <param name="row">The row facade to delete.</param>
    public void DeleteRow(JsDataRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        row.Row.Delete();
    }

    /// <summary>
    /// Returns a new table facade containing only the changed rows
    /// (added/modified/deleted), as preserved by the DiffGram transfer.
    /// </summary>
    /// <returns>A facade over the changes table.</returns>
    public JsDataTable GetChanges() => new(_engine, Table.GetChanges() ?? Table.Clone());

    /// <summary>
    /// Accepts all pending changes (all rows become <c>unchanged</c>).
    /// </summary>
    public void AcceptChanges() => Table.AcceptChanges();

    /// <summary>
    /// Merges another table (same schema) into this one, preserving row states.
    /// </summary>
    /// <param name="other">The table facade to merge in.</param>
    public void Merge(JsDataTable other)
    {
        ArgumentNullException.ThrowIfNull(other);
        Table.Merge(other.Table);
    }

    /// <summary>
    /// Converts all rows into a JS array of plain objects.
    /// </summary>
    /// <returns>A JS array with one object per row.</returns>
    public JsValue ToArray()
    {
        var arr = (ArrayInstance)_engine.Intrinsics.Array.Construct(new JsValue[0]);
        foreach (DataRow row in Table.Rows)
            arr.Push(new JsDataRow(_engine, row).ToObject());
        return arr;
    }
}
