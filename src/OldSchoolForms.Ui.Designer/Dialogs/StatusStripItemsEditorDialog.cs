using System;
using System.Collections.Generic;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Designer.PropertyGrid;
using OldSchoolForms.Ui.Resources;

namespace OldSchoolForms.Ui.Designer.Dialogs
{
    using PropertyGrid = OldSchoolForms.Ui.Designer.PropertyGrid.PropertyGrid;

    /// <summary>
    /// A modal dialog that lets the user view, create, edit, reorder and delete the
    /// <see cref="ToolStripStatusLabel"/> entries of a <see cref="StatusStrip"/>. The labels
    /// are listed on the left in a compact two-column grid (Type, Text) while the property
    /// set of the selected label is edited in a <see cref="PropertyGrid"/> on the right.
    /// Changes are applied to the control only when the dialog is confirmed with OK;
    /// cancelling discards all edits.
    /// </summary>
    public class StatusStripItemsEditorDialog : Form
    {
        private readonly StatusStrip _target;
        private readonly List<ToolStripStatusLabel> _working = new();
        private ToolStripStatusLabel? _selectedItem;
        private DataGridView? _grid;
        private readonly PropertyGrid _propertyGrid;

        /// <summary>
        /// Opens the status strip items editor as a separate top-level window and blocks
        /// until the user clicks OK or Cancel.
        /// </summary>
        /// <param name="statusStrip">The control whose labels are edited.</param>
        /// <param name="owner">The owner form, or null.</param>
        /// <returns><see cref="DialogResult.OK"/> if confirmed, otherwise <see cref="DialogResult.Cancel"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="statusStrip"/> is null.</exception>
        public static DialogResult ShowDialog(StatusStrip statusStrip, Form? owner = null)
        {
            if (statusStrip == null) throw new ArgumentNullException(nameof(statusStrip));

            var dialog = new StatusStripItemsEditorDialog(statusStrip)
            {
                Text = LangRes.GetString("StatusStripItemsDialog_Title")
            };

            return dialog.ShowDialog(owner);
        }

        internal StatusStripItemsEditorDialog(StatusStrip statusStrip)
        {
            _target = statusStrip;

            Size = new Size(860, 520);
            FormBorderStyle = FormBorderStyle.Sizable;

            var toolbar = new ToolStrip
            {
                Dock = DockStyle.Top,
                Height = 30
            };

            var btnAddLabel = MakeToolButton("StatusStripItemsDialog_AddLabel", Icons.AddCircle16, () => AddItem(new ToolStripStatusLabel { Text = LangRes.GetString("StatusStripItemsDialog_NewLabel") }));
            var btnDelete = MakeToolButton("StatusStripItemsDialog_Delete", Icons.Trash, DeleteSelectedItem);
            var btnMoveUp = MakeToolButton("StatusStripItemsDialog_MoveUp", null, () => MoveSelected(-1));
            var btnMoveDown = MakeToolButton("StatusStripItemsDialog_MoveDown", null, () => MoveSelected(1));
            var btnOk = MakeToolButton("StatusStripItemsDialog_Ok", Icons.CheckmarkCircle16, () => CloseDialog(true));
            var btnCancel = MakeToolButton("StatusStripItemsDialog_Cancel", Icons.DismissCircle16, () => CloseDialog(false));

            toolbar.Items.Add(btnAddLabel);
            toolbar.Items.Add(new ToolStripSeparator());
            toolbar.Items.Add(btnDelete);
            toolbar.Items.Add(btnMoveUp);
            toolbar.Items.Add(btnMoveDown);
            toolbar.Items.Add(new ToolStripSeparator());
            toolbar.Items.Add(btnOk);
            toolbar.Items.Add(btnCancel);

            var split = new SplitPanel
            {
                Dock = DockStyle.Fill,
                Orientation = SplitOrientation.Horizontal,
                SplitterDistance = 680
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
                Name = "Type",
                HeaderText = LangRes.GetString("StatusStripItemsDialog_ColType"),
                Width = 160,
                CellEditType = DataGridViewColumnEditType.None
            });
            _grid.Columns.Add(new DataGridViewColumn
            {
                Name = "Text",
                HeaderText = LangRes.GetString("StatusStripItemsDialog_ColText"),
                Width = 220,
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

        private static ToolStripButton MakeToolButton(string resourceKey, SvgImage? icon, Action onClick)
        {
            var button = new ToolStripButton(LangRes.GetString(resourceKey))
            {
                DisplayStyle = icon != null ? ToolStripItemDisplayStyle.ImageAndText : ToolStripItemDisplayStyle.Text
            };
            if (icon != null) button.Image = icon;
            button.Click += (_, _) => onClick();
            return button;
        }

        /// <summary>
        /// Creates a copy of a status strip label, preserving its text, colors, font and
        /// visibility so the working copy can be edited independently of the target.
        /// </summary>
        /// <param name="source">The label to clone.</param>
        /// <returns>A detached clone of the label.</returns>
        public static ToolStripStatusLabel CloneStatusLabel(ToolStripStatusLabel source)
        {
            return new ToolStripStatusLabel
            {
                Text = source.Text,
                ForeColor = source.ForeColor,
                Font = source.Font,
                Visible = source.Visible
            };
        }

        private void PopulateWorking()
        {
            _working.Clear();
            foreach (var label in _target.Items)
            {
                _working.Add(CloneStatusLabel(label));
            }
        }

        private void PopulateGrid()
        {
            if (_grid == null) return;

            _grid.Rows.Clear();
            foreach (var label in _working)
            {
                var row = new DataGridViewRow();
                row.Cells.Add(new DataGridViewCell { Value = label.GetType().Name });
                row.Cells.Add(new DataGridViewCell { Value = label.Text });
                row.DataBoundItem = label;
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
            var item = index >= 0 && index < _working.Count ? _working[index] : null;
            _selectedItem = item;
            _propertyGrid.BindObject(item);
        }

        private void OnPropertyValueChanged(object? sender, PropertyValueChangedEventArgs e)
        {
            if (_selectedItem == null) return;

            if (e.PropertyName == nameof(ToolStripStatusLabel.Text))
            {
                RefreshRowFor(_selectedItem);
            }
        }

        private void RefreshRowFor(ToolStripStatusLabel label)
        {
            if (_grid == null) return;

            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                if (!ReferenceEquals(_grid.Rows[i].DataBoundItem, label)) continue;

                _grid.Rows[i].Cells[1].Value = label.Text;
                _grid.Invalidate();
                return;
            }
        }

        private void AddItem(ToolStripStatusLabel label)
        {
            if (_grid == null) return;

            _working.Add(label);

            var row = new DataGridViewRow();
            row.Cells.Add(new DataGridViewCell { Value = label.GetType().Name });
            row.Cells.Add(new DataGridViewCell { Value = label.Text });
            row.DataBoundItem = label;
            _grid.Rows.Add(row);

            _grid.SelectedRowIndex = _working.Count - 1;
            BindSelected();
        }

        private void DeleteSelectedItem()
        {
            if (_grid == null) return;

            int index = _grid.SelectedRowIndex;
            if (index < 0 || index >= _working.Count) return;

            _working.RemoveAt(index);

            if (index < _grid.Rows.Count)
            {
                _grid.Rows.Remove(_grid.Rows[index]);
            }

            if (_working.Count > 0)
            {
                _grid.SelectedRowIndex = index < _working.Count ? index : _working.Count - 1;
            }
            else
            {
                _selectedItem = null;
            }
            BindSelected();
        }

        private void MoveSelected(int delta)
        {
            if (_grid == null) return;

            int index = _grid.SelectedRowIndex;
            int target = index + delta;
            if (index < 0 || index >= _working.Count) return;
            if (target < 0 || target >= _working.Count) return;

            var item = _working[index];
            _working.RemoveAt(index);
            _working.Insert(target, item);

            PopulateGrid();
            _grid.SelectedRowIndex = target;
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
        /// Pushes the working copy of the labels onto the target <see cref="StatusStrip"/>,
        /// replacing its existing label set with the edited clones.
        /// </summary>
        private void ApplyChanges()
        {
            _target.Items.Clear();
            foreach (var label in _working)
            {
                _target.Items.Add(label);
            }

            _target.Invalidate();
            _target.PerformLayout();
        }

        /// <summary>
        /// Handles keyboard shortcuts. Enter confirms (OK) and Escape cancels the dialog.
        /// </summary>
        /// <param name="e">The key event data.</param>
        protected override void OnKeyDown(KeyEventArgs e)
        {
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
