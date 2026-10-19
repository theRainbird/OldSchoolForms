using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Reports;

namespace OldSchoolForms.Ui.Designer.Reports;

/// <summary>
/// Design-time proxy for a data-bound <see cref="ReportTextBox"/>.
/// </summary>
[ToolboxIcon("OldSchoolForms.Ui.Resources.Icons.Controls.ctrl_textbox.svg")]
public class DesignReportTextBox : DesignReportControl
{
    /// <summary>
    /// Creates a new text box proxy with a default report text box.
    /// </summary>
    public DesignReportTextBox()
        : this(new ReportTextBox())
    {
    }

    /// <summary>
    /// Wraps an existing report text box.
    /// </summary>
    /// <param name="textBox">The report text box to edit.</param>
    internal DesignReportTextBox(ReportTextBox textBox)
        : base(textBox)
    {
    }

    /// <summary>
    /// Gets the wrapped report text box.
    /// </summary>
    public new ReportTextBox TextBox => (ReportTextBox)Wrapped;
}
