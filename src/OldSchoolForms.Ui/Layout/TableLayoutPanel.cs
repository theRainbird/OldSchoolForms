using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Design;
using OldSchoolForms.Ui.Theming;
using OldSchoolForms.Ui.Rendering;

namespace OldSchoolForms.Ui.Layout;

/// <summary>
/// A layout panel that arranges child controls in rows and columns.
/// </summary>
/// <remarks>
/// Child controls are placed into cells by their explicit cell assignment (see
/// <see cref="SetCellPosition"/>); controls without an assignment fill the remaining
/// cells in order (left to right, top to bottom).
/// By default all cells are equally sized. The optional <see cref="RowStyles"/> and
/// <see cref="ColumnStyles"/> collections allow per-row and per-column sizing with
/// absolute, percentage, content-driven (auto-size) or fill behavior. The effective
/// row and column count is the larger of the explicit <see cref="RowCount"/> /
/// <see cref="ColumnCount"/>, the number of defined styles and the highest assigned
/// cell position.
/// </remarks>
[ToolboxIcon("OldSchoolForms.Ui.Resources.Icons.Controls.ctrl_tablelayout.svg")]
public class TableLayoutPanel : ContainerControl
{
    private int _rowCount = 2;
    private int _columnCount = 2;
    private float _autoEmptyMinSize = 24f;
    private Padding _cellSpacing = Padding.Empty;
    private readonly List<TableLayoutStyle> _rowStyles = new();
    private readonly List<TableLayoutStyle> _columnStyles = new();
    private readonly Dictionary<Control, (int Row, int Col)> _cellPositions = new();
    private (Point location, Size size)[]? _layoutCache;
    private int _cachedLayoutVersion = -1;

    /// <summary>
    /// Initializes a new instance of TableLayoutPanel.
    /// </summary>
    public TableLayoutPanel()
    {
        Size = new Size(300, 200);
        _backColor = ThemeManager.CurrentTheme.ControlBackground;
    }

    /// <summary>
    /// Called when the theme changes. Updates tablelayoutpanel-specific colors.
    /// </summary>
    /// <param name="newTheme">The new theme that was activated.</param>
    public override void OnThemeChanged(Theme newTheme)
    {
        if (!_backColorSet)
            _backColor = newTheme.ControlBackground;
        Invalidate();
    }

    /// <summary>
    /// Gets or sets the number of rows in the table.
    /// </summary>
    public int RowCount
    {
        get => _rowCount;
        set
        {
            _rowCount = value;
            MarkLayoutDirty();
            PerformLayout();
        }
    }

    /// <summary>
    /// Gets or sets the number of columns in the table.
    /// </summary>
    public int ColumnCount
    {
        get => _columnCount;
        set
        {
            _columnCount = value;
            MarkLayoutDirty();
            PerformLayout();
        }
    }

    /// <summary>
    /// Gets the per-row sizing styles. Entries beyond <see cref="RowCount"/> increase
    /// the effective number of rows. After mutating the collection call
    /// <see cref="InvalidateTableLayout"/> so the panel recomputes its layout.
    /// </summary>
    [System.ComponentModel.Category("Layout")]
    [EditorButton("…")]
    public List<TableLayoutStyle> RowStyles => _rowStyles;

    /// <summary>
    /// Gets the per-column sizing styles. Entries beyond <see cref="ColumnCount"/>
    /// increase the effective number of columns. After mutating the collection call
    /// <see cref="InvalidateTableLayout"/> so the panel recomputes its layout.
    /// </summary>
    [System.ComponentModel.Category("Layout")]
    [EditorButton("…")]
    public List<TableLayoutStyle> ColumnStyles => _columnStyles;

    /// <summary>
    /// Gets or sets the minimum size in pixels of a row or column that is sized by
    /// <see cref="SizeType.AutoSize"/> but contains no child controls. Without it an
    /// empty auto-sized row or column would collapse to zero and could no longer be
    /// clicked into. Defaults to 24.
    /// </summary>
    [System.ComponentModel.Category("Layout")]
    public float AutoEmptyMinSize
    {
        get => _autoEmptyMinSize;
        set
        {
            _autoEmptyMinSize = Math.Max(0f, value);
            InvalidateTableLayout();
        }
    }

    /// <summary>
    /// Gets or sets the distance between a cell's content and the cell's grid lines on
    /// all four sides. A single value applies uniformly to every cell of the table.
    /// Each side is clamped to zero or more. The default is <see cref="Padding.Empty"/>,
    /// so a child fills its cell completely and touches the grid lines. The panel's own
    /// 2px outer border is not affected by this setting.
    /// </summary>
    [System.ComponentModel.Category("Layout")]
    public Padding CellSpacing
    {
        get => _cellSpacing;
        set
        {
            _cellSpacing = new Padding(
                Math.Max(0, value.Left),
                Math.Max(0, value.Top),
                Math.Max(0, value.Right),
                Math.Max(0, value.Bottom));
            InvalidateTableLayout();
            PerformLayout();
        }
    }

    /// <summary>
    /// Gets the effective number of rows: the larger of <see cref="RowCount"/>,
    /// the number of <see cref="RowStyles"/> entries and the lowest assigned row
    /// position plus one.
    /// </summary>
    public int EffectiveRowCount
    {
        get
        {
            var count = Math.Max(_rowCount, _rowStyles.Count);
            foreach (var pos in _cellPositions.Values)
            {
                if (pos.Row + 1 > count) count = pos.Row + 1;
            }
            return count;
        }
    }

    /// <summary>
    /// Gets the effective number of columns: the larger of <see cref="ColumnCount"/>,
    /// the number of <see cref="ColumnStyles"/> entries and the lowest assigned column
    /// position plus one.
    /// </summary>
    public int EffectiveColumnCount
    {
        get
        {
            var count = Math.Max(_columnCount, _columnStyles.Count);
            foreach (var pos in _cellPositions.Values)
            {
                if (pos.Col + 1 > count) count = pos.Col + 1;
            }
            return count;
        }
    }

    /// <summary>
    /// Assigns a child control to a specific table cell. The control is laid out in
    /// that cell instead of taking part in the sequential fill order.
    /// </summary>
    /// <param name="child">The child control to place.</param>
    /// <param name="row">The zero-based row index.</param>
    /// <param name="col">The zero-based column index.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="child"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when row or col is negative.</exception>
    public void SetCellPosition(Control child, int row, int col)
    {
        if (child == null) throw new ArgumentNullException(nameof(child));
        if (row < 0) throw new ArgumentOutOfRangeException(nameof(row));
        if (col < 0) throw new ArgumentOutOfRangeException(nameof(col));

        _cellPositions[child] = (row, col);
        InvalidateTableLayout();
        PerformLayout();
    }

    /// <summary>
    /// Gets the explicitly assigned cell of a child control, if any.
    /// </summary>
    /// <param name="child">The child control to look up.</param>
    /// <returns>The assigned cell, or <c>null</c> when the control has no explicit position.</returns>
    public (int Row, int Col)? GetCellPosition(Control child)
    {
        if (child == null) return null;
        return _cellPositions.TryGetValue(child, out var pos) ? pos : null;
    }

    /// <summary>
    /// Removes the explicit cell assignment of a child control so it takes part in the
    /// sequential fill order again.
    /// </summary>
    /// <param name="child">The child control to release.</param>
    public void ClearCellPosition(Control child)
    {
        if (child != null && _cellPositions.Remove(child))
        {
            InvalidateTableLayout();
            PerformLayout();
        }
    }

    /// <summary>
    /// Computes the content-driven (auto) sizes for every row and column: the largest
    /// preferred size of the children assigned to each row/column. Empty auto-sized
    /// rows/columns get <see cref="AutoEmptyMinSize"/> so they stay clickable.
    /// </summary>
    /// <param name="rows">The number of rows to compute.</param>
    /// <param name="cols">The number of columns to compute.</param>
    /// <returns>A tuple with the per-column and per-row auto sizes.</returns>
    public (float[] Columns, float[] Rows) ComputeAutoSizes(int rows, int cols)
        => ComputeAutoSizes(rows, cols, _rowStyles, _columnStyles);

    /// <summary>
    /// Computes the content-driven sizes for the given grid dimensions using explicit
    /// style lists instead of the panel's own styles. Used by the designer preview,
    /// which shows the effect of not-yet-applied working styles.
    /// </summary>
    /// <param name="rows">The number of rows to compute.</param>
    /// <param name="cols">The number of columns to compute.</param>
    /// <param name="rowStyles">The row styles to consider; may be <c>null</c>.</param>
    /// <param name="columnStyles">The column styles to consider; may be <c>null</c>.</param>
    /// <returns>A tuple with the per-column and per-row auto sizes.</returns>
    public (float[] Columns, float[] Rows) ComputeAutoSizes(
        int rows,
        int cols,
        IReadOnlyList<TableLayoutStyle>? rowStyles,
        IReadOnlyList<TableLayoutStyle>? columnStyles)
    {
        var autoColumns = new float[Math.Max(0, cols)];
        var autoRows = new float[Math.Max(0, rows)];
        var cells = AssignCells(rows, cols);

        // The layout insets each child from its cell edges by CellSpacing when it fills
        // its cell. To keep a child at its own size, the auto cell must be the child's
        // size plus that spacing.
        var gapX = (float)_cellSpacing.Horizontal;
        var gapY = (float)_cellSpacing.Vertical;

        for (var i = 0; i < Controls.Count && i < cells.Length; i++)
        {
            if (cells[i] == null) continue;
            var (row, col) = cells[i]!.Value;
            var child = Controls[i];
            var content = new Size(
                Math.Max(child.Size.Width, child.PreferredSize.Width),
                Math.Max(child.Size.Height, child.PreferredSize.Height));
            if (content.Width + gapX > autoColumns[col]) autoColumns[col] = content.Width + gapX;
            if (content.Height + gapY > autoRows[row]) autoRows[row] = content.Height + gapY;
        }

        for (var c = 0; c < autoColumns.Length; c++)
        {
            if (columnStyles != null && c < columnStyles.Count &&
                columnStyles[c].SizeType == SizeType.AutoSize && autoColumns[c] <= 0f)
                autoColumns[c] = _autoEmptyMinSize;
        }
        for (var r = 0; r < autoRows.Length; r++)
        {
            if (rowStyles != null && r < rowStyles.Count &&
                rowStyles[r].SizeType == SizeType.AutoSize && autoRows[r] <= 0f)
                autoRows[r] = _autoEmptyMinSize;
        }

        return (autoColumns, autoRows);
    }

    /// <summary>
    /// Determines the cell of each child control: explicitly assigned cells first,
    /// remaining controls fill the free cells in row-major order.
    /// </summary>
    /// <param name="rows">The number of rows in the grid.</param>
    /// <param name="cols">The number of columns in the grid.</param>
    /// <returns>An array with one entry per control; the entry at index i is the cell of
    /// <c>Controls[i]</c>, or <c>null</c> when the grid has no free cell left.</returns>
    public (int Row, int Col)?[] AssignCells(int rows, int cols)
    {
        var result = new (int Row, int Col)?[Controls.Count];
        if (rows <= 0 || cols <= 0) return result;

        var taken = new bool[rows * cols];

        for (var i = 0; i < Controls.Count; i++)
        {
            if (_cellPositions.TryGetValue(Controls[i], out var pos))
            {
                var r = Math.Min(pos.Row, rows - 1);
                var c = Math.Min(pos.Col, cols - 1);
                result[i] = (r, c);
                taken[r * cols + c] = true;
            }
        }

        var fill = 0;
        for (var i = 0; i < Controls.Count; i++)
        {
            if (result[i] != null) continue;
            while (fill < taken.Length && taken[fill]) fill++;
            if (fill >= taken.Length) break;
            result[i] = (fill / cols, fill % cols);
            taken[fill] = true;
        }

        return result;
    }

    /// <summary>
    /// Invalidates the cached table layout so the next layout pass recomputes all row
    /// heights and column widths from the current styles. Call this after mutating
    /// <see cref="RowStyles"/> or <see cref="ColumnStyles"/>.
    /// </summary>
    public void InvalidateTableLayout()
    {
        MarkLayoutDirty();
        Invalidate();
    }

    /// <summary>
    /// Computes the pixel widths of all columns and heights of all rows from the
    /// current styles, the panel's inner size and the children's preferred sizes.
    /// </summary>
    /// <returns>A tuple with the column widths and the row heights.</returns>
    public (float[] Widths, float[] Heights) ComputeGridSizes()
    {
        var cols = EffectiveColumnCount;
        var rows = EffectiveRowCount;
        var innerWidth = Math.Max(0, Width - Padding.Horizontal - 4);
        var innerHeight = Math.Max(0, Height - Padding.Vertical - 4);

        var (autoColumns, autoRows) = ComputeAutoSizes(rows, cols);

        var widths = TableLayoutCalculator.ComputeSizes(_columnStyles, cols, innerWidth, autoColumns);
        var heights = TableLayoutCalculator.ComputeSizes(_rowStyles, rows, innerHeight, autoRows);
        return (widths, heights);
    }

    /// <summary>
    /// Determines which table cell the given panel-local point falls into.
    /// </summary>
    /// <param name="p">A point in the panel's own coordinate space.</param>
    /// <returns>
    /// The zero-based row and column of the cell containing <paramref name="p"/>, or
    /// <c>null</c> when the point lies outside the cell grid (e.g. in the padding area).
    /// </returns>
    public (int Row, int Col)? HitTestCell(Point p)
    {
        var (widths, heights) = ComputeGridSizes();
        var cols = widths.Length;
        var rows = heights.Length;
        if (cols == 0 || rows == 0) return null;

        var colOffsets = CumulativeOffsets(widths);
        var rowOffsets = CumulativeOffsets(heights);
        var padLeft = Padding.Left + 2;
        var padTop = Padding.Top + 2;

        var x = p.X - padLeft;
        var y = p.Y - padTop;
        if (x < 0 || y < 0) return null;
        if (x >= colOffsets[cols] || y >= rowOffsets[rows]) return null;

        var col = cols - 1;
        for (var c = 0; c < cols; c++)
        {
            if (x < colOffsets[c + 1])
            {
                col = c;
                break;
            }
        }

        var row = rows - 1;
        for (var r = 0; r < rows; r++)
        {
            if (y < rowOffsets[r + 1])
            {
                row = r;
                break;
            }
        }

        return (row, col);
    }

    private void LayoutControls()
    {
        if (Controls.Count == 0) return;

        if (_cachedLayoutVersion == _layoutVersion && _layoutCache != null && _layoutCache.Length == Controls.Count)
        {
            for (int i = 0; i < Controls.Count; i++)
            {
                var child = Controls[i];
                child._layoutDrivenBoundsChange = true;
                child.Location = _layoutCache[i].location;
                child.Size = _layoutCache[i].size;
                child._layoutDrivenBoundsChange = false;
            }
            return;
        }

        int padLeft = Padding.Left + 2;
        int padTop = Padding.Top + 2;

        var (widths, heights) = ComputeGridSizes();
        var cols = widths.Length;
        var rows = heights.Length;

        var colOffsets = CumulativeOffsets(widths);
        var rowOffsets = CumulativeOffsets(heights);

        var cache = new (Point location, Size size)[Controls.Count];
        var cells = AssignCells(rows, cols);

        for (var i = 0; i < Controls.Count; i++)
        {
            if (cells[i] == null) continue;
            var (row, col) = cells[i]!.Value;
            var child = Controls[i];
            child._layoutDrivenBoundsChange = true;
            var loc = new Point(colOffsets[col] + padLeft + _cellSpacing.Left,
                               rowOffsets[row] + padTop + _cellSpacing.Top);

            // In an auto-sized row/column the child keeps its own size (the row/column
            // was sized to fit it); in fill/percent/absolute cells the child fills the
            // cell. This prevents a shorter control from being stretched to the row
            // height and keeps the user's control size stable.
            bool rowAuto = row < _rowStyles.Count && _rowStyles[row].SizeType == SizeType.AutoSize;
            bool colAuto = col < _columnStyles.Count && _columnStyles[col].SizeType == SizeType.AutoSize;

            int w = colAuto
                ? child.Size.Width
                : Math.Max(0, colOffsets[col + 1] - colOffsets[col] - _cellSpacing.Horizontal);
            int h = rowAuto
                ? child.Size.Height
                : Math.Max(0, rowOffsets[row + 1] - rowOffsets[row] - _cellSpacing.Vertical);

            var sz = new Size(w, h);
            child.Location = loc;
            child.Size = sz;
            child._layoutDrivenBoundsChange = false;
            cache[i] = (loc, sz);
        }

        _layoutCache = cache;
        _cachedLayoutVersion = _layoutVersion;
    }

    /// <summary>
    /// Builds the cumulative pixel offsets for the given sizes, with one extra entry
    /// at the end holding the total.
    /// </summary>
    private static int[] CumulativeOffsets(float[] sizes)
    {
        var offsets = new int[sizes.Length + 1];
        var accumulated = 0f;
        for (var i = 0; i < sizes.Length; i++)
        {
            offsets[i] = (int)Math.Round(accumulated);
            accumulated += sizes[i];
        }
        offsets[sizes.Length] = (int)Math.Round(accumulated);
        return offsets;
    }

    /// <summary>
    /// Called when the control needs to perform layout.
    /// </summary>
    protected override void OnLayout()
    {
        LayoutControls();
    }

    /// <summary>
    /// Renders the control, its background, and grid lines.
    /// </summary>
    /// <param name="g">The Graphics object to use for rendering.</param>
    public override void Render(Graphics g)
    {
        if (!Visible) return;

        var theme = ThemeManager.CurrentTheme;

        g.FillRectangle(BackColor, 0, 0, Width, Height);

        int padLeft = Padding.Left + 2;
        int padTop = Padding.Top + 2;

        var (widths, heights) = ComputeGridSizes();
        var colOffsets = CumulativeOffsets(widths);
        var rowOffsets = CumulativeOffsets(heights);

        for (int row = 0; row <= heights.Length; row++)
        {
            var y = rowOffsets[row] + padTop;
            g.DrawLine(theme.TableLayoutGridLine, padLeft, y, Width - Padding.Right - 2, y);
        }
        for (int col = 0; col <= widths.Length; col++)
        {
            var x = colOffsets[col] + padLeft;
            g.DrawLine(theme.TableLayoutGridLine, x, padTop, x, Height - Padding.Bottom - 2);
        }

        base.Render(g);
    }
}
