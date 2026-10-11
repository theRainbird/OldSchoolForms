using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// Typed JS facade for a <see cref="PictureBox"/>. Exposes the image sizing
/// mode on top of the generic control members.
/// </summary>
public class JsPictureBox : JsControl
{
    /// <summary>
    /// Creates the facade around a picture box.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="pictureBox">The wrapped picture box.</param>
    public JsPictureBox(JsEngineHost host, PictureBox pictureBox)
        : base(host, pictureBox)
    {
        PictureBox = pictureBox;
    }

    /// <summary>The wrapped picture box.</summary>
    public PictureBox PictureBox { get; }

    /// <summary>
    /// How the image is sized inside the box
    /// (Normal, StretchImage, AutoSize, CenterImage, Zoom).
    /// </summary>
    public string SizeMode
    {
        get => PictureBox.SizeMode.ToString();
        set
        {
            if (Enum.TryParse<PictureBoxSizeMode>(value, true, out var mode))
                PictureBox.SizeMode = mode;
        }
    }
}
