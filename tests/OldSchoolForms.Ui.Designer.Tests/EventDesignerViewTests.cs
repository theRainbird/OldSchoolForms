using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Designer.PropertyGrid;
using OldSchoolForms.Ui.Designer.Services;
using Xunit;
using PropertyGridControl = OldSchoolForms.Ui.Designer.PropertyGrid.PropertyGrid;

namespace OldSchoolForms.Ui.Designer.Tests;

/// <summary>
/// Verifies the property grid's properties/events toggle and the create / rename / delete
/// interactions on event rows, plus the action-based undo/redo used by the designer for
/// event binding changes.
/// </summary>
public class EventDesignerViewTests
{
    private static readonly BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    private static void SwitchView(PropertyGridControl grid, PropertyGridControl.ViewMode mode)
        => typeof(PropertyGridControl)
            .GetMethod("SwitchView", PrivateInstance)!
            .Invoke(grid, new object[] { mode });

    private static List<PropertyGridRow> RowsOf(PropertyGridControl grid)
        => (List<PropertyGridRow>)typeof(PropertyGridControl)
            .GetField("_rows", PrivateInstance)!
            .GetValue(grid)!;

    private static string EditBufferOf(PropertyGridRow row)
        => (string?)typeof(PropertyGridRow).GetField("_editBuffer", PrivateInstance)?.GetValue(row)
           ?? string.Empty;

    [Fact]
    public void DefaultView_IsProperties()
    {
        var grid = new PropertyGridControl { ShowEventsToggle = true };
        Assert.Equal(PropertyGridControl.ViewMode.Properties, grid.CurrentView);
    }

    [Fact]
    public void SwitchView_ToEvents_ChangesCurrentView()
    {
        var grid = new PropertyGridControl
        {
            ShowEventsToggle = true,
            EventStore = new EventBindingStore()
        };
        grid.BindObject(new Button { Name = "button1" });

        SwitchView(grid, PropertyGridControl.ViewMode.Events);
        Assert.Equal(PropertyGridControl.ViewMode.Events, grid.CurrentView);

        SwitchView(grid, PropertyGridControl.ViewMode.Properties);
        Assert.Equal(PropertyGridControl.ViewMode.Properties, grid.CurrentView);
    }

    [Fact]
    public void EventsView_RowsAreAllEventCategory()
    {
        var grid = new PropertyGridControl
        {
            ShowEventsToggle = true,
            EventStore = new EventBindingStore()
        };
        grid.BindObject(new Button { Name = "button1" });

        SwitchView(grid, PropertyGridControl.ViewMode.Events);
        var rows = RowsOf(grid);

        Assert.NotEmpty(rows);
        Assert.All(rows, r => Assert.Equal(EventService.EventCategory, r.Category));
        Assert.Contains(rows, r => r.Name == "Click");
    }

    [Fact]
    public void PropertiesView_RowsAreNotEventCategory()
    {
        var grid = new PropertyGridControl
        {
            ShowEventsToggle = true,
            EventStore = new EventBindingStore()
        };
        grid.BindObject(new Button { Name = "button1" });

        SwitchView(grid, PropertyGridControl.ViewMode.Properties);
        var rows = RowsOf(grid);

        Assert.NotEmpty(rows);
        Assert.DoesNotContain(rows, r => r.Category == EventService.EventCategory);
    }

    [Fact]
    public void EventsView_NonEventTarget_HasNoRows()
    {
        var grid = new PropertyGridControl
        {
            ShowEventsToggle = true,
            EventStore = new EventBindingStore()
        };
        grid.BindObject("a plain string");

        SwitchView(grid, PropertyGridControl.ViewMode.Events);
        Assert.Empty(RowsOf(grid));
    }

    [Fact]
    public void StartEdit_EmptyEventBinding_PrefillsSuggestedName()
    {
        var store = new EventBindingStore();
        var button = new Button { Name = "saveButton" };
        var click = EventService.GetEventDescriptors(button, store).Single(d => d.Name == "Click");
        var row = new PropertyGridRow(click, 140);

        row.StartEdit();

        Assert.Equal("saveButton_Click", EditBufferOf(row));
    }

    [Fact]
    public void StartEdit_ExistingEventBinding_KeepsCurrentValue()
    {
        var store = new EventBindingStore();
        var button = new Button { Name = "saveButton" };
        store.SetHandler(button, "Click", "ExistingHandler");
        var click = EventService.GetEventDescriptors(button, store).Single(d => d.Name == "Click");
        var row = new PropertyGridRow(click, 140);

        row.StartEdit();

        Assert.Equal("ExistingHandler", EditBufferOf(row));
    }

    [Fact]
    public void CommitEdit_RenamesBinding()
    {
        var store = new EventBindingStore();
        var button = new Button { Name = "button1" };
        store.SetHandler(button, "Click", "OldName");
        var click = EventService.GetEventDescriptors(button, store).Single(d => d.Name == "Click");
        var row = new PropertyGridRow(click, 140);

        row.StartEdit();
        SetBuffer(row, "RenamedHandler");
        row.CommitEdit();

        Assert.Equal("RenamedHandler", store.GetHandler(button, "Click"));
    }

    [Fact]
    public void TryHandleClearClick_NonEmptyBinding_RemovesIt()
    {
        var store = new EventBindingStore();
        var button = new Button { Name = "button1" };
        store.SetHandler(button, "Click", "Button1_Click");
        var click = EventService.GetEventDescriptors(button, store).Single(d => d.Name == "Click");
        var row = new PropertyGridRow(click, 140);

        int valueColumnX = 140;
        int valueColumnWidth = 160;
        int clearX = valueColumnX + valueColumnWidth - PropertyGridRow.ClearButtonWidth + 3;

        bool handled = row.TryHandleClearClick(clearX, 10, valueColumnX, valueColumnWidth);

        Assert.True(handled);
        Assert.Equal(string.Empty, store.GetHandler(button, "Click"));
    }

    [Fact]
    public void TryHandleClearClick_EmptyBinding_ReturnsFalse()
    {
        var store = new EventBindingStore();
        var button = new Button { Name = "button1" };
        var click = EventService.GetEventDescriptors(button, store).Single(d => d.Name == "Click");
        var row = new PropertyGridRow(click, 140);

        int valueColumnX = 140;
        int valueColumnWidth = 160;
        int clearX = valueColumnX + valueColumnWidth - PropertyGridRow.ClearButtonWidth + 3;

        Assert.False(row.TryHandleClearClick(clearX, 10, valueColumnX, valueColumnWidth));
    }

    [Fact]
    public void TryHandleClearClick_OffButton_ReturnsFalse()
    {
        var store = new EventBindingStore();
        var button = new Button { Name = "button1" };
        store.SetHandler(button, "Click", "Button1_Click");
        var click = EventService.GetEventDescriptors(button, store).Single(d => d.Name == "Click");
        var row = new PropertyGridRow(click, 140);

        int valueColumnX = 140;
        int valueColumnWidth = 160;

        Assert.False(row.TryHandleClearClick(valueColumnX + 5, 10, valueColumnX, valueColumnWidth));
    }

    [Fact]
    public void TryHandleClearClick_NonEventRow_ReturnsFalse()
    {
        var property = typeof(Button).GetProperty(nameof(Button.Text))!;
        var row = new PropertyGridRow(new PropertyDescriptor(property, new Button { Text = "x" }), 140);

        Assert.False(row.TryHandleClearClick(290, 10, 140, 160));
    }

    [Fact]
    public void UndoRedo_EventBinding_TogglesHandler()
    {
        var store = new EventBindingStore();
        var button = new Button { Name = "button1" };
        var undo = new UndoService();

        string oldHandler = string.Empty;
        string newHandler = "Button1_Click";
        store.SetHandler(button, "Click", newHandler);
        undo.PushAction(
            () => store.SetHandler(button, "Click", oldHandler),
            () => store.SetHandler(button, "Click", newHandler));

        undo.Undo();
        Assert.Equal(string.Empty, store.GetHandler(button, "Click"));

        undo.Redo();
        Assert.Equal("Button1_Click", store.GetHandler(button, "Click"));
    }

    private static void SetBuffer(PropertyGridRow row, string value)
        => typeof(PropertyGridRow).GetField("_editBuffer", PrivateInstance)!.SetValue(row, value);
}
