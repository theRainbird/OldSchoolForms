using Jint.Native;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// Typed JS facade for a <see cref="ToolStripLabel"/>. Exposes the link state
/// and link colors in addition to the generic strip item members.
/// </summary>
public class JsToolStripLabel : JsToolStripItem
{
    /// <summary>
    /// Creates the facade around a tool strip label.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="label">The wrapped tool strip label.</param>
    public JsToolStripLabel(JsEngineHost host, ToolStripLabel label)
        : base(host, label)
    {
        Label = label;
    }

    /// <summary>The wrapped tool strip label.</summary>
    public ToolStripLabel Label { get; }

    /// <summary>Whether the label renders as a hyperlink.</summary>
    public bool IsLink
    {
        get => Label.IsLink;
        set => Label.IsLink = value;
    }

    /// <summary>
    /// The link color as a hex string (<c>#rrggbb</c> / <c>#aarrggbb</c>)
    /// or an <c>{r,g,b,a}</c> object. Invalid values are ignored.
    /// </summary>
    public JsValue LinkColor
    {
        get => JsValue.FromObject(Host.Engine, JsMarshal.FromColor(Label.LinkColor));
        set => Label.LinkColor = JsMarshal.ToColor(value, Label.LinkColor);
    }

    /// <summary>
    /// The visited link color as a hex string or <c>{r,g,b,a}</c> object.
    /// Invalid values are ignored.
    /// </summary>
    public JsValue VisitedLinkColor
    {
        get => JsValue.FromObject(Host.Engine, JsMarshal.FromColor(Label.VisitedLinkColor));
        set => Label.VisitedLinkColor = JsMarshal.ToColor(value, Label.VisitedLinkColor);
    }

    /// <summary>Whether the link has been visited.</summary>
    public bool Visited
    {
        get => Label.Visited;
        set => Label.Visited = value;
    }
}
