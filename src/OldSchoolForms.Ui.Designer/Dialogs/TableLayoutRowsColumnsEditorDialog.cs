using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Layout;
using OldSchoolForms.Ui.Resources;

namespace OldSchoolForms.Ui.Designer.Dialogs
{
    /// <summary>
    /// A modal dialog that lets the user configure the rows and columns of a
    /// <see cref="TableLayoutPanel"/> the way the Windows Forms designer does: a
    /// tabbed editor with one grid per direction, each row offering a size-type
    /// combo box (AutoSize / Percent / Absolute) and a size field, plus a live
    /// preview of the resulting layout at the bottom. Changes are applied to the
    /// panel only when the dialog is confirmed with OK; cancelling discards them.
    /// </summary>
    public class TableLayoutRowsColumnsEditorDialog : Form
    {
        private readonly TableLayoutPanel _target;
        private readonly List<TableLayoutStyle> _rowWorking = new();
        private readonly List<TableLayoutStyle> _columnWorking = new();
        private TabControl _tabs = null!;
        private DataGridView _rowsGrid = null!;
        private DataGridView _columnsGrid = null!;
        private TableLayoutPreviewControl _preview = null!;

        /// <summary>The size types in the order shown by the combo box.</summary>
        private static readonly SizeType[] SizeTypeOrder =
        {
            SizeType.AutoSize,
            SizeType.Fill,
            SizeType.Absolute,
            SizeType.Percent
        };

        /// <summary>
        /// Opens the rows-and-columns editor as a separate top-level window and blocks
        /// until the user clicks OK or Cancel.
        /// </summary>
        /// <param name="tableLayoutPanel">The panel whose rows and columns are edited.</param>
        /// <param name="owner">The owner form, or null.</param>
        /// <returns><see cref="DialogResult.OK"/> if confirmed, otherwise <see cref="DialogResult.Cancel"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tableLayoutPanel"/> is null.</exception>
        public static DialogResult ShowDialog(TableLayoutPanel tableLayoutPanel, Form? owner = null)
        {
            if (tableLayoutPanel == null) throw new ArgumentNullException(nameof(tableLayoutPanel));

            var dialog = new TableLayoutRowsColumnsEditorDialog(tableLayoutPanel)
            {
                Text = LangRes.GetString("TableLayoutDialog_Title")
            };

            return dialog.ShowDialog(owner);
        }

        internal TableLayoutRowsColumnsEditorDialog(TableLayoutPanel tableLayoutPanel)
        {
            _target = tableLayoutPanel;

            Size = new Size(720, 560);
            FormBorderStyle = FormBorderStyle.Sizable;

            var toolbar = new ToolStrip
            {
                Dock = DockStyle.Top,
                Height = 30
            };

            var btnAdd = new ToolStripButton(LangRes.GetString("TableLayoutDialog_Add"))
            {
                Image = Icons.AddCircle16,
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText
            };
            btnAdd.Click += (_, _) => AddEntry();

            var btnDelete = new ToolStripButton(LangRes.GetString("TableLayoutDialog_Delete"))
            {
                Image = Icons.Trash,
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText
            };
            btnDelete.Click += (_, _) => DeleteSelectedEntry();

            var btnOk = new ToolStripButton(LangRes.GetString("TableLayoutDialog_Ok"))
            {
                Image = Icons.CheckmarkCircle16,
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText
            };
            btnOk.Click += (_, _) => CloseDialog(true);

            var btnCancel = new ToolStripButton(LangRes.GetString("TableLayoutDialog_Cancel"))
            {
                Image = Icons.DismissCircle16,
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText
            };
            btnCancel.Click += (_, _) => CloseDialog(false);

            toolbar.Items.Add(btnAdd);
            toolbar.Items.Add(btnDelete);
            toolbar.Items.Add(new ToolStripSeparator());
            toolbar.Items.Add(btnOk);
            toolbar.Items.Add(btnCancel);

            _preview = new TableLayoutPreviewControl
            {
                Dock = DockStyle.Bottom,
                Height = 220,
                Target = _target,
                WorkingRowStyles = _rowWorking,
                WorkingColumnStyles = _columnWorking
            };

            _tabs = new TabControl
            {
                Dock = DockStyle.Fill
            };

            var rowsPage = new TabPage { Text = LangRes.GetString("TableLayoutDialog_RowsTab") };
            var columnsPage = new TabPage { Text = LangRes.GetString("TableLayoutDialog_ColumnsTab") };

            _rowsGrid = CreateStyleGrid();
            _columnsGrid = CreateStyleGrid();

            rowsPage.Controls.Add(_rowsGrid);
            columnsPage.Controls.Add(_columnsGrid);
            _tabs.AddTabPage(rowsPage);
            _tabs.AddTabPage(columnsPage);

            Controls.Add(_tabs);
            Controls.Add(_preview);
            Controls.Add(toolbar);

            SeedWorking(_rowWorking, _target.RowStyles, _target.EffectiveRowCount);
            SeedWorking(_columnWorking, _target.ColumnStyles, _target.EffectiveColumnCount);

            PopulateGrid(_rowsGrid, _rowWorking);
            PopulateGrid(_columnsGrid, _columnWorking);

            _tabs.SelectedIndex = 0;
            if (_rowWorking.Count > 0)
                _rowsGrid.SelectedRowIndex = 0;

            _preview.SetHighlight(true, _rowsGrid.SelectedRowIndex);
        }

        /// <summary>
        /// Creates one of the two style grids (rows or columns) with the size-type
        /// combo box and the size text column.
        /// </summary>
        private DataGridView CreateStyleGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                ColumnHeadersVisible = true,
                RowHeadersVisible = false,
                ShowGridLines = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = false
            };

            grid.Columns.Add(new DataGridViewColumn
            {
                Name = "SizeType",
                HeaderText = LangRes.GetString("TableLayoutDialog_ColSizeType"),
                Width = 180,
                CellEditType = DataGridViewColumnEditType.ComboBox,
                Items = new List<object>(SizeTypeOrder.Select(DisplayFor))
            });
            grid.Columns.Add(new DataGridViewColumn
            {
                Name = "Size",
                HeaderText = LangRes.GetString("TableLayoutDialog_ColSize"),
                Width = 140,
                CellEditType = DataGridViewColumnEditType.TextBox
            });

            grid.CellEndEdit += OnStyleCellEndEdit;
            grid.SelectionChanged += OnStyleSelectionChanged;
            return grid;
        }

        /// <summary>
        /// Fills the working list with clones of the panel's existing styles so that
        /// reopening the dialog shows what is currently configured. Missing entries
        /// (rows/columns beyond the stored styles) are seeded as percent styles that
        /// reproduce the panel's equal distribution.
        /// </summary>
        private static void SeedWorking(List<TableLayoutStyle> working, IList<TableLayoutStyle> source, int effectiveCount)
        {
            working.Clear();
            foreach (var style in source)
                working.Add(style.Clone());

            while (working.Count < effectiveCount)
                working.Add(new TableLayoutStyle(SizeType.Percent, 100f / effectiveCount));
        }

        private static void PopulateGrid(DataGridView grid, List<TableLayoutStyle> styles)
        {
            grid.Rows.Clear();
            foreach (var style in styles)
            {
                var row = new DataGridViewRow();
                row.Cells.Add(new DataGridViewCell { Value = DisplayFor(style.SizeType) });
                row.Cells.Add(new DataGridViewCell { Value = SizeDisplay(style) });
                row.DataBoundItem = style;
                grid.Rows.Add(row);
            }
        }

        private void OnStyleSelectionChanged(object? sender, EventArgs e)
        {
            var grid = sender as DataGridView;
            if (grid == null) return;
            if (!IsActiveGrid(grid)) return;

            var index = grid.SelectedRowIndex;
            _preview.SetHighlight(ReferenceEquals(grid, _rowsGrid), index);
        }

        private void OnStyleCellEndEdit(object? sender, DataGridViewCellEventArgs e)
        {
            if (sender is not DataGridView grid) return;
            var styles = WorkingListFor(grid);
            if (e.RowIndex < 0 || e.RowIndex >= styles.Count) return;

            var style = styles[e.RowIndex];
            var cell = e.ColumnIndex >= 0 && e.ColumnIndex < grid.Rows[e.RowIndex].Cells.Count
                ? grid.Rows[e.RowIndex].Cells[e.ColumnIndex]
                : null;
            if (cell == null) return;

            if (e.ColumnIndex == 0)
            {
                var parsed = ParseSizeTypeDisplay(cell.Value as string);
                if (parsed.HasValue)
                {
                    style.SizeType = parsed.Value;
                    cell.Value = DisplayFor(style.SizeType);
                }
                else
                {
                    cell.Value = DisplayFor(style.SizeType);
                }

                // The size column display depends on the size type.
                if (e.ColumnIndex == 0 && grid.Rows[e.RowIndex].Cells.Count > 1)
                    grid.Rows[e.RowIndex].Cells[1].Value = SizeDisplay(style);
            }
            else if (e.ColumnIndex == 1)
            {
                // Fill and AutoSize take no numeric value; revert the cell to empty.
                if (style.SizeType is SizeType.Fill or SizeType.AutoSize)
                {
                    cell.Value = string.Empty;
                }
                else if (TryParseSize(cell.Value as string, out var value))
                {
                    style.Value = value;
                    cell.Value = SizeDisplay(style);
                }
                else
                {
                    cell.Value = SizeDisplay(style);
                }
            }

            grid.Invalidate();
            _preview.RefreshPreview();
        }

        private bool IsActiveGrid(DataGridView grid)
        {
            var active = ActiveGrid;
            return ReferenceEquals(active, grid);
        }

        private DataGridView ActiveGrid => _tabs.SelectedIndex == 0 ? _rowsGrid : _columnsGrid;

        private List<TableLayoutStyle> WorkingListFor(DataGridView grid)
            => ReferenceEquals(grid, _rowsGrid) ? _rowWorking : _columnWorking;

        private void AddEntry()
        {
            var grid = ActiveGrid;
            var styles = WorkingListFor(grid);

            var averagePercent = styles
                .Where(s => s.SizeType == SizeType.Percent)
                .Select(s => s.Value)
                .DefaultIfEmpty(100f)
                .Average();

            var style = new TableLayoutStyle(SizeType.Percent, averagePercent > 0 ? averagePercent : 100f);
            styles.Add(style);

            var row = new DataGridViewRow();
            row.Cells.Add(new DataGridViewCell { Value = DisplayFor(style.SizeType) });
            row.Cells.Add(new DataGridViewCell { Value = SizeDisplay(style) });
            row.DataBoundItem = style;
            grid.Rows.Add(row);

            grid.SelectedRowIndex = styles.Count - 1;
            _preview.SetHighlight(ReferenceEquals(grid, _rowsGrid), styles.Count - 1);
            _preview.RefreshPreview();
        }

        private void DeleteSelectedEntry()
        {
            var grid = ActiveGrid;
            var styles = WorkingListFor(grid);
            var index = grid.SelectedRowIndex;
            if (index < 0 || index >= styles.Count) return;

            styles.RemoveAt(index);
            if (index < grid.Rows.Count)
                grid.Rows.Remove(grid.Rows[index]);

            var next = index < styles.Count ? index : Math.Max(0, styles.Count - 1);
            if (styles.Count > 0)
                grid.SelectedRowIndex = next;

            _preview.SetHighlight(ReferenceEquals(grid, _rowsGrid), styles.Count > 0 ? next : -1);
            _preview.RefreshPreview();
        }

        private void CloseDialog(bool ok)
        {
            if (ok)
            {
                // A toolbar click does not route through the grid, so a cell that is
                // still in edit mode would never commit its value. End the edit session
                // first so the last change lands in the working list before applying.
                if (_rowsGrid.IsCurrentCellInEditMode)
                    _rowsGrid.EndEdit(true);
                if (_columnsGrid.IsCurrentCellInEditMode)
                    _columnsGrid.EndEdit(true);

                ApplyChanges();
            }

            DialogResult = ok ? DialogResult.OK : DialogResult.Cancel;
            _modal = false;
        }

        /// <summary>
        /// Pushes the working copies of the row and column styles onto the target
        /// panel, replacing its existing style collections, and refreshes its layout.
        /// </summary>
        private void ApplyChanges()
        {
            _target.RowStyles.Clear();
            foreach (var style in _rowWorking)
                _target.RowStyles.Add(style);

            _target.ColumnStyles.Clear();
            foreach (var style in _columnWorking)
                _target.ColumnStyles.Add(style);

            _target.InvalidateTableLayout();
            _target.PerformLayout();
        }

        /// <summary>
        /// Handles keyboard shortcuts. While a grid cell editor is active, all keys are
        /// delegated to it. Otherwise Enter confirms (OK) and Escape cancels the dialog.
        /// </summary>
        /// <param name="e">The key event data.</param>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if ((_rowsGrid.IsCurrentCellInEditMode || _columnsGrid.IsCurrentCellInEditMode))
            {
                base.OnKeyDown(e);
                return;
            }

            if (e.KeyCode == Keys.Enter)
            {
                CloseDialog(true);
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Escape)
            {
                CloseDialog(false);
                e.Handled = true;
                return;
            }

            base.OnKeyDown(e);
        }

        /// <summary>
        /// Returns the localized display string for a size type.
        /// </summary>
        private static string DisplayFor(SizeType sizeType) => sizeType switch
        {
            SizeType.AutoSize => LangRes.GetString("TableLayoutDialog_SizeTypeAutoSize"),
            SizeType.Fill => LangRes.GetString("TableLayoutDialog_SizeTypeFill"),
            SizeType.Absolute => LangRes.GetString("TableLayoutDialog_SizeTypeAbsolute"),
            _ => LangRes.GetString("TableLayoutDialog_SizeTypePercent"),
        };

        /// <summary>
        /// Maps a localized display string back to its <see cref="SizeType"/>,
        /// accepting the invariant enum names as a fallback.
        /// </summary>
        private static SizeType? ParseSizeTypeDisplay(string? display)
        {
            if (string.IsNullOrWhiteSpace(display)) return null;

            foreach (var sizeType in SizeTypeOrder)
            {
                if (string.Equals(display, DisplayFor(sizeType), StringComparison.OrdinalIgnoreCase))
                    return sizeType;
            }

            if (Enum.TryParse<SizeType>(display, true, out var parsed))
                return parsed;

            return null;
        }

        /// <summary>
        /// Formats the size value for the size column. Auto-sized and fill rows/columns
        /// show an empty cell because their size is content-driven or takes the rest.
        /// </summary>
        private static string SizeDisplay(TableLayoutStyle style)
            => style.SizeType is SizeType.AutoSize or SizeType.Fill
                ? string.Empty
                : style.Value.ToString("0.##", CultureInfo.InvariantCulture);

        private static bool TryParseSize(string? text, out float value)
            => float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
               && value >= 0f;
    }
}
