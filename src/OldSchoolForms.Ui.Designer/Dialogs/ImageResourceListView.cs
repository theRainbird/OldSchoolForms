using System;
using System.Collections.Generic;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Designer.Services;
using OldSchoolForms.Ui.Theming;
using Graphics = OldSchoolForms.Ui.Rendering.Graphics;

namespace OldSchoolForms.Ui.Designer.Dialogs;

/// <summary>
/// A scrollable single-column list of embedded SVG resources. Each row shows a 24×24 icon
/// thumbnail (lazily rasterized from the manifest resource) next to the resource's short
/// name. Supports hover highlighting, selection and keyboard-free mouse scrolling. Used by
/// the <see cref="ImagePickerDialog"/> to let the user pick an image from the framework's
/// embedded icon set.
/// </summary>
public sealed class ImageResourceListView : Panel
{
    private readonly List<string> _resources = new();
    private readonly Dictionary<string, SvgImage?> _iconCache = new();
    private int _hoveredIndex = -1;
    private int _selectedIndex = -1;
    private ImageResourceScrollBarContext? _scrollBarContext;

    internal readonly ScrollBarEngine _vScrollBar = new();

    private const int RowHeight = 28;
    private const int IconSize = 24;
    private const int IconX = 4;
    private const int TextX = 32;
    private const int ScrollBarWidth = ScrollBarEngine.DefaultScrollBarSize;

    /// <summary>
    /// Raised when the selected resource changes.
    /// </summary>
    public event EventHandler? SelectedResourceChanged;

    /// <summary>
    /// Gets the index of the currently selected resource, or <c>-1</c> when nothing is selected.
    /// </summary>
    public int SelectedIndex => _selectedIndex;

    /// <summary>
    /// Gets the manifest resource name of the currently selected resource, or <c>null</c>.
    /// </summary>
    public string? SelectedResource =>
        _selectedIndex >= 0 && _selectedIndex < _resources.Count ? _resources[_selectedIndex] : null;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageResourceListView"/> class.
    /// </summary>
    public ImageResourceListView()
    {
        BackColor = ThemeManager.CurrentTheme.ControlLight;

        _vScrollBar.Orientation = ScrollBarEngine.ScrollBarOrientation.Vertical;
        _vScrollBar.ScrollBarSize = ScrollBarEngine.DefaultScrollBarSize;
        _vScrollBar.SmallChange = RowHeight;
        _vScrollBar.LargeChange = RowHeight * 5;
        _vScrollBar.Scroll += (_, _) => Invalidate();
    }

    /// <summary>
    /// Replaces the displayed resource list.
    /// </summary>
    /// <param name="resourceNames">The manifest resource names to display.</param>
    public void SetResources(IList<string> resourceNames)
    {
        _resources.Clear();
        if (resourceNames != null)
            _resources.AddRange(resourceNames);
        _selectedIndex = -1;
        _hoveredIndex = -1;
        _vScrollBar.ScrollTo(0);
        Invalidate();
    }

    /// <summary>
    /// Selects the resource with the given manifest name, if present.
    /// </summary>
    /// <param name="resourceName">The resource name to select.</param>
    public void SelectResource(string? resourceName)
    {
        if (string.IsNullOrEmpty(resourceName))
        {
            SetSelected(-1);
            return;
        }
        var index = _resources.IndexOf(resourceName!);
        SetSelected(index);
        if (index >= 0)
            ScrollToIndex(index);
    }

    private void SetSelected(int index)
    {
        if (_selectedIndex == index) return;
        _selectedIndex = index;
        SelectedResourceChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    private void ScrollToIndex(int index)
    {
        int itemTop = index * RowHeight;
        int itemBottom = itemTop + RowHeight;
        if (itemTop < _vScrollBar.Value)
            _vScrollBar.ScrollTo(itemTop);
        else if (itemBottom > _vScrollBar.Value + Height)
            _vScrollBar.ScrollTo(itemBottom - Height);
    }

    private void UpdateScrollBar()
    {
        _vScrollBar.ViewSize = Height;
        _vScrollBar.ContentSize = _resources.Count * RowHeight;
    }

    private int HitTestItem(int mouseY)
    {
        int contentY = mouseY + _vScrollBar.Value;
        if (contentY < 0) return -1;
        int index = contentY / RowHeight;
        return index >= 0 && index < _resources.Count ? index : -1;
    }

    /// <summary>
    /// Renders the resource list with the current theme, including icon thumbnails,
    /// hover/selection highlighting and the vertical scrollbar.
    /// </summary>
    /// <param name="g">The graphics surface to draw on.</param>
    public override void Render(Graphics g)
    {
        var theme = ThemeManager.CurrentTheme;
        UpdateScrollBar();

        int sbw = ScrollBarWidth;
        g.FillRectangle(theme.ControlLight, 0, 0, Width, Height);

        int scroll = _vScrollBar.Value;
        g.SetClip(new Rectangle(0, 0, Width - sbw, Height));

        for (int i = 0; i < _resources.Count; i++)
        {
            int drawY = i * RowHeight - scroll;
            if (drawY + RowHeight < 0 || drawY > Height) continue;

            bool selected = i == _selectedIndex;
            bool hovered = i == _hoveredIndex && !selected;

            if (selected)
                g.FillRectangle(Color.FromArgb(0, 120, 215), 0, drawY, Width - sbw, RowHeight);
            else if (hovered)
                g.FillRectangle(theme.HoverHighlight, 0, drawY, Width - sbw, RowHeight);

            var icon = GetIcon(_resources[i]);
            if (icon != null)
                g.DrawImage(icon, IconX, drawY + (RowHeight - IconSize) / 2, IconSize, IconSize);

            var textColor = selected ? theme.HighlightText : theme.ControlText;
            g.DrawString(ShortName(_resources[i]), theme.DefaultFont, textColor, TextX, drawY + 6);
        }

        g.ResetClip();
        _vScrollBar.Render(g, new Rectangle(Width - sbw, 0, sbw, Height), theme);
    }

    private SvgImage? GetIcon(string resourceName)
    {
        if (_iconCache.TryGetValue(resourceName, out var cached))
            return cached;
        var icon = SvgImage.FromSvgResource(resourceName, IconSize);
        _iconCache[resourceName] = icon;
        return icon;
    }

    /// <summary>
    /// Derives a short display name from a manifest resource name by stripping the
    /// namespace prefix and the <c>.svg</c> extension.
    /// </summary>
    /// <param name="resourceName">The fully qualified manifest resource name.</param>
    /// <returns>The short name for display.</returns>
    public static string ShortName(string resourceName)
    {
        var name = resourceName;
        int lastDot = name.LastIndexOf('.');
        if (lastDot > 0)
            name = name[..lastDot];
        int lastSegment = name.LastIndexOf('.');
        if (lastSegment >= 0)
            name = name[(lastSegment + 1)..];
        return name;
    }

    protected override void OnMouseDown(EventArgs e)
    {
        if (e is MouseEventArgs args && args.X >= Width - ScrollBarWidth)
        {
            _vScrollBar.HandleMouseDown(new Point(args.X, args.Y),
                new Rectangle(Width - ScrollBarWidth, 0, ScrollBarWidth, Height), ScrollBarContext);
            base.OnMouseDown(e);
            return;
        }
        if (e is not MouseEventArgs mouseArgs) return;
        int index = HitTestItem(mouseArgs.Y);
        if (index >= 0)
            SetSelected(index);
    }

    protected override void OnMouseMove(EventArgs e)
    {
        if (e is MouseEventArgs args && args.X >= Width - ScrollBarWidth)
        {
            _vScrollBar.HandleMouseMove(new Point(args.X, args.Y),
                new Rectangle(Width - ScrollBarWidth, 0, ScrollBarWidth, Height), ScrollBarContext);
            base.OnMouseMove(e);
            return;
        }
        if (e is MouseEventArgs moveArgs)
        {
            int index = HitTestItem(moveArgs.Y);
            if (index != _hoveredIndex)
            {
                _hoveredIndex = index;
                Invalidate();
            }
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseWheel(EventArgs e)
    {
        if (e is MouseEventArgs wheelArgs)
        {
            UpdateScrollBar();
            if (_vScrollBar.NeedsScrollbar)
                _vScrollBar.HandleMouseWheel(wheelArgs.Delta, ScrollBarContext);
        }
        base.OnMouseWheel(e);
    }

    protected override void OnMouseUp(EventArgs e)
    {
        if (e is MouseEventArgs && (_vScrollBar.IsDragging || _vScrollBar.IsUpButtonPressed || _vScrollBar.IsDownButtonPressed))
            _vScrollBar.HandleMouseUp(ScrollBarContext);
        base.OnMouseUp(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hoveredIndex = -1;
        _vScrollBar.HandleMouseLeave(ScrollBarContext);
        base.OnMouseLeave(e);
    }

    private ImageResourceScrollBarContext ScrollBarContext => _scrollBarContext ??= new ImageResourceScrollBarContext(this);

    /// <summary>
    /// Provides the <see cref="IScrollBarContext"/> implementation for the resource list
    /// scrollbar, delegating invalidation, mouse capture and zoom to this control.
    /// </summary>
    private sealed class ImageResourceScrollBarContext : IScrollBarContext
    {
        private readonly ImageResourceListView _owner;
        public ImageResourceScrollBarContext(ImageResourceListView owner) => _owner = owner;
        public float Zoom => _owner.EffectiveZoom;
        public void Invalidate() => _owner.Invalidate();
        public void CaptureMouse(bool capture) => _owner.CapturingMouse = capture;
    }
}
