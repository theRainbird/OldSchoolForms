using System.Data;
using System.Reflection;
using Jint.Native;
using OldSchoolForms.Shared.Scripting;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade;

/// <summary>
/// JS-friendly wrapper around a <see cref="Control"/>. Provides a curated,
/// stable API so that JavaScript does not access the raw CLR surface directly.
/// Properties are written in PascalCase; thanks to the engine's camelCase
/// fallback they are reachable from JS as <c>text</c>, <c>visible</c>, etc.
/// Typed facades (e.g. <c>JsCheckBox</c>) derive from this class and are resolved
/// through the <see cref="JsFacadeRegistry"/>.
/// </summary>
public class JsControl
{
    /// <summary>The JS engine used for event callbacks.</summary>
    protected readonly JsEngineHost Host;

    /// <summary>The event handlers registered through <see cref="On"/>, for teardown.</summary>
    protected readonly List<(string Event, Delegate Handler)> Wired = new();

    /// <summary>
    /// Creates a facade around a control.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="control">The wrapped control.</param>
    public JsControl(JsEngineHost host, Control control)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(control);
        Host = host;
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

    /// <summary>
    /// The text of the control. Some controls (e.g. <c>TextBox</c>, <c>ComboBox</c>)
    /// shadow <see cref="Control.Text"/> with a <c>new</c> property backed by their own
    /// storage; this reads/writes the most-derived <c>Text</c> so the facade reflects
    /// the value the user sees and the data binding writes, rather than the base field.
    /// </summary>
    public string Text
    {
        get => ReadText();
        set => WriteText(value ?? string.Empty);
    }

    private string ReadText()
    {
        var prop = Control.GetType().GetProperty("Text", BindingFlags.Public | BindingFlags.Instance);
        if (prop != null && prop.CanRead && prop.DeclaringType != typeof(Control))
            return (prop.GetValue(Control) as string) ?? string.Empty;
        return Control.Text;
    }

    private void WriteText(string value)
    {
        var prop = Control.GetType().GetProperty("Text", BindingFlags.Public | BindingFlags.Instance);
        if (prop != null && prop.CanWrite && prop.DeclaringType != typeof(Control))
            prop.SetValue(Control, value);
        else
            Control.Text = value;
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
        return found == null ? null : JsFacadeRegistry.WrapControl(Host, found);
    }

    /// <summary>
    /// The direct children of this control.
    /// </summary>
    /// <returns>A list of child facades.</returns>
    public JsControl[] Children()
    {
        var list = new List<JsControl>();
        foreach (Control c in Control.Controls)
            list.Add(JsFacadeRegistry.WrapControl(Host, c));
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
            throw new InvalidOperationException(string.Format(LangRes.GetEnglish("Runtime_NotAContainer"), Control.GetType().Name));
        return child;
    }

    /// <summary>
    /// Binds a <see cref="JsDataTable"/> to a <see cref="DataGridView"/> for
    /// read-only display. Each non-deleted row becomes a grid row; cell values are
    /// matched to the grid columns by <c>DataPropertyName</c>. The grid is not
    /// two-way bound — edit the table facade and re-bind to refresh.
    /// </summary>
    /// <param name="table">The table facade to display, or <c>null</c> to clear.</param>
    /// <exception cref="InvalidOperationException">The wrapped control is not a <see cref="DataGridView"/>.</exception>
    public void BindTable(JsDataTable? table)
    {
        if (Control is not DataGridView grid)
            throw new InvalidOperationException(string.Format(LangRes.GetEnglish("Runtime_NotAContainer"), Control.GetType().Name));

        grid.Rows.Clear();
        if (table == null)
        {
            grid.Invalidate();
            return;
        }

        var dt = table.Table;
        foreach (DataRow dr in dt.Rows)
        {
            if (dr.RowState == DataRowState.Deleted)
                continue;

            var row = new DataGridViewRow { DataBoundItem = dr };
            foreach (var column in grid.Columns)
            {
                object? value = null;
                if (!string.IsNullOrEmpty(column.DataPropertyName) && dt.Columns.Contains(column.DataPropertyName))
                    value = dr[column.DataPropertyName];
                row.Cells.Add(new DataGridViewCell { Value = value });
            }

            grid.Rows.Add(row);
        }

        grid.Invalidate();
    }

    /// <summary>
    /// Binds a <see cref="JsBindingSource"/> to a <see cref="DataGridView"/> for a
    /// live, filtered and sorted view. The grid follows the binding source's
    /// <c>ListChanged</c> notifications, and cell edits are written back to the
    /// underlying <see cref="DataRow"/> (marking it modified for optimistic
    /// concurrency via <c>getChanges()</c>).
    /// </summary>
    /// <param name="source">The binding source facade, or <c>null</c> to clear.</param>
    /// <exception cref="InvalidOperationException">The wrapped control is not a <see cref="DataGridView"/>.</exception>
    public void BindSource(JsBindingSource? source)
    {
        if (Control is not DataGridView grid)
            throw new InvalidOperationException(string.Format(LangRes.GetEnglish("Runtime_NotAContainer"), Control.GetType().Name));

        grid.DataSource = source?.Source;
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
    public virtual void On(string eventName, JsValue callback)
    {
        if (callback is not Jint.Native.Function.Function)
            throw new ArgumentException(LangRes.GetEnglish("Runtime_CallbackMustBeFunction"), nameof(callback));

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
            _ => throw new ArgumentException(string.Format(LangRes.GetEnglish("Runtime_UnknownEvent"), eventName), nameof(eventName))
        };

        Wire(normalized, handler);
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
    /// Raises a JavaScript callback for an event within the owner module's scope.
    /// Typed facades use this for their control-specific events.
    /// </summary>
    /// <param name="type">The normalized event type name.</param>
    /// <param name="callback">The JavaScript function.</param>
    /// <param name="e">The CLR event arguments.</param>
    protected void Raise(string type, JsValue callback, EventArgs e)
    {
        var jsEvent = BuildEvent(type, e);
        try
        {
            using (Host.UseModule(OwnerModuleId))
                Host.CallFunction(callback, Jint.Native.JsValue.FromObject(Host.Engine, jsEvent));
        }
        catch (Exception ex)
        {
            Host.ReportError($"Event '{type}' on '{Control.Name}'", ex);
        }
        finally
        {
            if (jsEvent.Handled && e is KeyEventArgs ke)
                ke.Handled = true;
            if (jsEvent.Handled && e is KeyPressEventArgs kpe)
                kpe.Handled = true;
        }
    }

    protected JsEvent BuildEvent(string type, EventArgs e)
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

    protected virtual void Wire(string normalized, Delegate handler)
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

    protected virtual void Unwire(string normalized, Delegate handler)
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
