using Jint.Native;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// Typed JS facade for a <see cref="FontPicker"/>. Exposes the selected font,
/// preview text and the <c>selectedfontchanged</c> event.
/// </summary>
public class JsFontPicker : JsControl
{
    /// <summary>
    /// Creates the facade around a font picker.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="fontPicker">The wrapped font picker.</param>
    public JsFontPicker(JsEngineHost host, FontPicker fontPicker)
        : base(host, fontPicker)
    {
        FontPicker = fontPicker;
    }

    /// <summary>The wrapped font picker.</summary>
    public FontPicker FontPicker { get; }

    /// <summary>The selected font family name.</summary>
    public string SelectedFontFamily
    {
        get => FontPicker.SelectedFontFamily;
        set => FontPicker.SelectedFontFamily = value ?? string.Empty;
    }

    /// <summary>The selected font size in points.</summary>
    public float FontSize
    {
        get => FontPicker.FontSize;
        set => FontPicker.FontSize = value;
    }

    /// <summary>The text shown as a font preview.</summary>
    public string PreviewText
    {
        get => FontPicker.PreviewText;
        set => FontPicker.PreviewText = value ?? string.Empty;
    }

    /// <summary>Maximum height of the drop-down list in pixels.</summary>
    public int DropDownHeight
    {
        get => FontPicker.DropDownHeight;
        set => FontPicker.DropDownHeight = value;
    }

    /// <summary>Whether the drop-down list is currently open (read-only).</summary>
    public bool DroppedDown => FontPicker.DroppedDown;

    /// <summary>Reloads the available font families from the font manager.</summary>
    public void RefreshFontList() => FontPicker.RefreshFontList();

    /// <inheritdoc />
    protected override Delegate? WireCustom(string normalized, JsValue callback)
    {
        if (normalized != "selectedfontchanged")
            return null;

        EventHandler handler = (s, e) => Raise("selectedfontchanged", callback, e);
        FontPicker.SelectedFontChanged += handler;
        return handler;
    }

    /// <inheritdoc />
    protected override bool UnwireCustom(string normalized, Delegate handler)
    {
        if (normalized != "selectedfontchanged")
            return false;

        FontPicker.SelectedFontChanged -= (EventHandler)handler;
        return true;
    }
}
