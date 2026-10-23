using OldSchoolForms.Ui.Reports;

namespace OldSchoolForms.Ui.Designer.Reports;

/// <summary>
/// Ambient holder for the <see cref="DesignReport"/> that is currently being edited.
/// Value editors that need access to the whole report (field lists, page setup,
/// undo service) resolve it from here because the wrapped <see cref="ReportControl"/>
/// instances have no back-reference to their owning <see cref="Report"/>.
/// </summary>
internal static class ReportDesignContext
{
    /// <summary>
    /// Gets or sets the report root currently hosted by the design surface,
    /// or <see langword="null"/> when no report document is open.
    /// </summary>
    public static DesignReport? Current { get; set; }

    /// <summary>
    /// Collects the field names available for data binding: the explicitly declared
    /// <see cref="Report.DesignFields"/> plus every field referenced by existing
    /// report controls (data fields, expression fields, group fields, crosstab fields).
    /// </summary>
    /// <param name="report">The report to collect fields from.</param>
    /// <returns>A sorted, case-insensitively deduplicated list of field names.</returns>
    public static IReadOnlyList<string> GetAvailableFields(Report report)
    {
        var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in report.DesignFields)
        {
            if (!string.IsNullOrWhiteSpace(field)) set.Add(field.Trim());
        }

        foreach (var field in DesignRecord.Build(report).Keys)
        {
            if (!string.IsNullOrWhiteSpace(field)) set.Add(field);
        }

        return set.ToList();
    }

    /// <summary>
    /// Resolves the data source a band binds to: the named source when found,
    /// otherwise the first declared source. Returns <see langword="null"/> when
    /// the report has no data source definitions (legacy mode).
    /// </summary>
    /// <param name="report">The report to resolve against.</param>
    /// <param name="band">The band whose <see cref="ReportBand.DataSourceName"/> is resolved.</param>
    /// <returns>The resolved data source, or <see langword="null"/> for legacy mode.</returns>
    public static ReportDataSource? ResolveBandSource(Report report, ReportBand? band)
    {
        if (report.DataSources.Count == 0) return null;
        var name = band?.DataSourceName;
        if (!string.IsNullOrEmpty(name))
        {
            var named = report.DataSources[name];
            if (named != null) return named;
        }
        return report.DataSources[0];
    }

    /// <summary>
    /// Gets the field names available for binding within a specific band. When the
    /// report declares data sources, only the fields of the band's resolved source
    /// are offered; otherwise the report-wide available fields are returned.
    /// </summary>
    /// <param name="report">The report to collect fields from.</param>
    /// <param name="band">The band whose controls are being bound.</param>
    /// <returns>A sorted, case-insensitively deduplicated list of field names.</returns>
    public static IReadOnlyList<string> GetFieldsForBand(Report report, ReportBand? band)
    {
        var source = ResolveBandSource(report, band);
        if (source == null)
            return GetAvailableFields(report);

        var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in source.Fields)
        {
            if (!string.IsNullOrWhiteSpace(field.Name)) set.Add(field.Name.Trim());
        }
        return set.ToList();
    }

    /// <summary>
    /// Gets the qualified <c>Source.Field</c> names across all declared data
    /// sources, for insertion into cross-source expressions.
    /// </summary>
    /// <param name="report">The report to collect qualified names from.</param>
    /// <returns>A sorted list of <c>Source.Field</c> names.</returns>
    public static IReadOnlyList<string> GetQualifiedFields(Report report)
    {
        var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in report.DataSources)
        {
            if (string.IsNullOrWhiteSpace(source.Name)) continue;
            foreach (var field in source.Fields)
            {
                if (!string.IsNullOrWhiteSpace(field.Name))
                    set.Add(source.Name + "." + field.Name);
            }
        }
        return set.ToList();
    }

    /// <summary>
    /// Finds the band that currently contains the given report control by walking
    /// the design surface's band containers for the matching proxy.
    /// </summary>
    /// <param name="report">The report the control belongs to.</param>
    /// <param name="control">The report control to locate.</param>
    /// <returns>The owning band, or <see langword="null"/> when not found.</returns>
    public static ReportBand? GetBandForControl(Report report, ReportControl control)
    {
        var root = Current;
        if (root == null) return null;
        foreach (var band in root.GetBands())
        {
            var container = root.GetBandContainer(band);
            if (container == null) continue;
            foreach (var proxy in container.Controls.OfType<DesignReportControl>())
            {
                if (ReferenceEquals(proxy.Wrapped, control))
                    return band;
            }
        }
        return null;
    }
}
