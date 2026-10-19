using System.Linq;
using OldSchoolForms.Ui.Designer.Reports;
using OldSchoolForms.Ui.Designer.Services;
using Xunit;

namespace OldSchoolForms.Ui.Designer.Tests;

/// <summary>
/// Ensures every report control offered in the report designer toolbox carries a
/// resolvable icon, analogous to the standard form controls.
/// </summary>
public class ReportToolboxIconTests
{
    [Fact]
    public void AllReportToolboxItems_HaveIconResource()
    {
        var service = new ToolboxService();
        service.Clear();
        ReportToolbox.RegisterReportItems(service);

        Assert.NotEmpty(service.Items);
        Assert.All(service.Items, i => Assert.False(string.IsNullOrEmpty(i.IconResourceName)));
        Assert.All(service.Items, i => Assert.NotNull(i.GetIcon()));
    }

    [Fact]
    public void ReportToolboxItems_IconResourcesUseControlsIconNamespace()
    {
        var service = new ToolboxService();
        service.Clear();
        ReportToolbox.RegisterReportItems(service);

        Assert.All(service.Items,
            i => Assert.StartsWith("OldSchoolForms.Ui.Resources.Icons.Controls.", i.IconResourceName));
    }
}
