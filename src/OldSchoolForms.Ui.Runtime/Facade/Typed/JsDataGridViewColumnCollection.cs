using Jint;
using Jint.Native;
using Jint.Native.Object;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// JS facade for the columns of a <see cref="DataGridView"/>. Columns are added
/// via configuration objects, e.g.
/// <c>grid.columns.add({ headerText: 'Name', dataPropertyName: 'name', editType: 'TextBox' })</c>.
/// </summary>
public class JsDataGridViewColumnCollection
{
    private readonly JsEngineHost _host;
    private readonly DataGridView _grid;

    /// <summary>
    /// Creates the facade over the grid's column collection.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="grid">The owning grid.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="grid"/> is <see langword="null"/>.</exception>
    public JsDataGridViewColumnCollection(JsEngineHost host, DataGridView grid)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(grid);
        _host = host;
        _grid = grid;
    }

    /// <summary>The number of columns.</summary>
    public int Count => _grid.Columns.Count;

    /// <summary>
    /// Gets the column facade at the given index.
    /// </summary>
    /// <param name="index">The zero-based column index.</param>
    /// <returns>The column facade.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside the collection.</exception>
    public JsDataGridViewColumn GetAt(int index) => new(_host, _grid.Columns[index]);

    /// <summary>
    /// Adds a column from a JS configuration object.
    /// </summary>
    /// <param name="config">The configuration object (see <see cref="JsDataGridViewColumn.ApplyConfig"/>).</param>
    /// <returns>The new column facade.</returns>
    /// <exception cref="ArgumentException">The config is not an object.</exception>
    public JsDataGridViewColumn Add(JsValue config)
    {
        if (config is not ObjectInstance obj)
            throw new ArgumentException("Column configuration must be an object.", nameof(config));
        var column = new DataGridViewColumn();
        var facade = new JsDataGridViewColumn(_host, column);
        facade.ApplyConfig(obj);
        _grid.Columns.Add(column);
        return facade;
    }

    /// <summary>
    /// Removes the column at the given index.
    /// </summary>
    /// <param name="index">The zero-based column index.</param>
    /// <returns><see langword="true"/> when a column was removed.</returns>
    public bool RemoveAt(int index)
    {
        if (index < 0 || index >= _grid.Columns.Count)
            return false;
        _grid.Columns.Remove(_grid.Columns[index]);
        return true;
    }

    /// <summary>Removes all columns.</summary>
    public void Clear() => _grid.Columns.Clear();
}
