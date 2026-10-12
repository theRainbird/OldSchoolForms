using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// JS facade for the rows of a <see cref="DataGridView"/>.
/// </summary>
public class JsDataGridViewRowCollection
{
    private readonly JsEngineHost _host;
    private readonly DataGridView _grid;

    /// <summary>
    /// Creates the facade over the grid's row collection.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="grid">The owning grid.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="grid"/> is <see langword="null"/>.</exception>
    public JsDataGridViewRowCollection(JsEngineHost host, DataGridView grid)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(grid);
        _host = host;
        _grid = grid;
    }

    /// <summary>The number of rows.</summary>
    public int Count => _grid.Rows.Count;

    /// <summary>
    /// Gets the row facade at the given index.
    /// </summary>
    /// <param name="index">The zero-based row index.</param>
    /// <returns>The row facade.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside the collection.</exception>
    public JsDataGridViewRow GetAt(int index) => new(_host, _grid.Rows[index]);

    /// <summary>Removes all rows.</summary>
    public void Clear() => _grid.Rows.Clear();
}
