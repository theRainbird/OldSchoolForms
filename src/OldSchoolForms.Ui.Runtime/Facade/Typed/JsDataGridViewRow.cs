using Jint;
using Jint.Native;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// JS facade for a <see cref="DataGridViewRow"/>. Provides access to the cell
/// values and the data-bound item of the row.
/// </summary>
public class JsDataGridViewRow
{
    private readonly JsEngineHost _host;

    /// <summary>The wrapped row.</summary>
    public DataGridViewRow Row { get; }

    /// <summary>
    /// Creates a facade around a grid row.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="row">The wrapped row.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="row"/> is <see langword="null"/>.</exception>
    public JsDataGridViewRow(JsEngineHost host, DataGridViewRow row)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(row);
        _host = host;
        Row = row;
    }

    /// <summary>The number of cells in the row.</summary>
    public int CellCount => Row.Cells.Count;

    /// <summary>
    /// Gets the value of the cell at the given column index.
    /// </summary>
    /// <param name="columnIndex">The zero-based column index.</param>
    /// <returns>The cell value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside the row.</exception>
    public JsValue GetValue(int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= Row.Cells.Count)
            throw new ArgumentOutOfRangeException(nameof(columnIndex));
        return JsValue.FromObject(_host.Engine, Row.Cells[columnIndex].Value);
    }

    /// <summary>
    /// Sets the value of the cell at the given column index.
    /// </summary>
    /// <param name="columnIndex">The zero-based column index.</param>
    /// <param name="value">The new cell value.</param>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside the row.</exception>
    public void SetValue(int columnIndex, JsValue value)
    {
        if (columnIndex < 0 || columnIndex >= Row.Cells.Count)
            throw new ArgumentOutOfRangeException(nameof(columnIndex));
        Row.Cells[columnIndex].Value = value?.ToObject();
    }

    /// <summary>All cell values as a JS array.</summary>
    public JsValue[] Cells
    {
        get
        {
            var values = new JsValue[Row.Cells.Count];
            for (var i = 0; i < Row.Cells.Count; i++)
                values[i] = JsValue.FromObject(_host.Engine, Row.Cells[i].Value);
            return values;
        }
    }

    /// <summary>The data-bound item behind this row, or <c>null</c>.</summary>
    public JsValue DataBoundItem => JsValue.FromObject(_host.Engine, Row.DataBoundItem);
}
