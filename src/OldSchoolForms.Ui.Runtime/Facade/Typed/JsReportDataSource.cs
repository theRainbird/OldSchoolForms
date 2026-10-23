using Jint;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using OldSchoolForms.Shared.Scripting;
using OldSchoolForms.Ui.Reports;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// JS facade for a <see cref="ReportDataSource"/>. Exposes the source name,
/// its schema fields and the runtime data. Data assignment accepts the same
/// value shapes as <c>report.dataSource</c>: a <c>JsDataTable</c> (default
/// view), a <c>JsDataSet</c> (first table's default view), a JS array of
/// objects (converted to record maps) or any CLR object.
/// </summary>
public class JsReportDataSource
{
    private readonly JsEngineHost _host;
    private readonly ReportDataSource _source;

    /// <summary>The wrapped data source.</summary>
    public ReportDataSource Source => _source;

    /// <summary>
    /// Creates a facade around a data source.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="source">The wrapped data source.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="source"/> is <see langword="null"/>.</exception>
    public JsReportDataSource(JsEngineHost host, ReportDataSource source)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(source);
        _host = host;
        _source = source;
    }

    /// <summary>The data source name.</summary>
    public string Name
    {
        get => _source.Name;
        set => _source.Name = value ?? string.Empty;
    }

    /// <summary>The number of schema fields.</summary>
    public int FieldCount => _source.Fields.Count;

    /// <summary>The schema field names in declaration order.</summary>
    public JsValue FieldNames
        => JsValue.FromObject(_host.Engine, _source.Fields.Select(f => f.Name).ToArray());

    /// <summary>
    /// Gets the schema field name at the given index.
    /// </summary>
    /// <param name="index">The zero-based field index.</param>
    /// <returns>The field name.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside the field list.</exception>
    public string GetFieldNameAt(int index) => _source.Fields[index].Name;

    /// <summary>
    /// Gets the schema field data type at the given index as a lowercase name.
    /// </summary>
    /// <param name="index">The zero-based field index.</param>
    /// <returns>The field data type name (e.g. <c>"decimal"</c>).</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside the field list.</exception>
    public string GetFieldTypeAt(int index) => _source.Fields[index].DataType.ToJsonName();

    /// <summary>
    /// Adds a schema field to this data source.
    /// </summary>
    /// <param name="name">The field name.</param>
    /// <param name="type">The field type name (e.g. <c>"string"</c>, <c>"decimal"</c>); defaults to string.</param>
    /// <returns>This facade for chaining.</returns>
    public JsReportDataSource AddField(string name, JsValue? type = null)
    {
        var dataType = type is null || type.IsUndefined() || type.IsNull()
            ? ReportDataType.String
            : ReportDataTypeMapper.ParseOrString(type.ToString());
        _source.Fields.Add(new ReportDataSourceField(name ?? string.Empty, dataType));
        return this;
    }

    /// <summary>
    /// Removes the schema field at the given index.
    /// </summary>
    /// <param name="index">The zero-based field index.</param>
    /// <returns><see langword="true"/> when a field was removed.</returns>
    public bool RemoveFieldAt(int index)
    {
        if (index < 0 || index >= _source.Fields.Count)
            return false;
        _source.Fields.RemoveAt(index);
        return true;
    }

    /// <summary>
    /// The runtime data of this data source. Accepts a <c>JsDataTable</c>,
    /// a <c>JsDataSet</c>, a JS array of objects or any CLR object.
    /// </summary>
    public JsValue Data
    {
        get => JsValue.FromObject(_host.Engine, _source.Data);
        set => _source.Data = ConvertDataSourceValue(value);
    }

    /// <summary>
    /// Applies an optional JS configuration object to a data source. Recognized
    /// members: <c>fields</c> (array of names or <c>{name, type}</c> objects) and
    /// <c>data</c> (any accepted data shape). When the config itself is a
    /// <c>JsDataTable</c>, its columns are imported as the schema and its default
    /// view as the data.
    /// </summary>
    /// <param name="source">The data source to configure.</param>
    /// <param name="config">The JS configuration value.</param>
    internal static void ApplyConfig(ReportDataSource source, JsValue? config)
    {
        if (config is null || config.IsUndefined() || config.IsNull())
            return;

        if (config.ToObject() is JsDataTable table)
        {
            ImportSchema(source, table.Table);
            source.Data = table.Table.DefaultView;
            return;
        }

        if (config.ToObject() is JsDataSet set && set.Set.Tables.Count > 0)
        {
            ImportSchema(source, set.Set.Tables[0]);
            source.Data = set.Set.Tables[0].DefaultView;
            return;
        }

        if (config is not ObjectInstance obj)
            return;

        if (obj["fields"] is JsArray fields)
        {
            foreach (var fieldNode in fields.ToArray())
            {
                if (fieldNode is null || fieldNode.IsUndefined() || fieldNode.IsNull())
                    continue;

                if (fieldNode is ObjectInstance fo)
                {
                    var name = JsMarshal.GetString(fo, "name") ?? string.Empty;
                    var typeStr = JsMarshal.GetString(fo, "type");
                    var dataType = ReportDataTypeMapper.ParseOrString(typeStr);
                    source.Fields.Add(new ReportDataSourceField(name, dataType));
                }
                else
                {
                    var name = fieldNode.ToString() ?? string.Empty;
                    if (!string.IsNullOrEmpty(name))
                        source.Fields.Add(new ReportDataSourceField(name, ReportDataType.String));
                }
            }
        }

        if (obj["data"] is { } data && !data.IsUndefined() && !data.IsNull())
            source.Data = ConvertDataSourceValue(data);
    }

    private static void ImportSchema(ReportDataSource source, System.Data.DataTable table)
    {
        foreach (System.Data.DataColumn column in table.Columns)
        {
            if (source.FindField(column.ColumnName) != null)
                continue;
            source.Fields.Add(new ReportDataSourceField(
                column.ColumnName,
                ReportDataTypeMapper.FromClrType(column.DataType)));
        }
    }

    /// <summary>
    /// Converts a JS data value to the CLR record collection shape used by
    /// <see cref="Report.DataSource"/> and <see cref="ReportDataSource.Data"/>.
    /// </summary>
    /// <param name="value">The JS value to convert.</param>
    /// <returns>The converted CLR data object, or <c>null</c>.</returns>
    internal static object? ConvertDataSourceValue(JsValue? value)
    {
        if (value is null || value.IsUndefined() || value.IsNull())
            return null;

        if (value.ToObject() is JsDataTable table)
            return table.Table.DefaultView;

        if (value.ToObject() is JsDataSet set)
            return set.Set.Tables.Count > 0 ? set.Set.Tables[0].DefaultView : null;

        if (value is JsArray)
            return JsMarshal.ToRecordList(value);

        return value.ToObject();
    }
}
