using System.Collections.ObjectModel;

namespace OldSchoolForms.Ui.Reports;

/// <summary>
/// Defines a named data source of a report: a schema (list of
/// <see cref="ReportDataSourceField"/> entries) plus the runtime data
/// assigned to it. Bands reference a data source by name via
/// <see cref="ReportBand.DataSourceName"/>; the render engine resolves the
/// band's records from <see cref="Data"/>.
/// Only <see cref="Name"/> and <see cref="Fields"/> are persisted in the
/// <c>OldSchoolForms.Report</c> JSON format; <see cref="Data"/> is runtime-only,
/// consistent with the legacy <see cref="Report.DataSource"/>.
/// </summary>
public class ReportDataSource
{
    private readonly List<ReportDataSourceField> _fields = new();

    /// <summary>
    /// Gets or sets the unique name of this data source, referenced by
    /// <see cref="ReportBand.DataSourceName"/> and qualified expressions
    /// (<c>{SourceName.FieldName}</c>).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets the schema fields of this data source.
    /// </summary>
    public IList<ReportDataSourceField> Fields => _fields;

    /// <summary>
    /// Gets or sets the runtime data for this data source.
    /// Must be an IEnumerable (list, array, DataView, or any enumerable
    /// collection of records). Not persisted by the JSON serializer.
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// Initializes a new empty data source.
    /// </summary>
    public ReportDataSource()
    {
    }

    /// <summary>
    /// Initializes a new data source with the given name.
    /// </summary>
    /// <param name="name">The data source name.</param>
    public ReportDataSource(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Adds a schema field to this data source.
    /// </summary>
    /// <param name="name">The field name.</param>
    /// <param name="dataType">The field data type.</param>
    /// <returns>The newly added field.</returns>
    public ReportDataSourceField AddField(string name, ReportDataType dataType = ReportDataType.String)
    {
        var field = new ReportDataSourceField(name, dataType);
        _fields.Add(field);
        return field;
    }

    /// <summary>
    /// Tries to find a schema field by name (case-insensitive).
    /// </summary>
    /// <param name="name">The field name to look up.</param>
    /// <returns>The matching field, or <c>null</c> when not found.</returns>
    public ReportDataSourceField? FindField(string? name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        foreach (var f in _fields)
        {
            if (string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))
                return f;
        }
        return null;
    }
}
