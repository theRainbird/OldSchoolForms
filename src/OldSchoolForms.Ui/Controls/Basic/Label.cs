using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Theming;
using Graphics = OldSchoolForms.Ui.Rendering.Graphics;

namespace OldSchoolForms.Ui.Controls.Basic;

/// <summary>
/// A control that displays static text.
/// </summary>
[ToolboxIcon("OldSchoolForms.Ui.Resources.Icons.FluentColor.ic_fluent_document_text_16_color.svg")]
public class Label : Control
{
    private ContentAlignment _textAlign = ContentAlignment.MiddleLeft;

    /// <summary>
    /// Initializes a new instance of Label.
    /// </summary>
    public Label()
    {
        _backColor = Color.Transparent;
        Size = new Size(150, 28);
    }

    /// <summary>
    /// Gets or sets the alignment of the text within the label bounds.
    /// </summary>
    public ContentAlignment TextAlign
    {
        get => _textAlign;
        set
        {
            if (_textAlign != value)
            {
                _textAlign = value;
                Invalidate();
            }
        }
    }

    /// <summary>
    /// Called when the theme changes. Updates ForeColor while keeping the transparent background.
    /// </summary>
    /// <param name="newTheme">The new theme that was activated.</param>
    public override void OnThemeChanged(Theme newTheme)
    {
        if (!_foreColorSet)
            _foreColor = newTheme.ControlText;
        Invalidate();
    }

    /// <summary>
    /// Renders the label with its text.
    /// </summary>
    /// <param name="g">The Graphics object to use for rendering.</param>
    public override void Render(Rendering.Graphics g)
    {
        if (Visible)
        {
            if (BackColor.A > 0)
            {
                g.FillRectangle(BackColor, 0, 0, Width, Height);
            }

            var theme = ThemeManager.CurrentTheme;
            var textColor = Enabled ? ForeColor : theme.GrayText;
            var font = EffectiveFont;
            var zoom = EffectiveZoom;
            var (textWidth, _) = CoordinateTransform.MeasureText(Text, font, zoom);
            float scaledSize = font.Size * zoom;

            float x = _textAlign switch
            {
                ContentAlignment.TopCenter or ContentAlignment.MiddleCenter or ContentAlignment.BottomCenter => (Width - textWidth) / 2f,
                ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight => Width - textWidth - 3,
                _ => 3
            };

            float y = _textAlign switch
            {
                ContentAlignment.TopLeft or ContentAlignment.TopCenter or ContentAlignment.TopRight => 0f,
                ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight => Height - scaledSize,
                _ => CoordinateTransform.CenterVertically(Height, font, zoom)
            };

            g.DrawString(Text, font, textColor, x, y);

            base.Render(g);
        }
    }
}