using Jint;
using Jint.Native;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// JS facade for a <see cref="DataGridView"/>. Provides column/row management,
/// selection, editing control and cell events. Cell events expose the affected
/// cell through <c>e.row</c> and <c>e.column</c>; the <c>cellformatting</c>
/// event additionally exposes <c>e.value</c> which the callback can override
/// together with <c>e.handled = true</c>.
/// </summary>
public class JsDataGridView : JsControl
{
    private readonly JsDataGridViewColumnCollection _columns;
    private readonly JsDataGridViewRowCollection _rows;

    /// <summary>The wrapped grid.</summary>
    public DataGridView Grid { get; }

    /// <summary>
    /// Creates a facade around a data grid.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="grid">The wrapped grid.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="grid"/> is <see langword="null"/>.</exception>
    public JsDataGridView(JsEngineHost host, DataGridView grid)
        : base(host, grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        Grid = grid;
        _columns = new JsDataGridViewColumnCollection(host, grid);
        _rows = new JsDataGridViewRowCollection(host, grid);
    }

    /// <summary>The column collection facade.</summary>
    public JsDataGridViewColumnCollection Columns => _columns;

    /// <summary>The row collection facade.</summary>
    public JsDataGridViewRowCollection Rows => _rows;

    /// <summary>The selected row index, or <c>-1</c>.</summary>
    public int SelectedRowIndex
    {
        get => Grid.SelectedRowIndex;
        set => Grid.SelectedRowIndex = value;
    }

    /// <summary>The selected column index, or <c>-1</c>.</summary>
    public int SelectedColumnIndex
    {
        get => Grid.SelectedColumnIndex;
        set => Grid.SelectedColumnIndex = value;
    }

    /// <summary>The value of the selected cell, or <c>null</c>.</summary>
    public JsValue SelectedValue => JsValue.FromObject(Host.Engine, Grid.SelectedValue);

    /// <summary>Whether the whole grid is read-only.</summary>
    public bool ReadOnly
    {
        get => Grid.ReadOnly;
        set => Grid.ReadOnly = value;
    }

    /// <summary>Whether the user may add rows via the new-row placeholder.</summary>
    public bool AllowUserToAddRows
    {
        get => Grid.AllowUserToAddRows;
        set => Grid.AllowUserToAddRows = value;
    }

    /// <summary>Whether the user may delete rows.</summary>
    public bool AllowUserToDeleteRows
    {
        get => Grid.AllowUserToDeleteRows;
        set => Grid.AllowUserToDeleteRows = value;
    }

    /// <summary>Whether multiple rows may be selected.</summary>
    public bool MultiSelect
    {
        get => Grid.MultiSelect;
        set => Grid.MultiSelect = value;
    }

    /// <summary>Whether column headers are shown.</summary>
    public bool ColumnHeadersVisible
    {
        get => Grid.ColumnHeadersVisible;
        set => Grid.ColumnHeadersVisible = value;
    }

    /// <summary>Whether row headers are shown.</summary>
    public bool RowHeadersVisible
    {
        get => Grid.RowHeadersVisible;
        set => Grid.RowHeadersVisible = value;
    }

    /// <summary>Whether grid lines are drawn.</summary>
    public bool ShowGridLines
    {
        get => Grid.ShowGridLines;
        set => Grid.ShowGridLines = value;
    }

    /// <summary>
    /// The selection mode as a string (<c>RowHeaderSelect</c>, <c>ColumnHeaderSelect</c>,
    /// <c>FullRowSelect</c>, <c>FullColumnSelect</c>, <c>CellSelect</c>).
    /// Invalid values are ignored.
    /// </summary>
    public string SelectionMode
    {
        get => Grid.SelectionMode.ToString();
        set
        {
            if (Enum.TryParse<DataGridViewSelectionMode>(value, true, out var mode))
                Grid.SelectionMode = mode;
        }
    }

    /// <summary>
    /// The edit trigger mode as a string (<c>EditOnEnter</c>, <c>EditOnF2</c>,
    /// <c>EditProgrammatically</c>). Invalid values are ignored.
    /// </summary>
    public string EditMode
    {
        get => Grid.EditMode.ToString();
        set
        {
            if (Enum.TryParse<DataGridViewEditMode>(value, true, out var mode))
                Grid.EditMode = mode;
        }
    }

    /// <summary>Whether the current cell is in edit mode.</summary>
    public bool IsCurrentCellInEditMode => Grid.IsCurrentCellInEditMode;

    /// <summary>
    /// Appends a row with the given cell values.
    /// </summary>
    /// <param name="values">The cell values for the new row.</param>
    public void AddRow(params JsValue[] values)
    {
        var clrValues = new object[values.Length];
        for (var i = 0; i < values.Length; i++)
            clrValues[i] = values[i]?.ToObject() ?? string.Empty;
        Grid.AddRow(clrValues);
    }

    /// <summary>Enters edit mode on the current cell.</summary>
    public void BeginEdit() => Grid.BeginEdit();

    /// <summary>
    /// Leaves edit mode on the current cell.
    /// </summary>
    /// <param name="commit">Whether to commit the edited value.</param>
    public void EndEdit(bool commit) => Grid.EndEdit(commit);

    /// <summary>
    /// Groups the grid by the column with the given index.
    /// </summary>
    /// <param name="columnIndex">The zero-based column index to group by.</param>
    public void AddGroupColumn(int columnIndex) => Grid.AddGroupColumn(columnIndex);

    /// <summary>
    /// Removes a grouping level.
    /// </summary>
    /// <param name="levelIndex">The grouping level index.</param>
    public void RemoveGroupColumn(int levelIndex) => Grid.RemoveGroupColumn(levelIndex);

    /// <summary>Removes all grouping levels.</summary>
    public void ClearGrouping() => Grid.ClearGrouping();

    /// <inheritdoc />
    protected override Delegate? WireCustom(string normalized, JsValue callback)
    {
        switch (normalized)
        {
            case "cellclick":
                EventHandler click = (s, e) =>
                {
                    var jsEvent = BuildEvent("cellclick", e);
                    if (e is DataGridViewCellEventArgs cea)
                    {
                        jsEvent.Row = cea.RowIndex;
                        jsEvent.Column = cea.ColumnIndex;
                    }
                    RaiseEvent("cellclick", callback, jsEvent, e);
                };
                Grid.CellClick += click;
                return click;
            case "cellvaluechanged":
                EventHandler<DataGridViewCellEventArgs> valueChanged = (s, e) =>
                {
                    var jsEvent = BuildEvent("cellvaluechanged", e);
                    jsEvent.Row = e.RowIndex;
                    jsEvent.Column = e.ColumnIndex;
                    RaiseEvent("cellvaluechanged", callback, jsEvent, e);
                };
                Grid.CellValueChanged += valueChanged;
                return valueChanged;
            case "cellbeginedit":
                EventHandler<DataGridViewCellEventArgs> beginEdit = (s, e) =>
                {
                    var jsEvent = BuildEvent("cellbeginedit", e);
                    jsEvent.Row = e.RowIndex;
                    jsEvent.Column = e.ColumnIndex;
                    RaiseEvent("cellbeginedit", callback, jsEvent, e);
                };
                Grid.CellBeginEdit += beginEdit;
                return beginEdit;
            case "cellendedit":
                EventHandler<DataGridViewCellEventArgs> endEdit = (s, e) =>
                {
                    var jsEvent = BuildEvent("cellendedit", e);
                    jsEvent.Row = e.RowIndex;
                    jsEvent.Column = e.ColumnIndex;
                    RaiseEvent("cellendedit", callback, jsEvent, e);
                };
                Grid.CellEndEdit += endEdit;
                return endEdit;
            case "cellbuttonclick":
                EventHandler<DataGridViewCellEventArgs> buttonClick = (s, e) =>
                {
                    var jsEvent = BuildEvent("cellbuttonclick", e);
                    jsEvent.Row = e.RowIndex;
                    jsEvent.Column = e.ColumnIndex;
                    RaiseEvent("cellbuttonclick", callback, jsEvent, e);
                };
                Grid.CellButtonClick += buttonClick;
                return buttonClick;
            case "cellformatting":
                EventHandler<DataGridViewCellFormattingEventArgs> formatting = (s, e) =>
                {
                    var jsEvent = BuildEvent("cellformatting", e);
                    jsEvent.Row = e.RowIndex;
                    jsEvent.Column = e.ColumnIndex;
                    jsEvent.Value = e.Value;
                    RaiseEvent("cellformatting", callback, jsEvent, e);
                };
                Grid.CellFormatting += formatting;
                return formatting;
            case "selectionchanged":
                EventHandler selection = (s, e) => Raise("selectionchanged", callback, e);
                Grid.SelectionChanged += selection;
                return selection;
            default:
                return null;
        }
    }

    /// <inheritdoc />
    protected override bool UnwireCustom(string normalized, Delegate handler)
    {
        switch (normalized)
        {
            case "cellclick":
                Grid.CellClick -= (EventHandler)handler;
                return true;
            case "cellvaluechanged":
                Grid.CellValueChanged -= (EventHandler<DataGridViewCellEventArgs>)handler;
                return true;
            case "cellbeginedit":
                Grid.CellBeginEdit -= (EventHandler<DataGridViewCellEventArgs>)handler;
                return true;
            case "cellendedit":
                Grid.CellEndEdit -= (EventHandler<DataGridViewCellEventArgs>)handler;
                return true;
            case "cellbuttonclick":
                Grid.CellButtonClick -= (EventHandler<DataGridViewCellEventArgs>)handler;
                return true;
            case "cellformatting":
                Grid.CellFormatting -= (EventHandler<DataGridViewCellFormattingEventArgs>)handler;
                return true;
            case "selectionchanged":
                Grid.SelectionChanged -= (EventHandler)handler;
                return true;
            default:
                return false;
        }
    }
}
