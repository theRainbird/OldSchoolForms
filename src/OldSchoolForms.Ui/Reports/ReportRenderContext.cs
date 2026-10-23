using System.Collections;
using OldSchoolForms.Ui.Data;
using OldSchoolForms.Ui.Rendering;

namespace OldSchoolForms.Ui.Reports;

/// <summary>
/// Provides contextual information during report rendering, including
/// the current record, page state, aggregation variables, and render resolution.
/// </summary>
public class ReportRenderContext
{
    private readonly Dictionary<string, object?> _firstRecordCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the report definition being rendered.
    /// </summary>
    public Report Report { get; }

    /// <summary>
    /// Gets or sets the current data record being processed.
    /// </summary>
    public object? CurrentRecord { get; set; }

    /// <summary>
    /// Gets or sets the name of the data source the current record belongs to.
    /// Set by the render engine when binding a band; used to decide whether a
    /// qualified <c>{Source.Field}</c> expression resolves against the current
    /// record or against the first record of another source.
    /// </summary>
    public string? CurrentSourceName { get; set; }

    /// <summary>
    /// Gets or sets the zero-based index of the current record.
    /// </summary>
    public int CurrentRowIndex { get; set; }

    /// <summary>
    /// Gets or sets the current page number (1-based).
    /// </summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// Gets or sets the total number of pages.
    /// </summary>
    public int TotalPages { get; set; }

    /// <summary>
    /// Gets or sets the current group (when processing grouped reports).
    /// </summary>
    public ReportGroup? CurrentGroup { get; set; }

    /// <summary>
    /// Gets or sets the field value of the current group.
    /// Set by the render engine before rendering group headers/footers.
    /// Used by <see cref="ReportCrossTab"/> to filter records.
    /// </summary>
    public object? CurrentGroupValue { get; set; }

    /// <summary>
    /// Dictionary of named variables for aggregations (sum, count, etc.).
    /// </summary>
    public Dictionary<string, object> Variables { get; } = new();

    /// <summary>
    /// Gets or sets the render resolution in DPI.
    /// 96 for screen preview, 72 for PDF export.
    /// </summary>
    public float RenderDpi { get; set; } = 96f;

    /// <summary>
    /// Gets the Graphics.MeasureText callback, if available.
    /// </summary>
    public Graphics.MeasureTextCallback? MeasureTextCallback { get; set; }

    /// <summary>
    /// Initializes a new render context for the given report.
    /// </summary>
    public ReportRenderContext(Report report)
    {
        Report = report;
    }

    /// <summary>
    /// Gets the value of a named field from the current record.
    /// Supports special variables: PageNumber, TotalPages, CurrentRowIndex.
    /// Supports qualified names <c>SourceName.FieldName</c>: if the prefix names a
    /// data source of the report, the field resolves against the current record when
    /// the prefix matches <see cref="CurrentSourceName"/>, otherwise against the
    /// first record of that data source. An unknown prefix is treated as part of a
    /// plain field name for backward compatibility.
    /// Field lookup uses <see cref="DataRecord.GetValue"/> (case-insensitive,
    /// dictionary- and <c>ICustomTypeDescriptor</c>-aware).
    /// </summary>
    /// <param name="fieldName">The name of the field, or a qualified <c>Source.Field</c> name.</param>
    /// <returns>The field value, or null if not found.</returns>
    public object? GetFieldValue(string fieldName)
    {
        if (string.IsNullOrEmpty(fieldName)) return null;
        if (string.Equals(fieldName, "PageNumber", StringComparison.OrdinalIgnoreCase))
            return PageNumber;
        if (string.Equals(fieldName, "TotalPages", StringComparison.OrdinalIgnoreCase))
            return TotalPages;
        if (string.Equals(fieldName, "CurrentRowIndex", StringComparison.OrdinalIgnoreCase))
            return CurrentRowIndex;

        int dot = fieldName.IndexOf('.');
        if (dot > 0 && dot < fieldName.Length - 1)
        {
            var sourceName = fieldName.Substring(0, dot);
            var fieldPart = fieldName.Substring(dot + 1);
            var source = Report.DataSources[sourceName];
            if (source != null)
            {
                if (string.Equals(source.Name, CurrentSourceName, StringComparison.OrdinalIgnoreCase))
                    return DataRecord.GetValue(CurrentRecord, fieldPart);
                return DataRecord.GetValue(FirstRecordOf(sourceName), fieldPart);
            }
            // Unknown source prefix: fall through and treat the whole token as a field name.
        }

        return DataRecord.GetValue(CurrentRecord, fieldName);
    }

    /// <summary>
    /// Resolves the effective data source for a band: the named source when found,
    /// otherwise the first defined source. Returns <c>null</c> when the report has
    /// no data source definitions (legacy mode using <see cref="Report.DataSource"/>).
    /// </summary>
    /// <param name="dataSourceName">The band's data source name (may be null or empty).</param>
    /// <returns>The resolved data source, or <c>null</c> for legacy mode.</returns>
    public ReportDataSource? ResolveSource(string? dataSourceName)
    {
        if (Report.DataSources.Count == 0) return null;
        if (!string.IsNullOrEmpty(dataSourceName))
        {
            var named = Report.DataSources[dataSourceName];
            if (named != null) return named;
        }
        return Report.DataSources[0];
    }

    /// <summary>
    /// Gets the first record of the data source resolved for the given name,
    /// falling back to the legacy <see cref="Report.DataSource"/> when the
    /// resolved source carries no data. The result is cached per source name.
    /// </summary>
    /// <param name="dataSourceName">The data source name to look up (may be null or empty).</param>
    /// <returns>The first record, or <c>null</c> when the source is empty.</returns>
    internal object? FirstRecordOf(string? dataSourceName)
    {
        var source = ResolveSource(dataSourceName);
        var key = source?.Name ?? "\0legacy";
        if (_firstRecordCache.TryGetValue(key, out var cached))
            return cached;

        var data = source?.Data ?? Report.DataSource;
        object? first = null;
        if (data is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (item != null)
                {
                    first = item;
                    break;
                }
            }
        }

        _firstRecordCache[key] = first;
        return first;
    }

    /// <summary>
    /// Seeds the cached first record for a data source. Used by the designer to
    /// install synthetic placeholder records so qualified cross-source
    /// expressions (<c>{Source.Field}</c>) render readable placeholders at
    /// design time when no runtime data is attached.
    /// </summary>
    /// <param name="sourceName">The data source name to seed.</param>
    /// <param name="record">The synthetic record to return for that source.</param>
    public void SeedFirstRecord(string sourceName, object? record)
    {
        if (string.IsNullOrEmpty(sourceName)) return;
        _firstRecordCache[sourceName] = record;
    }
}
