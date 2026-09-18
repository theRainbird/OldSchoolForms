using System;
using System.Collections.Generic;
using System.Linq;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Designer.Services;
using OldSchoolForms.Ui.Theming;
using Graphics = OldSchoolForms.Ui.Rendering.Graphics;

namespace OldSchoolForms.Ui.Designer.Standalone;

/// <summary>
/// Event args for toolbox drag operations.
/// </summary>
public class ToolboxDragEventArgs : EventArgs
{
    /// <summary>
    /// Gets the toolbox item being dragged.
    /// </summary>
    public ToolboxItem Item { get; }

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="item">The toolbox item being dragged.</param>
    public ToolboxDragEventArgs(ToolboxItem item)
    {
        Item = item;
    }
}

/// <summary>
/// A visual toolbox control that displays available controls grouped by category.
/// Supports initiating drag operations for placing controls on the design surface.
/// </summary>
public class ToolboxControl : Panel
{
    private readonly ToolboxService _toolboxService;
    private readonly List<ToolboxItem> _filteredItems = new();
    private int _hoveredIndex = -1;
    private int _selectedIndex = -1;
    private int _lastMouseDownY;
    private string? _activeCategory;
    private ToolboxScrollBarContext? _scrollBarContext;

    internal readonly ScrollBarEngine _vScrollBar = new();

    private const int HeaderHeight = 24;
    private const int ItemHeight = 26;
    private const int CategoryGap = 20;
    private const int ScrollBarWidth = ScrollBarEngine.DefaultScrollBarSize;

    /// <summary>
    /// Raised when the user starts dragging a toolbox item.
    /// </summary>
    public event EventHandler<ToolboxDragEventArgs>? DragStarted;

    /// <summary>
    /// Gets the currently selected toolbox item, or null.
    /// </summary>
    public ToolboxItem? SelectedItem =>
        _selectedIndex >= 0 && _selectedIndex < _filteredItems.Count
            ? _filteredItems[_selectedIndex]
            : null;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="toolboxService">The toolbox service.</param>
    public ToolboxControl(ToolboxService toolboxService)
    {
        _toolboxService = toolboxService ?? throw new ArgumentNullException(nameof(toolboxService));
        BackColor = ThemeManager.CurrentTheme.ControlLight;

        _vScrollBar.Orientation = ScrollBarEngine.ScrollBarOrientation.Vertical;
        _vScrollBar.ScrollBarSize = ScrollBarEngine.DefaultScrollBarSize;
        _vScrollBar.SmallChange = ItemHeight;
        _vScrollBar.LargeChange = ItemHeight * 3;
        _vScrollBar.Scroll += (_, _) => Invalidate();

        BuildFilteredList();
    }

    /// <summary>
    /// Filters to show only a specific category. Pass null for all.
    /// </summary>
    public void FilterByCategory(string? category)
    {
        _activeCategory = category;
        BuildFilteredList();
        UpdateScrollBar();
        _vScrollBar.ScrollTo(0);
        Invalidate();
    }

    private int ContentHeight()
    {
        int height = HeaderHeight + 4;
        string? currentCat = null;
        foreach (var item in _filteredItems)
        {
            if (_activeCategory == null && item.Category != currentCat)
            {
                currentCat = item.Category;
                height += CategoryGap;
            }
            height += ItemHeight;
        }
        return height;
    }

    private void UpdateScrollBar()
    {
        _vScrollBar.ViewSize = Height;
        _vScrollBar.ContentSize = ContentHeight();
    }

    private void BuildFilteredList()
    {
        _filteredItems.Clear();
        if (_activeCategory != null)
            _filteredItems.AddRange(_toolboxService.Items.Where(i => i.Category == _activeCategory));
        else
            _filteredItems.AddRange(_toolboxService.Items);
        _selectedIndex = -1;
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
        if (e is not MouseEventArgs moveArgs) return;
        _lastMouseDownY = moveArgs.Y;
        int index = HitTestItem(moveArgs.Y);
        if (index >= 0 && index < _filteredItems.Count)
        {
            _selectedIndex = index;
            _hoveredIndex = index;
            var item = _filteredItems[index];
            // Immediately enter drop mode on click (no drag required)
            DragStarted?.Invoke(this, new ToolboxDragEventArgs(item));
            Invalidate();
        }
    }

    private List<(int y, int height, ToolboxItem? item, bool isHeader, string category)> BuildLayout()
    {
        var layout = new List<(int y, int height, ToolboxItem? item, bool isHeader, string category)>();
        int y = HeaderHeight + 4;
        string? currentCat = null;

        foreach (var item in _toolboxService.Items)
        {
            if (_activeCategory == null && item.Category != currentCat)
            {
                currentCat = item.Category;
                layout.Add((y, CategoryGap, null, true, currentCat));
                y += CategoryGap;
            }

            if (_activeCategory != null && item.Category != _activeCategory)
                continue;

            layout.Add((y, ItemHeight, item, false, item.Category));
            y += ItemHeight;
        }

        return layout;
    }

    private int HitTestItem(int mouseY)
    {
        int contentY = mouseY + _vScrollBar.Value;
        var layout = BuildLayout();
        foreach (var entry in layout)
        {
            if (entry.isHeader) continue;
            if (contentY >= entry.y && contentY < entry.y + entry.height)
                return _filteredItems.IndexOf(entry.item!);
        }
        return -1;
    }

    public override void Render(Graphics g)
    {
        var theme = ThemeManager.CurrentTheme;

        // Keep the scrollbar range in sync with the current content.
        UpdateScrollBar();

        int sbw = ScrollBarWidth;

        g.FillRectangle(theme.ControlLight, 0, 0, Width, Height);
        g.FillRectangle(theme.ActiveCaption, 0, 0, Width, HeaderHeight);
        g.DrawString("Toolbox", theme.DefaultFont, theme.ActiveCaptionText, 6, 4);

        int scroll = _vScrollBar.Value;
        var layout = BuildLayout();

        // Clip the content so items never paint under the scrollbar or the header.
        g.SetClip(new Rectangle(0, HeaderHeight, Width - sbw, Height - HeaderHeight));

        foreach (var entry in layout)
        {
            int drawY = entry.y - scroll;
            if (entry.isHeader)
            {
                g.FillRectangle(theme.ControlDark, 0, drawY, Width - sbw, entry.height);
                g.DrawString(entry.category, theme.DefaultFont, theme.ControlText, 4, drawY + 2);
            }
            else
            {
                int idx = _filteredItems.IndexOf(entry.item!);
                var bg = idx == _hoveredIndex ? theme.HoverHighlight : Color.Transparent;
                if (bg.A > 0)
                    g.FillRectangle(bg, 2, drawY, Width - sbw - 2, entry.height);

                var textColor = idx == _hoveredIndex ? theme.HighlightText : theme.ControlText;
                g.DrawString(entry.item!.DisplayName, theme.DefaultFont, textColor, 8, drawY + 4);
            }
        }

        g.ResetClip();

        _vScrollBar.Render(g, new Rectangle(Width - sbw, 0, sbw, Height), theme);

        if (BorderStyle == BorderStyle.FixedSingle)
        {
            g.DrawRectangle(theme.PanelBorder, 0, 0, Width, Height, 1);
        }
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
        base.OnMouseMove(e);
    }

    protected override void OnMouseWheel(EventArgs e)
    {
        if (e is MouseEventArgs)
        {
            UpdateScrollBar();
            if (_vScrollBar.NeedsScrollbar)
            {
                _vScrollBar.HandleMouseWheel(((MouseEventArgs)e).Delta, ScrollBarContext);
            }
        }
        base.OnMouseWheel(e);
    }

    protected override void OnMouseUp(EventArgs e)
    {
        if (e is MouseEventArgs && (_vScrollBar.IsDragging || _vScrollBar.IsUpButtonPressed || _vScrollBar.IsDownButtonPressed))
        {
            _vScrollBar.HandleMouseUp(ScrollBarContext);
        }
        base.OnMouseUp(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _vScrollBar.HandleMouseLeave(ScrollBarContext);
        base.OnMouseLeave(e);
    }

    private ToolboxScrollBarContext ScrollBarContext => _scrollBarContext ??= new ToolboxScrollBarContext(this);

    /// <summary>
    /// Provides the <see cref="IScrollBarContext"/> implementation for the toolbox scrollbar,
    /// delegating invalidation, mouse capture and zoom to this control.
    /// </summary>
    private sealed class ToolboxScrollBarContext : IScrollBarContext
    {
        private readonly ToolboxControl _owner;
        public ToolboxScrollBarContext(ToolboxControl owner) => _owner = owner;
        public float Zoom => _owner.EffectiveZoom;
        public void Invalidate() => _owner.Invalidate();
        public void CaptureMouse(bool capture) => _owner.CapturingMouse = capture;
    }
}
