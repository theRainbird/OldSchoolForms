using Jint.Native;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade;

/// <summary>
/// JS-friendly wrapper around a <see cref="Control"/>. Provides a curated,
/// stable API so that JavaScript does not access the raw CLR surface directly.
/// Properties are written in PascalCase; thanks to the engine's camelCase
/// fallback they are reachable from JS as <c>text</c>, <c>visible</c>, etc.
/// </summary>
public sealed class JsControl
{
    private readonly JsEngineHost _host;
    private readonly List<(string Event, Delegate Handler)> _wired = new();

    /// <summary>
    /// Creates a facade around a control.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="control">The wrapped control.</param>
    public JsControl(JsEngineHost host, Control control)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(control);
        _host = host;
        Control = control;
    }

    /// <summary>The wrapped control.</summary>
    public Control Control { get; }

    /// <summary>
    /// The ID of the module that owns this facade. Event callbacks run within this
    /// module's scope, so timers created in handlers are cleared when the module closes.
    /// </summary>
    public string? OwnerModuleId { get; set; }

    /// <summary>The name of the control.</summary>
    public string Name
    {
        get => Control.Name;
        set => Control.Name = value ?? string.Empty;
    }

    /// <summary>The text of the control.</summary>
    public string Text
    {
        get => Control.Text;
        set => Control.Text = value ?? string.Empty;
    }

    /// <summary>Visibility of the control.</summary>
    public bool Visible
    {
        get => Control.Visible;
        set => Control.Visible = value;
    }

    /// <summary>Enabled state of the control.</summary>
    public bool Enabled
    {
        get => Control.Enabled;
        set => Control.Enabled = value;
    }

    /// <summary>X position.</summary>
    public int X
    {
        get => Control.X;
        set => Control.X = value;
    }

    /// <summary>Y position.</summary>
    public int Y
    {
        get => Control.Y;
        set => Control.Y = value;
    }

    /// <summary>Width.</summary>
    public int Width
    {
        get => Control.Width;
        set => Control.Width = value;
    }

    /// <summary>Height.</summary>
    public int Height
    {
        get => Control.Height;
        set => Control.Height = value;
    }

    /// <summary>
    /// Docking behavior as a string (None, Left, Right, Top, Bottom, Fill).
    /// </summary>
    public string Dock
    {
        get => Control.Dock.ToString();
        set
        {
            if (Enum.TryParse<DockStyle>(value, true, out var style))
                Control.Dock = style;
        }
    }

    /// <summary>
    /// Recursively searches for a child control by name.
    /// </summary>
    /// <param name="name">The name of the child control.</param>
    /// <returns>The facade or <c>null</c>.</returns>
    public JsControl? Find(string name)
    {
        var found = Control.Controls.FindControl(name, true);
        return found == null ? null : new JsControl(_host, found);
    }

    /// <summary>
    /// The direct children of this control.
    /// </summary>
    /// <returns>A list of child facades.</returns>
    public JsControl[] Children()
    {
        var list = new List<JsControl>();
        foreach (Control c in Control.Controls)
            list.Add(new JsControl(_host, c));
        return list.ToArray();
    }

    /// <summary>
    /// Adds a child control to this control (which must be a container).
    /// Used for building layouts programmatically from JavaScript.
    /// </summary>
    /// <param name="child">The child facade to add.</param>
    /// <returns>The added child facade (for chaining).</returns>
    /// <exception cref="InvalidOperationException">This control is not a container.</exception>
    public JsControl Add(JsControl child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (Control is ContainerControl container)
            container.Controls.Add(child.Control);
        else
            throw new InvalidOperationException(string.Format(LangRes.GetString("Runtime_NotAContainer"), Control.GetType().Name));
        return child;
    }

    /// <summary>
    /// Focuses the control by activating its containing container.
    /// </summary>
    public void Focus()
    {
        if (Control.Parent is ContainerControl container)
            container.ActiveControl = Control;
    }

    /// <summary>
    /// Marks the control as needing to be redrawn.
    /// </summary>
    public void Invalidate() => Control.Invalidate();

    /// <summary>
    /// Registers a JavaScript callback for an event.
    /// Supported: click, dblclick, mousedown, mouseup, mousemove, mousewheel,
    /// keydown, keyup, keypress, textchanged, gotfocus, lostfocus.
    /// </summary>
    /// <param name="eventName">The event name (case-insensitive).</param>
    /// <param name="callback">The JavaScript function.</param>
    /// <exception cref="ArgumentException">Unknown event or callback is not a function.</exception>
    public void On(string eventName, JsValue callback)
    {
        if (callback is not Jint.Native.Function.Function)
            throw new ArgumentException(LangRes.GetString("Runtime_CallbackMustBeFunction"), nameof(callback));

        var normalized = (eventName ?? string.Empty).Trim().ToLowerInvariant();
        Delegate handler = normalized switch
        {
            "click" => new EventHandler((s, e) => Raise("click", callback, e)),
            "dblclick" => new EventHandler((s, e) => Raise("dblclick", callback, e)),
            "mousedown" => new EventHandler((s, e) => Raise("mousedown", callback, e)),
            "mouseup" => new EventHandler((s, e) => Raise("mouseup", callback, e)),
            "mousemove" => new EventHandler((s, e) => Raise("mousemove", callback, e)),
            "mousewheel" => new EventHandler((s, e) => Raise("mousewheel", callback, e)),
            "keydown" => new EventHandler((s, e) => Raise("keydown", callback, e)),
            "keyup" => new EventHandler((s, e) => Raise("keyup", callback, e)),
            "keypress" => new EventHandler((s, e) => Raise("keypress", callback, e)),
            "textchanged" => new System.ComponentModel.PropertyChangedEventHandler((s, e) =>
            {
                if (e.PropertyName == nameof(Control.Text))
                    Raise("textchanged", callback, EventArgs.Empty);
            }),
            "gotfocus" => new EventHandler((s, e) => Raise("gotfocus", callback, e)),
            "lostfocus" => new EventHandler((s, e) => Raise("lostfocus", callback, e)),
            _ => throw new ArgumentException(string.Format(LangRes.GetString("Runtime_UnknownEvent"), eventName), nameof(eventName))
        };

        Wire(normalized, handler);
        _wired.Add((normalized, handler));
    }

    /// <summary>
    /// Removes all registered callbacks (on module teardown).
    /// </summary>
    public void OffAll()
    {
        foreach (var (evt, handler) in _wired)
            Unwire(evt, handler);
        _wired.Clear();
    }

    private void Raise(string type, JsValue callback, EventArgs e)
    {
        var jsEvent = BuildEvent(type, e);
        try
        {
            using (_host.UseModule(OwnerModuleId))
                _host.CallFunction(callback, Jint.Native.JsValue.FromObject(_host.Engine, jsEvent));
        }
        catch (Exception ex)
        {
            _host.ReportError($"Event '{type}' on '{Control.Name}'", ex);
        }
        finally
        {
            if (jsEvent.Handled && e is KeyEventArgs ke)
                ke.Handled = true;
            if (jsEvent.Handled && e is KeyPressEventArgs kpe)
                kpe.Handled = true;
        }
    }

    private JsEvent BuildEvent(string type, EventArgs e)
    {
        var jsEvent = new JsEvent(type, this) { Inner = e };
        switch (e)
        {
            case MouseEventArgs me:
                jsEvent.X = me.X;
                jsEvent.Y = me.Y;
                jsEvent.Button = me.Button.ToString();
                jsEvent.Delta = me.Delta;
                break;
            case KeyEventArgs ke:
                jsEvent.KeyCode = ke.KeyCode.ToString();
                jsEvent.Shift = (ke.Modifiers & ModifierKeys.Shift) != 0;
                jsEvent.Control = (ke.Modifiers & ModifierKeys.Control) != 0;
                jsEvent.Alt = (ke.Modifiers & ModifierKeys.Alt) != 0;
                break;
            case KeyPressEventArgs kpe:
                jsEvent.KeyChar = kpe.KeyChar.ToString();
                break;
        }

        return jsEvent;
    }

    private void Wire(string normalized, Delegate handler)
    {
        switch (normalized)
        {
            case "click": Control.Click += (EventHandler)handler; break;
            case "dblclick": Control.DoubleClick += (EventHandler)handler; break;
            case "mousedown": Control.MouseDown += (EventHandler)handler; break;
            case "mouseup": Control.MouseUp += (EventHandler)handler; break;
            case "mousemove": Control.MouseMove += (EventHandler)handler; break;
            case "mousewheel": Control.MouseWheel += (EventHandler)handler; break;
            case "keydown": Control.KeyDown += (EventHandler)handler; break;
            case "keyup": Control.KeyUp += (EventHandler)handler; break;
            case "keypress": Control.KeyPress += (EventHandler)handler; break;
            case "textchanged": Control.PropertyChanged += (System.ComponentModel.PropertyChangedEventHandler)handler; break;
            case "gotfocus": Control.GotFocus += (EventHandler)handler; break;
            case "lostfocus": Control.LostFocus += (EventHandler)handler; break;
        }
    }

    private void Unwire(string normalized, Delegate handler)
    {
        switch (normalized)
        {
            case "click": Control.Click -= (EventHandler)handler; break;
            case "dblclick": Control.DoubleClick -= (EventHandler)handler; break;
            case "mousedown": Control.MouseDown -= (EventHandler)handler; break;
            case "mouseup": Control.MouseUp -= (EventHandler)handler; break;
            case "mousemove": Control.MouseMove -= (EventHandler)handler; break;
            case "mousewheel": Control.MouseWheel -= (EventHandler)handler; break;
            case "keydown": Control.KeyDown -= (EventHandler)handler; break;
            case "keyup": Control.KeyUp -= (EventHandler)handler; break;
            case "keypress": Control.KeyPress -= (EventHandler)handler; break;
            case "textchanged": Control.PropertyChanged -= (System.ComponentModel.PropertyChangedEventHandler)handler; break;
            case "gotfocus": Control.GotFocus -= (EventHandler)handler; break;
            case "lostfocus": Control.LostFocus -= (EventHandler)handler; break;
        }
    }
}
