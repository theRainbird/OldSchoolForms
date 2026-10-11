using Jint.Native;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// Typed JS facade for a <see cref="SplitPanel"/>. Exposes the splitter geometry,
/// the two child panels and the <c>splittermoving</c>/<c>splittermoved</c> events.
/// The new splitter position is available as <c>e.x</c> on the moving event.
/// </summary>
public class JsSplitPanel : JsControl
{
    /// <summary>
    /// Creates the facade around a split panel.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="splitPanel">The wrapped split panel.</param>
    public JsSplitPanel(JsEngineHost host, SplitPanel splitPanel)
        : base(host, splitPanel)
    {
        SplitPanel = splitPanel;
    }

    /// <summary>The wrapped split panel.</summary>
    public SplitPanel SplitPanel { get; }

    /// <summary>
    /// The orientation as a string (<c>Horizontal</c> = left/right, <c>Vertical</c> = top/bottom).
    /// Invalid values are ignored.
    /// </summary>
    public string Orientation
    {
        get => SplitPanel.Orientation.ToString();
        set
        {
            if (Enum.TryParse<SplitOrientation>(value, true, out var orientation))
                SplitPanel.Orientation = orientation;
        }
    }

    /// <summary>Thickness of the splitter bar in pixels.</summary>
    public int SplitterWidth
    {
        get => SplitPanel.SplitterWidth;
        set => SplitPanel.SplitterWidth = value;
    }

    /// <summary>Position of the splitter in pixels.</summary>
    public int SplitterDistance
    {
        get => SplitPanel.SplitterDistance;
        set => SplitPanel.SplitterDistance = value;
    }

    /// <summary>Minimum size of panel 1 in pixels.</summary>
    public int Panel1MinSize
    {
        get => SplitPanel.Panel1MinSize;
        set => SplitPanel.Panel1MinSize = value;
    }

    /// <summary>Minimum size of panel 2 in pixels.</summary>
    public int Panel2MinSize
    {
        get => SplitPanel.Panel2MinSize;
        set => SplitPanel.Panel2MinSize = value;
    }

    /// <summary>
    /// The border style as a string (<c>None</c>, <c>FixedSingle</c>, <c>Fixed3D</c>).
    /// Invalid values are ignored.
    /// </summary>
    public string BorderStyle
    {
        get => SplitPanel.BorderStyle.ToString();
        set
        {
            if (Enum.TryParse<Controls.Containers.BorderStyle>(value, true, out var style))
                SplitPanel.BorderStyle = style;
        }
    }

    /// <summary>The left/top panel facade.</summary>
    public JsControl Panel1 => JsFacadeRegistry.WrapControl(Host, SplitPanel.Panel1);

    /// <summary>The right/bottom panel facade.</summary>
    public JsControl Panel2 => JsFacadeRegistry.WrapControl(Host, SplitPanel.Panel2);

    /// <inheritdoc />
    protected override Delegate? WireCustom(string normalized, JsValue callback)
    {
        switch (normalized)
        {
            case "splittermoving":
            {
                EventHandler<SplitterDragEventArgs> handler = (s, e) => Raise("splittermoving", callback, e);
                SplitPanel.SplitterMoving += handler;
                return handler;
            }
            case "splittermoved":
            {
                EventHandler handler = (s, e) => Raise("splittermoved", callback, e);
                SplitPanel.SplitterMoved += handler;
                return handler;
            }
            default:
                return null;
        }
    }

    /// <inheritdoc />
    protected override bool UnwireCustom(string normalized, Delegate handler)
    {
        switch (normalized)
        {
            case "splittermoving":
                SplitPanel.SplitterMoving -= (EventHandler<SplitterDragEventArgs>)handler;
                return true;
            case "splittermoved":
                SplitPanel.SplitterMoved -= (EventHandler)handler;
                return true;
            default:
                return false;
        }
    }
}
