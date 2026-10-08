using Jint.Native;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Core;
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
public sealed class JsToolStripItem
{
    private readonly JsEngineHost _host;
    private readonly List<(string Event, Delegate Handler)> _wired = new();

    /// <summary>
    /// Creates a facade around a tool strip item.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="item">The wrapped tool strip item.</param>
    public JsToolStripItem(JsEngineHost host, ToolStripItem item)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(item);
        _host = host;
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
    /// Programmatically raises the item's <c>click</c> event (honoring its enabled state).
    /// </summary>
    public void PerformClick() => Item.PerformClick();

    /// <summary>
    /// Registers a JavaScript callback for an event.
    /// Supported: click.
    /// </summary>
    /// <param name="eventName">The event name (case-insensitive).</param>
    /// <param name="callback">The JavaScript function.</param>
    /// <exception cref="ArgumentException">Unknown event or callback is not a function.</exception>
    public void On(string eventName, JsValue callback)
    {
        if (callback is not Jint.Native.Function.Function)
            throw new ArgumentException(LangRes.GetEnglish("Runtime_CallbackMustBeFunction"), nameof(callback));

        var normalized = (eventName ?? string.Empty).Trim().ToLowerInvariant();
        Delegate handler = normalized switch
        {
            "click" => new EventHandler((s, e) => Raise("click", callback, e)),
            _ => throw new ArgumentException(string.Format(LangRes.GetEnglish("Runtime_UnknownEvent"), eventName), nameof(eventName))
        };

        Item.Click += (EventHandler)handler;
        _wired.Add((normalized, handler));
    }

    /// <summary>
    /// Removes all registered callbacks (on module teardown).
    /// </summary>
    public void OffAll()
    {
        foreach (var (_, handler) in _wired)
            Item.Click -= (EventHandler)handler;
        _wired.Clear();
    }

    private void Raise(string type, JsValue callback, EventArgs e)
    {
        var jsEvent = new JsEvent(type, null) { Inner = e };
        try
        {
            using (_host.UseModule(OwnerModuleId))
                _host.CallFunction(callback, Jint.Native.JsValue.FromObject(_host.Engine, jsEvent));
        }
        catch (Exception ex)
        {
            _host.ReportError($"Event '{type}' on strip item '{Item.Name}'", ex);
        }
    }
}
