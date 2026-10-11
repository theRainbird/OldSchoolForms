using Jint.Native;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade;

/// <summary>
/// JS-friendly wrapper around the main <see cref="Form"/>. Allows JavaScript
/// window operations and finding controls without exposing the raw CLR surface.
/// </summary>
public sealed class JsForm
{
    private readonly JsEngineHost _host;
    private readonly List<Delegate> _wired = new();

    /// <summary>
    /// Creates the facade around a form.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="form">The wrapped form.</param>
    public JsForm(JsEngineHost host, Form form)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(form);
        _host = host;
        Form = form;
    }

    /// <summary>The wrapped form.</summary>
    public Form Form { get; }

    /// <summary>Window title.</summary>
    public string Title
    {
        get => Form.Text;
        set => Form.Text = value ?? string.Empty;
    }

    /// <summary>Window width.</summary>
    public int Width
    {
        get => Form.Width;
        set => Form.Width = value;
    }

    /// <summary>Window height.</summary>
    public int Height
    {
        get => Form.Height;
        set => Form.Height = value;
    }

    /// <summary>
    /// Closes the window.
    /// </summary>
    public void Close() => Form.Close();

    /// <summary>Minimizes the window.</summary>
    public void Minimize() => Form.Minimize();

    /// <summary>Maximizes the window.</summary>
    public void Maximize() => Form.Maximize();

    /// <summary>Restores the window.</summary>
    public void Restore() => Form.Restore();

    /// <summary>
    /// Finds a control in the window by name.
    /// </summary>
    /// <param name="name">The control name.</param>
    /// <returns>The facade or <c>null</c>.</returns>
    public JsControl? Find(string name)
    {
        var found = Form.Controls.FindControl(name, true);
        return found == null ? null : JsFacadeRegistry.WrapControl(_host, found);
    }

    /// <summary>
    /// Adds a control directly to the form. Used for building layouts
    /// programmatically from JavaScript.
    /// </summary>
    /// <param name="child">The control facade to add.</param>
    /// <returns>The added child facade (for chaining).</returns>
    public JsControl Add(JsControl child)
    {
        ArgumentNullException.ThrowIfNull(child);
        Form.Controls.Add(child.Control);
        return child;
    }

    /// <summary>
    /// Registers a callback for the window closing. If the callback sets
    /// <c>handled = true</c>, the close is canceled.
    /// </summary>
    /// <param name="callback">The JavaScript function.</param>
    public void OnClosing(JsValue callback)
    {
        if (callback is not Jint.Native.Function.Function)
            throw new ArgumentException(LangRes.GetEnglish("Runtime_CallbackMustBeFunction"), nameof(callback));

        var handler = new EventHandler((s, e) =>
        {
            var jsEvent = new JsEvent("closing", new JsControl(_host, Form)) { Inner = e };
            try
            {
                _host.CallFunction(callback, Jint.Native.JsValue.FromObject(_host.Engine, jsEvent));
                if (jsEvent.Handled && e is CancelEventArgs ce)
                    ce.Cancel = true;
            }
            catch (Exception ex)
            {
                _host.ReportError("closing", ex);
            }
        });

        Form.FormClosing += handler;
        _wired.Add(handler);
    }

    /// <summary>
    /// Removes all registered callbacks.
    /// </summary>
    public void OffAll()
    {
        foreach (var handler in _wired)
            Form.FormClosing -= (EventHandler)handler;
        _wired.Clear();
    }
}
