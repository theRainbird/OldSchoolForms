using OldSchoolForms.Ui.Core;

namespace OldSchoolForms.Ui.Runtime.Facade;

/// <summary>
/// JS-freundliche Repräsentation eines Steuerereignisses. Wird an JavaScript-
/// Callbacks übergeben und kapselt Maus-, Tastatur- und Textereignisse.
/// </summary>
public sealed class JsEvent
{
    /// <summary>
    /// Erzeugt ein Ereignis mit Typ und Ziel.
    /// </summary>
    /// <param name="type">Der Ereignistyp (z. B. <c>click</c>).</param>
    /// <param name="target">Die Ziel-Fassade.</param>
    public JsEvent(string type, JsControl target)
    {
        Type = type;
        Target = target;
    }

    /// <summary>Der Ereignistyp.</summary>
    public string Type { get; }

    /// <summary>Das auslösende Steuerelement.</summary>
    public JsControl Target { get; }

    /// <summary>X-Koordinate (Maus).</summary>
    public int X { get; set; }

    /// <summary>Y-Koordinate (Maus).</summary>
    public int Y { get; set; }

    /// <summary>Gedrückte Maustaste als String.</summary>
    public string Button { get; set; } = "none";

    /// <summary>Mausrad-Delta.</summary>
    public double Delta { get; set; }

    /// <summary>Tastencode als String (Tastatur).</summary>
    public string KeyCode { get; set; } = string.Empty;

    /// <summary>Gedrücktes Zeichen (Keypress).</summary>
    public string KeyChar { get; set; } = string.Empty;

    /// <summary>Shift-Taste gedrückt.</summary>
    public bool Shift { get; set; }

    /// <summary>Strg-Taste gedrückt.</summary>
    public bool Control { get; set; }

    /// <summary>Alt-Taste gedrückt.</summary>
    public bool Alt { get; set; }

    /// <summary>
    /// Vom Callback auf <see langword="true"/> gesetzt, um die weitere Verarbeitung
    /// zu verhindern (bei Tastaturereignissen).
    /// </summary>
    public bool Handled { get; set; }

    internal EventArgs? Inner { get; set; }
}
