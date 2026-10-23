using System.Collections.ObjectModel;

namespace OldSchoolForms.Ui.Reports;

/// <summary>
/// An ordered collection of <see cref="ReportDataSource"/> definitions of a report,
/// with case-insensitive lookup by name.
/// </summary>
public class ReportDataSourceCollection : Collection<ReportDataSource>
{
    /// <summary>
    /// Initializes a new empty data source collection.
    /// </summary>
    public ReportDataSourceCollection()
    {
    }

    /// <summary>
    /// Initializes a new data source collection with the specified sources.
    /// </summary>
    /// <param name="sources">The data sources to add.</param>
    public ReportDataSourceCollection(IEnumerable<ReportDataSource> sources)
    {
        foreach (var s in sources)
            Add(s);
    }

    /// <summary>
    /// Gets the data source with the given name (case-insensitive).
    /// </summary>
    /// <param name="name">The data source name to look up.</param>
    /// <returns>The matching data source, or <c>null</c> when not found.</returns>
    public ReportDataSource? this[string? name]
    {
        get
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var s in this)
            {
                if (string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
                    return s;
            }
            return null;
        }
    }

    /// <summary>
    /// Adds a new data source with the given name to the collection.
    /// </summary>
    /// <param name="name">The data source name.</param>
    /// <returns>The newly added data source.</returns>
    public ReportDataSource Add(string name)
    {
        var source = new ReportDataSource(name);
        Add(source);
        return source;
    }

    /// <summary>
    /// Determines whether a data source with the given name exists (case-insensitive).
    /// </summary>
    /// <param name="name">The data source name to look up.</param>
    /// <returns><c>true</c> when a data source with that name exists.</returns>
    public bool Contains(string? name) => this[name] != null;

    /// <summary>
    /// Removes the data source with the given name (case-insensitive).
    /// </summary>
    /// <param name="name">The data source name to remove.</param>
    /// <returns><c>true</c> when the data source was found and removed.</returns>
    public bool Remove(string? name)
    {
        var source = this[name];
        return source != null && Remove(source);
    }
}
