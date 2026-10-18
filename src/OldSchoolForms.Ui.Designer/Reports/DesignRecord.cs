using System.Collections.Generic;
using OldSchoolForms.Ui.Reports;

namespace OldSchoolForms.Ui.Designer.Reports;

/// <summary>
/// Synthetic design-time data record used to render report controls WYSIWYG on the
/// design surface. Field values are the field names themselves so bound controls
/// show a readable placeholder for every referenced field.
/// </summary>
internal sealed class DesignRecord : Dictionary<string, object>
{
    /// <summary>
    /// Builds a record covering every field referenced by the report's controls and
    /// groups.
    /// </summary>
    /// <param name="report">The report whose field references are collected.</param>
    /// <returns>A record mapping each referenced field name to itself.</returns>
    public static DesignRecord Build(Report report)
    {
        var record = new DesignRecord();
        foreach (var band in EnumerateBands(report))
        {
            foreach (var control in band.Controls)
                CollectFrom(control, record);
        }
        CollectFromGroups(report.Groups, record);
        return record;
    }

    private static void CollectFromGroups(ReportGroupCollection groups, DesignRecord record)
    {
        foreach (var group in groups)
        {
            if (!string.IsNullOrEmpty(group.GroupField))
                record[group.GroupField] = group.GroupField;
            CollectFromGroups(group.Groups, record);
        }
    }

    private static void CollectFrom(ReportControl control, DesignRecord record)
    {
        switch (control)
        {
            case ReportTextBox textBox:
                if (!string.IsNullOrEmpty(textBox.DataField))
                    record[textBox.DataField] = textBox.DataField;
                if (!string.IsNullOrEmpty(textBox.Expression))
                    foreach (var field in ExpressionFields(textBox.Expression))
                        record[field] = field;
                break;

            case ReportCheckBox checkBox:
                if (!string.IsNullOrEmpty(checkBox.DataField))
                    record[checkBox.DataField] = checkBox.DataField;
                break;

            case ReportCrossTab crossTab:
                if (!string.IsNullOrEmpty(crossTab.GroupField))
                    record[crossTab.GroupField] = crossTab.GroupField;
                foreach (var field in crossTab.Fields)
                    if (!string.IsNullOrEmpty(field.DataField))
                        record[field.DataField] = field.DataField;
                break;
        }
    }

    /// <summary>
    /// Extracts the field names referenced by <c>{Field}</c> or <c>{Field:format}</c>
    /// tokens in an expression template.
    /// </summary>
    /// <param name="expression">The expression template to scan.</param>
    /// <returns>The referenced field names.</returns>
    public static IEnumerable<string> ExpressionFields(string expression)
    {
        int start = expression.IndexOf('{');
        while (start >= 0)
        {
            int end = expression.IndexOf('}', start);
            if (end < 0) yield break;
            string token = expression.Substring(start + 1, end - start - 1);
            int colon = token.IndexOf(':');
            string field = colon >= 0 ? token[..colon] : token;
            if (!string.IsNullOrWhiteSpace(field))
                yield return field.Trim();
            start = expression.IndexOf('{', end + 1);
        }
    }

    private static IEnumerable<ReportBand> EnumerateBands(Report report)
    {
        yield return report.ReportHeader;
        yield return report.PageHeader;
        yield return report.Detail;
        yield return report.PageFooter;
        yield return report.ReportFooter;
        foreach (var band in EnumerateGroupBands(report.Groups))
            yield return band;
    }

    private static IEnumerable<ReportBand> EnumerateGroupBands(ReportGroupCollection groups)
    {
        foreach (var group in groups)
        {
            yield return group.Header;
            yield return group.Footer;
            foreach (var band in EnumerateGroupBands(group.Groups))
                yield return band;
        }
    }
}
