using OldSchoolForms.Ui.Reports;
using OldSchoolForms.Ui.Core;

namespace OldSchoolForms.Ui.Designer.Reports;

/// <summary>
/// Design-time proxy for a data-bound <see cref="ReportCheckBox"/>.
/// </summary>
public class DesignReportCheckBox : DesignReportControl
{
    /// <summary>
    /// Creates a new check box proxy with a default report check box.
    /// </summary>
    public DesignReportCheckBox()
        : this(new ReportCheckBox { Caption = LangRes.GetString("ReportDesigner_DefaultCheckBox") })
    {
    }

    /// <summary>
    /// Wraps an existing report check box.
    /// </summary>
    /// <param name="checkBox">The report check box to edit.</param>
    internal DesignReportCheckBox(ReportCheckBox checkBox)
        : base(checkBox)
    {
    }

    /// <summary>
    /// Gets the wrapped report check box.
    /// </summary>
    public new ReportCheckBox CheckBox => (ReportCheckBox)Wrapped;
}
