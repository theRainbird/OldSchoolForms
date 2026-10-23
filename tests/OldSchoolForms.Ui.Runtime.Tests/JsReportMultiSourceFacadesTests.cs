using System.Data;
using Jint;
using Jint.Native;
using OldSchoolForms.Shared.Scripting;
using OldSchoolForms.Ui.Reports;
using OldSchoolForms.Ui.Runtime.Facade;
using OldSchoolForms.Ui.Runtime.Facade.Typed;
using OldSchoolForms.Ui.Runtime.Scripting;
using Xunit;

namespace OldSchoolForms.Ui.Runtime.Tests;

/// <summary>
/// Tests for the multi-data-source JS facades: <see cref="JsReportDataSource"/>,
/// <see cref="JsReportDataSourceCollection"/> and the per-band
/// <c>dataSourceName</c> on <see cref="JsReportBand"/>.
/// </summary>
public class JsReportMultiSourceFacadesTests
{
    private static JsEngineHost CreateEngine() =>
        new JsEngineHost(new OldSchoolForms.Ui.Runtime.Sources.FileSystemAppSource(TestPaths.SampleApp));

    private static JsValue Wrap(JsEngineHost engine, object facade) =>
        JsValue.FromObject(engine.Engine, facade);

    private static JsReport CreateFacade(JsEngineHost engine, string name = "T")
    {
        var facade = new JsReport(engine, new Report(name));
        engine.Engine.SetValue("r", Wrap(engine, facade));
        return facade;
    }

    [Fact]
    public void DataSources_AddWithFieldsConfig_Roundtrips()
    {
        using var engine = CreateEngine();
        var facade = CreateFacade(engine);

        engine.Engine.Evaluate("""
            r.dataSources.add('Auftragskopf', {
                fields: [ { name: 'Kunde', type: 'string' }, { name: 'Nr', type: 'int' } ]
            });
            """);

        Assert.Equal(1, facade.DataSources.Count);
        var kopf = facade.DataSources.Get("auftragskopf");
        Assert.NotNull(kopf);
        Assert.Equal(2, kopf!.FieldCount);
        Assert.Equal("Kunde", kopf.GetFieldNameAt(0));
        Assert.Equal("string", kopf.GetFieldTypeAt(0));
        Assert.Equal("int", kopf.GetFieldTypeAt(1));
        Assert.Equal(ReportDataType.Int, kopf.Source.FindField("NR")!.DataType);
    }

    [Fact]
    public void DataSources_AddWithJsDataTable_ImportsSchemaAndData()
    {
        using var engine = CreateEngine();
        var facade = CreateFacade(engine);

        var table = new DataTable("Pos");
        table.Columns.Add("Artikel", typeof(string));
        table.Columns.Add("Betrag", typeof(decimal));
        table.Rows.Add("A1", 10m);
        table.Rows.Add("A2", 20m);
        var jsTable = new JsDataTable(engine.Engine, table);

        var src = facade.DataSources.Add("Positionen", Wrap(engine, jsTable));

        Assert.Equal(2, src.FieldCount);
        Assert.Equal(ReportDataType.Decimal, src.Source.FindField("Betrag")!.DataType);
        Assert.IsType<DataView>(src.Source.Data);
        Assert.Equal(2, ((DataView)src.Source.Data!).Count);
    }

    [Fact]
    public void DataSources_Get_Missing_ReturnsNull()
    {
        using var engine = CreateEngine();
        var facade = CreateFacade(engine);
        Assert.Null(facade.DataSources.Get("nope"));
    }

    [Fact]
    public void DataSources_RemoveAt_And_Clear()
    {
        using var engine = CreateEngine();
        var facade = CreateFacade(engine);
        engine.Engine.Evaluate("r.dataSources.add('A'); r.dataSources.add('B');");
        Assert.Equal(2, facade.DataSources.Count);

        Assert.True(facade.DataSources.RemoveAt(0));
        Assert.Equal("B", facade.DataSources.GetAt(0).Name);
        Assert.True(facade.DataSources.Remove("b"));
        Assert.Equal(0, facade.DataSources.Count);
    }

    [Fact]
    public void DataSource_Data_AssignJsArray_ConvertsToRecordList()
    {
        using var engine = CreateEngine();
        var facade = CreateFacade(engine);

        engine.Engine.Evaluate("""
            var s = r.dataSources.add('Pos');
            s.data = [ { artikel: 'X', betrag: 5 }, { artikel: 'Y', betrag: 7 } ];
            """);

        var s = facade.DataSources.Get("Pos")!;
        var list = Assert.IsAssignableFrom<System.Collections.IEnumerable>(s.Source.Data);
        var items = list.Cast<object>().ToList();
        Assert.Equal(2, items.Count);
        var first = Assert.IsAssignableFrom<IDictionary<string, object?>>(items[0]);
        Assert.Equal("X", first["artikel"]);
    }

    [Fact]
    public void Band_DataSourceName_SettableViaFacade()
    {
        using var engine = CreateEngine();
        var facade = CreateFacade(engine);

        engine.Engine.Evaluate("r.detail.dataSourceName = 'Auftragspositionen';");
        Assert.Equal("Auftragspositionen", facade.Detail.DataSourceName);
        Assert.Equal("Auftragspositionen", facade.Report.Detail.DataSourceName);
    }

    [Fact]
    public void Report_DataSources_RoundTripThroughJson()
    {
        using var engine = CreateEngine();
        var facade = CreateFacade(engine);

        engine.Engine.Evaluate("""
            r.dataSources.add('Kopf', { fields: [{ name: 'Kunde', type: 'string' }] });
            r.dataSources.add('Pos', { fields: [{ name: 'Artikel', type: 'string' }, { name: 'Betrag', type: 'decimal' }] });
            r.reportHeader.dataSourceName = 'Kopf';
            r.detail.dataSourceName = 'Pos';
            """);

        var json = ReportJsonSerializer.Save(facade.Report);
        var loaded = ReportJsonSerializer.Load(json);

        Assert.Equal(2, loaded.DataSources.Count);
        Assert.Equal("Kopf", loaded.DataSources[0].Name);
        Assert.Equal(ReportDataType.Decimal, loaded.DataSources[1].FindField("Betrag")!.DataType);
        Assert.Equal("Kopf", loaded.ReportHeader.DataSourceName);
        Assert.Equal("Pos", loaded.Detail.DataSourceName);
    }

    [Fact]
    public void EndToEnd_MultiSource_RenderProducesPages()
    {
        using var engine = CreateEngine();
        var facade = CreateFacade(engine, "Multi");

        engine.Engine.Evaluate("""
            r.pageSetup.paperSize = 'A4';
            r.dataSources.add('Kopf', {
                fields: [{ name: 'Kunde', type: 'string' }],
                data: [ { kunde: 'Mueller' } ]
            });
            r.dataSources.add('Pos', {
                fields: [{ name: 'Artikel', type: 'string' }],
                data: [ { artikel: 'P1' }, { artikel: 'P2' }, { artikel: 'P3' } ]
            });
            r.reportHeader.height = 1;
            r.reportHeader.dataSourceName = 'Kopf';
            r.reportHeader.controls.add({ type: 'textBox', dataField: 'Kunde', left: 0, top: 0, width: 5, height: 0.5 });
            r.detail.height = 0.5;
            r.detail.dataSourceName = 'Pos';
            r.detail.controls.add({ type: 'textBox', dataField: 'Artikel', left: 0, top: 0, width: 3, height: 0.4 });
            """);

        Assert.True(facade.PageCount() >= 1);
        var b64 = facade.ExportPdfBytes();
        Assert.True(b64.Length > 100);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(Convert.FromBase64String(b64), 0, 4));
    }
}
