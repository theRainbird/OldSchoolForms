using System.ComponentModel;
using System.Data;
using Jint.Native;
using Jint.Native.Object;
using OldSchoolForms.Shared.Scripting;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Data;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade;

/// <summary>
/// JS-friendly wrapper around <see cref="BindingSource"/>. Provides live
/// filtering (<see cref="DataView.RowFilter"/> syntax), sorting, currency
/// navigation and two-way control bindings on top of a <see cref="JsDataTable"/>
/// or a table of a <see cref="JsDataSet"/>.
/// All members are PascalCase and reachable from JS via the camelCase fallback
/// (<c>bs.filter</c>, <c>bs.moveNext()</c>, ...).
/// </summary>
public sealed class JsBindingSource : IDisposable
{
    private readonly JsEngineHost _host;
    private readonly BindingSource _source;
    private readonly List<(JsControl Control, string Property)> _bindings = new();
    private JsValue _listChanged = JsValue.Undefined;
    private JsValue _currentChanged = JsValue.Undefined;
    private bool _disposed;

    /// <summary>
    /// Creates a binding source over a table facade.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="table">The table facade to bind.</param>
    /// <exception cref="ArgumentNullException">Host or table is <c>null</c>.</exception>
    public JsBindingSource(JsEngineHost host, JsDataTable table)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(table);
        _host = host;
        _source = new BindingSource { DataSource = table.Table };
        WireEvents();
    }

    /// <summary>
    /// Creates a binding source over one table of a dataset facade.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="set">The dataset facade.</param>
    /// <param name="tableName">The name of the table within the dataset.</param>
    /// <exception cref="ArgumentNullException">Host or set is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">The dataset has no table with the given name.</exception>
    public JsBindingSource(JsEngineHost host, JsDataSet set, string tableName)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(set);
        _host = host;
        if (!set.Set.Tables.Contains(tableName))
            throw new InvalidOperationException(string.Format(LangRes.GetEnglish("Runtime_BindingSourceNoTable"), tableName));
        _source = new BindingSource { DataSource = set.Set, DataMember = tableName };
        WireEvents();
    }

    /// <summary>The wrapped binding source.</summary>
    internal BindingSource Source => _source;

    /// <summary>
    /// Gets or sets the row filter using <see cref="DataView.RowFilter"/> syntax,
    /// e.g. <c>"age &gt;= 18 AND name LIKE 'A*'"</c>.
    /// </summary>
    public string Filter
    {
        get => _source.Filter;
        set => _source.Filter = value ?? string.Empty;
    }

    /// <summary>
    /// Gets or sets the sort expression, e.g. <c>"name ASC, age DESC"</c>.
    /// </summary>
    public string Sort
    {
        get => _source.Sort ?? string.Empty;
        set => _source.Sort = value ?? string.Empty;
    }

    /// <summary>Number of visible (filtered) rows.</summary>
    public int Count => _source.Count;

    /// <summary>Whether the filtered list is empty.</summary>
    public bool IsEmpty => _source.IsEmpty;

    /// <summary>Whether the current source supports filtering.</summary>
    public bool SupportsFiltering => _source.SupportsFiltering;

    /// <summary>
    /// Gets or sets the current position within the filtered/sorted view.
    /// Setting it raises the current-change notifications.
    /// </summary>
    public int Position
    {
        get => _source.Position;
        set => _source.Position = value;
    }

    /// <summary>
    /// The current row as a <see cref="JsDataRow"/>, or <c>null</c> when the
    /// list is empty.
    /// </summary>
    public JsValue Current
    {
        get
        {
            var row = CurrentDataRow();
            return row == null ? JsValue.Null : JsValue.FromObject(_host.Engine, new JsDataRow(_host.Engine, row));
        }
    }

    /// <summary>
    /// The row at the given view index (filtered/sorted order).
    /// </summary>
    /// <param name="index">The zero-based index within the view.</param>
    /// <returns>The row facade.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside the view.</exception>
    public JsDataRow GetRow(int index)
    {
        if (_source.List == null || index < 0 || index >= _source.List.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        return new JsDataRow(_host.Engine, UnderlyingRow(_source.List[index]));
    }

    /// <summary>Moves the currency to the first row.</summary>
    public void MoveFirst() => _source.MoveFirst();

    /// <summary>Moves the currency to the previous row.</summary>
    public void MovePrevious() => _source.MovePrevious();

    /// <summary>Moves the currency to the next row.</summary>
    public void MoveNext() => _source.MoveNext();

    /// <summary>Moves the currency to the last row.</summary>
    public void MoveLast() => _source.MoveLast();

    /// <summary>
    /// Appends a new row to the underlying table and makes it current.
    /// </summary>
    /// <returns>The new row facade.</returns>
    public JsDataRow AddNew()
    {
        var item = _source.AddNew()
            ?? throw new InvalidOperationException(LangRes.GetEnglish("Runtime_BindingSourceAddNewFailed"));
        return new JsDataRow(_host.Engine, UnderlyingRow(item));
    }

    /// <summary>
    /// Removes the row at the given view index.
    /// </summary>
    /// <param name="index">The zero-based index within the view.</param>
    public void RemoveAt(int index) => _source.RemoveAt(index);

    /// <summary>
    /// Removes the row behind a row facade from the underlying table.
    /// </summary>
    /// <param name="row">The row facade to remove.</param>
    /// <exception cref="ArgumentNullException">The row facade is <c>null</c>.</exception>
    public void Remove(JsDataRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        _source.Remove(row.Row);
    }

    /// <summary>
    /// Finds the view index of the first row whose column matches the value.
    /// Numeric values are compared with type normalization.
    /// </summary>
    /// <param name="column">The column name.</param>
    /// <param name="value">The value to search for.</param>
    /// <returns>The zero-based view index, or <c>-1</c> when not found.</returns>
    public int Find(string column, JsValue value)
    {
        var clr = value.ToObject();
        return _source.Find(column, clr ?? DBNull.Value);
    }

    /// <summary>
    /// Binds a control property to a column of the current row (two-way).
    /// The control property name is PascalCased automatically
    /// (<c>'text'</c> → <c>Text</c>, <c>'checked'</c> → <c>Checked</c>).
    /// </summary>
    /// <param name="control">The control facade to bind.</param>
    /// <param name="controlProperty">The control property (camelCase or PascalCase).</param>
    /// <param name="column">The data column name.</param>
    /// <exception cref="ArgumentNullException">The control facade is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">A binding for the same control property already exists.</exception>
    public void Bind(JsControl control, string controlProperty, string column)
    {
        ArgumentNullException.ThrowIfNull(control);
        var prop = PascalCase(controlProperty);
        control.Control.DataBindings.Add(prop, _source, column);
        _bindings.Add((control, prop));
    }

    /// <summary>
    /// Removes a previously created binding for the control property.
    /// </summary>
    /// <param name="control">The control facade.</param>
    /// <param name="controlProperty">The control property name.</param>
    public void Unbind(JsControl control, string controlProperty)
    {
        ArgumentNullException.ThrowIfNull(control);
        var prop = PascalCase(controlProperty);
        control.Control.DataBindings.Remove(prop);
        _bindings.RemoveAll(b => ReferenceEquals(b.Control, control) && b.Property == prop);
    }

    /// <summary>
    /// Registers a JS callback invoked on every list change with
    /// <c>{ type, index }</c> (type: reset/itemAdded/itemChanged/itemDeleted).
    /// </summary>
    /// <param name="callback">The JS function.</param>
    public void OnListChanged(JsValue callback) => _listChanged = callback ?? JsValue.Undefined;

    /// <summary>
    /// Registers a JS callback invoked when the current row changes; it receives
    /// the current <see cref="JsDataRow"/> or <c>null</c>.
    /// </summary>
    /// <param name="callback">The JS function.</param>
    public void OnCurrentChanged(JsValue callback) => _currentChanged = callback ?? JsValue.Undefined;

    private DataRow? CurrentDataRow()
    {
        var cur = _source.Current;
        return cur == null ? null : UnderlyingRow(cur);
    }

    private static DataRow UnderlyingRow(object item)
    {
        if (item is DataRowView drv)
            return drv.Row;
        if (item is DataRow dr)
            return dr;
        throw new InvalidOperationException(string.Format(LangRes.GetEnglish("Runtime_BindingSourceUnsupportedItem"), item.GetType().Name));
    }

    private static string PascalCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.IsLower(name[0]) ? char.ToUpperInvariant(name[0]) + name[1..] : name;
    }

    private void WireEvents()
    {
        _source.ListChanged += (_, e) =>
        {
            if (_listChanged is not Jint.Native.Function.Function) return;
            try
            {
                var args = _host.NewObject();
                args.Set("type", ListChangedTypeName(e.ListChangedType));
                args.Set("index", JsValue.FromObject(_host.Engine, e.NewIndex));
                using (_host.UseModule(null))
                    _host.CallFunction(_listChanged, args);
            }
            catch (Exception ex)
            {
                _host.ReportError("bindingSource.onListChanged", ex);
            }
        };

        _source.CurrentChanged += (_, _) =>
        {
            if (_currentChanged is not Jint.Native.Function.Function) return;
            try
            {
                var row = CurrentDataRow();
                var arg = row == null ? JsValue.Null : JsValue.FromObject(_host.Engine, new JsDataRow(_host.Engine, row));
                using (_host.UseModule(null))
                    _host.CallFunction(_currentChanged, arg);
            }
            catch (Exception ex)
            {
                _host.ReportError("bindingSource.onCurrentChanged", ex);
            }
        };
    }

    private static string ListChangedTypeName(ListChangedType type) => type switch
    {
        ListChangedType.ItemAdded => "itemAdded",
        ListChangedType.ItemChanged => "itemChanged",
        ListChangedType.ItemDeleted => "itemDeleted",
        ListChangedType.Reset => "reset",
        _ => type.ToString()
    };

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var (control, prop) in _bindings)
        {
            try { control.Control.DataBindings.Remove(prop); }
            catch { /* control may already be disposed */ }
        }
        _bindings.Clear();

        _listChanged = JsValue.Undefined;
        _currentChanged = JsValue.Undefined;
        _source.Dispose();
    }
}
