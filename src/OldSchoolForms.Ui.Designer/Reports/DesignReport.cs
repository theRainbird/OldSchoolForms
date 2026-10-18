using System;
using System.Collections.Generic;
using System.Linq;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Reports;
using OldSchoolForms.Ui.Theming;
using Graphics = OldSchoolForms.Ui.Rendering.Graphics;

namespace OldSchoolForms.Ui.Designer.Reports;

/// <summary>
/// Design-time root for a banded report. Renders the page sheet, the margin guides,
/// and the stacked band layout of the wrapped <see cref="Reports.Report"/>.
/// <para>
/// The child coordinate space of this container is the <em>printable area</em>: the
/// origin (0, 0) is the top-left corner inside the page margins. This mirrors the
/// report model, where band content and control coordinates are relative to the
/// left/top margins. Band children are positioned at their cumulative band offset.
/// </para>
/// <para>
/// The control size is locked to the page size derived from
/// <see cref="ReportPageSetup"/> and <see cref="PageZoom"/>; resizing snaps back.
/// Page geometry changes go through <see cref="RefreshGeometry"/>.
/// </para>
/// </summary>
public class DesignReport : ContainerControl
{
    private const float BaseDpi = 96f;
    private const float MinBandDisplayPx = 16f;
    private bool _applyingGeometry;
    private bool _layingOut;
    private bool _syncingProxies;
    private float _pageZoom = 1f;
    private ReportRenderContext? _designContext;
    private readonly List<DesignBand> _bandContainers = new();

    /// <summary>
    /// Gets the design surface this root is hosted on, or <see langword="null"/> before
    /// <see cref="AttachSurface"/> was called.
    /// </summary>
    internal DesignSurface? Surface { get; private set; }

    /// <summary>
    /// Gets whether the band layout is currently being applied. Used to suppress
    /// write-back synchronization from the band containers during layout.
    /// </summary>
    internal bool IsLayingOut => _layingOut;

    /// <summary>
    /// Gets the printable width in pixels at the current zoom.
    /// </summary>
    internal float PrintableWidthPx => CmToPx(Report.PrintableWidth);

    /// <summary>
    /// Gets the report model this root renders and that design edits write through to.
    /// </summary>
    public Report Report { get; }

    /// <summary>
    /// Gets or sets the display zoom of the page. A value of 1 renders centimeters at
    /// 96 DPI; the persisted report model is unaffected by zoom.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not between 0.25 and 4.0.</exception>
    public float PageZoom
    {
        get => _pageZoom;
        set
        {
            if (value < 0.25f || value > 4.0f)
                throw new ArgumentOutOfRangeException(nameof(value), LangRes.GetString("ReportDesigner_ZoomRange"));
            if (Math.Abs(_pageZoom - value) < 0.0001f) return;
            _pageZoom = value;
            RefreshGeometry();
        }
    }

    /// <summary>
    /// Initializes a new instance of <see cref="DesignReport"/> wrapping the given report.
    /// </summary>
    /// <param name="report">The report model to design. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="report"/> is null.</exception>
    public DesignReport(Report report)
    {
        Report = report ?? throw new ArgumentNullException(nameof(report));
        BackColor = Color.White;
        RefreshGeometry();
    }

    /// <summary>
    /// Converts a centimeter measurement to design-surface pixels at the current zoom.
    /// </summary>
    /// <param name="cm">The measurement in centimeters.</param>
    /// <returns>The length in pixels.</returns>
    public float CmToPx(Cm cm) => cm.ToPixelF(BaseDpi) * _pageZoom;

    /// <summary>
    /// Converts a pixel length at the current zoom back to centimeters.
    /// </summary>
    /// <param name="px">The length in pixels.</param>
    /// <returns>The measurement in centimeters.</returns>
    public Cm PxToCm(float px) => Cm.FromMm(px * 25.4 / (BaseDpi * _pageZoom));

    /// <summary>
    /// Gets the display height of a band in pixels at the current zoom. Zero-height
    /// bands are shown with a minimum height so their labels and hit areas remain
    /// readable and clickable instead of stacking on top of each other.
    /// </summary>
    /// <param name="band">The band to measure for display.</param>
    /// <returns>The display height in pixels, at least <see cref="MinBandDisplayPx"/>.</returns>
    private float DisplayBandHeightPx(ReportBand band) => Math.Max(MinBandDisplayPx, CmToPx(band.Height));

    /// <summary>
    /// Gets the cached design-time render context used to render report controls
    /// WYSIWYG on the design surface. The context is rebuilt whenever the geometry
    /// or content changes.
    /// </summary>
    /// <returns>A render context with a synthetic sample record at 96 DPI.</returns>
    internal ReportRenderContext GetDesignContext()
    {
        if (_designContext == null)
        {
            _designContext = new ReportRenderContext(Report)
            {
                RenderDpi = BaseDpi,
                PageNumber = 1,
                TotalPages = 1,
                CurrentRecord = DesignRecord.Build(Report)
            };
        }
        return _designContext;
    }

    /// <summary>
    /// Gets the bands in render order: report header, page header, group headers
    /// (outer to inner), detail, group footers (inner to outer), page footer,
    /// report footer.
    /// </summary>
    /// <returns>The ordered list of bands displayed on the page.</returns>
    public IReadOnlyList<ReportBand> GetBands()
    {
        var bands = new List<ReportBand>
        {
            Report.ReportHeader,
            Report.PageHeader
        };
        AppendGroupHeaders(bands, Report.Groups);
        bands.Add(Report.Detail);
        AppendGroupFooters(bands, Report.Groups);
        bands.Add(Report.PageFooter);
        bands.Add(Report.ReportFooter);
        return bands;
    }

    private static void AppendGroupHeaders(List<ReportBand> bands, ReportGroupCollection groups)
    {
        foreach (var group in groups)
        {
            bands.Add(group.Header);
            AppendGroupHeaders(bands, group.Groups);
        }
    }

    private static void AppendGroupFooters(List<ReportBand> bands, ReportGroupCollection groups)
    {
        for (int i = groups.Count - 1; i >= 0; i--)
        {
            AppendGroupFooters(bands, groups[i].Groups);
            bands.Add(groups[i].Footer);
        }
    }

    /// <summary>
    /// Gets the vertical offset, in pixels within the printable child space, of the
    /// given band — the sum of the heights of all bands rendered before it.
    /// </summary>
    /// <param name="band">The band to locate.</param>
    /// <returns>The top offset in pixels.</returns>
    /// <exception cref="ArgumentException">Thrown when the band is not part of this report's render order.</exception>
    public float GetBandTopPx(ReportBand band)
    {
        float y = 0;
        foreach (var b in GetBands())
        {
            if (ReferenceEquals(b, band)) return y;
            y += DisplayBandHeightPx(b);
        }
        throw new ArgumentException(LangRes.GetString("ReportDesigner_BandNotInReport"), nameof(band));
    }

    /// <summary>
    /// Reapplies the page size derived from the report's page setup and the current
    /// zoom, and repositions band children. Call after changing page setup or bands.
    /// </summary>
    public void RefreshGeometry()
    {
        _designContext = null;
        var size = ComputePageSize();
        if (_applyingGeometry) return;
        _applyingGeometry = true;
        try
        {
            Size = size;
        }
        finally
        {
            _applyingGeometry = false;
        }
        LayoutBands();
        Invalidate();
    }

    private Size ComputePageSize() => new(
        Math.Max(1, (int)Math.Round(CmToPx(Report.PageWidth))),
        Math.Max(1, (int)Math.Round(CmToPx(Report.PageHeight))));

    /// <summary>
    /// Attaches the hosting design surface and rebuilds the band containers as tracked
    /// structural design items so selection and drop targeting work for every band.
    /// </summary>
    /// <param name="surface">The design surface hosting this root.</param>
    internal void AttachSurface(DesignSurface surface)
    {
        Surface = surface ?? throw new ArgumentNullException(nameof(surface));
        foreach (var container in _bandContainers)
            Controls.Remove(container);
        _bandContainers.Clear();
        RefreshGeometry();
    }

    /// <summary>
    /// Synchronizes the <see cref="DesignBand"/> container children with the report's
    /// current band list: creates containers for new bands, removes stale ones, and
    /// repositions all containers to their cumulative vertical offsets.
    /// </summary>
    protected virtual void LayoutBands()
    {
        if (_layingOut) return;
        _layingOut = true;
        try
        {
            var bands = GetBands();

            for (int i = _bandContainers.Count - 1; i >= 0; i--)
            {
                var container = _bandContainers[i];
                if (!bands.Contains(container.Band))
                {
                    if (Surface != null)
                        Surface.UnregisterStructuralItem(container);
                    else
                        Controls.Remove(container);
                    _bandContainers.RemoveAt(i);
                }
            }

            float y = 0;
            foreach (var band in bands)
            {
                var container = GetBandContainer(band);
                int height = (int)Math.Round(DisplayBandHeightPx(band));
                if (container == null)
                {
                    container = new DesignBand(this, band)
                    {
                        Name = BandContainerName(band)
                    };
                    if (band.BandType is ReportBandType.GroupHeader or ReportBandType.GroupFooter)
                        container.OwnerGroup = FindGroupForBand(band);

                    if (Surface != null)
                        Surface.RegisterStructuralItem(container, this);
                    else
                        Controls.Add(container);
                    _bandContainers.Add(container);
                }

                container.Bounds = new Rectangle(0, (int)Math.Round(y),
                    Math.Max(1, (int)Math.Round(PrintableWidthPx)), height);
                y += DisplayBandHeightPx(band);
            }
        }
        finally
        {
            _layingOut = false;
        }
        SyncControlProxies();
    }

    /// <summary>
    /// Synchronizes the <see cref="DesignReportControl"/> proxies with the report's
    /// band control lists: moves wrapped controls to the band their proxy currently
    /// lives in, creates proxies for new controls, repositions proxies whose wrapped
    /// geometry changed, and removes proxies that no longer belong to any band.
    /// </summary>
    internal void SyncControlProxies()
    {
        if (_syncingProxies) return;
        _syncingProxies = true;
        try
        {
            // Build a global Wrapped -> proxy map across every band container before any
            // relocation happens. A control that already owns a proxy somewhere must never
            // be given a second proxy while it is mid-move between bands: the source band's
            // model list still contains it until the destination band's pass detaches it,
            // so a per-container "is it represented here?" check would wrongly create a
            // duplicate in the source container.
            var proxyByWrapped = new Dictionary<ReportControl, DesignReportControl>();
            foreach (var container in _bandContainers)
            {
                foreach (var proxy in container.Controls.OfType<DesignReportControl>())
                {
                    if (proxy.Wrapped != null)
                        proxyByWrapped[proxy.Wrapped] = proxy;
                }
            }

            foreach (var container in _bandContainers)
            {
                var band = container.Band;

                foreach (var proxy in container.Controls.OfType<DesignReportControl>().ToList())
                {
                    if (!band.Controls.Contains(proxy.Wrapped))
                    {
                        DetachWrappedFromAllBands(proxy.Wrapped);
                        band.Controls.Add(proxy.Wrapped);
                        proxy.PushBoxToWrapped();
                    }
                }

                foreach (var modelControl in band.Controls.ToList())
                {
                    // Skip if any proxy (in this or another container) already represents
                    // this model control; a proxy living in another container will relocate
                    // the wrapped control to itself via the pass above.
                    if (proxyByWrapped.ContainsKey(modelControl)) continue;

                    var newProxy = ReportProxyFactory.Create(modelControl);
                    if (newProxy == null) continue;
                    newProxy.Name = modelControl.Name;
                    if (Surface != null)
                        Surface.RegisterStructuralItem(newProxy, container);
                    else
                        container.Controls.Add(newProxy);
                    newProxy.SetBoundsFromWrapped();
                    proxyByWrapped[modelControl] = newProxy;
                }

                foreach (var proxy in container.Controls.OfType<DesignReportControl>().ToList())
                {
                    if (!proxy.BoundsMatchWrapped())
                        proxy.SetBoundsFromWrapped();
                }
            }

            if (Surface != null)
            {
                foreach (var item in Surface.Items.ToList())
                {
                    if (item.Control is DesignReportControl orphan && orphan.OwnerBand == null)
                    {
                        DetachWrappedFromAllBands(orphan.Wrapped);
                        Surface.UnregisterStructuralItem(orphan);
                    }
                }
            }
        }
        finally
        {
            _syncingProxies = false;
        }
    }

    private void DetachWrappedFromAllBands(ReportControl wrapped)
    {
        foreach (var band in GetBands())
            band.Controls.Remove(wrapped);
    }

    /// <summary>
    /// Gets the design container for the given band, if one exists.
    /// </summary>
    /// <param name="band">The band whose container is requested.</param>
    /// <returns>The band container, or <see langword="null"/> if not found.</returns>
    internal DesignBand? GetBandContainer(ReportBand band)
    {
        foreach (var container in _bandContainers)
            if (ReferenceEquals(container.Band, band))
                return container;
        return null;
    }

    private static string BandContainerName(ReportBand band)
    {
        string baseName = band.BandType switch
        {
            ReportBandType.ReportHeader => "reportHeaderBand",
            ReportBandType.PageHeader => "pageHeaderBand",
            ReportBandType.GroupHeader => "groupHeaderBand",
            ReportBandType.Detail => "detailBand",
            ReportBandType.GroupFooter => "groupFooterBand",
            ReportBandType.PageFooter => "pageFooterBand",
            ReportBandType.ReportFooter => "reportFooterBand",
            _ => "band"
        };
        return string.IsNullOrEmpty(band.GroupField) ? baseName : $"{baseName}_{band.GroupField}";
    }

    /// <summary>
    /// Finds the report group that owns the given group header or footer band.
    /// </summary>
    /// <param name="band">The group band to look up.</param>
    /// <returns>The owning group, or <see langword="null"/> if not found.</returns>
    internal ReportGroup? FindGroupForBand(ReportBand band)
        => FindGroupForBand(Report.Groups, band);

    private static ReportGroup? FindGroupForBand(ReportGroupCollection groups, ReportBand band)
    {
        foreach (var group in groups)
        {
            if (ReferenceEquals(group.Header, band) || ReferenceEquals(group.Footer, band))
                return group;
            var nested = FindGroupForBand(group.Groups, band);
            if (nested != null) return nested;
        }
        return null;
    }

    /// <summary>
    /// Finds the group collection that directly contains the given group.
    /// </summary>
    /// <param name="group">The group whose parent collection is requested.</param>
    /// <returns>The containing collection, or <see langword="null"/> if not found.</returns>
    internal ReportGroupCollection? FindParentCollection(ReportGroup group)
        => FindParentCollection(Report.Groups, group);

    private static ReportGroupCollection? FindParentCollection(ReportGroupCollection owner, ReportGroup group)
    {
        foreach (var candidate in owner)
        {
            if (ReferenceEquals(candidate, group)) return owner;
            var nested = FindParentCollection(candidate.Groups, group);
            if (nested != null) return nested;
        }
        return null;
    }

    /// <summary>
    /// Gets the clickable gutter rectangles of the band header labels in the left
    /// margin, used to select bands (including zero-height ones) via their labels.
    /// </summary>
    /// <returns>A list of band and gutter hit rectangle pairs in root coordinates.</returns>
    internal IReadOnlyList<(ReportBand Band, Rectangle GutterRect)> GetGutterHitAreas()
    {
        var result = new List<(ReportBand, Rectangle)>();
        int leftMargin = Math.Max(1, (int)Math.Round(CmToPx(Report.LeftMargin)));
        float y = CmToPx(Report.TopMargin);
        foreach (var band in GetBands())
        {
            int height = (int)Math.Round(DisplayBandHeightPx(band));
            result.Add((band, new Rectangle(0, (int)Math.Round(y), leftMargin, height)));
            y += DisplayBandHeightPx(band);
        }
        return result;
    }

    /// <summary>
    /// Locks the control size to the computed page size. Any external resize
    /// (for example a user drag on the root) snaps back to the page geometry.
    /// </summary>
    protected override void OnBoundsChanged()
    {
        base.OnBoundsChanged();
        if (_applyingGeometry) return;
        var expected = ComputePageSize();
        if (Width != expected.Width || Height != expected.Height)
        {
            _applyingGeometry = true;
            try
            {
                Size = expected;
            }
            finally
            {
                _applyingGeometry = false;
            }
            LayoutBands();
            Invalidate();
        }
    }

    /// <summary>
    /// Gets the child render offset: the printable area origin (left/top margins).
    /// </summary>
    protected override Point GetChildRenderOffset() =>
        new((int)Math.Round(CmToPx(Report.LeftMargin)), (int)Math.Round(CmToPx(Report.TopMargin)));

    /// <summary>
    /// Gets the child clip rectangle covering the printable area.
    /// </summary>
    protected override Rectangle GetChildClipRectangle() =>
        new(0, 0,
            Math.Max(0, (int)Math.Round(CmToPx(Report.PrintableWidth))),
            Math.Max(0, (int)Math.Round(CmToPx(Report.PrintableHeight))));

    /// <summary>
    /// Gets the child layout area covering the printable area.
    /// </summary>
    protected override Rectangle GetChildLayoutArea() => GetChildClipRectangle();

    /// <summary>
    /// Renders the page sheet: drop shadow, white page, margin guides, and the
    /// stacked band layout with band header labels in the left margin gutter.
    /// </summary>
    /// <param name="g">The graphics context.</param>
    public override void Render(Graphics g)
    {
        // Drop shadow
        g.FillRectangle(Color.FromArgb(0, 0, 0, 30), 4, 4, Width, Height);

        // Page sheet
        g.FillRectangle(Color.White, 0, 0, Width, Height);

        var theme = ThemeManager.CurrentTheme;
        float leftMargin = CmToPx(Report.LeftMargin);
        float topMargin = CmToPx(Report.TopMargin);
        float printableWidth = CmToPx(Report.PrintableWidth);

        // Band strips
        var bands = GetBands();
        float y = topMargin;
        var bandBorder = Color.FromArgb(180, 180, 180);
        var bandFill = Color.FromArgb(245, 248, 252);
        var groupFill = Color.FromArgb(250, 246, 238);
        var labelColor = Color.FromArgb(90, 90, 90);
        var dash = new[] { 3f, 3f };

        foreach (var band in bands)
        {
            float h = DisplayBandHeightPx(band);
            bool isGroup = band.BandType is ReportBandType.GroupHeader or ReportBandType.GroupFooter;

            if (band.Visible)
            {
                g.FillRectangle(isGroup ? groupFill : bandFill, leftMargin, y, printableWidth, h);
            }
            else
            {
                g.FillRectangle(Color.FromArgb(235, 235, 235), leftMargin, y, printableWidth, h);
            }

            g.DrawRectangle(bandBorder, leftMargin, y, printableWidth, h, 1);

            // Band header label in the left margin gutter
            string label = GetBandDisplayName(band);
            if (band.BandType is ReportBandType.GroupHeader or ReportBandType.GroupFooter
                && !string.IsNullOrEmpty(band.GroupField))
            {
                label += " · " + band.GroupField;
            }
            if (!band.Visible) label += LangRes.GetString("ReportDesigner_BandHiddenSuffix");

            g.DrawString(label, theme.DefaultFont, labelColor,
                Math.Max(2f, leftMargin - 130f), y + 2f);

            y += h;
        }

        // Margin guides (dashed) around the printable area
        var marginColor = Color.FromArgb(120, 0, 120, 215);
        g.DrawLine(marginColor, leftMargin, 0, leftMargin, Height, 1, dash);
        g.DrawLine(marginColor, leftMargin + printableWidth, 0, leftMargin + printableWidth, Height, 1, dash);
        g.DrawLine(marginColor, 0, topMargin, Width, topMargin, 1, dash);
        g.DrawLine(marginColor, 0, Height - CmToPx(Report.BottomMargin), Width, Height - CmToPx(Report.BottomMargin), 1, dash);

        // Page border
        g.DrawRectangle(Color.FromArgb(160, 160, 160), 0, 0, Width, Height, 1);

        // Render child controls (bands' content) in the printable coordinate space
        g.Save();
        var offset = GetChildRenderOffset();
        g.TranslateTransform(offset.X, offset.Y);
        base.Render(g);
        g.Restore();
    }

    /// <summary>
    /// Gets the German display name for a band type.
    /// </summary>
    /// <param name="band">The band to name.</param>
    /// <returns>The localized band caption.</returns>
    public static string GetBandDisplayName(ReportBand band) => band.BandType switch
    {
        ReportBandType.ReportHeader => LangRes.GetString("ReportDesigner_BandReportHeader"),
        ReportBandType.PageHeader => LangRes.GetString("ReportDesigner_BandPageHeader"),
        ReportBandType.GroupHeader => LangRes.GetString("ReportDesigner_BandGroupHeader"),
        ReportBandType.Detail => LangRes.GetString("ReportDesigner_BandDetail"),
        ReportBandType.GroupFooter => LangRes.GetString("ReportDesigner_BandGroupFooter"),
        ReportBandType.PageFooter => LangRes.GetString("ReportDesigner_BandPageFooter"),
        ReportBandType.ReportFooter => LangRes.GetString("ReportDesigner_BandReportFooter"),
        _ => band.BandType.ToString()
    };
}
