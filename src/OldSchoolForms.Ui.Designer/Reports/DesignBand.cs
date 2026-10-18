using System;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Reports;
using Graphics = OldSchoolForms.Ui.Rendering.Graphics;

namespace OldSchoolForms.Ui.Designer.Reports;

/// <summary>
/// Design-time container for a single report band. The container occupies the band's
/// rectangle inside the <see cref="DesignReport"/> printable child space and hosts the
/// band's control proxies as children.
/// <para>
/// The container is visually transparent: the page root renders the band strip, fill,
/// and gutter label. Resizing the container's height writes the value back to
/// <see cref="ReportBand.Height"/> in centimeters; position and width are locked to
/// the band layout and snap back after any external change.
/// </para>
/// </summary>
public class DesignBand : ContainerControl
{
    private bool _syncing;

    /// <summary>
    /// Gets the report band this container represents.
    /// </summary>
    public ReportBand Band { get; }

    /// <summary>
    /// Gets the design report root that owns the band layout.
    /// </summary>
    public DesignReport BandOwner { get; }

    /// <summary>
    /// Gets or sets the report group this band belongs to. Set for group header and
    /// group footer bands; <see langword="null"/> for the fixed report bands.
    /// </summary>
    public ReportGroup? OwnerGroup { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DesignBand"/> class.
    /// </summary>
    /// <param name="owner">The design report root owning the band layout.</param>
    /// <param name="band">The report band this container represents.</param>
    /// <exception cref="ArgumentNullException">Thrown when either argument is null.</exception>
    public DesignBand(DesignReport owner, ReportBand band)
    {
        BandOwner = owner ?? throw new ArgumentNullException(nameof(owner));
        Band = band ?? throw new ArgumentNullException(nameof(band));
        BackColor = Color.Transparent;
    }

    /// <summary>
    /// Gets the child render offset: band content starts at the container origin.
    /// </summary>
    protected override Point GetChildRenderOffset() => Point.Empty;

    /// <summary>
    /// Gets the child clip rectangle covering the full band area.
    /// </summary>
    protected override Rectangle GetChildClipRectangle() => new(0, 0, Width, Height);

    /// <summary>
    /// Gets the child layout area covering the full band area.
    /// </summary>
    protected override Rectangle GetChildLayoutArea() => new(0, 0, Width, Height);

    /// <summary>
    /// Synchronizes a user-driven height change back to the band model in centimeters
    /// and re-applies the band layout so position and width stay locked to the page.
    /// </summary>
    protected override void OnBoundsChanged()
    {
        base.OnBoundsChanged();
        if (_syncing || BandOwner.IsLayingOut) return;

        float modelHeightPx = BandOwner.CmToPx(Band.Height);
        // Tolerance covers the minimum display height of collapsed bands so the
        // minimum does not write a bogus height back into the model.
        if (Math.Abs(Height - modelHeightPx) > 2f)
        {
            _syncing = true;
            try
            {
                Band.Height = BandOwner.PxToCm(Height);
            }
            finally
            {
                _syncing = false;
            }
        }

        BandOwner.RefreshGeometry();
    }

    /// <summary>
    /// Renders the band's child content. The band strip itself is drawn by the page
    /// root, so this container only forwards rendering to its children.
    /// </summary>
    /// <param name="g">The graphics context.</param>
    public override void Render(Graphics g)
    {
        base.Render(g);
    }
}
