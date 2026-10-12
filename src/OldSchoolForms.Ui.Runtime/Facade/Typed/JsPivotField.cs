using Jint;
using Jint.Native.Object;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// JS facade for a <see cref="PivotTableField"/>.
/// </summary>
public class JsPivotField
{
    /// <summary>The JS engine used for callbacks.</summary>
    protected readonly JsEngineHost Host;

    /// <summary>The wrapped pivot field.</summary>
    public PivotTableField Field { get; }

    /// <summary>
    /// Creates a facade around a pivot field.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="field">The wrapped field.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="field"/> is <see langword="null"/>.</exception>
    public JsPivotField(JsEngineHost host, PivotTableField field)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(field);
        Host = host;
        Field = field;
    }

    /// <summary>The field display name.</summary>
    public string Name
    {
        get => Field.Name;
        set => Field.Name = value ?? string.Empty;
    }

    /// <summary>The data source property bound to this field.</summary>
    public string DataPropertyName
    {
        get => Field.DataPropertyName;
        set => Field.DataPropertyName = value ?? string.Empty;
    }

    /// <summary>
    /// The field usage as a string (<c>RowField</c>, <c>ColumnField</c>,
    /// <c>ValueField</c>, <c>FilterField</c>). Invalid values are ignored.
    /// </summary>
    public string Usage
    {
        get => Field.Usage.ToString();
        set
        {
            if (Enum.TryParse<PivotTableFieldUsage>(value, true, out var usage))
                Field.Usage = usage;
        }
    }

    /// <summary>
    /// The aggregation for value fields as a string (<c>None</c>, <c>Sum</c>,
    /// <c>Count</c>, <c>Average</c>, <c>Min</c>, <c>Max</c>). Invalid values are ignored.
    /// </summary>
    public string Aggregation
    {
        get => Field.Aggregation.ToString();
        set
        {
            if (Enum.TryParse<PivotTableAggregation>(value, true, out var aggregation))
                Field.Aggregation = aggregation;
        }
    }

    /// <summary>The format string applied to values (e.g. <c>N2</c>).</summary>
    public string Format
    {
        get => Field.FormatString ?? string.Empty;
        set => Field.FormatString = string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>The column width in pixels.</summary>
    public int Width
    {
        get => Field.Width;
        set => Field.Width = value;
    }

    /// <summary>Whether the field is visible.</summary>
    public bool Visible
    {
        get => Field.Visible;
        set => Field.Visible = value;
    }

    /// <summary>Whether the field's hierarchy nodes are expanded.</summary>
    public bool IsExpanded
    {
        get => Field.IsExpanded;
        set => Field.IsExpanded = value;
    }

    /// <summary>Display values as a percentage of the row total.</summary>
    public bool ShowAsPercentageOfRow
    {
        get => Field.ShowAsPercentageOfRow;
        set => Field.ShowAsPercentageOfRow = value;
    }

    /// <summary>Display values as a percentage of the column total.</summary>
    public bool ShowAsPercentageOfColumn
    {
        get => Field.ShowAsPercentageOfColumn;
        set => Field.ShowAsPercentageOfColumn = value;
    }

    /// <summary>Display values as a percentage of the grand total.</summary>
    public bool ShowAsPercentageOfGrandTotal
    {
        get => Field.ShowAsPercentageOfGrandTotal;
        set => Field.ShowAsPercentageOfGrandTotal = value;
    }

    /// <summary>The field's sort order within its usage group.</summary>
    public int Order
    {
        get => Field.Order;
        set => Field.Order = value;
    }

    /// <summary>
    /// Applies a JS configuration object to this field. Recognized members:
    /// <c>name</c>, <c>dataPropertyName</c>, <c>usage</c>, <c>aggregation</c>,
    /// <c>format</c>, <c>width</c>, <c>visible</c>.
    /// </summary>
    /// <param name="config">The JS configuration object.</param>
    internal void ApplyConfig(ObjectInstance config)
    {
        if (JsMarshal.GetString(config, "name") is { } name) Name = name;
        if (JsMarshal.GetString(config, "dataPropertyName") is { } dataProp) DataPropertyName = dataProp;
        if (JsMarshal.GetString(config, "usage") is { } usage) Usage = usage;
        if (JsMarshal.GetString(config, "aggregation") is { } aggregation) Aggregation = aggregation;
        if (JsMarshal.GetString(config, "format") is { } format) Format = format;
        if (JsMarshal.GetInt(config, "width") is { } width) Width = width;
        if (JsMarshal.GetBool(config, "visible") is { } visible) Visible = visible;
    }
}
