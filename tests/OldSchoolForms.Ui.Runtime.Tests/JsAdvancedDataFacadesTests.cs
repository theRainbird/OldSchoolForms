using System.Reflection;
using Jint;
using Jint.Native;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Runtime.Facade;
using OldSchoolForms.Ui.Runtime.Facade.Typed;
using OldSchoolForms.Ui.Runtime.Scripting;
using OldSchoolForms.Ui.Runtime.Sources;
using Xunit;

namespace OldSchoolForms.Ui.Runtime.Tests;

/// <summary>
/// Tests für die typisierten Phase-4-Fassaden der datenintensiven Controls:
/// Registry-Auflösung, Konfigurations-Marschalling, DataSource-Konvertierung
/// und Event-Payloads (inkl. Cancelable-Events).
/// </summary>
public class JsAdvancedDataFacadesTests : IDisposable
{
    public JsAdvancedDataFacadesTests() => JsFacadeRegistry.Clear();

    public void Dispose() => JsFacadeRegistry.Clear();

    private static JsEngineHost CreateEngine() =>
        new JsEngineHost(new FileSystemAppSource(TestPaths.SampleApp));

    private static JsValue Wrap(JsEngineHost engine, JsControl facade) =>
        JsValue.FromObject(engine.Engine, facade);

    private static bool EventHasSubscribers(object target, string eventName)
    {
        var field = target.GetType().GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic);
        return (field?.GetValue(target) as MulticastDelegate)?.GetInvocationList().Length > 0;
    }

    private static Delegate GetWired(JsControl facade, string eventName)
    {
        var field = typeof(JsControl).GetField("Wired", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var wired = (System.Collections.IEnumerable)field.GetValue(facade)!;
        foreach (var entry in wired)
        {
            var entryType = entry.GetType();
            var name = (string?)entryType.GetField("Item1")!.GetValue(entry);
            if (name == eventName)
                return (Delegate)entryType.GetField("Item2")!.GetValue(entry)!;
        }
        throw new InvalidOperationException($"Kein verdrahteter Handler für '{eventName}'.");
    }

    [Fact]
    public void Registry_ReturnsTypedFacades_ForAllAdvancedDataControls()
    {
        using var engine = CreateEngine();

        Assert.IsType<JsDataGridView>(JsFacadeRegistry.WrapControl(engine, new DataGridView()));
        Assert.IsType<JsCalendarView>(JsFacadeRegistry.WrapControl(engine, new CalendarView()));
        Assert.IsType<JsKanbanBoardView>(JsFacadeRegistry.WrapControl(engine, new KanbanBoardView()));
        Assert.IsType<JsPivotTable>(JsFacadeRegistry.WrapControl(engine, new PivotTable()));
        Assert.IsType<JsDiagramView>(JsFacadeRegistry.WrapControl(engine, new DiagramView()));
        Assert.IsType<JsHtmlBox>(JsFacadeRegistry.WrapControl(engine, new HtmlBox()));
    }

    [Fact]
    public void DataGridView_ColumnsAddConfig_Roundtrips()
    {
        using var engine = CreateEngine();
        var grid = new DataGridView();
        var facade = (JsDataGridView)JsFacadeRegistry.WrapControl(engine, grid);
        engine.Engine.SetValue("grid", Wrap(engine, facade));

        engine.Engine.Execute(
            "var col = grid.columns.add({ headerText: 'Name', dataPropertyName: 'name', editType: 'ComboBox', width: 150, format: 'N2', items: ['a', 'b'] });");

        Assert.Equal(1, grid.Columns.Count);
        var column = (JsValue)engine.Engine.Evaluate("col.headerText");
        Assert.Equal("Name", column.ToString());
        Assert.Equal("name", grid.Columns[0].DataPropertyName);
        Assert.Equal(DataGridViewColumnEditType.ComboBox, grid.Columns[0].CellEditType);
        Assert.Equal(150, grid.Columns[0].Width);
        Assert.Equal("N2", grid.Columns[0].FormatString);
        Assert.Equal(2, grid.Columns[0].Items!.Count);
        Assert.Equal("ComboBox", engine.Engine.Evaluate("col.editType").ToString());
        Assert.Equal(1, (int)engine.Engine.Evaluate("grid.columns.count").AsNumber());
    }

    [Fact]
    public void DataGridView_AddRow_ReadsCells()
    {
        using var engine = CreateEngine();
        var grid = new DataGridView();
        grid.Columns.Add(new DataGridViewColumn { HeaderText = "A" });
        grid.Columns.Add(new DataGridViewColumn { HeaderText = "B" });
        var facade = (JsDataGridView)JsFacadeRegistry.WrapControl(engine, grid);
        engine.Engine.SetValue("grid", Wrap(engine, facade));

        engine.Engine.Execute("grid.addRow('hallo', 42);");

        Assert.Equal(1, grid.Rows.Count);
        Assert.Equal("hallo", engine.Engine.Evaluate("grid.rows.getAt(0).getValue(0)").ToString());
        Assert.Equal(42d, engine.Engine.Evaluate("grid.rows.getAt(0).getValue(1)").AsNumber());
    }

    [Fact]
    public void DataGridView_CellEvents_CarryRowAndColumn()
    {
        using var engine = CreateEngine();
        var grid = new DataGridView();
        var facade = (JsDataGridView)JsFacadeRegistry.WrapControl(engine, grid);
        engine.Engine.SetValue("grid", Wrap(engine, facade));
        engine.Engine.Execute("var log = []; grid.on('cellclick', function (e) { log.push('c' + e.row + '/' + e.column); });" +
                             "grid.on('cellvaluechanged', function (e) { log.push('v' + e.row + '/' + e.column); });");

        GetWired(facade, "cellclick").DynamicInvoke(grid, new DataGridViewCellEventArgs(2, 1));
        GetWired(facade, "cellvaluechanged").DynamicInvoke(grid, new DataGridViewCellEventArgs(3, 5));

        Assert.Equal("c1/2|v5/3", engine.Engine.Evaluate("log.join('|')").ToString());
    }

    [Fact]
    public void DataGridView_CellFormatting_CallbackOverridesValue()
    {
        using var engine = CreateEngine();
        var grid = new DataGridView();
        var facade = (JsDataGridView)JsFacadeRegistry.WrapControl(engine, grid);
        engine.Engine.SetValue("grid", Wrap(engine, facade));
        engine.Engine.Execute("grid.on('cellformatting', function (e) { e.value = 'UEBER' + e.value; e.handled = true; });");

        var args = new DataGridViewCellFormattingEventArgs(0, 0, "RAW", null);
        GetWired(facade, "cellformatting").DynamicInvoke(grid, args);

        Assert.Equal("UEBERRAW", args.Value);
        Assert.True(args.FormattingApplied);
    }

    [Fact]
    public void CalendarView_BasicsAndAppointments()
    {
        using var engine = CreateEngine();
        var calendar = new CalendarView();
        var facade = (JsCalendarView)JsFacadeRegistry.WrapControl(engine, calendar);
        engine.Engine.SetValue("cal", Wrap(engine, facade));

        engine.Engine.Execute("cal.viewType = 'Week'; cal.allowEdit = false;");
        Assert.Equal(CalendarViewType.Week, calendar.ViewType);
        Assert.False(calendar.AllowEdit);

        engine.Engine.Execute(
            "cal.appointments.add({ subject: 'Standup', startTime: '2026-03-15T09:00:00', endTime: '2026-03-15T09:15:00', color: '#ff0000', allDay: false });");
        Assert.Single(calendar.Appointments);
        Assert.Equal("Standup", calendar.Appointments[0].Subject);
        Assert.Equal(new DateTime(2026, 3, 15, 9, 0, 0), calendar.Appointments[0].StartTime);
        Assert.Equal("#ff0000", engine.Engine.Evaluate("cal.appointments.getAt(0).categoryColor").ToString());
        Assert.Equal("Standup", engine.Engine.Evaluate("cal.appointments.getAt(0).subject").ToString());

        engine.Engine.Execute(            "cal.appointments.add({ subject: 'X' }); cal.appointments.removeAt(1);");
        Assert.Single(calendar.Appointments);
    }

    [Fact]
    public void CalendarView_GoToToday_ChangesSelectedDate()
    {
        using var engine = CreateEngine();
        var calendar = new CalendarView { SelectedDate = new DateTime(2020, 1, 1) };
        var facade = (JsCalendarView)JsFacadeRegistry.WrapControl(engine, calendar);
        engine.Engine.SetValue("cal", Wrap(engine, facade));

        engine.Engine.Execute("cal.goToToday();");

        Assert.Equal(DateTime.Today, calendar.SelectedDate.Date);
        Assert.Equal(JsMarshal.FromDateTime(DateTime.Today), engine.Engine.Evaluate("cal.selectedDate").ToString());
    }

    [Fact]
    public void CalendarView_DateSelectedPayload_IsIso()
    {
        using var engine = CreateEngine();
        var calendar = new CalendarView();
        var facade = (JsCalendarView)JsFacadeRegistry.WrapControl(engine, calendar);
        engine.Engine.SetValue("cal", Wrap(engine, facade));
        engine.Engine.Execute("var got = ''; cal.on('dateselected', function (e) { got = e.value; });");

        GetWired(facade, "dateselected").DynamicInvoke(calendar, new DateSelectedEventArgs(new DateTime(2026, 5, 1)));

        Assert.Equal("2026-05-01T00:00:00", engine.Engine.Evaluate("got").ToString());
    }

    [Fact]
    public void CalendarView_AppointmentChanged_IsCancelable()
    {
        using var engine = CreateEngine();
        var calendar = new CalendarView();
        var facade = (JsCalendarView)JsFacadeRegistry.WrapControl(engine, calendar);
        engine.Engine.SetValue("cal", Wrap(engine, facade));
        engine.Engine.Execute("var changeType = ''; cal.on('appointmentchanged', function (e) { changeType = e.value; e.handled = true; });");

        var appointment = new CalendarAppointment { Subject = "S" };
        var args = new CalendarAppointmentChangedEventArgs(appointment, "edit");
        GetWired(facade, "appointmentchanged").DynamicInvoke(calendar, args);

        Assert.Equal("edit", engine.Engine.Evaluate("changeType").ToString());
        Assert.True(args.Cancel);
    }

    [Fact]
    public void Kanban_DataSourceFromJsArray_BuildsCards()
    {
        using var engine = CreateEngine();
        var board = new KanbanBoardView { DisplayMember = "title", StateMember = "state" };
        var facade = (JsKanbanBoardView)JsFacadeRegistry.WrapControl(engine, board);
        engine.Engine.SetValue("board", Wrap(engine, facade));

        engine.Engine.Execute(
            "board.columns.add({ name: 'Todo', stateValue: 'todo' });" +
            "board.columns.add({ name: 'Done', stateValue: 'done' });" +
            "board.dataSource = [{ title: 'A', state: 'todo' }, { title: 'B', state: 'done' }, { title: 'C', state: 'todo' }];");

        Assert.Equal(2, board.Columns.Count);
        Assert.Equal(2, board.Columns[0].CardCount);
        Assert.Equal(1, board.Columns[1].CardCount);
        Assert.Equal("Todo", engine.Engine.Evaluate("board.columns.getAt(0).name").ToString());
    }

    [Fact]
    public void Kanban_CardClickPayload_AndMoveCancel()
    {
        using var engine = CreateEngine();
        var board = new KanbanBoardView { DisplayMember = "title", StateMember = "state" };
        var facade = (JsKanbanBoardView)JsFacadeRegistry.WrapControl(engine, board);
        engine.Engine.SetValue("board", Wrap(engine, facade));
        engine.Engine.Execute(
            "var clicked = ''; board.on('cardclick', function (e) { clicked = e.payload.displayText; });" +
            "board.on('cardmoving', function (e) { e.handled = true; });");

        var card = new KanbanBoardCard(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["title"] = "Karte" })
        {
            StateValue = "todo",
            DisplayText = "Karte"
        };
        var from = new KanbanBoardColumn { Name = "Todo", StateValue = "todo" };
        var to = new KanbanBoardColumn { Name = "Done", StateValue = "done" };

        GetWired(facade, "cardclick").DynamicInvoke(board, new KanbanBoardCardEventArgs(card));
        Assert.Equal("Karte", engine.Engine.Evaluate("clicked").ToString());

        var moveArgs = new KanbanBoardCardMovingEventArgs(card, from, to, 0);
        GetWired(facade, "cardmoving").DynamicInvoke(board, moveArgs);
        Assert.True(moveArgs.Cancel);
    }

    [Fact]
    public void Pivot_FieldsAndRebuild()
    {
        using var engine = CreateEngine();
        var pivot = new PivotTable();
        var facade = (JsPivotTable)JsFacadeRegistry.WrapControl(engine, pivot);
        engine.Engine.SetValue("pivot", Wrap(engine, facade));

        engine.Engine.Execute(
            "pivot.fields.add({ name: 'Region', dataPropertyName: 'region', usage: 'RowField' });" +
            "pivot.fields.add({ name: 'Umsatz', dataPropertyName: 'amount', usage: 'ValueField', aggregation: 'Sum' });" +
            "pivot.dataSource = [{ region: 'Nord', amount: 10 }, { region: 'Sued', amount: 20 }];" +
            "pivot.rebuildMatrix();");

        Assert.Equal(2, pivot.Fields.Count);
        Assert.Equal(PivotTableFieldUsage.RowField, pivot.Fields[0].Usage);
        Assert.Equal(PivotTableAggregation.Sum, pivot.Fields[1].Aggregation);
        Assert.Equal("ValueField", engine.Engine.Evaluate("pivot.fields.getAt(1).usage").ToString());

        engine.Engine.Execute("pivot.setFilter('region', 'Nord'); pivot.clearFilters(); pivot.expandAll(); pivot.collapseAll();");
    }

    [Fact]
    public void Pivot_CellClickPayload_CarriesTotalFlag()
    {
        using var engine = CreateEngine();
        var pivot = new PivotTable();
        var facade = (JsPivotTable)JsFacadeRegistry.WrapControl(engine, pivot);
        engine.Engine.SetValue("pivot", Wrap(engine, facade));
        engine.Engine.Execute("var logRow = -1; var total = null; pivot.on('cellclick', function (e) { total = e.payload; logRow = e.row; });");

        GetWired(facade, "cellclick").DynamicInvoke(pivot, new PivotTableCellClickEventArgs(1, 2, true));

        Assert.True((bool)engine.Engine.Evaluate("total").ToObject()!);
        Assert.Equal(1, (int)engine.Engine.Evaluate("logRow").AsNumber());
    }

    [Fact]
    public void Diagram_SeriesAxesAndTrendLine()
    {
        using var engine = CreateEngine();
        var chart = new DiagramView();
        var facade = (JsDiagramView)JsFacadeRegistry.WrapControl(engine, chart);
        engine.Engine.SetValue("chart", Wrap(engine, facade));

        engine.Engine.Execute(
            "chart.chartType = 'Line'; chart.title = 'Umsatz'; chart.showLegend = true; chart.legendPosition = 'Bottom';" +
            "chart.series.add('Q1', [{ label: 'Jan', value: 120 }, { label: 'Feb', value: 140, color: '#00ff00' }]);" +
            "chart.xAxis.visible = true; chart.yAxis.minValue = 0; chart.yAxis.maxValue = 200;" +
            "chart.trendLine.type = 'LinearRegression';");

        Assert.Equal(DiagramType.Line, chart.ChartType);
        Assert.Equal("Umsatz", chart.Title);
        Assert.Equal(LegendPosition.Bottom, chart.LegendPosition);
        Assert.Single(chart.Series);
        Assert.Equal(2, chart.Series[0].Points.Count);
        Assert.Equal("Jan", chart.Series[0].Points[0].Label);
        Assert.Equal(140d, chart.Series[0].Points[1].Value);
        Assert.Equal(Color.FromArgb(0, 255, 0), chart.Series[0].Points[1].Color!.Value);
        Assert.True(chart.XAxis.Visible);
        Assert.Equal(0d, chart.YAxis.MinValue);
        Assert.Equal(200d, chart.YAxis.MaxValue);
        Assert.Equal(TrendLineType.LinearRegression, chart.TrendLine.Type);
        Assert.Equal("Line", engine.Engine.Evaluate("chart.chartType").ToString());

        engine.Engine.Execute("chart.resetView(); chart.fitToBounds();");
    }

    [Fact]
    public void HtmlBox_PropsAndLinkClickCancel()
    {
        using var engine = CreateEngine();
        var box = new HtmlBox();
        var facade = (JsHtmlBox)JsFacadeRegistry.WrapControl(engine, box);
        engine.Engine.SetValue("box", Wrap(engine, facade));

        engine.Engine.Execute("box.html = '<b>Hi</b>'; box.readOnly = true; box.showToolbar = false; box.linkBehavior = 'RaiseEvent';");
        Assert.Contains("Hi", box.Html);
        Assert.True(box.ReadOnly);
        Assert.False(box.ShowToolbar);
        Assert.Equal(LinkBehavior.RaiseEvent, box.LinkBehavior);

        engine.Engine.Execute("var url = ''; box.on('linkclick', function (e) { url = e.value; e.handled = true; });");
        var args = new HtmlLinkEventArgs("https://example.com", null);
        GetWired(facade, "linkclick").DynamicInvoke(box, args);
        Assert.Equal("https://example.com", engine.Engine.Evaluate("url").ToString());
        Assert.True(args.Handled);

        Assert.True(EventHasSubscribers(box, "ContentChanged") == false);
        engine.Engine.Execute("box.on('contentchanged', function () {});");
        Assert.True(EventHasSubscribers(box, "ContentChanged"));
    }
}
