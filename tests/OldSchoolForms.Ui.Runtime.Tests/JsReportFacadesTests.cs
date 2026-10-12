using System.Data;
using System.Globalization;
using Jint;
using Jint.Native;
using OldSchoolForms.Shared.Scripting;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Reports;
using OldSchoolForms.Ui.Runtime.Facade;
using OldSchoolForms.Ui.Runtime.Facade.Typed;
using OldSchoolForms.Ui.Runtime.Hosting;
using OldSchoolForms.Ui.Runtime.Localization;
using OldSchoolForms.Ui.Runtime.Modules;
using OldSchoolForms.Ui.Runtime.Scripting;
using OldSchoolForms.Ui.Runtime.Serialization;
using OldSchoolForms.Ui.Runtime.Sources;
using Xunit;

namespace OldSchoolForms.Ui.Runtime.Tests;

/// <summary>
/// Tests für die Report-Fassaden (Phase 5): JSON-Round-trip im Format
/// <c>OldSchoolForms.Report</c>, JS-Erzeugung aller Report-Control-Typen,
/// Gruppierung, DataSource-Konvertierung, headloser PDF-Export und die
/// <c>ctx.report</c>-Integration im Modul-Kontext.
/// </summary>
public class JsReportFacadesTests : IDisposable
{
    private readonly string _root;

    public JsReportFacadesTests()
    {
        JsFacadeRegistry.Clear();
        _root = Path.Combine(Path.GetTempPath(), "osf-report-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        WriteApp();
    }

    public void Dispose()
    {
        JsFacadeRegistry.Clear();
        try { Directory.Delete(_root, true); } catch (IOException) { }
    }

    private void WriteApp()
    {
        File.WriteAllText(Path.Combine(_root, "app.json"), """
        { "name": "ReportTestApp", "version": "1.0.0", "modules": ["modules/rep"] }
        """);

        var rep = Path.Combine(_root, "modules", "rep");
        Directory.CreateDirectory(rep);
        File.WriteAllText(Path.Combine(rep, "module.json"), """
        { "id": "rep", "title": "Rep", "layout": "layout.json", "script": "index.js", "order": 1 }
        """);
        File.WriteAllText(Path.Combine(rep, "layout.json"), """
        { "format": "OldSchoolForms.Layout", "version": 1, "controls": [] }
        """);
        File.WriteAllText(Path.Combine(rep, "report.json"), """
        {
          "format": "OldSchoolForms.Report",
          "name": "Aus Datei",
          "pageSetup": { "portraitWidth": 21, "portraitHeight": 29.7, "landscape": false,
                        "leftMargin": 2, "rightMargin": 2, "topMargin": 2, "bottomMargin": 2 },
          "reportHeader": { "height": 0, "visible": true, "canGrow": false, "repeatOnNewPage": false, "keepTogether": false, "controls": [] },
          "pageHeader": { "height": 0, "visible": true, "canGrow": false, "repeatOnNewPage": false, "keepTogether": false, "controls": [] },
          "detail": { "height": 1, "visible": true, "canGrow": false, "repeatOnNewPage": false, "keepTogether": false,
            "controls": [ { "type": "label", "name": "l1", "left": 1, "top": 0, "width": 5, "height": 0.8,
                           "visible": true, "canGrow": false, "canShrink": false, "text": "Aus Datei" } ] },
          "pageFooter": { "height": 0, "visible": true, "canGrow": false, "repeatOnNewPage": false, "keepTogether": false, "controls": [] },
          "reportFooter": { "height": 0, "visible": true, "canGrow": false, "repeatOnNewPage": false, "keepTogether": false, "controls": [] },
          "groups": []
        }
        """);
        File.WriteAllText(Path.Combine(rep, "index.js"), """
        export default class RepForm extends FormBase {
            constructor(ctx) {
                super(ctx);
                this.loadLayout();
                var r = ctx.report.create('JsReport');
                r.pageSetup.paperSize = 'A4';
                r.pageSetup.landscape = true;
                r.pageSetup.setMargins(1.5);
                r.detail.height = 2.5;
                var lbl = r.detail.controls.add({ type: 'label', text: 'Hello', left: 1, top: 0.5, width: 8, height: 0.8 });
                r.groups.add('Category', { name: 'By Cat', keepTogether: true });
                r.dataSource = [{ name: 'A', value: 1 }, { name: 'B', value: 2 }];
                globalThis.__name = r.name;
                globalThis.__paper = r.pageSetup.paperSize;
                globalThis.__landscape = r.pageSetup.landscape;
                globalThis.__margin = r.pageSetup.leftMargin;
                globalThis.__detailHeight = r.detail.height;
                globalThis.__lblText = lbl.text;
                globalThis.__groupCount = r.groups.count;
                globalThis.__pages = r.pageCount();
                globalThis.__b64 = r.exportPdfBytes();
                var loaded = ctx.report.load('report.json');
                globalThis.__loadedName = loaded.name;
                globalThis.__loadedCtlCount = loaded.detail.controls.count;
                globalThis.__loadedText = loaded.detail.controls.getAt(0).text;
            }
        }
        """);
    }

    private static JsEngineHost CreateEngine() =>
        new JsEngineHost(new FileSystemAppSource(TestPaths.SampleApp));

    private static JsValue Wrap(JsEngineHost engine, object facade) =>
        JsValue.FromObject(engine.Engine, facade);

    private static JsReport CreateFacade(JsEngineHost engine, string name = "T")
    {
        var facade = new JsReport(engine, new Report(name));
        engine.Engine.SetValue("r", Wrap(engine, facade));
        return facade;
    }

    [Fact]
    public void CtxReport_CreateAndLoad_RunsInModuleScope()
    {
        var source = new FileSystemAppSource(_root);
        var i18n = new RuntimeI18n(source, "i18n", "en");
        LocalizationManager.SetCulture(new CultureInfo("en"));
        using var engine = new JsEngineHost(source) { I18n = i18n };
        var loader = new RuntimeLayoutLoader { I18n = i18n };
        var manifest = AppManifest.Load(source);
        var registry = new ModuleRegistry(manifest.Modules);
        var tabs = new TabControl();
        var shellForm = new JsForm(engine, new Form());
        var host = new HostServices(source, manifest.Name, manifest.Version) { I18n = i18n };
        var moduleHost = new ModuleHost(source, engine, loader, registry, tabs, shellForm, host);
        host.ModuleHost = moduleHost;
        moduleHost.AttachTabActivation();

        moduleHost.OpenModule("rep");

        Assert.Equal("JsReport", engine.Engine.GetValue("__name").AsString());
        Assert.Equal("A4", engine.Engine.GetValue("__paper").AsString());
        Assert.True(engine.Engine.GetValue("__landscape").AsBoolean());
        Assert.Equal(1.5, engine.Engine.GetValue("__margin").AsNumber(), 3);
        Assert.Equal(2.5, engine.Engine.GetValue("__detailHeight").AsNumber(), 3);
        Assert.Equal("Hello", engine.Engine.GetValue("__lblText").AsString());
        Assert.Equal(1, (int)engine.Engine.GetValue("__groupCount").AsNumber());
        Assert.True(engine.Engine.GetValue("__pages").AsNumber() >= 1);
        var b64 = engine.Engine.GetValue("__b64").AsString();
        Assert.True(b64.Length > 100);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(Convert.FromBase64String(b64), 0, 4));
        Assert.Equal("Aus Datei", engine.Engine.GetValue("__loadedName").AsString());
        Assert.Equal(1, (int)engine.Engine.GetValue("__loadedCtlCount").AsNumber());
        Assert.Equal("Aus Datei", engine.Engine.GetValue("__loadedText").AsString());
    }

    [Fact]
    public void JsonRoundTrip_AllControlTypes_AndNestedGroups()
    {
        var report = new Report("RoundTrip");
        report.PageSetup.SetPaperSize(ReportPaperSize.A4, landscape: true);
        report.PageSetup.SetMargins(1.5);
        report.Detail.Height = 2.0;

        report.Detail.Controls.Add(new ReportLabel { Text = "Statisch", Left = 1, Top = 0, Width = 5, Height = 0.8 });
        report.Detail.Controls.Add(new ReportTextBox { DataField = "Name", Expression = "{Name}", Left = 1, Top = 1, Width = 5, Height = 0.8, Format = "N2" });
        report.Detail.Controls.Add(new ReportImage { Sizing = ImageSizing.Stretch, Left = 7, Top = 0, Width = 3, Height = 2 });
        report.Detail.Controls.Add(new ReportLine { X2 = 10, Y2 = 0.5, LineWidth = 0.1, LineColor = Color.FromArgb(255, 0, 0), Left = 0, Top = 0, Width = 10, Height = 0.5 });
        report.Detail.Controls.Add(new ReportCheckBox { DataField = "Active", Caption = "Aktiv", Left = 0, Top = 1.5, Width = 3, Height = 0.5 });

        var chart = new ReportChartControl { ChartType = DiagramType.Line, Title = "Umsatz", ShowLegend = false, Left = 0, Top = 2.5, Width = 10, Height = 5 };
        var series = new DiagramViewSeries { Name = "Q1" };
        series.Add("Jan", 10);
        series.Add("Feb", 20);
        chart.Series.Add(series);
        report.Detail.Controls.Add(chart);

        var crossTab = new ReportCrossTab { Left = 0, Top = 8, Width = 10, Height = 4 };
        crossTab.Fields.Add(new CrossTabField { DataField = "Region", Usage = FieldUsage.RowField });
        crossTab.Fields.Add(new CrossTabField { DataField = "Year", Usage = FieldUsage.ColumnField });
        crossTab.Fields.Add(new CrossTabField { DataField = "Sales", Usage = FieldUsage.ValueField, Aggregation = CrossTabAggregation.Avg });
        report.Detail.Controls.Add(crossTab);

        var group = new ReportGroup("Category", "Nach Kategorie") { KeepTogether = true };
        group.Header.Controls.Add(new ReportLabel { Text = "Gruppe", Left = 0, Top = 0, Width = 5, Height = 0.6 });
        group.Footer.Controls.Add(new ReportLabel { Text = "Footer", Left = 0, Top = 0, Width = 5, Height = 0.6 });
        var nested = new ReportGroup("SubCategory", "Untergruppe");
        group.Groups.Add(nested);
        report.Groups.Add(group);

        var json = ReportJsonSerializer.Save(report);
        var loaded = ReportJsonSerializer.Load(json);

        Assert.Equal("RoundTrip", loaded.Name);
        Assert.True(loaded.PageSetup.Landscape);
        Assert.Equal(1.5, loaded.PageSetup.LeftMargin.Value, 3);
        Assert.Equal(2.0, loaded.Detail.Height.Value, 3);
        Assert.Equal(7, loaded.Detail.Controls.Count);

        Assert.IsType<ReportLabel>(loaded.Detail.Controls[0]);
        Assert.Equal("Statisch", ((ReportLabel)loaded.Detail.Controls[0]).Text);

        var tb = Assert.IsType<ReportTextBox>(loaded.Detail.Controls[1]);
        Assert.Equal("Name", tb.DataField);
        Assert.Equal("{Name}", tb.Expression);
        Assert.Equal("N2", tb.Format);

        var img = Assert.IsType<ReportImage>(loaded.Detail.Controls[2]);
        Assert.Equal(ImageSizing.Stretch, img.Sizing);

        var line = Assert.IsType<ReportLine>(loaded.Detail.Controls[3]);
        Assert.Equal(10, line.X2.Value, 3);
        Assert.Equal(0.5, line.Y2.Value, 3);
        Assert.Equal(0.1, line.LineWidth.Value, 3);
        Assert.Equal(Color.FromArgb(255, 0, 0), line.LineColor);

        var cb = Assert.IsType<ReportCheckBox>(loaded.Detail.Controls[4]);
        Assert.Equal("Active", cb.DataField);
        Assert.Equal("Aktiv", cb.Caption);

        var loadedChart = Assert.IsType<ReportChartControl>(loaded.Detail.Controls[5]);
        Assert.Equal(DiagramType.Line, loadedChart.ChartType);
        Assert.Equal("Umsatz", loadedChart.Title);
        Assert.False(loadedChart.ShowLegend);
        Assert.Single(loadedChart.Series);
        Assert.Equal("Q1", loadedChart.Series[0].Name);
        Assert.Equal(2, loadedChart.Series[0].Points.Count);
        Assert.Equal(20, loadedChart.Series[0].Points[1].Value, 3);

        var loadedCross = Assert.IsType<ReportCrossTab>(loaded.Detail.Controls[6]);
        Assert.Equal(3, loadedCross.Fields.Count);
        Assert.Equal(FieldUsage.RowField, loadedCross.Fields[0].Usage);
        Assert.Equal(FieldUsage.ColumnField, loadedCross.Fields[1].Usage);
        Assert.Equal(CrossTabAggregation.Avg, loadedCross.Fields[2].Aggregation);

        Assert.Single(loaded.Groups);
        Assert.Equal("Category", loaded.Groups[0].GroupField);
        Assert.Equal("Nach Kategorie", loaded.Groups[0].Name);
        Assert.True(loaded.Groups[0].KeepTogether);
        Assert.Single(loaded.Groups[0].Header.Controls);
        Assert.Single(loaded.Groups[0].Footer.Controls);
        Assert.Single(loaded.Groups[0].Groups);
        Assert.Equal("SubCategory", loaded.Groups[0].Groups[0].GroupField);
    }

    [Fact]
    public void JsonLoad_WrongFormat_ThrowsInvalidData()
    {
        Assert.Throws<InvalidDataException>(() => ReportJsonSerializer.Load("""
            { "format": "OldSchoolForms.Layout", "version": 1 }
            """));
    }

    [Fact]
    public void Facade_AllSevenControlTypes_ViaJsConfig()
    {
        using var engine = CreateEngine();
        var facade = CreateFacade(engine);

        engine.Engine.Evaluate("""
            r.detail.controls.add({ type: 'label', text: 'L', left: 1, top: 0, width: 4, height: 0.8 });
            r.detail.controls.add({ type: 'textBox', dataField: 'F1', expression: '{F1}', format: 'F2' });
            r.detail.controls.add({ type: 'image', sizing: 'Zoom' });
            r.detail.controls.add({ type: 'line', x2: 9, y2: 1, lineWidth: 0.05 });
            r.detail.controls.add({ type: 'checkBox', dataField: 'Ok', caption: 'Check' });
            r.detail.controls.add({ type: 'chart', chartType: 'Pie', title: 'Chart', showLegend: true });
            r.detail.controls.add({ type: 'crossTab', groupField: 'Cat' });
            """);

        Assert.Equal(7, facade.Detail.Controls.Count);
        Assert.IsType<JsReportLabel>(facade.Detail.Controls.GetAt(0));
        Assert.Equal("L", ((JsReportLabel)facade.Detail.Controls.GetAt(0)).Text);
        var tb = (JsReportTextBox)facade.Detail.Controls.GetAt(1);
        Assert.Equal("F1", tb.DataField);
        Assert.Equal("{F1}", tb.Expression);
        Assert.Equal("F2", tb.Format);
        Assert.Equal("Zoom", ((JsReportImage)facade.Detail.Controls.GetAt(2)).Sizing);
        var line = (JsReportLine)facade.Detail.Controls.GetAt(3);
        Assert.Equal(9, line.X2, 3);
        Assert.Equal(1, line.Y2, 3);
        Assert.Equal(0.05, line.LineWidth, 4);
        var box = (JsReportCheckBox)facade.Detail.Controls.GetAt(4);
        Assert.Equal("Ok", box.DataField);
        Assert.Equal("Check", box.Caption);
        var chart = (JsReportChart)facade.Detail.Controls.GetAt(5);
        Assert.Equal("Pie", chart.ChartType);
        Assert.Equal("Chart", chart.Title);
        Assert.True(chart.ShowLegend);
        var cross = (JsReportCrossTab)facade.Detail.Controls.GetAt(6);
        Assert.Equal("Cat", cross.GroupField);
    }

    [Fact]
    public void Facade_InvalidControlType_Throws()
    {
        using var engine = CreateEngine();
        CreateFacade(engine);
        Assert.ThrowsAny<Exception>(() =>
            engine.Engine.Evaluate("r.detail.controls.add({ type: 'bogus' });"));
    }

    [Fact]
    public void Facade_ChartSeries_ViaJs()
    {
        using var engine = CreateEngine();
        var facade = CreateFacade(engine);

        engine.Engine.Evaluate("""
            var chart = r.detail.controls.add({ type: 'chart' });
            chart.series.add('Q1', [{ label: 'Jan', value: 10 }, { label: 'Feb', value: 20 }]);
            """);

        var chart = (JsReportChart)facade.Detail.Controls.GetAt(0);
        Assert.Equal(1, chart.Series.Count);
        Assert.Equal("Q1", chart.Series.GetAt(0).Name);
        Assert.Equal(2, chart.Series.GetAt(0).PointCount);
    }

    [Fact]
    public void Facade_CrossTabFields_ViaJs()
    {
        using var engine = CreateEngine();
        var facade = CreateFacade(engine);

        engine.Engine.Evaluate("""
            var ct = r.detail.controls.add({ type: 'crossTab' });
            ct.fields.add({ dataField: 'Region', usage: 'RowField' });
            ct.fields.add({ dataField: 'Year', usage: 'ColumnField' });
            ct.fields.add({ dataField: 'Sales', usage: 'ValueField', aggregation: 'Sum' });
            """);

        var ct = (JsReportCrossTab)facade.Detail.Controls.GetAt(0);
        Assert.Equal(3, ct.Fields.Count);
        Assert.Equal("RowField", ct.Fields.GetAt(0).Usage);
        Assert.Equal("ColumnField", ct.Fields.GetAt(1).Usage);
        Assert.Equal("Sum", ct.Fields.GetAt(2).Aggregation);
    }

    [Fact]
    public void Facade_Groups_Nested_ViaJs()
    {
        using var engine = CreateEngine();
        var facade = CreateFacade(engine);

        engine.Engine.Evaluate("""
            var g = r.groups.add('Category', { name: 'Kat', keepTogether: true });
            g.groups.add('SubCat');
            g.header.controls.add({ type: 'label', text: 'H' });
            """);

        Assert.Equal(1, facade.Groups.Count);
        var g = facade.Groups.GetAt(0);
        Assert.Equal("Category", g.GroupField);
        Assert.Equal("Kat", g.Name);
        Assert.True(g.KeepTogether);
#pragma warning disable xUnit2013 // Facade-Collections implementieren kein IEnumerable
        Assert.Equal(1, g.Groups.Count);
#pragma warning restore xUnit2013
        Assert.Equal("SubCat", g.Groups.GetAt(0).GroupField);
        Assert.Equal(1, g.Header.Controls.Count);
    }

    [Fact]
    public void Facade_PageSetup_PaperSizeRoundtrip()
    {
        using var engine = CreateEngine();
        var facade = CreateFacade(engine);

        engine.Engine.Evaluate("r.pageSetup.paperSize = 'Letter';");
        Assert.Equal("Letter", facade.PageSetup.PaperSize);

        engine.Engine.Evaluate("r.pageSetup.paperSize = 'A5'; r.pageSetup.landscape = true;");
        Assert.Equal("A5", facade.PageSetup.PaperSize);
        Assert.True(facade.PageSetup.Landscape);
        Assert.True(facade.PageSetup.PaperWidth > facade.PageSetup.PaperHeight);
    }

    [Fact]
    public void Facade_ExportPdfBytes_StartsWithPdf()
    {
        using var engine = CreateEngine();
        var facade = CreateFacade(engine);
        facade.Report.Detail.Controls.Add(new ReportLabel { Text = "X", Left = 1, Top = 0, Width = 3, Height = 0.8 });

        var b64 = facade.ExportPdfBytes();
        var bytes = Convert.FromBase64String(b64);
        Assert.True(bytes.Length > 100);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public void Facade_ExportPdf_WritesFile()
    {
        using var engine = CreateEngine();
        var facade = CreateFacade(engine);
        facade.Report.Detail.Controls.Add(new ReportLabel { Text = "X", Left = 1, Top = 0, Width = 3, Height = 0.8 });

        var path = Path.Combine(_root, "out.pdf");
        facade.ExportPdf(path);

        Assert.True(File.Exists(path));
        var bytes = File.ReadAllBytes(path);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public void Facade_DataSource_JsDataTable_UsesDefaultView()
    {
        using var engine = CreateEngine();
        var facade = CreateFacade(engine);

        var table = new DataTable("T");
        table.Columns.Add("Name", typeof(string));
        table.Rows.Add("A");
        var jsTable = new JsDataTable(engine.Engine, table);

        facade.DataSource = Wrap(engine, jsTable);

        Assert.IsType<DataView>(facade.Report.DataSource);
        Assert.Equal(1, ((DataView)facade.Report.DataSource).Count);
    }

    [Fact]
    public void OpenReport_CreatesTabWithViewer()
    {
        var source = new FileSystemAppSource(_root);
        var i18n = new RuntimeI18n(source, "i18n", "en");
        LocalizationManager.SetCulture(new CultureInfo("en"));
        using var engine = new JsEngineHost(source) { I18n = i18n };
        var loader = new RuntimeLayoutLoader { I18n = i18n };
        var manifest = AppManifest.Load(source);
        var registry = new ModuleRegistry(manifest.Modules);
        var tabs = new TabControl();
        var shellForm = new JsForm(engine, new Form());
        var host = new HostServices(source, manifest.Name, manifest.Version) { I18n = i18n };
        var moduleHost = new ModuleHost(source, engine, loader, registry, tabs, shellForm, host);
        host.ModuleHost = moduleHost;
        moduleHost.AttachTabActivation();

        var report = new Report("ViewerReport");
        report.Detail.Controls.Add(new ReportLabel { Text = "V", Left = 1, Top = 0, Width = 3, Height = 0.8 });
        var tab = moduleHost.OpenReport(report);

        Assert.Equal("ViewerReport", tab.Text);
        Assert.Single(tabs.TabPages);
        Assert.Contains(tab.Controls.OfType<ReportViewer>(), v => v.Report == report);
        Assert.True(report.PageSetup.PrintableWidth.Value > 0);
    }
}
