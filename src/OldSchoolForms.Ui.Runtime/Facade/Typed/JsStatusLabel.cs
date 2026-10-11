using Jint.Native;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// JS-friendly wrapper around a <see cref="ToolStripStatusLabel"/>. Status labels
/// are components, not controls, so this facade stands on its own next to
/// <see cref="JsControl"/>.
/// </summary>
public class JsStatusLabel
{
    /// <summary>
    /// Creates the facade around a status label.
    /// </summary>
    /// <param name="host">The JS engine for value conversion.</param>
    /// <param name="label">The wrapped status label.</param>
    public JsStatusLabel(JsEngineHost host, ToolStripStatusLabel label)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(label);
        Host = host;
        Label = label;
    }

    /// <summary>The JS engine used for value conversion.</summary>
    protected JsEngineHost Host { get; }

    /// <summary>The wrapped status label.</summary>
    public ToolStripStatusLabel Label { get; }

    /// <summary>The text of the status label.</summary>
    public string Text
    {
        get => Label.Text;
        set => Label.Text = value ?? string.Empty;
    }

    /// <summary>Visibility of the status label.</summary>
    public bool Visible
    {
        get => Label.Visible;
        set => Label.Visible = value;
    }

    /// <summary>
    /// The foreground color as a hex string (<c>#rrggbb</c> / <c>#aarrggbb</c>)
    /// or an <c>{r,g,b,a}</c> object. Invalid values are ignored.
    /// </summary>
    public JsValue ForeColor
    {
        get => JsValue.FromObject(Host.Engine, JsMarshal.FromColor(Label.ForeColor));
        set
        {
            var color = JsMarshal.ToColor(value, Label.ForeColor);
            Label.ForeColor = color;
        }
    }
}
