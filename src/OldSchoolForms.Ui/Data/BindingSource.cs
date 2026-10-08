using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Reflection;

namespace OldSchoolForms.Ui.Data;

/// <summary>
/// Wraps a data source and provides binding management, currency, and change notification.
/// Analogous to System.Windows.Forms.BindingSource.
/// </summary>
public class BindingSource : Component, IBindingList, ICancelAddNew, IRaiseItemChangedEvents
{
    private object? _dataSource;
    private string _dataMember = string.Empty;
    private IBindingList? _list;
    private DataView? _view;
    private PropertyManager? _propertyManager;
    private int _position;
    private bool _rebuilding;

    /// <summary>
    /// Occurs when the list changes.
    /// </summary>
    public event ListChangedEventHandler? ListChanged;

    /// <summary>
    /// Occurs when the current item changes.
    /// </summary>
    public event EventHandler? CurrentChanged;

    /// <summary>
    /// Occurs when the current item is about to change.
    /// </summary>
    public event EventHandler? CurrentItemChanged;

    /// <summary>
    /// Occurs when the position in the list changes.
    /// </summary>
    public event EventHandler? PositionChanged;

    /// <summary>
    /// Occurs before a new item is added.
    /// </summary>
    public event AddingNewEventHandler? AddingNew;

    /// <summary>
    /// Gets or sets the data source.
    /// </summary>
    public object? DataSource
    {
        get => _dataSource;
        set
        {
            if (_dataSource != value)
            {
                _dataSource = value;
                ResetList();
            }
        }
    }

    /// <summary>
    /// Gets or sets the data member (property path for nested lists).
    /// </summary>
    public string DataMember
    {
        get => _dataMember;
        set
        {
            if (_dataMember != value)
            {
                _dataMember = value ?? string.Empty;
                ResetList();
            }
        }
    }

    /// <summary>
    /// Gets the underlying list.
    /// </summary>
    public IBindingList? List => _list;

    /// <summary>
    /// Gets the current item.
    /// </summary>
    public object? Current
    {
        get
        {
            if (_list != null && _position >= 0 && _position < _list.Count)
                return _list[_position];
            return _propertyManager?.Current;
        }
    }

    /// <summary>
    /// Gets or sets the current position in the list.
    /// </summary>
    public int Position
    {
        get => _position;
        set
        {
            if (_list == null)
                return;

            if (value < 0) value = 0;
            if (value >= _list.Count) value = Math.Max(0, _list.Count - 1);

            if (_position != value)
            {
                _position = value;
                OnPositionChanged(EventArgs.Empty);
                OnCurrentChanged(EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// Gets the number of items in the list.
    /// </summary>
    public int Count => _list?.Count ?? (_propertyManager != null ? 1 : 0);

    /// <summary>
    /// Gets whether the list allows adding new items.
    /// </summary>
    public bool AllowNew => _list?.AllowNew ?? false;

    /// <summary>
    /// Gets whether the list allows editing items.
    /// </summary>
    public bool AllowEdit => _list?.AllowEdit ?? false;

    /// <summary>
    /// Gets whether the list allows removing items.
    /// </summary>
    public bool AllowRemove => _list?.AllowRemove ?? false;

    /// <summary>
    /// Gets whether the data source supports change notification.
    /// </summary>
    public bool SupportsChangeNotification => _list?.SupportsChangeNotification ?? false;

    /// <summary>
    /// Gets a value indicating whether the list is empty.
    /// </summary>
    public bool IsEmpty => Count == 0;

    /// <summary>
    /// Gets or sets the sort expression (e.g. <c>"Name ASC, Age DESC"</c>).
    /// When the data source is a <see cref="DataTable"/> or <see cref="DataView"/>,
    /// the sort is applied live through the underlying view.
    /// </summary>
    public string? Sort
    {
        get => _view?.Sort ?? _sortExpression;
        set
        {
            _sortExpression = value ?? string.Empty;
            if (_view != null)
                _view.Sort = _sortExpression;
            SortDirection = ParseFirstSortDirection(_sortExpression);
        }
    }

    private string _sortExpression = string.Empty;

    /// <summary>
    /// Gets or sets the filter expression using <see cref="DataView.RowFilter"/>
    /// syntax (e.g. <c>"Age &gt;= 18 AND Name LIKE 'A*'"</c>).
    /// Only effective when the data source is a <see cref="DataTable"/> or
    /// <see cref="DataView"/>; assigning to a non-view source throws.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the current data source does not support filtering.
    /// </exception>
    public string Filter
    {
        get => _view?.RowFilter ?? string.Empty;
        set
        {
            if (_view == null)
                throw new InvalidOperationException("The current data source does not support filtering. Use a DataTable or DataView as DataSource.");
            _view.RowFilter = value ?? string.Empty;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the current data source supports filtering.
    /// </summary>
    public bool SupportsFiltering => _view != null;

    /// <summary>
    /// Gets a value indicating whether the current list is sorted.
    /// </summary>
    public bool IsSorted => _view is IBindingList vl && vl.IsSorted;

    /// <summary>
    /// Gets the sort direction of the first sort column.
    /// </summary>
    public ListSortDirection SortDirection { get; private set; }

    private static ListSortDirection ParseFirstSortDirection(string? sort)
    {
        if (string.IsNullOrWhiteSpace(sort))
            return ListSortDirection.Ascending;
        var parts = sort.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return ListSortDirection.Ascending;
        var tokens = parts[0].Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return tokens.Length > 1 && string.Equals(tokens[1], "DESC", StringComparison.OrdinalIgnoreCase)
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;
    }

    // ===== IBindingList implementation =====

    bool IBindingList.AllowNew => _list?.AllowNew ?? false;
    bool IBindingList.AllowEdit => _list?.AllowEdit ?? false;
    bool IBindingList.AllowRemove => _list?.AllowRemove ?? false;
    bool IBindingList.SupportsChangeNotification => _list?.SupportsChangeNotification ?? false;
    bool IBindingList.SupportsSearching => false;
    bool IBindingList.SupportsSorting => false;
    bool IBindingList.IsSorted => false;
    PropertyDescriptor? IBindingList.SortProperty => null;
    ListSortDirection IBindingList.SortDirection => ListSortDirection.Ascending;
    bool IRaiseItemChangedEvents.RaisesItemChangedEvents => true;

    bool IList.IsFixedSize => _list is IList list && list.IsFixedSize;
    bool IList.IsReadOnly => _list is IList list && list.IsReadOnly;
    bool ICollection.IsSynchronized => (_list as ICollection)?.IsSynchronized ?? false;
    object ICollection.SyncRoot => (_list as ICollection)?.SyncRoot ?? this;

    object? IList.this[int index]
    {
        get => _list is IList list ? list[index] : _list?[index];
        set { if (_list is IList list) list[index] = value; }
    }

    int IList.Add(object? value)
    {
        if (_list != null)
        {
            var idx = _list.Add(value ?? throw new ArgumentNullException(nameof(value)));
            return idx;
        }
        return -1;
    }

    bool IList.Contains(object? value) => (_list as IList)?.Contains(value) ?? false;
    int IList.IndexOf(object? value) => (_list as IList)?.IndexOf(value) ?? -1;

    void IList.Insert(int index, object? value)
    {
        if (_list is IList list)
            list.Insert(index, value);
    }

    void IList.Remove(object? value)
    {
        if (_list != null)
            _list.Remove(value);
    }

    void IList.RemoveAt(int index)
    {
        _list?.RemoveAt(index);
    }

    void IList.Clear()
    {
        _list?.Clear();
    }

    void ICollection.CopyTo(Array array, int index)
    {
        if (_list is ICollection col)
            col.CopyTo(array, index);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        if (_list is IList list)
            return list.GetEnumerator();
        if (_list is IEnumerable enumerable)
            return enumerable.GetEnumerator();
        return Enumerable.Empty<object>().GetEnumerator();
    }

    void IBindingList.AddIndex(PropertyDescriptor property) { }
    void IBindingList.RemoveIndex(PropertyDescriptor property) { }

    void IBindingList.ApplySort(PropertyDescriptor property, ListSortDirection direction)
    {
    }

    void IBindingList.RemoveSort()
    {
    }

    int IBindingList.Find(PropertyDescriptor property, object key) => -1;

    object? IBindingList.AddNew()
    {
        return AddNew();
    }

    void ICancelAddNew.CancelNew(int itemIndex)
    {
        if (_list is ICancelAddNew cancel)
            cancel.CancelNew(itemIndex);
    }

    void ICancelAddNew.EndNew(int itemIndex)
    {
        if (_list is ICancelAddNew end)
            end.EndNew(itemIndex);
    }

    /// <summary>
    /// Initializes a new instance of BindingSource.
    /// </summary>
    public BindingSource()
    {
    }

    /// <summary>
    /// Initializes a new instance with a data source.
    /// </summary>
    /// <param name="dataSource">The data source.</param>
    /// <param name="dataMember">The data member.</param>
    public BindingSource(object dataSource, string dataMember = "")
    {
        _dataSource = dataSource;
        _dataMember = dataMember ?? string.Empty;
        ResetList();
    }

    /// <summary>
    /// Gets the list as an IList.
    /// </summary>
    public IList? GetList()
    {
        return _list as IList;
    }

    /// <summary>
    /// Moves to the first item.
    /// </summary>
    public void MoveFirst() => Position = 0;

    /// <summary>
    /// Moves to the previous item.
    /// </summary>
    public void MovePrevious() => Position = _position - 1;

    /// <summary>
    /// Moves to the next item.
    /// </summary>
    public void MoveNext() => Position = _position + 1;

    /// <summary>
    /// Moves to the last item.
    /// </summary>
    public void MoveLast() => Position = _list != null ? Math.Max(0, _list.Count - 1) : 0;

    /// <summary>
    /// Adds a new item to the list.
    /// </summary>
    public object? AddNew()
    {
        var args = new AddingNewEventArgs(null);
        OnAddingNew(args);

        object? newItem;
        if (args.NewObject != null)
        {
            newItem = args.NewObject;
        }
        else if (_list != null)
        {
            newItem = _list.AddNew();
        }
        else
        {
            return null;
        }

        Position = _list?.Count - 1 ?? 0;
        return newItem;
    }

    /// <summary>
    /// Adds an existing item to the list.
    /// </summary>
    public int Add(object? value)
    {
        if (_list != null && value != null)
        {
            var index = _list.Add(value);
            return index;
        }
        return -1;
    }

    /// <summary>
    /// Removes the specified item from the list.
    /// </summary>
    public void Remove(object? value)
    {
        if (_list != null && value != null)
        {
            _list.Remove(value);
        }
    }

    /// <summary>
    /// Removes the item at the specified index.
    /// </summary>
    public void RemoveAt(int index)
    {
        if (_list != null && index >= 0 && index < _list.Count)
        {
            _list.RemoveAt(index);
        }
    }

    /// <summary>
    /// Clears the list.
    /// </summary>
    public void Clear()
    {
        _list?.Clear();
    }

    /// <summary>
    /// Finds the index of the item with the specified property value.
    /// </summary>
    public int Find(string propertyName, object key)
    {
        if (_list == null)
            return -1;

        for (int i = 0; i < _list.Count; i++)
        {
            var item = _list[i];
            if (item != null)
            {
                var desc = ReflectionPropertyDescriptor.GetProperty(item, propertyName);
                if (desc == null)
                    continue;
                var value = desc.GetValue(item);
                if (ValuesEqual(value, key))
                    return i;
            }
        }
        return -1;
    }

    private static bool ValuesEqual(object? a, object? b)
    {
        if (Equals(a, b))
            return true;
        // Numeric normalization: JS marshals integrals as long, columns may be int, and vice versa.
        if (a != null && b != null && IsNumeric(a) && IsNumeric(b))
        {
            try
            {
                return Convert.ToDouble(a).Equals(Convert.ToDouble(b));
            }
            catch
            {
                return false;
            }
        }
        return false;
    }

    private static bool IsNumeric(object value)
    {
        var code = Type.GetTypeCode(value.GetType());
        return code is TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16
            or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64
            or TypeCode.Single or TypeCode.Double or TypeCode.Decimal;
    }

    /// <summary>
    /// Gets the value of the specified property on the current item.
    /// </summary>
    public object? GetPropertyValue(string propertyName)
    {
        var current = Current;
        if (current == null)
            return null;

        var desc = ReflectionPropertyDescriptor.GetProperty(current, propertyName);
        return desc?.GetValue(current);
    }

    /// <summary>
    /// Resets the list based on the current DataSource and DataMember.
    /// </summary>
    protected virtual void ResetList()
    {
        if (_rebuilding)
            return;

        _rebuilding = true;
        try
        {
            UnsubscribeFromList();
            _list = CreateBindingList();
            _position = _list != null && _list.Count > 0 ? 0 : -1;
            SubscribeToList();
            OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
            OnCurrentChanged(EventArgs.Empty);
            OnPositionChanged(EventArgs.Empty);
        }
        finally
        {
            _rebuilding = false;
        }
    }

    private IBindingList? CreateBindingList()
    {
        if (_dataSource == null)
            return null;

        _view = null;

        if (_dataSource is DataView view)
        {
            _view = view;
            ApplyPendingSort();
            return view;
        }

        if (_dataSource is DataTable table)
        {
            _view = table.DefaultView;
            ApplyPendingSort();
            return _view;
        }

        if (_dataSource is DataSet dataSet && !string.IsNullOrEmpty(_dataMember))
        {
            var memberTable = dataSet.Tables[_dataMember];
            if (memberTable != null)
            {
                _view = memberTable.DefaultView;
                ApplyPendingSort();
                return _view;
            }
        }

        if (_dataSource is IBindingList bl)
        {
            if (!string.IsNullOrEmpty(_dataMember))
                return ResolveNestedList(bl, _dataMember);
            return bl;
        }

        if (_dataSource is IList list && _dataSource.GetType().IsGenericType)
        {
            var wrapper = new BindingList<object>();
            foreach (var item in list)
                wrapper.Add(item);
            return wrapper;
        }

        if (_dataSource is IEnumerable enumerable && _dataSource is not string)
        {
            var wrapper = new BindingList<object>();
            foreach (var item in enumerable)
                wrapper.Add(item);
            return wrapper;
        }

        _propertyManager = new PropertyManager(_dataSource);
        return null;
    }

    private void ApplyPendingSort()
    {
        if (_view != null && !string.IsNullOrEmpty(_sortExpression))
            _view.Sort = _sortExpression;
    }

    private static IBindingList? ResolveNestedList(IBindingList parent, string dataMember)
    {
        if (parent.Count == 0 || string.IsNullOrEmpty(dataMember))
            return parent;

        var first = parent[0];
        if (first == null)
            return parent;

        var prop = first.GetType().GetProperty(dataMember);
        if (prop == null)
            return parent;

        var value = prop.GetValue(first);
        if (value is IBindingList nestedList)
            return nestedList;

        return parent;
    }

    private void SubscribeToList()
    {
        if (_list != null)
        {
            _list.ListChanged += OnListChangedInternal;
        }
    }

    private void UnsubscribeFromList()
    {
        if (_list != null)
        {
            _list.ListChanged -= OnListChangedInternal;
        }
    }

    private void OnListChangedInternal(object? sender, ListChangedEventArgs e)
    {
        bool currentChanged = false;
        bool positionChanged = false;

        switch (e.ListChangedType)
        {
            case ListChangedType.ItemDeleted:
                if (e.NewIndex <= _position)
                {
                    if (_list!.Count == 0)
                        _position = -1;
                    else
                        _position = Math.Max(0, Math.Min(_position - 1, _list.Count - 1));
                    currentChanged = true;
                    positionChanged = true;
                }
                break;

            case ListChangedType.ItemAdded:
                if (e.NewIndex <= _position)
                {
                    _position++;
                    currentChanged = true;
                    positionChanged = true;
                }
                break;

            case ListChangedType.Reset:
                _position = _list != null && _list.Count > 0 ? 0 : -1;
                currentChanged = true;
                positionChanged = true;
                break;

            case ListChangedType.ItemChanged:
                if (e.NewIndex == _position)
                    currentChanged = true;
                break;
        }

        OnListChanged(e);

        if (positionChanged)
            OnPositionChanged(EventArgs.Empty);
        if (currentChanged)
            OnCurrentChanged(EventArgs.Empty);
    }

    /// <summary>
    /// Raises the ListChanged event.
    /// </summary>
    protected virtual void OnListChanged(ListChangedEventArgs e)
    {
        ListChanged?.Invoke(this, e);
    }

    /// <summary>
    /// Raises the CurrentChanged event.
    /// </summary>
    protected virtual void OnCurrentChanged(EventArgs e)
    {
        CurrentChanged?.Invoke(this, e);
        CurrentItemChanged?.Invoke(this, e);
    }

    /// <summary>
    /// Raises the PositionChanged event.
    /// </summary>
    protected virtual void OnPositionChanged(EventArgs e)
    {
        PositionChanged?.Invoke(this, e);
    }

    /// <summary>
    /// Raises the AddingNew event.
    /// </summary>
    protected virtual void OnAddingNew(AddingNewEventArgs e)
    {
        AddingNew?.Invoke(this, e);
    }

    /// <summary>
    /// Disposes the binding source.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            UnsubscribeFromList();
        }
        base.Dispose(disposing);
    }
}
