using System.Linq;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Designer.PropertyGrid;
using OldSchoolForms.Ui.Designer.Services;
using Xunit;

namespace OldSchoolForms.Ui.Designer.Tests;

/// <summary>
/// Verifies <see cref="EventService"/> enumeration of the curated event set and the
/// delegate-backed <see cref="PropertyDescriptor"/> instances it produces, including the
/// suggested handler name, the read/write round-trip through the store and the
/// <see cref="PropertyValueChangedEventArgs.IsEventBinding"/> flag.
/// </summary>
public class EventServiceTests
{
    [Fact]
    public void IsEventTarget_TrueForControlAndToolStripItem()
    {
        Assert.True(EventService.IsEventTarget(new Button()));
        Assert.True(EventService.IsEventTarget(new Control()));
        Assert.True(EventService.IsEventTarget(new ToolStripButton()));
    }

    [Fact]
    public void IsEventTarget_FalseForPlainObjects()
    {
        Assert.False(EventService.IsEventTarget("a string"));
        Assert.False(EventService.IsEventTarget(new object()));
        Assert.False(EventService.IsEventTarget(null));
    }

    [Fact]
    public void GetEventDescriptors_Button_IncludesCuratedEvents_SortedByOrdinal()
    {
        var store = new EventBindingStore();
        var button = new Button { Name = "button1" };

        var descriptors = EventService.GetEventDescriptors(button, store);
        var names = descriptors.Select(d => d.Name).ToList();

        Assert.Contains("Click", names);
        Assert.Contains("DoubleClick", names);
        Assert.Contains("MouseDown", names);
        Assert.Contains("KeyDown", names);
        Assert.Contains("GotFocus", names);

        // Sorted by ordinal name.
        Assert.Equal(names.OrderBy(n => n, System.StringComparer.Ordinal), names);
    }

    [Fact]
    public void GetEventDescriptors_ExcludesInfrastructureEvents()
    {
        var store = new EventBindingStore();
        var button = new Button { Name = "button1" };

        var names = EventService.GetEventDescriptors(button, store).Select(d => d.Name).ToList();

        Assert.DoesNotContain("PropertyChanged", names);
        Assert.DoesNotContain("BoundsChanged", names);
    }

    [Fact]
    public void GetEventDescriptors_AllRows_AreEventBindingsWithStringType()
    {
        var store = new EventBindingStore();
        var button = new Button { Name = "button1" };

        foreach (var d in EventService.GetEventDescriptors(button, store))
        {
            Assert.True(d.IsEventBinding);
            Assert.Equal(typeof(string), d.PropertyType);
            Assert.Equal(EventService.EventCategory, d.Category);
            Assert.False(d.IsReadOnly);
        }
    }

    [Fact]
    public void GetEventDescriptors_SuggestedName_IsControlNameUnderscoreEvent()
    {
        var store = new EventBindingStore();
        var button = new Button { Name = "saveButton" };

        var click = EventService.GetEventDescriptors(button, store).Single(d => d.Name == "Click");

        Assert.Equal("saveButton_Click", click.SuggestedValue);
    }

    [Fact]
    public void GetEventDescriptors_SanitizesInvalidNameCharacters()
    {
        var store = new EventBindingStore();
        var button = new Button { Name = "my button!" };

        var click = EventService.GetEventDescriptors(button, store).Single(d => d.Name == "Click");

        // SanitizeIdentifier keeps only letters/digits/underscore (space and '!' dropped).
        Assert.Equal("mybutton_Click", click.SuggestedValue);
    }

    [Fact]
    public void Descriptor_GetterReadsStore_SetterWritesStore()
    {
        var store = new EventBindingStore();
        var button = new Button { Name = "button1" };
        var click = EventService.GetEventDescriptors(button, store).Single(d => d.Name == "Click");

        Assert.Equal(string.Empty, click.GetValue());

        click.SetValue("MyHandler");
        Assert.Equal("MyHandler", click.GetValue());
        Assert.Equal("MyHandler", store.GetHandler(button, "Click"));
    }

    [Fact]
    public void Descriptor_SetValue_RaisesValueChanged_WithIsEventBinding()
    {
        var store = new EventBindingStore();
        var button = new Button { Name = "button1" };
        var click = EventService.GetEventDescriptors(button, store).Single(d => d.Name == "Click");

        PropertyValueChangedEventArgs? captured = null;
        click.ValueChanged += (_, e) => captured = e;

        click.SetValue("HandlerA");

        Assert.NotNull(captured);
        Assert.True(captured!.IsEventBinding);
        Assert.Equal("Click", captured.PropertyName);
        Assert.Equal(string.Empty, captured.OldValue);
        Assert.Equal("HandlerA", captured.NewValue);
        Assert.Same(button, captured.Target);
    }

    [Fact]
    public void GetEventDescriptors_NullStoreOrNullTarget_ReturnsEmpty()
    {
        Assert.Empty(EventService.GetEventDescriptors(new Button(), null));
        Assert.Empty(EventService.GetEventDescriptors(null, new EventBindingStore()));
    }

    [Fact]
    public void GetEventDescriptors_NonEventTarget_ReturnsEmpty()
    {
        var store = new EventBindingStore();
        Assert.Empty(EventService.GetEventDescriptors("not a control", store));
    }

    [Fact]
    public void ToolStripItem_HasClickEventDescriptor()
    {
        var store = new EventBindingStore();
        var item = new ToolStripButton { Text = "X" };

        var names = EventService.GetEventDescriptors(item, store).Select(d => d.Name).ToList();

        Assert.Contains("Click", names);
    }
}
