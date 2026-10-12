using Jint;
using Jint.Native;
using Jint.Native.Object;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// JS facade for the columns of a <see cref="KanbanBoardView"/>. Columns are
/// added via configuration objects, e.g.
/// <c>board.columns.add({ name: 'Todo', stateValue: 'todo', width: 280 })</c>.
/// </summary>
public class JsKanbanColumnCollection
{
    private readonly JsEngineHost _host;
    private readonly KanbanBoardView _board;

    /// <summary>
    /// Creates the facade over the board's column collection.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="board">The owning kanban board.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="board"/> is <see langword="null"/>.</exception>
    public JsKanbanColumnCollection(JsEngineHost host, KanbanBoardView board)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(board);
        _host = host;
        _board = board;
    }

    /// <summary>The number of columns.</summary>
    public int Count => _board.Columns.Count;

    /// <summary>
    /// Gets the column facade at the given index.
    /// </summary>
    /// <param name="index">The zero-based column index.</param>
    /// <returns>The column facade.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside the collection.</exception>
    public JsKanbanColumn GetAt(int index) => new(_host, _board.Columns[index]);

    /// <summary>
    /// Adds a column from a JS configuration object.
    /// </summary>
    /// <param name="config">The configuration object (see <see cref="JsKanbanColumn.ApplyConfig"/>).</param>
    /// <returns>The new column facade.</returns>
    /// <exception cref="ArgumentException">The config is not an object.</exception>
    public JsKanbanColumn Add(JsValue config)
    {
        if (config is not ObjectInstance obj)
            throw new ArgumentException("Column configuration must be an object.", nameof(config));
        var column = new KanbanBoardColumn();
        var facade = new JsKanbanColumn(_host, column);
        facade.ApplyConfig(obj);
        _board.Columns.Add(column);
        return facade;
    }

    /// <summary>
    /// Removes the column at the given index.
    /// </summary>
    /// <param name="index">The zero-based column index.</param>
    /// <returns><see langword="true"/> when a column was removed.</returns>
    public bool RemoveAt(int index)
    {
        if (index < 0 || index >= _board.Columns.Count)
            return false;
        _board.Columns.Remove(_board.Columns[index]);
        return true;
    }

    /// <summary>Removes all columns.</summary>
    public void Clear() => _board.Columns.Clear();
}
