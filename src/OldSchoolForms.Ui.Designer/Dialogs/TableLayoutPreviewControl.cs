using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Layout;
using OldSchoolForms.Ui.Rendering;
using OldSchoolForms.Ui.Resources;
using OldSchoolForms.Ui.Theming;

namespace OldSchoolForms.Ui.Designer.Dialogs;

/// <summary>
/// A live miniature preview of a <see cref="TableLayoutPanel"/>'s row and column
/// sizing. It renders the grid from the dialog's working style copies using the same
/// <see cref="TableLayoutCalculator"/> the panel itself uses, so the preview always
/// matches the layout that will result when the dialog is confirmed. Child controls
/// are shown as placeholders with their names, and the row or column currently
/// selected in the editor grid is highlighted.
/// </summary>
public class TableLayoutPreviewControl : Control
{
    private const int ColumnLabelHeight = 18;
    private const int RowLabelWidth = 46;

    private TableLayoutPanel? _target;
    private IReadOnlyList<TableLayoutStyle> _rowStyles = new List<TableLayoutStyle>();
    private IReadOnlyList<TableLayoutStyle> _columnStyles = new List<TableLayoutStyle>();
    private int _highlightIndex = -1;
    private bool _highlightIsRow = true;

    /// <summary>
    /// Gets or sets the panel whose size and children provide the preview's proportions
    /// and placeholder contents.
    /// </summary>
    public TableLayoutPanel? Target
    {
        get => _target;
        set { _target = value; Invalidate(); }
    }

    /// <summary>
    /// Gets or sets the working row styles shown in the preview.
    /// </summary>
    public IReadOnlyList<TableLayoutStyle> WorkingRowStyles
    {
        get => _rowStyles;
        set { _rowStyles = value ?? new List<TableLayoutStyle>(); Invalidate(); }
    }

    /// <summary>
    /// Gets or sets the working column styles shown in the preview.
    /// </summary>
    public IReadOnlyList<TableLayoutStyle> WorkingColumnStyles
    {
        get => _columnStyles;
        set { _columnStyles = value ?? new List<TableLayoutStyle>(); Invalidate(); }
    }

    /// <summary>
    /// Sets which row or column of the editor grid is currently selected and should be
    /// highlighted in the preview.
    /// </summary>
    /// <param name="isRow"><see langword="true"/> to highlight a row, <see langword="false"/> for a column.</param>
    /// <param name="index">The zero-based index of the highlighted row or column, or -1 for none.</param>
    public void SetHighlight(bool isRow, int index)
    {
        _highlightIsRow = isRow;
        _highlightIndex = index;
        Invalidate();
    }

    /// <summary>
    /// Repaints the preview to reflect the current working styles.
    /// </summary>
    public void RefreshPreview()
    {
        Invalidate();
    }

    /// <summary>
    /// Renders the preview: background, highlighted band, grid lines, per-row/column
    /// size annotations and child placeholders.
    /// </summary>
    /// <param name="g">The graphics surface to draw on.</param>
    public override void Render(Graphics g)
    {
        if (!Visible) return;

        var theme = ThemeManager.CurrentTheme;
        g.FillRectangle(theme.WindowBackground, 0, 0, Width, Height);
        g.DrawRectangle(theme.Border, 0.5f, 0.5f, Width - 1f, Height - 1f);

        if (_target == null) return;

        var rows = Math.Max(_target.EffectiveRowCount, _rowStyles.Count);
        var cols = Math.Max(_target.EffectiveColumnCount, _columnStyles.Count);
        if (rows <= 0) rows = 1;
        if (cols <= 0) cols = 1;

        var gridRect = new Rectangle(
            RowLabelWidth + 1,
            ColumnLabelHeight + 1,
            Width - RowLabelWidth - 3,
            Height - ColumnLabelHeight - 3);
        if (gridRect.Width <= 0 || gridRect.Height <= 0) return;

        var (widths, heights) = ComputeScaledSizes(rows, cols);
        if (widths == null || heights == null) return;

        var colOffsets = ScaledOffsets(widths, gridRect.Width);
        var rowOffsets = ScaledOffsets(heights, gridRect.Height);

        // Highlighted band for the selected row/column.
        if (_highlightIndex >= 0)
        {
            if (_highlightIsRow && _highlightIndex < rows)
            {
                g.FillRectangle(theme.HoverHighlight,
                    gridRect.X, gridRect.Y + rowOffsets[_highlightIndex],
                    gridRect.Width, rowOffsets[_highlightIndex + 1] - rowOffsets[_highlightIndex]);
            }
            else if (!_highlightIsRow && _highlightIndex < cols)
            {
                g.FillRectangle(theme.HoverHighlight,
                    gridRect.X + colOffsets[_highlightIndex], gridRect.Y,
                    colOffsets[_highlightIndex + 1] - colOffsets[_highlightIndex], gridRect.Height);
            }
        }

        // Grid lines.
        for (var r = 0; r <= rows; r++)
        {
            var y = gridRect.Y + rowOffsets[r];
            g.DrawLine(theme.GridLine, gridRect.X, y, gridRect.Right, y);
        }
        for (var c = 0; c <= cols; c++)
        {
            var x = gridRect.X + colOffsets[c];
            g.DrawLine(theme.GridLine, x, gridRect.Y, x, gridRect.Bottom);
        }

        // Column size annotations along the top.
        for (var c = 0; c < cols; c++)
        {
            var style = c < _columnStyles.Count ? _columnStyles[c] : null;
            DrawCentered(g, FormatStyleDisplay(style),
                gridRect.X + colOffsets[c], 2,
                colOffsets[c + 1] - colOffsets[c], ColumnLabelHeight - 2,
                theme.ControlText);
        }

        // Row size annotations along the left.
        for (var r = 0; r < rows; r++)
        {
            var style = r < _rowStyles.Count ? _rowStyles[r] : null;
            DrawRightAligned(g, FormatStyleDisplay(style),
                2, gridRect.Y + rowOffsets[r],
                RowLabelWidth - 6, rowOffsets[r + 1] - rowOffsets[r],
                theme.ControlText);
        }

        // Child placeholders at their assigned cells (explicit positions first,
        // remaining controls fill free cells in row-major order).
        var cells = _target.AssignCells(rows, cols);
        for (var i = 0; i < _target.Controls.Count && i < cells.Length; i++)
        {
            if (cells[i] == null) continue;
            var child = _target.Controls[i];
            var (row, col) = cells[i]!.Value;
            var cell = new Rectangle(
                gridRect.X + colOffsets[col],
                gridRect.Y + rowOffsets[row],
                colOffsets[col + 1] - colOffsets[col],
                rowOffsets[row + 1] - rowOffsets[row]);

            var inner = new Rectangle(cell.X + 3, cell.Y + 3, cell.Width - 6, cell.Height - 6);
            if (inner.Width <= 2 || inner.Height <= 2) continue;

            g.FillRectangle(theme.ControlBackground, inner.X, inner.Y, inner.Width, inner.Height);
            g.DrawRectangle(theme.Border, inner.X + 0.5f, inner.Y + 0.5f, inner.Width - 1f, inner.Height - 1f);

            var label = !string.IsNullOrEmpty(child.Name)
                ? child.Name
                : child.GetType().Name;
            DrawCentered(g, label, inner.X, inner.Y, inner.Width, inner.Height, theme.ControlText);
        }
    }

    /// <summary>
    /// Computes the unscaled pixel sizes for the preview using the target panel's
    /// inner dimensions and its children's preferred sizes.
    /// </summary>
    private (float[]? Widths, float[]? Heights) ComputeScaledSizes(int rows, int cols)
    {
        if (_target == null) return (null, null);

        var (autoColumns, autoRows) = _target.ComputeAutoSizes(rows, cols, _rowStyles, _columnStyles);

        var innerWidth = Math.Max(1, _target.Width - _target.Padding.Horizontal - 4);
        var innerHeight = Math.Max(1, _target.Height - _target.Padding.Vertical - 4);

        var widths = TableLayoutCalculator.ComputeSizes(_columnStyles, cols, innerWidth, autoColumns);
        var heights = TableLayoutCalculator.ComputeSizes(_rowStyles, rows, innerHeight, autoRows);
        return (widths, heights);
    }

    /// <summary>
    /// Converts the computed sizes into integer cumulative offsets scaled so that the
    /// total spans exactly <paramref name="totalPixels"/>.
    /// </summary>
    private static int[] ScaledOffsets(float[] sizes, int totalPixels)
    {
        var sum = sizes.Sum();
        if (sum <= 0f)
            sum = sizes.Length;

        var offsets = new int[sizes.Length + 1];
        var accumulated = 0f;
        for (var i = 0; i < sizes.Length; i++)
        {
            offsets[i] = (int)Math.Round(accumulated);
            var size = sizes[i] > 0f ? sizes[i] : 1f;
            accumulated += size / sum * totalPixels;
        }
        offsets[sizes.Length] = totalPixels;
        return offsets;
    }

    private static void DrawCentered(Graphics g, string text, int x, int y, int width, int height, Color color)
    {
        if (string.IsNullOrEmpty(text) || width <= 0 || height <= 0) return;
        var (tw, th) = CoordinateTransform.MeasureText(text, Font.Default, 1f);
        if (tw > width)
            text = Truncate(text, width);
        (tw, th) = CoordinateTransform.MeasureText(text, Font.Default, 1f);
        g.DrawString(text, Font.Default, color, x + (width - tw) / 2f, y + (height - th) / 2f);
    }

    private static void DrawRightAligned(Graphics g, string text, int x, int y, int width, int height, Color color)
    {
        if (string.IsNullOrEmpty(text) || width <= 0 || height <= 0) return;
        var (tw, th) = CoordinateTransform.MeasureText(text, Font.Default, 1f);
        if (tw > width)
            text = Truncate(text, width);
        (tw, th) = CoordinateTransform.MeasureText(text, Font.Default, 1f);
        g.DrawString(text, Font.Default, color, x + width - tw, y + (height - th) / 2f);
    }

    private static string Truncate(string text, int maxWidth)
    {
        var result = text;
        while (result.Length > 1 && CoordinateTransform.MeasureText(result + "…", Font.Default, 1f).width > maxWidth)
            result = result[..^1];
        return result.Length > 1 ? result + "…" : result;
    }

    /// <summary>
    /// Formats a style for display in the preview annotations.
    /// </summary>
    /// <param name="style">The style to format, or <c>null</c> for an unstyled row/column.</param>
    /// <returns>A human-readable size annotation.</returns>
    internal static string FormatStyleDisplay(TableLayoutStyle? style)
    {
        if (style == null)
            return "—";

        return style.SizeType switch
        {
            SizeType.AutoSize => LangRes.GetString("TableLayoutDialog_SizeTypeAutoSize"),
            SizeType.Fill => LangRes.GetString("TableLayoutDialog_SizeTypeFill"),
            SizeType.Absolute => style.Value.ToString("0.##", CultureInfo.InvariantCulture) + " px",
            _ => style.Value.ToString("0.##", CultureInfo.InvariantCulture) + " %",
        };
    }
}
