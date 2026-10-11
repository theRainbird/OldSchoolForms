using Jint.Native;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Runtime.Serialization;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade;

/// <summary>
/// JS-friendly wrapper around a <see cref="ToolStripItem"/> (button, label or
/// separator living inside a <see cref="ToolStrip"/>). Provides a curated, stable
/// API so JavaScript can read/write the item's caption and enabled/visible state
/// and subscribe to its <c>click</c> event. Properties are written in PascalCase;
/// thanks to the engine's camelCase fallback they are reachable from JS as
/// <c>text</c>, <c>enabled</c>, <c>visible</c>, etc.
/// </summary>
public class JsToolStripItem
{
    /// <summary>The JS engine used for event callbacks.</summary>
    protected readonly JsEngineHost Host;

    /// <summary>The event handlers registered through <see cref="On"/>, for teardown.</summary>
    protected readonly List<(string Event, Delegate Handler)> Wired = new();

    /// <summary>
    /// Creates a facade around a tool strip item.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="item">The wrapped tool strip item.</param>
    public JsToolStripItem(JsEngineHost host, ToolStripItem item)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(item);
        Host = host;
        Item = item;
    }

    /// <summary>The wrapped tool strip item.</summary>
    public ToolStripItem Item { get; }

    /// <summary>
    /// The ID of the module that owns this facade. Event callbacks run within this
    /// module's scope, so timers created in handlers are cleared when the module closes.
    /// </summary>
    public string? OwnerModuleId { get; set; }

    /// <summary>The name of the item.</summary>
    public string Name
    {
        get => Item.Name;
        set => Item.Name = value ?? string.Empty;
    }

    /// <summary>The text of the item.</summary>
    public string Text
    {
        get => Item.Text;
        set => Item.Text = value ?? string.Empty;
    }

    /// <summary>Enabled state of the item.</summary>
    public bool Enabled
    {
        get => Item.Enabled;
        set => Item.Enabled = value;
    }

    /// <summary>Visibility of the item.</summary>
    public bool Visible
    {
        get => Item.Visible;
        set => Item.Visible = value;
    }

    /// <summary>
    /// Sets the item's icon from a core icon key (for example <c>nav_first</c> or
    /// <c>NavFirst</c>). Assigning an unknown or empty key leaves the current image unchanged.
    /// Setting this to a non-null image does not change the <c>displayStyle</c>; use
    /// <see cref="DisplayStyle"/> to control whether the icon, the text, or both are shown.
    /// </summary>
    public string? Icon
    {
        set
        {
            var icon = RuntimeLayoutLoader.ResolveIcon(value);
            if (icon != null)
                Item.Image = icon;
        }
    }

    /// <summary>
    /// Gets or sets how the item renders its icon and text. Accepts <c>Image</c>,
    /// <c>ImageAndText</c> or <c>Text</c> (case-insensitive). Invalid values are ignored.
    /// </summary>
    public string? DisplayStyle
    {
        get => Item.DisplayStyle.ToString();
        set
        {
            if (Enum.TryParse<ToolStripItemDisplayStyle>(value, true, out var style))
                Item.DisplayStyle = style;
        }
    }

    /// <summary>
    /// Programmatically raises the item's <c>click</c> event (honoring its enabled state).
    /// </summary>
    public void PerformClick() => Item.PerformClick();

    /// <summary>
    /// Registers a JavaScript callback for an event.
    /// Supported: click, plus typed-facade events via <see cref="WireCustom"/>.
    /// </summary>
    /// <param name="eventName">The event name (case-insensitive).</param>
    /// <param name="callback">The JavaScript function.</param>
    /// <exception cref="ArgumentException">Unknown event or callback is not a function.</exception>
    public virtual void On(string eventName, JsValue callback)
    {
        if (callback is not Jint.Native.Function.Function)
            throw new ArgumentException(LangRes.GetEnglish("Runtime_CallbackMustBeFunction"), nameof(callback));

        var normalized = (eventName ?? string.Empty).Trim().ToLowerInvariant();

        var customHandler = WireCustom(normalized, callback);
        if (customHandler != null)
        {
            Wired.Add((normalized, customHandler));
            return;
        }

        Delegate handler = normalized switch
        {
            "click" => new EventHandler((s, e) => Raise("click", callback, e)),
            _ => throw new ArgumentException(string.Format(LangRes.GetEnglish("Runtime_UnknownEvent"), eventName), nameof(eventName))
        };

        Item.Click += (EventHandler)handler;
        Wired.Add((normalized, handler));
    }

    /// <summary>
    /// Removes all registered callbacks (on module teardown).
    /// </summary>
    public virtual void OffAll()
    {
        foreach (var (evt, handler) in Wired)
            Unwire(evt, handler);
        Wired.Clear();
    }

    /// <summary>
    /// Hook for typed facades to wire item-specific events. Return the created
    /// handler when the event was handled; return <see langword="null"/> to let
    /// the base class wire the generic <c>click</c> event.
    /// </summary>
    /// <param name="normalized">The normalized (lowercase) event name.</param>
    /// <param name="callback">The JavaScript function.</param>
    /// <returns>The wired handler, or <see langword="null"/> when not handled.</returns>
    protected virtual Delegate? WireCustom(string normalized, JsValue callback) => null;

    /// <summary>
    /// Hook for typed facades to unwire item-specific events created by
    /// <see cref="WireCustom"/>. Return <see langword="true"/> when the event was
    /// handled; otherwise the base class attempts the generic events.
    /// </summary>
    /// <param name="normalized">The normalized (lowercase) event name.</param>
    /// <param name="handler">The handler originally created by <see cref="WireCustom"/>.</param>
    /// <returns><see langword="true"/> when the handler was unwired.</returns>
    protected virtual bool UnwireCustom(string normalized, Delegate handler) => false;

    /// <summary>
    /// Removes a previously registered handler for the given event.
    /// </summary>
    /// <param name="normalized">The normalized (lowercase) event name.</param>
    /// <param name="handler">The handler to remove.</param>
    protected virtual void Unwire(string normalized, Delegate handler)
    {
        if (UnwireCustom(normalized, handler))
            return;

        if (normalized == "click")
            Item.Click -= (EventHandler)handler;
    }

    /// <summary>
    /// Raises a JavaScript callback for an event within the owner module's scope.
    /// Typed facades use this for their item-specific events.
    /// </summary>
    /// <param name="type">The normalized event type name.</param>
    /// <param name="callback">The JavaScript function.</param>
    /// <param name="e">The CLR event arguments.</param>
    protected void Raise(string type, JsValue callback, EventArgs e)
    {
        var jsEvent = new JsEvent(type, null) { Inner = e };
        RaiseEvent(type, callback, jsEvent);
    }

    /// <summary>
    /// Raises a pre-built <see cref="JsEvent"/> within the owner module's scope.
    /// </summary>
    /// <param name="type">The normalized event type name.</param>
    /// <param name="callback">The JavaScript function.</param>
    /// <param name="jsEvent">The pre-built JS event.</param>
    protected void RaiseEvent(string type, JsValue callback, JsEvent jsEvent)
    {
        try
        {
            using (Host.UseModule(OwnerModuleId))
                Host.CallFunction(callback, JsValue.Undefined, JsValue.FromObject(Host.Engine, jsEvent));
        }
        catch (Exception ex)
        {
            Host.ReportError($"Event '{type}' on strip item '{Item.Name}'", ex);
        }
    }
}
