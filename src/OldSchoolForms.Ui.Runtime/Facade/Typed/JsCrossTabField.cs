using Jint.Native.Object;
using OldSchoolForms.Ui.Reports;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// JS facade for a <see cref="CrossTabField"/>.
/// </summary>
public class JsCrossTabField
{
    /// <summary>The wrapped cross-tab field.</summary>
    public CrossTabField Field { get; }

    /// <summary>
    /// Creates a facade around a cross-tab field.
    /// </summary>
    /// <param name="field">The wrapped field.</param>
    /// <exception cref="ArgumentNullException"><paramref name="field"/> is <see langword="null"/>.</exception>
    public JsCrossTabField(CrossTabField field)
    {
        ArgumentNullException.ThrowIfNull(field);
        Field = field;
    }

    /// <summary>The data source field name.</summary>
    public string DataField
    {
        get => Field.DataField;
        set => Field.DataField = value ?? string.Empty;
    }

    /// <summary>The column/row header text.</summary>
    public string HeaderText
    {
        get => Field.HeaderText ?? string.Empty;
        set => Field.HeaderText = string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>
    /// The field usage as a string (<c>RowField</c>, <c>ColumnField</c>,
    /// <c>ValueField</c>). Invalid values are ignored.
    /// </summary>
    public string Usage
    {
        get => Field.Usage.ToString();
        set
        {
            if (Enum.TryParse<FieldUsage>(value, true, out var usage))
                Field.Usage = usage;
        }
    }

    /// <summary>
    /// The aggregation function as a string (<c>Sum</c>, <c>Count</c>,
    /// <c>Avg</c>, <c>Min</c>, <c>Max</c>). Invalid values are ignored.
    /// </summary>
    public string Aggregation
    {
        get => Field.Aggregation.ToString();
        set
        {
            if (Enum.TryParse<CrossTabAggregation>(value, true, out var agg))
                Field.Aggregation = agg;
        }
    }

    /// <summary>The format string for the aggregated value.</summary>
    public string Format
    {
        get => Field.Format ?? string.Empty;
        set => Field.Format = string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>
    /// Applies a JS configuration object to this field.
    /// </summary>
    /// <param name="config">The JS configuration object.</param>
    internal void ApplyConfig(ObjectInstance config)
    {
        if (JsMarshal.GetString(config, "dataField") is { } dataField) DataField = dataField;
        if (JsMarshal.GetString(config, "headerText") is { } header) HeaderText = header;
        if (JsMarshal.GetString(config, "usage") is { } usage) Usage = usage;
        if (JsMarshal.GetString(config, "aggregation") is { } agg) Aggregation = agg;
        if (JsMarshal.GetString(config, "format") is { } format) Format = format;
    }
}
