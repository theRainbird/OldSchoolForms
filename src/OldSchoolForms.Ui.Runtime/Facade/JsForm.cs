using Jint.Native;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade;

/// <summary>
/// JS-freundliche Hülle um das Haupt-<see cref="Form"/>. Erlaubt JavaScript
/// Fensteroperationen und das Auffinden von Steuerelementen, ohne die rohe
/// CLR-Oberfläche freizugeben.
/// </summary>
public sealed class JsForm
{
    private readonly JsEngineHost _host;
    private readonly List<Delegate> _wired = new();

    /// <summary>
    /// Erzeugt die Fassade um ein Formular.
    /// </summary>
    /// <param name="host">Die JS-Engine für Callbacks.</param>
    /// <param name="form">Das umhüllte Formular.</param>
    public JsForm(JsEngineHost host, Form form)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(form);
        _host = host;
        Form = form;
    }

    /// <summary>Das umhüllte Formular.</summary>
    public Form Form { get; }

    /// <summary>Fenstertitel.</summary>
    public string Title
    {
        get => Form.Text;
        set => Form.Text = value ?? string.Empty;
    }

    /// <summary>Fensterbreite.</summary>
    public int Width
    {
        get => Form.Width;
        set => Form.Width = value;
    }

    /// <summary>Fensterhöhe.</summary>
    public int Height
    {
        get => Form.Height;
        set => Form.Height = value;
    }

    /// <summary>
    /// Schließt das Fenster.
    /// </summary>
    public void Close() => Form.Close();

    /// <summary>Minimiert das Fenster.</summary>
    public void Minimize() => Form.Minimize();

    /// <summary>Maximiert das Fenster.</summary>
    public void Maximize() => Form.Maximize();

    /// <summary>Stellt das Fenster wieder her.</summary>
    public void Restore() => Form.Restore();

    /// <summary>
    /// Sucht ein Steuerelement im Fenster nach Namen.
    /// </summary>
    /// <param name="name">Der Steuername.</param>
    /// <returns>Die Fassade oder <c>null</c>.</returns>
    public JsControl? Find(string name)
    {
        var found = Form.Controls.FindControl(name, true);
        return found == null ? null : new JsControl(_host, found);
    }

    /// <summary>
    /// Registriert einen Callback für das Schließen des Fensters. Setzt der
    /// Callback <c>handled = true</c>, wird das Schließen abgebrochen.
    /// </summary>
    /// <param name="callback">Die JavaScript-Funktion.</param>
    public void OnClosing(JsValue callback)
    {
        if (callback is not Jint.Native.Function.Function)
            throw new ArgumentException("Callback muss eine Funktion sein.", nameof(callback));

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
    /// Entfernt alle registrierten Callbacks.
    /// </summary>
    public void OffAll()
    {
        foreach (var handler in _wired)
            Form.FormClosing -= (EventHandler)handler;
        _wired.Clear();
    }
}
