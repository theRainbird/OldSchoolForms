using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Reports;

namespace OldSchoolForms.Ui.Designer.Reports;

/// <summary>
/// Design-time proxy for a <see cref="ReportCrossTab"/>. Renders the live cross-tab via the
/// shared WYSIWYG offscreen render pipeline and exposes the cross-tab model for editing.
/// </summary>
[ToolboxIcon("OldSchoolForms.Ui.Resources.Icons.Controls.ctrl_crosstab.svg")]
public class DesignReportCrossTab : DesignReportControl
{
    /// <summary>
    /// Initializes a new cross-tab proxy with an empty field list.
    /// </summary>
    public DesignReportCrossTab()
        : this(new ReportCrossTab())
    {
    }

    /// <summary>
    /// Initializes a new cross-tab proxy wrapping an existing cross-tab model.
    /// </summary>
    /// <param name="crossTab">The cross-tab model to wrap.</param>
    internal DesignReportCrossTab(ReportCrossTab crossTab)
        : base(crossTab)
    {
    }

    /// <summary>
    /// Gets the wrapped cross-tab model.
    /// </summary>
    public ReportCrossTab CrossTab => (ReportCrossTab)Wrapped;
}
