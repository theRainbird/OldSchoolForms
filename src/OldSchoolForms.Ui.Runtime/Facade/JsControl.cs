using Jint.Native;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade;

/// <summary>
/// JS-freundliche Hülle um ein <see cref="Control"/>. Stellt eine kuratierte,
/// stabile API bereit, damit JavaScript nicht direkt auf die rohe CLR-Oberfläche
/// zugreift. Eigenschaften sind in PascalCase geschrieben; über den
/// camelCase-Fallback der Engine sind sie aus JS als <c>text</c>, <c>visible</c>
/// usw. erreichbar.
/// </summary>
public sealed class JsControl
{
    private readonly JsEngineHost _host;
    private readonly List<(string Event, Delegate Handler)> _wired = new();

    /// <summary>
    /// Erzeugt eine Fassade um ein Steuerelement.
    /// </summary>
    /// <param name="host">Die JS-Engine für Callbacks.</param>
    /// <param name="control">Das umhüllte Steuerelement.</param>
    public JsControl(JsEngineHost host, Control control)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(control);
        _host = host;
        Control = control;
    }

    /// <summary>Das umhüllte Steuerelement.</summary>
    public Control Control { get; }

    /// <summary>Der Name des Steuerelements.</summary>
    public string Name
    {
        get => Control.Name;
        set => Control.Name = value ?? string.Empty;
    }

    /// <summary>Der Text des Steuerelements.</summary>
    public string Text
    {
        get => Control.Text;
        set => Control.Text = value ?? string.Empty;
    }

    /// <summary>Sichtbarkeit des Steuerelements.</summary>
    public bool Visible
    {
        get => Control.Visible;
        set => Control.Visible = value;
    }

    /// <summary>Aktivierung des Steuerelements.</summary>
    public bool Enabled
    {
        get => Control.Enabled;
        set => Control.Enabled = value;
    }

    /// <summary>X-Position.</summary>
    public int X
    {
        get => Control.X;
        set => Control.X = value;
    }

    /// <summary>Y-Position.</summary>
    public int Y
    {
        get => Control.Y;
        set => Control.Y = value;
    }

    /// <summary>Breite.</summary>
    public int Width
    {
        get => Control.Width;
        set => Control.Width = value;
    }

    /// <summary>Höhe.</summary>
    public int Height
    {
        get => Control.Height;
        set => Control.Height = value;
    }

    /// <summary>
    /// Dock-Verhalten als String (None, Left, Right, Top, Bottom, Fill).
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
    /// Sucht ein Kind-Steuerelement rekursiv nach Namen.
    /// </summary>
    /// <param name="name">Der Name des Kind-Steuerelements.</param>
    /// <returns>Die Fassade oder <c>null</c>.</returns>
    public JsControl? Find(string name)
    {
        var found = Control.Controls.FindControl(name, true);
        return found == null ? null : new JsControl(_host, found);
    }

    /// <summary>
    /// Die direkten Kinder dieses Steuerelements.
    /// </summary>
    /// <returns>Eine Liste der Kind-Fassaden.</returns>
    public JsControl[] Children()
    {
        var list = new List<JsControl>();
        foreach (Control c in Control.Controls)
            list.Add(new JsControl(_host, c));
        return list.ToArray();
    }

    /// <summary>
    /// Fokussiert das Steuerelement, indem der enthaltende Container aktiviert wird.
    /// </summary>
    public void Focus()
    {
        if (Control.Parent is ContainerControl container)
            container.ActiveControl = Control;
    }

    /// <summary>
    /// Markiert das Steuerelement als neu zu zeichnen.
    /// </summary>
    public void Invalidate() => Control.Invalidate();

    /// <summary>
    /// Registriert einen JavaScript-Callback für ein Ereignis.
    /// Unterstützt: click, dblclick, mousedown, mouseup, mousemove, mousewheel,
    /// keydown, keyup, keypress, textchanged, gotfocus, lostfocus.
    /// </summary>
    /// <param name="eventName">Der Ereignisname (case-insensitive).</param>
    /// <param name="callback">Die JavaScript-Funktion.</param>
    /// <exception cref="ArgumentException">Unbekanntes Ereignis oder Callback keine Funktion.</exception>
    public void On(string eventName, JsValue callback)
    {
        if (callback is not Jint.Native.Function.Function)
            throw new ArgumentException("Callback muss eine Funktion sein.", nameof(callback));

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
            _ => throw new ArgumentException($"Unbekanntes Ereignis: {eventName}", nameof(eventName))
        };

        Wire(normalized, handler);
        _wired.Add((normalized, handler));
    }

    /// <summary>
    /// Entfernt alle registrierten Callbacks (beim Modul-Teardown).
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
            _host.CallFunction(callback, Jint.Native.JsValue.FromObject(_host.Engine, jsEvent));
        }
        catch (Exception ex)
        {
            _host.ReportError($"Event '{type}' auf '{Control.Name}'", ex);
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
