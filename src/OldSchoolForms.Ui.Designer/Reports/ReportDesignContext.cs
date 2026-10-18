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
}
