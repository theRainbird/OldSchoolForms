namespace OldSchoolForms.Ui.Reports;

/// <summary>
/// Defines a single field (column) of a <see cref="ReportDataSource"/> schema:
/// a field name plus its <see cref="ReportDataType"/>. Only the schema is
/// persisted; actual values come from the data source's runtime data.
/// </summary>
public class ReportDataSourceField
{
    /// <summary>
    /// Gets or sets the field name as referenced by report control bindings
    /// (e.g. <c>DataField</c> or <c>{Field}</c> expressions).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the data type of this field.
    /// </summary>
    public ReportDataType DataType { get; set; } = ReportDataType.String;

    /// <summary>
    /// Gets the CLR type backing this field's <see cref="DataType"/>.
    /// </summary>
    public Type ClrType => DataType.ToClrType();

    /// <summary>
    /// Initializes a new empty data source field.
    /// </summary>
    public ReportDataSourceField()
    {
    }

    /// <summary>
    /// Initializes a new data source field with the given name and type.
    /// </summary>
    /// <param name="name">The field name.</param>
    /// <param name="dataType">The field data type.</param>
    public ReportDataSourceField(string name, ReportDataType dataType = ReportDataType.String)
    {
        Name = name;
        DataType = dataType;
    }
}
