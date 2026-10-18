using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Designer.Services;

namespace OldSchoolForms.Ui.Designer.Reports;

/// <summary>
/// Registers the report control proxies in a <see cref="ToolboxService"/> so the
/// designer toolbox offers the report control palette.
/// </summary>
public static class ReportToolbox
{
    /// <summary>
    /// Registers all report control toolbox items in the given service.
    /// </summary>
    /// <param name="service">The toolbox service to populate.</param>
    public static void RegisterReportItems(ToolboxService service)
    {
        service.Register(new ToolboxItem("Label", "Report", typeof(DesignReportLabel), new Size(190, 23)));
        service.Register(new ToolboxItem("TextBox", "Report", typeof(DesignReportTextBox), new Size(190, 23)));
        service.Register(new ToolboxItem("Image", "Report", typeof(DesignReportImage), new Size(151, 113)));
        service.Register(new ToolboxItem("Line", "Report", typeof(DesignReportLine), new Size(190, 2)));
        service.Register(new ToolboxItem("CheckBox", "Report", typeof(DesignReportCheckBox), new Size(151, 23)));
        service.Register(new ToolboxItem("Chart", "Report", typeof(DesignReportChart), new Size(320, 180)));
        service.Register(new ToolboxItem("CrossTab", "Report", typeof(DesignReportCrossTab), new Size(320, 120)));
    }
}
