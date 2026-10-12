using OldSchoolForms.Ui.Core;

namespace OldSchoolForms.Ui.Runtime.Facade;

/// <summary>
/// JS-friendly representation of a control event. Passed to JavaScript
/// callbacks; encapsulates mouse, keyboard, and text events.
/// </summary>
public sealed class JsEvent
{
    /// <summary>
    /// Creates an event with a type and target.
    /// </summary>
    /// <param name="type">The event type (e.g. <c>click</c>).</param>
    /// <param name="target">The target facade, or <c>null</c> for non-control targets such as tool strip items.</param>
    public JsEvent(string type, JsControl? target)
    {
        Type = type;
        Target = target;
    }

    /// <summary>The event type.</summary>
    public string Type { get; }

    /// <summary>The control that raised the event, or <c>null</c> for non-control targets such as tool strip items.</summary>
    public JsControl? Target { get; }

    /// <summary>X coordinate (mouse).</summary>
    public int X { get; set; }

    /// <summary>Y coordinate (mouse).</summary>
    public int Y { get; set; }

    /// <summary>Pressed mouse button as a string.</summary>
    public string Button { get; set; } = "none";

    /// <summary>Mouse wheel delta.</summary>
    public double Delta { get; set; }

    /// <summary>Key code as a string (keyboard).</summary>
    public string KeyCode { get; set; } = string.Empty;

    /// <summary>Pressed character (keypress).</summary>
    public string KeyChar { get; set; } = string.Empty;

    /// <summary>Shift key pressed.</summary>
    public bool Shift { get; set; }

    /// <summary>Ctrl key pressed.</summary>
    public bool Control { get; set; }

    /// <summary>Alt key pressed.</summary>
    public bool Alt { get; set; }

    /// <summary>
    /// Set to <see langword="true"/> by the callback to prevent further
    /// processing (for keyboard events).
    /// </summary>
    public bool Handled { get; set; }

    /// <summary>
    /// Additional event data, e.g. the affected tool strip item facade for
    /// <c>itemadded</c>/<c>itemremoved</c> events. <see langword="null"/> when
    /// the event carries no extra payload.
    /// </summary>
    public object? Payload { get; set; }

    /// <summary>Row index for grid- and pivot-style cell events.</summary>
    public int Row { get; set; }

    /// <summary>Column index for grid- and pivot-style cell events.</summary>
    public int Column { get; set; }

    /// <summary>
    /// Event value, e.g. the selected date of a calendar event or the display
    /// value of a grid cell during formatting. For formatting events the callback
    /// can assign a new value and set <see cref="Handled"/> to apply it.
    /// </summary>
    public object? Value { get; set; }

    internal EventArgs? Inner { get; set; }
}
