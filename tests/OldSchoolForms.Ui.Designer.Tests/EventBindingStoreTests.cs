using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Designer.Services;
using Xunit;

namespace OldSchoolForms.Ui.Designer.Tests;

/// <summary>
/// Verifies the <see cref="EventBindingStore"/> contract: the designer-side mapping of
/// event name to handler name per target object, including create, rename, remove, the
/// empty-string removal rule and the <see cref="EventBindingStore.Changed"/> notification.
/// </summary>
public class EventBindingStoreTests
{
    [Fact]
    public void SetHandler_ThenGetHandler_ReturnsName()
    {
        var store = new EventBindingStore();
        var button = new Button { Name = "button1" };

        store.SetHandler(button, "Click", "Button1_Click");

        Assert.Equal("Button1_Click", store.GetHandler(button, "Click"));
    }

    [Fact]
    public void GetHandler_UnknownTargetOrEvent_ReturnsEmpty()
    {
        var store = new EventBindingStore();
        var button = new Button { Name = "button1" };

        Assert.Equal(string.Empty, store.GetHandler(button, "Click"));
        Assert.Equal(string.Empty, store.GetHandler(null, "Click"));
        Assert.Equal(string.Empty, store.GetHandler(button, "Nope"));
    }

    [Fact]
    public void SetHandler_TrimsWhitespace()
    {
        var store = new EventBindingStore();
        var button = new Button { Name = "button1" };

        store.SetHandler(button, "Click", "  Button1_Click  ");

        Assert.Equal("Button1_Click", store.GetHandler(button, "Click"));
    }

    [Fact]
    public void SetHandler_EmptyOrNull_RemovesBinding()
    {
        var store = new EventBindingStore();
        var button = new Button { Name = "button1" };
        store.SetHandler(button, "Click", "Button1_Click");

        store.SetHandler(button, "Click", "   ");
        Assert.Equal(string.Empty, store.GetHandler(button, "Click"));
        Assert.False(store.HasBindings(button));

        store.SetHandler(button, "DoubleClick", "X");
        store.SetHandler(button, "DoubleClick", null);
        Assert.Equal(string.Empty, store.GetHandler(button, "DoubleClick"));
    }

    [Fact]
    public void SetHandler_SameValue_DoesNotRaiseChanged()
    {
        var store = new EventBindingStore();
        var button = new Button { Name = "button1" };
        store.SetHandler(button, "Click", "Button1_Click");

        int changedCount = 0;
        store.Changed += (_, _) => changedCount++;

        store.SetHandler(button, "Click", "Button1_Click");
        Assert.Equal(0, changedCount);
    }

    [Fact]
    public void SetHandler_NewValue_RaisesChanged()
    {
        var store = new EventBindingStore();
        var button = new Button { Name = "button1" };

        int changedCount = 0;
        store.Changed += (_, _) => changedCount++;

        store.SetHandler(button, "Click", "Button1_Click");
        Assert.Equal(1, changedCount);

        store.SetHandler(button, "Click", "Renamed");
        Assert.Equal(2, changedCount);
    }

    [Fact]
    public void ClearHandler_RemovesAndDropsEmptyTarget()
    {
        var store = new EventBindingStore();
        var button = new Button { Name = "button1" };
        store.SetHandler(button, "Click", "A");
        store.SetHandler(button, "DoubleClick", "B");

        store.ClearHandler(button, "Click");
        Assert.Equal(string.Empty, store.GetHandler(button, "Click"));
        Assert.True(store.HasBindings(button));

        store.ClearHandler(button, "DoubleClick");
        Assert.False(store.HasBindings(button));
    }

    [Fact]
    public void GetBindings_ReturnsAllForTarget_EmptyWhenNone()
    {
        var store = new EventBindingStore();
        var button = new Button { Name = "button1" };
        store.SetHandler(button, "Click", "A");
        store.SetHandler(button, "DoubleClick", "B");

        var bindings = store.GetBindings(button);
        Assert.Equal(2, bindings.Count);
        Assert.Equal("A", bindings["Click"]);
        Assert.Equal("B", bindings["DoubleClick"]);

        Assert.Empty(store.GetBindings(new Button()));
        Assert.Empty(store.GetBindings(null));
    }

    [Fact]
    public void ClearObject_DropsAllBindingsForTarget()
    {
        var store = new EventBindingStore();
        var a = new Button { Name = "a" };
        var b = new Button { Name = "b" };
        store.SetHandler(a, "Click", "A_Click");
        store.SetHandler(b, "Click", "B_Click");

        store.ClearObject(a);

        Assert.False(store.HasBindings(a));
        Assert.True(store.HasBindings(b));
    }

    [Fact]
    public void Clear_RemovesEverything_AndRaisesChanged()
    {
        var store = new EventBindingStore();
        var button = new Button { Name = "button1" };
        store.SetHandler(button, "Click", "A");

        int changedCount = 0;
        store.Changed += (_, _) => changedCount++;

        store.Clear();
        Assert.False(store.HasBindings(button));
        Assert.Equal(1, changedCount);

        // Clearing an already-empty store does not raise again.
        store.Clear();
        Assert.Equal(1, changedCount);
    }
}
