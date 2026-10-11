using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// Typed JS facade for a <see cref="StatusStrip"/>. Exposes the status labels
/// and allows creating them programmatically.
/// </summary>
public class JsStatusStrip : JsControl
{
    /// <summary>
    /// Creates the facade around a status strip.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="statusStrip">The wrapped status strip.</param>
    public JsStatusStrip(JsEngineHost host, StatusStrip statusStrip)
        : base(host, statusStrip)
    {
        StatusStrip = statusStrip;
    }

    /// <summary>The wrapped status strip.</summary>
    public StatusStrip StatusStrip { get; }

    /// <summary>The status label facades, in order.</summary>
    public JsStatusLabel[] Items
    {
        get
        {
            var list = new List<JsStatusLabel>();
            foreach (var label in StatusStrip.Items)
                list.Add(new JsStatusLabel(Host, label));
            return list.ToArray();
        }
    }

    /// <summary>
    /// Creates a status label with the given text and appends it.
    /// </summary>
    /// <param name="text">The label text.</param>
    /// <returns>The facade of the new status label.</returns>
    public JsStatusLabel AddItem(string text)
    {
        var label = new ToolStripStatusLabel { Text = text ?? string.Empty };
        StatusStrip.Items.Add(label);
        return new JsStatusLabel(Host, label);
    }
}
