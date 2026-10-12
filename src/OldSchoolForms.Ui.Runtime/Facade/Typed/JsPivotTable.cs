using Jint;
using Jint.Native;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// JS facade for a <see cref="PivotTable"/>. Provides field configuration,
/// data source binding, matrix rebuild and filter control.
/// </summary>
public class JsPivotTable : JsControl
{
    private readonly JsPivotFieldCollection _fields;

    /// <summary>The wrapped pivot table.</summary>
    public PivotTable Pivot { get; }

    /// <summary>
    /// Creates a facade around a pivot table.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="pivot">The wrapped pivot table.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="pivot"/> is <see langword="null"/>.</exception>
    public JsPivotTable(JsEngineHost host, PivotTable pivot)
        : base(host, pivot)
    {
        ArgumentNullException.ThrowIfNull(pivot);
        Pivot = pivot;
        _fields = new JsPivotFieldCollection(host, pivot);
    }

    /// <summary>The pivot field collection facade.</summary>
    public JsPivotFieldCollection Fields => _fields;

    /// <summary>
    /// The pivot table's data source. Accepts a <c>JsBindingSource</c>, a
    /// <c>JsDataTable</c>, an array of plain JS objects, or any CLR
    /// <see cref="IEnumerable{T}"/>.
    /// </summary>
    public JsValue DataSource
    {
        get => JsValue.FromObject(Host.Engine, Pivot.DataSource);
        set => Pivot.DataSource = JsMarshal.ToDataSource(value);
    }

    /// <summary>The data member (table name) when binding a data set.</summary>
    public string DataMember
    {
        get => Pivot.DataMember;
        set => Pivot.DataMember = value ?? string.Empty;
    }

    /// <summary>
    /// The layout mode as a string (<c>Compact</c>, <c>Outline</c>, <c>Tabular</c>).
    /// Invalid values are ignored.
    /// </summary>
    public string LayoutMode
    {
        get => Pivot.LayoutMode.ToString();
        set
        {
            if (Enum.TryParse<PivotTableLayoutMode>(value, true, out var mode))
                Pivot.LayoutMode = mode;
        }
    }

    /// <summary>
    /// The total visibility as a string (<c>None</c>, <c>RowGrandTotal</c>,
    /// <c>ColumnGrandTotal</c>, <c>Subtotals</c>, <c>All</c>). Invalid values are ignored.
    /// </summary>
    public string TotalVisibility
    {
        get => Pivot.TotalVisibility.ToString();
        set
        {
            if (Enum.TryParse<PivotTableTotalVisibility>(value, true, out var visibility))
                Pivot.TotalVisibility = visibility;
        }
    }

    /// <summary>The selected row index, or <c>-1</c>.</summary>
    public int SelectedRowIndex
    {
        get => Pivot.SelectedRowIndex;
        set => Pivot.SelectedRowIndex = value;
    }

    /// <summary>The selected column index, or <c>-1</c>.</summary>
    public int SelectedColumnIndex
    {
        get => Pivot.SelectedColumnIndex;
        set => Pivot.SelectedColumnIndex = value;
    }

    /// <summary>Recomputes the pivot matrix from the current data source and fields.</summary>
    public void RebuildMatrix() => Pivot.RebuildMatrix();

    /// <summary>Expands all hierarchy nodes.</summary>
    public void ExpandAll() => Pivot.ExpandAll();

    /// <summary>Collapses all hierarchy nodes.</summary>
    public void CollapseAll() => Pivot.CollapseAll();

    /// <summary>
    /// Applies a filter value to a data property.
    /// </summary>
    /// <param name="dataPropertyName">The data property to filter.</param>
    /// <param name="value">The filter value.</param>
    public void SetFilter(string dataPropertyName, JsValue value)
        => Pivot.SetFilter(dataPropertyName, value?.ToObject());

    /// <summary>Removes all filters.</summary>
    public void ClearFilters() => Pivot.ClearFilters();

    /// <inheritdoc />
    protected override Delegate? WireCustom(string normalized, JsValue callback)
    {
        switch (normalized)
        {
            case "cellclick":
                EventHandler<PivotTableCellClickEventArgs> click = (s, e) =>
                {
                    var jsEvent = BuildEvent("cellclick", e);
                    jsEvent.Row = e.RowIndex;
                    jsEvent.Column = e.ColumnIndex;
                    jsEvent.Payload = e.IsTotalCell;
                    RaiseEvent("cellclick", callback, jsEvent, e);
                };
                Pivot.CellClick += click;
                return click;
            case "drilldowntoggled":
                EventHandler<PivotTableDrilldownEventArgs> drilldown = (s, e) =>
                {
                    var jsEvent = BuildEvent("drilldowntoggled", e);
                    jsEvent.Payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["rowFieldIndex"] = e.RowFieldIndex,
                        ["nodePath"] = e.NodePath,
                        ["nodeValue"] = e.NodeValue,
                        ["isExpanded"] = e.IsExpanded
                    };
                    RaiseEvent("drilldowntoggled", callback, jsEvent, e);
                };
                Pivot.DrilldownToggled += drilldown;
                return drilldown;
            case "selectionchanged":
                EventHandler selection = (s, e) => Raise("selectionchanged", callback, e);
                Pivot.SelectionChanged += selection;
                return selection;
            case "matrixrebuilt":
                EventHandler rebuilt = (s, e) => Raise("matrixrebuilt", callback, e);
                Pivot.MatrixRebuilt += rebuilt;
                return rebuilt;
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
                Pivot.CellClick -= (EventHandler<PivotTableCellClickEventArgs>)handler;
                return true;
            case "drilldowntoggled":
                Pivot.DrilldownToggled -= (EventHandler<PivotTableDrilldownEventArgs>)handler;
                return true;
            case "selectionchanged":
                Pivot.SelectionChanged -= (EventHandler)handler;
                return true;
            case "matrixrebuilt":
                Pivot.MatrixRebuilt -= (EventHandler)handler;
                return true;
            default:
                return false;
        }
    }
}
