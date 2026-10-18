using OldSchoolForms.Ui.Reports;
using OldSchoolForms.Ui.Core;

namespace OldSchoolForms.Ui.Designer.Reports;

/// <summary>
/// Design-time proxy for a <see cref="ReportLabel"/>.
/// </summary>
public class DesignReportLabel : DesignReportControl
{
    /// <summary>
    /// Creates a new label proxy with a default report label.
    /// </summary>
    public DesignReportLabel()
        : this(new ReportLabel { Text = LangRes.GetString("ReportDesigner_DefaultLabel") })
    {
    }

    /// <summary>
    /// Wraps an existing report label.
    /// </summary>
    /// <param name="label">The report label to edit.</param>
    internal DesignReportLabel(ReportLabel label)
        : base(label)
    {
    }

    /// <summary>
    /// Gets the wrapped report label.
    /// </summary>
    public new ReportLabel Label => (ReportLabel)Wrapped;
}
