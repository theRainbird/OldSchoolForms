using System.Data;
using Jint;
using Jint.Native;

namespace OldSchoolForms.Shared.Scripting;

/// <summary>
/// JS-facing facade over a <see cref="System.Data.DataSet"/>. Mirrors
/// <see cref="JsDataTable"/> for a set of related tables (including
/// relations, which survive the DiffGram transfer). Used identically on
/// client and server.
/// </summary>
public sealed class JsDataSet
{
    private readonly Engine _engine;

    /// <summary>
    /// Creates a facade for the given set.
    /// </summary>
    /// <param name="engine">The JS engine this facade belongs to.</param>
    /// <param name="set">The underlying data set.</param>
    public JsDataSet(Engine engine, DataSet set)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(set);
        _engine = engine;
        Set = set;
    }

    /// <summary>The underlying CLR set. Trusted code only (not visible to JS).</summary>
    internal DataSet Set { get; }

    /// <summary>Gets or sets the set name.</summary>
    public string Name
    {
        get => Set.DataSetName;
        set => Set.DataSetName = value ?? string.Empty;
    }

    /// <summary>The table names in order.</summary>
    public string[] TableNames
    {
        get
        {
            var names = new string[Set.Tables.Count];
            for (var i = 0; i < names.Length; i++)
                names[i] = Set.Tables[i].TableName;
            return names;
        }
    }

    /// <summary>Number of tables.</summary>
    public int TableCount => Set.Tables.Count;

    /// <summary>
    /// Gets a table facade by name.
    /// </summary>
    /// <param name="name">The table name.</param>
    /// <returns>The table facade.</returns>
    public JsDataTable GetTable(string name)
    {
        var table = Set.Tables[name]
            ?? throw new InvalidOperationException($"Unknown table '{name}' in set '{Set.DataSetName}'.");
        return new JsDataTable(_engine, table);
    }

    /// <summary>
    /// Gets a table facade by index.
    /// </summary>
    /// <param name="index">The zero-based table index.</param>
    /// <returns>The table facade.</returns>
    public JsDataTable GetTableAt(int index) => new(_engine, Set.Tables[index]);

    /// <summary>
    /// Adds a copy of another table to this set. The source table keeps
    /// belonging to its own container.
    /// </summary>
    /// <param name="table">The table facade to add.</param>
    public void AddTable(JsDataTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (table.Table.DataSet == Set) return;

        if (Set.Tables.Contains(table.Table.TableName))
            Set.Merge(table.Table);
        else
            Set.Tables.Add(table.Table.Copy());
    }

    /// <summary>
    /// Merges another set into this one, preserving row states.
    /// </summary>
    /// <param name="other">The set facade to merge in.</param>
    public void Merge(JsDataSet other)
    {
        ArgumentNullException.ThrowIfNull(other);
        Set.Merge(other.Set);
    }

    /// <summary>
    /// Returns a new set facade containing only the changed tables/rows.
    /// </summary>
    /// <returns>A facade over the changes set.</returns>
    public JsDataSet GetChanges()
    {
        var changes = Set.GetChanges();
        if (changes != null) return new JsDataSet(_engine, changes);

        var empty = new DataSet(Set.DataSetName);
        foreach (DataTable table in Set.Tables)
            empty.Tables.Add(table.Clone());
        return new JsDataSet(_engine, empty);
    }

    /// <summary>
    /// Accepts all pending changes in all tables.
    /// </summary>
    public void AcceptChanges() => Set.AcceptChanges();
}
