using System;
using System.Linq;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Designer.Reports;
using OldSchoolForms.Ui.Reports;
using Xunit;

namespace OldSchoolForms.Ui.Designer.Tests;

public class ReportDesignerTests
{
    private static (DesignSurface Surface, DesignReport Root, Report Report) CreateReportSurface()
    {
        var report = new Report("TestReport");
        var surface = new DesignSurface { SnapToGrid = false };
        surface.BeginDesignReport(report);
        var root = Assert.IsType<DesignReport>(surface.RootControl);
        return (surface, root, report);
    }

    [Fact]
    public void BeginDesignReport_CreatesBandContainers_ForAllFiveFixedBands()
    {
        var (_, root, report) = CreateReportSurface();

        Assert.NotNull(root.GetBandContainer(report.ReportHeader));
        Assert.NotNull(root.GetBandContainer(report.PageHeader));
        Assert.NotNull(root.GetBandContainer(report.Detail));
        Assert.NotNull(root.GetBandContainer(report.PageFooter));
        Assert.NotNull(root.GetBandContainer(report.ReportFooter));
        Assert.Same(root, ReportDesignContext.Current);
    }

    [Fact]
    public void AddProxy_IntoBandContainer_RegistersWrappedControl_InBandModel()
    {
        var (surface, root, report) = CreateReportSurface();
        var detail = root.GetBandContainer(report.Detail)!;

        var proxy = new DesignReportLabel();
        surface.AddControl(proxy, detail, new Point(10, 5));

        Assert.Contains(proxy.Wrapped, report.Detail.Controls);
        Assert.Equal(root.PxToCm(10).Value, proxy.Wrapped.Left.Value, 1);
        Assert.Equal(root.PxToCm(5).Value, proxy.Wrapped.Top.Value, 1);
    }

    [Fact]
    public void ProxyMove_WritesBackCentimeters_ToWrappedModel()
    {
        var (surface, root, report) = CreateReportSurface();
        var detail = root.GetBandContainer(report.Detail)!;
        var proxy = new DesignReportTextBox();
        surface.AddControl(proxy, detail, new Point(0, 0));

        proxy.Bounds = new Rectangle(96, 48, 192, 24);

        Assert.Equal(root.PxToCm(96).Value, proxy.Wrapped.Left.Value, 1);
        Assert.Equal(root.PxToCm(48).Value, proxy.Wrapped.Top.Value, 1);
        Assert.Equal(root.PxToCm(192).Value, proxy.Wrapped.Width.Value, 1);
        Assert.Equal(root.PxToCm(24).Value, proxy.Wrapped.Height.Value, 1);
    }

    [Fact]
    public void ModelControlAddedDirectly_SyncCreatesProxy()
    {
        var (_, root, report) = CreateReportSurface();
        var detail = root.GetBandContainer(report.Detail)!;
        var extra = new ReportLabel { Name = "extraLabel", Text = "X" };

        report.Detail.Controls.Add(extra);
        root.SyncControlProxies();

        Assert.Contains(detail.Controls.OfType<DesignReportControl>(), p => ReferenceEquals(p.Wrapped, extra));
    }

    [Fact]
    public void DeleteProxy_DetachesWrappedFromBandModel()
    {
        var (surface, _, report) = CreateReportSurface();
        var detail = ((DesignReport)surface.RootControl).GetBandContainer(report.Detail)!;
        var proxy = new DesignReportCheckBox();
        surface.AddControl(proxy, detail, new Point(0, 0));
        Assert.Single(report.Detail.Controls);

        surface.RemoveControl(surface.FindItem(proxy)!);

        Assert.Empty(report.Detail.Controls);
        Assert.Null(surface.FindItem(proxy));
    }

    [Fact]
    public void AddGroup_RefreshGeometry_CreatesGroupBandContainers_WithOwnerGroup()
    {
        var (_, root, report) = CreateReportSurface();
        var group = new ReportGroup("Kunde", "KundenGruppe");

        report.Groups.Add(group);
        root.RefreshGeometry();

        var headerContainer = root.GetBandContainer(group.Header);
        var footerContainer = root.GetBandContainer(group.Footer);
        Assert.NotNull(headerContainer);
        Assert.NotNull(footerContainer);
        Assert.Same(group, headerContainer!.OwnerGroup);
        Assert.Same(group, footerContainer!.OwnerGroup);
    }

    [Fact]
    public void ProxyFactory_CreatesCorrectProxy_ForEveryReportControlType()
    {
        Assert.IsType<DesignReportLabel>(ReportProxyFactory.Create(new ReportLabel()));
        Assert.IsType<DesignReportTextBox>(ReportProxyFactory.Create(new ReportTextBox()));
        Assert.IsType<DesignReportImage>(ReportProxyFactory.Create(new ReportImage()));
        Assert.IsType<DesignReportLine>(ReportProxyFactory.Create(new ReportLine()));
        Assert.IsType<DesignReportCheckBox>(ReportProxyFactory.Create(new ReportCheckBox()));
        Assert.IsType<DesignReportChart>(ReportProxyFactory.Create(new ReportChartControl()));
        Assert.IsType<DesignReportCrossTab>(ReportProxyFactory.Create(new ReportCrossTab()));
    }

    [Fact]
    public void DesignFields_RoundTrip_ThroughReportJsonSerializer()
    {
        var report = new Report("FieldsReport");
        report.DesignFields.Add("Kunde");
        report.DesignFields.Add("Betrag");

        var json = ReportJsonSerializer.Save(report);
        Assert.Contains("designFields", json);

        var loaded = ReportJsonSerializer.Load(json);
        Assert.Equal(new[] { "Kunde", "Betrag" }, loaded.DesignFields.ToArray());
    }

    [Fact]
    public void ReportWithoutDesignFields_OmitsDesignFieldsKey_AndStillLoads()
    {
        var report = new Report("NoFields");
        var json = ReportJsonSerializer.Save(report);
        Assert.DoesNotContain("designFields", json);

        var loaded = ReportJsonSerializer.Load(json);
        Assert.Empty(loaded.DesignFields);
    }

    [Fact]
    public void DesignRecord_Build_CollectsAllReferencedFieldNames()
    {
        var report = new Report("RecordReport");
        report.Detail.Controls.Add(new ReportTextBox { DataField = "Betrag" });
        report.Detail.Controls.Add(new ReportTextBox { Expression = "Total: {Summe:C} von {Kunde}" });
        report.Detail.Controls.Add(new ReportCheckBox { DataField = "Bezahlt" });
        var group = new ReportGroup("Region", "RegionGruppe");
        report.Groups.Add(group);

        var record = DesignRecord.Build(report);

        Assert.Contains("Betrag", record.Keys);
        Assert.Contains("Summe", record.Keys);
        Assert.Contains("Kunde", record.Keys);
        Assert.Contains("Bezahlt", record.Keys);
        Assert.Contains("Region", record.Keys);
    }

    [Fact]
    public void GetAvailableFields_MergesDesignFields_AndUsedFields()
    {
        var (surface, root, report) = CreateReportSurface();
        report.DesignFields.Add("Spalte1");
        report.Detail.Controls.Add(new ReportTextBox { DataField = "Spalte2" });
        root.RefreshGeometry();

        var fields = ReportDesignContext.GetAvailableFields(report);

        Assert.Contains("Spalte1", fields);
        Assert.Contains("Spalte2", fields);
    }

    [Fact]
    public void LineProxy_ResizingKeepsOrientation_HorizontalStaysHorizontal()
    {
        var (surface, root, report) = CreateReportSurface();
        var detail = root.GetBandContainer(report.Detail)!;
        var proxy = new DesignReportLine { Size = new Size(189, 2) };
        surface.AddControl(proxy, detail, new Point(0, 0));

        proxy.Bounds = new Rectangle(48, 96, 240, 2);

        var line = (ReportLine)proxy.Wrapped;
        Assert.Equal(line.Top.Value, line.Y2.Value, 3);
        Assert.True(Math.Abs(line.X2.Value - line.Left.Value) > 0.1);
    }

    [Fact]
    public void PropertyService_ReportLabel_IncludesBaseReportControlProperties()
    {
        var service = new OldSchoolForms.Ui.Designer.PropertyGrid.PropertyService();
        var names = service.GetPropertiesFor(new ReportLabel()).Select(p => p.Name).ToList();

        Assert.Contains("Text", names);
        Assert.Contains("Left", names);
        Assert.Contains("Top", names);
        Assert.Contains("Width", names);
        Assert.Contains("Height", names);
        Assert.Contains("BackColor", names);
        Assert.Contains("ForeColor", names);
        Assert.Contains("Font", names);
        Assert.Contains("TextAlign", names);
        Assert.Contains("Format", names);
        Assert.Contains("Visible", names);
        Assert.DoesNotContain("Right", names);
        Assert.DoesNotContain("Bottom", names);
        Assert.DoesNotContain("EffectiveFont", names);
    }

    [Fact]
    public void PropertyService_ReportLine_IncludesLineAndBaseProperties()
    {
        var service = new OldSchoolForms.Ui.Designer.PropertyGrid.PropertyService();
        var names = service.GetPropertiesFor(new ReportLine()).Select(p => p.Name).ToList();

        Assert.Contains("LineColor", names);
        Assert.Contains("LineWidth", names);
        Assert.Contains("X2", names);
        Assert.Contains("Y2", names);
        Assert.Contains("Left", names);
        Assert.Contains("Top", names);
    }

    [Fact]
    public void LineProxy_Render_DrawsBlackLineAtVerticalCenter()
    {
        var (surface, root, report) = CreateReportSurface();
        var detail = root.GetBandContainer(report.Detail)!;
        var proxy = new DesignReportLine { Size = new Size(189, 2) };
        surface.AddControl(proxy, detail, new Point(0, 0));

        var g = new OldSchoolForms.Ui.Rendering.Graphics();
        proxy.Render(g);

        var lineCmd = g.GetCommands().FirstOrDefault(c => c.Type == OldSchoolForms.Ui.Rendering.DrawCommandType.DrawLine);
        Assert.NotNull(lineCmd);
        Assert.Equal(Color.Black, lineCmd.Color);
        Assert.Equal(1f, lineCmd.Y);
        Assert.Equal(1f, lineCmd.Y2);
    }

    [Fact]
    public void TwoLabels_Placed_BothHaveInitialText()
    {
        var (surface, root, report) = CreateReportSurface();
        report.Detail.Height = 3.0;
        root.RefreshGeometry();
        var detail = root.GetBandContainer(report.Detail)!;

        var label1 = new DesignReportLabel();
        surface.AddControl(label1, detail, new Point(10, 5));
        var label2 = new DesignReportLabel();
        surface.AddControl(label2, detail, new Point(10, 40));

        Assert.Equal("Beschriftung", ((ReportLabel)label1.Wrapped).Text);
        Assert.Equal("Beschriftung", ((ReportLabel)label2.Wrapped).Text);
        Assert.Equal(2, detail.Controls.OfType<DesignReportControl>().Count());
        Assert.Equal(2, report.Detail.Controls.Count);
    }

    [Fact]
    public void SelectingProxy_GridOwnHandler_BindsWrapped_NotEmpty()
    {
        var (surface, root, report) = CreateReportSurface();
        var detail = root.GetBandContainer(report.Detail)!;

        // Grid with ONLY its own selection subscription (no external BindObject driver).
        var grid = new OldSchoolForms.Ui.Designer.PropertyGrid.PropertyGrid(surface.Selection);

        // Adding the control auto-selects it, which fires the grid's own handler.
        var label = new DesignReportLabel();
        surface.AddControl(label, detail, new Point(10, 5));

        var rows = (System.Collections.IList)grid.GetType()
            .GetField("_rows", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(grid)!;

        // The grid must show the wrapped report control's properties, not be empty.
        Assert.True(rows.Count > 0, "grid must not be empty when a report proxy is selected");
        Assert.IsType<ReportLabel>(label.Wrapped);
    }

    private static int GridRowCount(OldSchoolForms.Ui.Designer.PropertyGrid.PropertyGrid grid)
    {
        var rows = (System.Collections.IList)grid.GetType()
            .GetField("_rows", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(grid)!;
        return rows.Count;
    }

    [Fact]
    public void Repro_DoubleDriver_TwoLabelsInResizedBand_GridStaysBound()
    {
        var (surface, root, report) = CreateReportSurface();
        report.Detail.Height = 3.0;
        root.RefreshGeometry();
        var detail = root.GetBandContainer(report.Detail)!;

        // Replicate the real DesignerForm double-driver: the grid self-subscribes to the
        // selection service AND the host rebinds via AdapterSelectedValue on every change.
        var grid = new OldSchoolForms.Ui.Designer.PropertyGrid.PropertyGrid(surface.Selection);
        surface.SelectionChanged += (_, _) => grid.BindObject(surface.AdapterSelectedValue);

        var label1 = new DesignReportLabel();
        surface.AddControl(label1, detail, new Point(10, 5));
        Assert.True(GridRowCount(grid) > 0, "grid empty after placing label1");

        var label2 = new DesignReportLabel();
        surface.AddControl(label2, detail, new Point(10, 40));
        Assert.True(GridRowCount(grid) > 0, "grid empty after placing label2");

        // Selecting the first label afterwards must still show its properties.
        var item1 = surface.FindItem(label1);
        Assert.NotNull(item1);
        surface.Selection.Select(item1!);
        Assert.True(GridRowCount(grid) > 0, "grid empty after re-selecting label1");
    }

    [Fact]
    public void Repro_DragResizedBand_ThenTwoLabels_GridStaysBound()
    {
        var (surface, root, report) = CreateReportSurface();
        var detail = root.GetBandContainer(report.Detail)!;

        // Simulate the user dragging the band taller (DragService sets the container bounds,
        // which writes the height back to the model via DesignBand.OnBoundsChanged).
        detail.Bounds = new Rectangle(detail.X, detail.Y, detail.Width, 120);

        var grid = new OldSchoolForms.Ui.Designer.PropertyGrid.PropertyGrid(surface.Selection);
        surface.SelectionChanged += (_, _) => grid.BindObject(surface.AdapterSelectedValue);

        var label1 = new DesignReportLabel();
        surface.AddControl(label1, detail, new Point(10, 5));
        Assert.True(GridRowCount(grid) > 0, "grid empty after placing label1 (drag-resized band)");

        var label2 = new DesignReportLabel();
        surface.AddControl(label2, detail, new Point(10, 40));
        Assert.True(GridRowCount(grid) > 0, "grid empty after placing label2 (drag-resized band)");

        var item1 = surface.FindItem(label1);
        Assert.NotNull(item1);
        surface.Selection.Select(item1!);
        Assert.True(GridRowCount(grid) > 0, "grid empty after re-selecting label1 (drag-resized band)");
    }

    [Fact]
    public void ReparentProxy_BetweenBands_SyncDoesNotCreateDuplicateProxy()
    {
        var (surface, root, report) = CreateReportSurface();
        var detail = root.GetBandContainer(report.Detail)!;
        var footer = root.GetBandContainer(report.PageFooter)!;

        var proxy = new DesignReportLabel();
        surface.AddControl(proxy, detail, new Point(10, 5));
        var wrapped = proxy.Wrapped;
        Assert.Contains(wrapped, report.Detail.Controls);

        // Simulate a cross-band drag: the proxy is reparented to another band container
        // while its wrapped control is still listed in the source band's model.
        proxy.Parent = footer;
        root.SyncControlProxies();

        // Exactly one proxy must reference the wrapped control (no duplicate in the source).
        var proxiesForWrapped = surface.Items
            .Select(i => i.Control)
            .OfType<DesignReportControl>()
            .Count(p => ReferenceEquals(p.Wrapped, wrapped));
        Assert.Equal(1, proxiesForWrapped);

        // The wrapped control follows the proxy into the destination band only.
        Assert.Same(footer, proxy.Parent);
        Assert.DoesNotContain(wrapped, report.Detail.Controls);
        Assert.Contains(wrapped, report.PageFooter.Controls);
    }

    [Fact]
    public void Select_FiresExactlyOneSelectionChanged_NoTransientEmpty()
    {
        var selection = new OldSchoolForms.Ui.Designer.Services.SelectionService();
        var a = new DesignItem(new Control());
        var b = new DesignItem(new Control());
        selection.Select(a);

        int eventCount = 0;
        int emptyDuringEvents = 0;
        selection.SelectionChanged += (_, _) =>
        {
            eventCount++;
            if (selection.SelectedCount == 0) emptyDuringEvents++;
        };

        selection.Select(b);

        Assert.Equal(1, eventCount);
        Assert.Equal(0, emptyDuringEvents);
        Assert.Same(b, selection.PrimarySelection);
    }
}
