using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Core;

namespace OldSchoolForms.Ui.Dialogs
{
    /// <summary>
    /// A modal dialog that lets the user view, create and delete the
    /// <see cref="DataGridViewColumn"/> entries of a <see cref="DataGridView"/>.
    /// Following the classic Windows Forms collection-editor layout, the columns are
    /// listed in a <see cref="DataGridView"/> where each row is a column and each
    /// grid column is one editable property of <see cref="DataGridViewColumn"/>.
    /// Changes are only applied to the control when the dialog is confirmed with OK;
    /// cancelling discards all edits without touching the control.
    /// </summary>
    public class DataGridViewColumnsEditorDialog : Form
    {
        private readonly DataGridView? _target;
        protected DataGridView? _grid;

        /// <summary>
        /// Describes one editable property of <see cref="DataGridViewColumn"/> as a
        /// column in the editor grid: its localized header, a getter that renders the
        /// current value as text, a setter that parses the edited text back onto the
        /// column, and whether the cell is read-only.
        /// </summary>
        private sealed class ColumnPropertySpec
        {
            public string HeaderKey { get; }
            public Func<DataGridViewColumn, string> Get { get; }
            public Action<DataGridViewColumn, string> Set { get; }
            public bool ReadOnly { get; }
            public int Width { get; }

            public ColumnPropertySpec(string headerKey, Func<DataGridViewColumn, string> get,
                Action<DataGridViewColumn, string> set, bool readOnly = false, int width = 120)
            {
                HeaderKey = headerKey;
                Get = get;
                Set = set;
                ReadOnly = readOnly;
                Width = width;
            }
        }

        private static readonly List<ColumnPropertySpec> Specs = new()
        {
            new("DataGridViewColumnsDialog_ColName", c => c.Name, (c, v) => c.Name = v),
            new("DataGridViewColumnsDialog_ColHeaderText", c => c.HeaderText, (c, v) => c.HeaderText = v),
            new("DataGridViewColumnsDialog_ColDataPropertyName", c => c.DataPropertyName, (c, v) => c.DataPropertyName = v),
            new("DataGridViewColumnsDialog_ColWidth", c => c.Width.ToString(CultureInfo.InvariantCulture),
                (c, v) => { if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var w)) c.Width = w; }),
            new("DataGridViewColumnsDialog_ColReadOnly", c => c.ReadOnly.ToString(),
                (c, v) => { if (bool.TryParse(v, out var b)) c.ReadOnly = b; }),
            new("DataGridViewColumnsDialog_ColValueType", c => c.ValueType?.Name ?? string.Empty,
                (c, v) => c.ValueType = ParseType(v)),
            new("DataGridViewColumnsDialog_ColTextAlign", c => c.TextAlign.ToString(),
                (c, v) => { if (Enum.TryParse(v, true, out DataGridViewContentAlignment a)) c.TextAlign = a; }),
            new("DataGridViewColumnsDialog_ColFormatString", c => c.FormatString ?? string.Empty,
                (c, v) => c.FormatString = string.IsNullOrEmpty(v) ? null : v),
            new("DataGridViewColumnsDialog_ColResizable", c => c.Resizable.ToString(),
                (c, v) => { if (bool.TryParse(v, out var b)) c.Resizable = b; }),
            new("DataGridViewColumnsDialog_ColSortable", c => c.Sortable.ToString(),
                (c, v) => { if (bool.TryParse(v, out var b)) c.Sortable = b; }),
            new("DataGridViewColumnsDialog_ColGroupable", c => c.Groupable.ToString(),
                (c, v) => { if (bool.TryParse(v, out var b)) c.Groupable = b; }),
            new("DataGridViewColumnsDialog_ColSortOrder", c => c.SortOrder.ToString(),
                (c, v) => { if (Enum.TryParse(v, true, out SortOrder s)) c.SortOrder = s; }),
            new("DataGridViewColumnsDialog_ColCellEditType", c => c.CellEditType.ToString(),
                (c, v) => { if (Enum.TryParse(v, true, out DataGridViewColumnEditType t)) c.CellEditType = t; }),
            new("DataGridViewColumnsDialog_ColItems", c => c.Items != null ? string.Join(", ", c.Items) : string.Empty,
                (c, v) => c.Items = ParseItems(v)),
            new("DataGridViewColumnsDialog_ColComboBoxDropDownStyle", c => c.ComboBoxDropDownStyle.ToString(),
                (c, v) => { if (Enum.TryParse(v, true, out DropDownStyle s)) c.ComboBoxDropDownStyle = s; }),
            new("DataGridViewColumnsDialog_ColTrueValue", c => c.TrueValue?.ToString() ?? string.Empty,
                (c, v) => c.TrueValue = ParseScalar(v, c)),
            new("DataGridViewColumnsDialog_ColFalseValue", c => c.FalseValue?.ToString() ?? string.Empty,
                (c, v) => c.FalseValue = ParseScalar(v, c)),
            new("DataGridViewColumnsDialog_ColPickerFormat", c => c.PickerFormat.ToString(),
                (c, v) => { if (Enum.TryParse(v, true, out DateTimePickerFormat f)) c.PickerFormat = f; }),
            new("DataGridViewColumnsDialog_ColPickerCustomFormat", c => c.PickerCustomFormat ?? string.Empty,
                (c, v) => c.PickerCustomFormat = string.IsNullOrEmpty(v) ? null : v),
            new("DataGridViewColumnsDialog_ColButtonText", c => c.ButtonText ?? string.Empty,
                (c, v) => c.ButtonText = string.IsNullOrEmpty(v) ? null : v),
            new("DataGridViewColumnsDialog_ColButtonIcon", c => c.ButtonIcon is SvgImage svg && !string.IsNullOrEmpty(svg.ResourceName)
                    ? svg.ResourceName!
                    : c.ButtonIcon?.ToString() ?? string.Empty,
                (_, _) => { }, readOnly: true, width: 220),
            new("DataGridViewColumnsDialog_ColHeaderCell", c => c.HeaderCell?.ToString() ?? string.Empty,
                (_, _) => { }, readOnly: true, width: 160),
        };

        /// <summary>
        /// Shows the columns editor modal dialog for the given data grid.
        /// </summary>
        /// <param name="dataGridView">The data grid whose columns are edited.</param>
        /// <param name="owner">The optional owning form; its zoom is inherited.</param>
        /// <returns>The dialog result (OK on commit, Cancel otherwise).</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="dataGridView"/> is null.</exception>
        public static DialogResult ShowDialog(DataGridView dataGridView, Form? owner = null)
        {
            if (dataGridView == null) throw new ArgumentNullException(nameof(dataGridView));

            var dialog = new DataGridViewColumnsEditorDialog(dataGridView)
            {
                Text = LangRes.GetString("DataGridViewColumnsDialog_Title")
            };

            return dialog.ShowDialog(owner);
        }

        internal DataGridViewColumnsEditorDialog(DataGridView dataGridView)
        {
            _target = dataGridView;

            Size = new Size(1000, 520);
            FormBorderStyle = FormBorderStyle.Sizable;

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                EditMode = DataGridViewEditMode.EditOnEnter,
                ColumnHeadersVisible = true,
                RowHeadersVisible = false,
                ShowGridLines = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = false
            };

            foreach (var spec in Specs)
            {
                _grid.Columns.Add(new DataGridViewColumn
                {
                    Name = spec.HeaderKey,
                    HeaderText = LangRes.GetString(spec.HeaderKey),
                    Width = spec.Width,
                    CellEditType = DataGridViewColumnEditType.TextBox,
                    ReadOnly = spec.ReadOnly
                });
            }

            PopulateGrid();

            Controls.Add(_grid!);

            var toolStrip = new ToolStrip
            {
                Dock = DockStyle.Bottom,
                Height = 30
            };

            var btnAdd = new ToolStripButton(LangRes.GetString("DataGridViewColumnsDialog_Add"));
            btnAdd.Click += (_, _) => AddRow();

            var btnDelete = new ToolStripButton(LangRes.GetString("DataGridViewColumnsDialog_Delete"));
            btnDelete.Click += (_, _) => DeleteSelectedRow();

            var separator = new ToolStripSeparator();

            var btnOk = new ToolStripButton(LangRes.GetString("DataGridViewColumnsDialog_Ok"));
            btnOk.Click += (_, _) => CloseDialog(true);

            var btnCancel = new ToolStripButton(LangRes.GetString("DataGridViewColumnsDialog_Cancel"));
            btnCancel.Click += (_, _) => CloseDialog(false);

            toolStrip.Items.Add(btnAdd);
            toolStrip.Items.Add(btnDelete);
            toolStrip.Items.Add(separator);
            toolStrip.Items.Add(btnOk);
            toolStrip.Items.Add(btnCancel);

            Controls.Add(toolStrip);
        }

        private void PopulateGrid()
        {
            if (_target == null) return;

            foreach (var column in _target.Columns)
            {
                var row = new DataGridViewRow();
                foreach (var spec in Specs)
                    row.Cells.Add(new DataGridViewCell { Value = spec.Get(column) });
                row.DataBoundItem = column;
                _grid!.Rows.Add(row);
            }
        }

        private void AddRow()
        {
            if (_grid == null) return;

            var row = new DataGridViewRow();
            foreach (var _ in Specs)
                row.Cells.Add(new DataGridViewCell { Value = string.Empty });
            row.DataBoundItem = null;
            _grid.Rows.Add(row);
            _grid.SelectedRowIndex = _grid.Rows.Count - 1;
        }

        private void DeleteSelectedRow()
        {
            if (_grid == null) return;

            int index = _grid.SelectedRowIndex;
            if (index < 0 || index >= _grid.Rows.Count) return;

            var row = _grid.Rows[index];
            _grid.Rows.Remove(row);
            _grid.SelectedRowIndex = index < _grid.Rows.Count ? index : Math.Max(0, _grid.Rows.Count - 1);
        }

        private void CloseDialog(bool ok)
        {
            if (ok)
                ApplyChanges();

            DialogResult = ok ? DialogResult.OK : DialogResult.Cancel;
            _modal = false;
        }

        /// <summary>
        /// Reconciles the target grid's column collection with the editor grid:
        /// existing columns are updated in place, new rows are appended as new columns,
        /// and columns whose rows were removed are deleted from the target.
        /// </summary>
        protected void ApplyChanges()
        {
            if (_target == null || _grid == null) return;

            var stillPresent = new List<DataGridViewColumn>();
            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                var row = _grid.Rows[i];
                var column = row.DataBoundItem as DataGridViewColumn;
                if (column == null) continue;

                ApplyRowToColumn(row, column);
                stillPresent.Add(column);
            }

            foreach (var column in _target.Columns.Where(c => !stillPresent.Contains(c)).ToList())
                _target.Columns.Remove(column);

            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                var row = _grid.Rows[i];
                if (row.DataBoundItem != null) continue;

                var newColumn = new DataGridViewColumn();
                ApplyRowToColumn(row, newColumn);
                _target.Columns.Add(newColumn);
            }

            _target.Invalidate();
            _target.PerformLayout();
        }

        private static void ApplyRowToColumn(DataGridViewRow row, DataGridViewColumn column)
        {
            for (int i = 0; i < Specs.Count && i < row.Cells.Count; i++)
            {
                var spec = Specs[i];
                if (spec.ReadOnly) continue;
                var text = row.Cells[i].Value as string ?? string.Empty;
                spec.Set(column, text);
            }
        }

        /// <summary>
        /// Parses a type name typed into the ValueType cell into a <see cref="Type"/>.
        /// Accepts common C# keyword aliases as well as fully qualified names.
        /// </summary>
        /// <param name="text">The type name text.</param>
        /// <returns>The resolved type, or <c>null</c> when empty or unresolvable.</returns>
        private static Type? ParseType(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            text = text.Trim();
            return text.ToLowerInvariant() switch
            {
                "int" or "int32" => typeof(int),
                "long" or "int64" => typeof(long),
                "double" => typeof(double),
                "float" or "single" => typeof(float),
                "decimal" => typeof(decimal),
                "bool" or "boolean" => typeof(bool),
                "string" => typeof(string),
                "datetime" => typeof(DateTime),
                _ => Type.GetType(text) ?? Type.GetType("System." + text)
            };
        }

        /// <summary>
        /// Parses a scalar cell value (used for <c>TrueValue</c>/<c>FalseValue</c>) into
        /// an object, honouring the column's <see cref="DataGridViewColumn.ValueType"/>
        /// as a conversion hint and otherwise inferring bool/int/decimal/string.
        /// </summary>
        /// <param name="text">The scalar text.</param>
        /// <param name="column">The column whose ValueType guides conversion.</param>
        /// <returns>The parsed value, or <c>null</c> when empty.</returns>
        private static object? ParseScalar(string text, DataGridViewColumn column)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            text = text.Trim();
            if (column.ValueType != null)
            {
                try { return Convert.ChangeType(text, column.ValueType, CultureInfo.InvariantCulture); }
                catch { return text; }
            }
            if (bool.TryParse(text, out var b)) return b;
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) return i;
            if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var d)) return d;
            return text;
        }

        /// <summary>
        /// Parses a comma-separated list of items for a combo-box column.
        /// </summary>
        /// <param name="text">The comma-separated item text.</param>
        /// <returns>A list of item strings, or <c>null</c> when empty.</returns>
        private static List<object>? ParseItems(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var items = text.Split(',')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(s => (object)s)
                .ToList();
            return items.Count > 0 ? items : null;
        }

        /// <summary>
        /// Handles keyboard shortcuts. While the grid's inline editor is active, all keys are
        /// delegated to it so Enter commits the cell, Tab moves to the next column and Escape
        /// cancels the edit. Otherwise Enter confirms (OK) and Escape cancels the dialog.
        /// </summary>
        /// <param name="e">The key event data.</param>
        protected internal override void OnKeyDown(KeyEventArgs e)
        {
            if (_grid != null && _grid.IsCurrentCellInEditMode)
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
    }
}
