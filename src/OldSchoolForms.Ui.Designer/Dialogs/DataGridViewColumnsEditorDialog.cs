using System;
using System.Collections.Generic;
using System.Linq;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Designer.PropertyGrid;
using OldSchoolForms.Ui.Resources;

namespace OldSchoolForms.Ui.Designer.Dialogs
{
    using PropertyGrid = OldSchoolForms.Ui.Designer.PropertyGrid.PropertyGrid;

    /// <summary>
    /// A modal dialog that lets the user view, create, edit and delete the
    /// <see cref="DataGridViewColumn"/> entries of a <see cref="DataGridView"/>. The columns
    /// are listed on the left in a compact three-column grid (Name, Header text, Cell edit type)
    /// while the full property set of the selected column is edited in a
    /// <see cref="PropertyGrid"/> on the right. Changes are applied to the control only when the
    /// dialog is confirmed with OK; cancelling discards all edits.
    /// </summary>
    public class DataGridViewColumnsEditorDialog : Form
    {
        private readonly DataGridView _target;
        protected readonly List<DataGridViewColumn> _working = new();
        private DataGridViewColumn? _selectedColumn;
        protected DataGridView? _grid;
        protected readonly PropertyGrid _propertyGrid;

        /// <summary>
        /// Opens the columns editor as a separate top-level window and blocks until the user
        /// clicks OK or Cancel.
        /// </summary>
        /// <param name="dataGridView">The control whose columns are edited.</param>
        /// <param name="owner">The owner form, or null.</param>
        /// <returns><see cref="DialogResult.OK"/> if confirmed, otherwise <see cref="DialogResult.Cancel"/>.</returns>
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

            Size = new Size(860, 560);
            FormBorderStyle = FormBorderStyle.Sizable;

            var toolbar = new ToolStrip
            {
                Dock = DockStyle.Top,
                Height = 30
            };

            var btnAdd = new ToolStripButton(LangRes.GetString("DataGridViewColumnsDialog_Add"))
            {
                Image = Icons.AddCircle16,
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText
            };
            btnAdd.Click += (_, _) => AddColumn();

            var btnDelete = new ToolStripButton(LangRes.GetString("DataGridViewColumnsDialog_Delete"))
            {
                Image = Icons.Trash,
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText
            };
            btnDelete.Click += (_, _) => DeleteSelectedColumn();

            var btnOk = new ToolStripButton(LangRes.GetString("DataGridViewColumnsDialog_Ok"))
            {
                Image = Icons.CheckmarkCircle16,
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText
            };
            btnOk.Click += (_, _) => CloseDialog(true);

            var btnCancel = new ToolStripButton(LangRes.GetString("DataGridViewColumnsDialog_Cancel"))
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

            var split = new SplitPanel
            {
                Dock = DockStyle.Fill,
                Orientation = SplitOrientation.Horizontal,
                SplitterDistance = 480
            };

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                ColumnHeadersVisible = true,
                RowHeadersVisible = false,
                ShowGridLines = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true
            };

            _grid.Columns.Add(new DataGridViewColumn
            {
                Name = "Name",
                HeaderText = LangRes.GetString("DataGridViewColumnsDialog_ColName"),
                Width = 150,
                CellEditType = DataGridViewColumnEditType.None
            });
            _grid.Columns.Add(new DataGridViewColumn
            {
                Name = "HeaderText",
                HeaderText = LangRes.GetString("DataGridViewColumnsDialog_ColHeaderText"),
                Width = 180,
                CellEditType = DataGridViewColumnEditType.None
            });
            _grid.Columns.Add(new DataGridViewColumn
            {
                Name = "CellEditType",
                HeaderText = LangRes.GetString("DataGridViewColumnsDialog_ColCellEditType"),
                Width = 120,
                CellEditType = DataGridViewColumnEditType.None
            });

            _grid.SelectionChanged += OnGridSelectionChanged;

            _propertyGrid = new PropertyGrid
            {
                Dock = DockStyle.Fill
            };
            _propertyGrid.PropertyValueChanged += OnPropertyValueChanged;

            split.Panel1.Controls.Add(_grid);
            split.Panel2.Controls.Add(_propertyGrid);

            Controls.Add(split);
            Controls.Add(toolbar);

            PopulateWorking();
            PopulateGrid();

            if (_working.Count > 0)
            {
                _grid.SelectedRowIndex = 0;
                BindSelected();
            }
        }

        private void PopulateWorking()
        {
            _working.Clear();
            foreach (var column in _target.Columns)
            {
                _working.Add(column.Clone());
            }
        }

        private void PopulateGrid()
        {
            if (_grid == null) return;

            _grid.Rows.Clear();
            foreach (var column in _working)
            {
                var row = new DataGridViewRow();
                row.Cells.Add(new DataGridViewCell { Value = column.Name });
                row.Cells.Add(new DataGridViewCell { Value = column.HeaderText });
                row.Cells.Add(new DataGridViewCell { Value = column.CellEditType.ToString() });
                row.DataBoundItem = column;
                _grid.Rows.Add(row);
            }
        }

        private void OnGridSelectionChanged(object? sender, EventArgs e)
        {
            BindSelected();
        }

        private void BindSelected()
        {
            if (_grid == null) return;

            int index = _grid.SelectedRowIndex;
            var column = index >= 0 && index < _working.Count ? _working[index] : null;
            _selectedColumn = column;
            _propertyGrid.BindObject(column);
        }

        private void OnPropertyValueChanged(object? sender, PropertyValueChangedEventArgs e)
        {
            if (_selectedColumn == null) return;

            if (e.PropertyName == nameof(DataGridViewColumn.Name)
                || e.PropertyName == nameof(DataGridViewColumn.HeaderText)
                || e.PropertyName == nameof(DataGridViewColumn.CellEditType))
            {
                RefreshRowFor(_selectedColumn);
            }
        }

        private void RefreshRowFor(DataGridViewColumn column)
        {
            if (_grid == null) return;

            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                if (!ReferenceEquals(_grid.Rows[i].DataBoundItem, column)) continue;

                _grid.Rows[i].Cells[0].Value = column.Name;
                _grid.Rows[i].Cells[1].Value = column.HeaderText;
                _grid.Rows[i].Cells[2].Value = column.CellEditType.ToString();
                _grid.Invalidate();
                return;
            }
        }

        protected void AddColumn()
        {
            if (_grid == null) return;

            var column = new DataGridViewColumn
            {
                Name = "Column" + (_working.Count + 1),
                HeaderText = LangRes.GetString("DataGridViewColumnsDialog_NewColumn"),
                CellEditType = DataGridViewColumnEditType.TextBox
            };

            _working.Add(column);

            var row = new DataGridViewRow();
            row.Cells.Add(new DataGridViewCell { Value = column.Name });
            row.Cells.Add(new DataGridViewCell { Value = column.HeaderText });
            row.Cells.Add(new DataGridViewCell { Value = column.CellEditType.ToString() });
            row.DataBoundItem = column;
            _grid.Rows.Add(row);

            _grid.SelectedRowIndex = _working.Count - 1;
            BindSelected();
        }

        protected void DeleteSelectedColumn()
        {
            if (_grid == null) return;

            int index = _grid.SelectedRowIndex;
            if (index < 0 || index >= _working.Count) return;

            var column = _working[index];
            _working.RemoveAt(index);

            if (index < _grid.Rows.Count)
            {
                _grid.Rows.Remove(_grid.Rows[index]);
            }

            int next = index < _working.Count ? index : Math.Max(0, _working.Count - 1);
            if (_working.Count > 0)
            {
                _grid.SelectedRowIndex = next;
            }
            else
            {
                _selectedColumn = null;
            }
            BindSelected();
        }

        private void CloseDialog(bool ok)
        {
            if (ok)
                ApplyChanges();

            DialogResult = ok ? DialogResult.OK : DialogResult.Cancel;
            _modal = false;
        }

        /// <summary>
        /// Pushes the working copy of the columns onto the target <see cref="DataGridView"/>,
        /// replacing its existing column set with the edited clones.
        /// </summary>
        protected void ApplyChanges()
        {
            _target.Columns.Clear();
            foreach (var column in _working)
            {
                _target.Columns.Add(column);
            }

            _target.Invalidate();
            _target.PerformLayout();
        }

        /// <summary>
        /// Handles keyboard shortcuts. While the grid's inline editor is active, all keys are
        /// delegated to it. Otherwise Enter confirms (OK) and Escape cancels the dialog.
        /// </summary>
        /// <param name="e">The key event data.</param>
        protected override void OnKeyDown(KeyEventArgs e)
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
