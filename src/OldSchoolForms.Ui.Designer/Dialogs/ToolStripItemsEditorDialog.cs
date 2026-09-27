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
    /// <see cref="ToolStripItem"/> entries of a <see cref="ToolStrip"/>. The items are listed
    /// on the left in a compact two-column grid (Type, Text) while the full property set of
    /// the selected item is edited in a <see cref="PropertyGrid"/> on the right. Changes are
    /// applied to the control only when the dialog is confirmed with OK; cancelling discards
    /// all edits.
    /// </summary>
    public class ToolStripItemsEditorDialog : Form
    {
        private readonly ToolStrip _target;
        protected readonly List<ToolStripItem> _working = new();
        private ToolStripItem? _selectedItem;
        protected DataGridView? _grid;
        protected readonly PropertyGrid _propertyGrid;

        /// <summary>
        /// Opens the items editor as a separate top-level window and blocks until the user
        /// clicks OK or Cancel.
        /// </summary>
        /// <param name="toolStrip">The control whose items are edited.</param>
        /// <param name="owner">The owner form, or null.</param>
        /// <returns><see cref="DialogResult.OK"/> if confirmed, otherwise <see cref="DialogResult.Cancel"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="toolStrip"/> is null.</exception>
        public static DialogResult ShowDialog(ToolStrip toolStrip, Form? owner = null)
        {
            if (toolStrip == null) throw new ArgumentNullException(nameof(toolStrip));

            var dialog = new ToolStripItemsEditorDialog(toolStrip)
            {
                Text = LangRes.GetString("ToolStripItemsDialog_Title")
            };

            return dialog.ShowDialog(owner);
        }

        internal ToolStripItemsEditorDialog(ToolStrip toolStrip)
        {
            _target = toolStrip;

            Size = new Size(860, 520);
            FormBorderStyle = FormBorderStyle.Sizable;

            var toolbar = new ToolStrip
            {
                Dock = DockStyle.Top,
                Height = 30
            };

            var btnAddButton = MakeToolButton("ToolStripItemsDialog_AddButton", Icons.AddCircle16, () => AddItem(new ToolStripButton { Text = LangRes.GetString("ToolStripItemsDialog_NewButton") }));
            var btnAddLabel = MakeToolButton("ToolStripItemsDialog_AddLabel", null, () => AddItem(new ToolStripLabel(LangRes.GetString("ToolStripItemsDialog_NewLabel"))));
            var btnAddSeparator = MakeToolButton("ToolStripItemsDialog_AddSeparator", null, () => AddItem(new ToolStripSeparator()));
            var btnAddTextBox = MakeToolButton("ToolStripItemsDialog_AddTextBox", null, () => AddItem(new ToolStripTextBox()));

            var btnDelete = MakeToolButton("ToolStripItemsDialog_Delete", Icons.Trash, DeleteSelectedItem);
            var btnMoveUp = MakeToolButton("ToolStripItemsDialog_MoveUp", null, () => MoveSelected(-1));
            var btnMoveDown = MakeToolButton("ToolStripItemsDialog_MoveDown", null, () => MoveSelected(1));

            var btnOk = MakeToolButton("ToolStripItemsDialog_Ok", Icons.CheckmarkCircle16, () => CloseDialog(true));
            var btnCancel = MakeToolButton("ToolStripItemsDialog_Cancel", Icons.DismissCircle16, () => CloseDialog(false));

            toolbar.Items.Add(btnAddButton);
            toolbar.Items.Add(btnAddLabel);
            toolbar.Items.Add(btnAddSeparator);
            toolbar.Items.Add(btnAddTextBox);
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
                HeaderText = LangRes.GetString("ToolStripItemsDialog_ColType"),
                Width = 160,
                CellEditType = DataGridViewColumnEditType.None
            });
            _grid.Columns.Add(new DataGridViewColumn
            {
                Name = "Text",
                HeaderText = LangRes.GetString("ToolStripItemsDialog_ColText"),
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

        private void PopulateWorking()
        {
            _working.Clear();
            foreach (var item in _target.Items)
            {
                _working.Add(CloneItem(item));
            }
        }

        /// <summary>
        /// Creates a deep copy of a tool strip item, including nested dropdown items and
        /// type-specific properties.
        /// </summary>
        /// <param name="source">The item to clone.</param>
        /// <returns>A detached clone of the item.</returns>
        public static ToolStripItem CloneItem(ToolStripItem source)
        {
            var clone = (ToolStripItem)Activator.CreateInstance(source.GetType())!;
            clone.Enabled = source.Enabled;
            clone.Visible = source.Visible;
            clone.DisplayStyle = source.DisplayStyle;
            clone.ImageScaling = source.ImageScaling;
            clone.TextAlign = source.TextAlign;
            clone.Padding = source.Padding;
            clone.Image = source.Image;

            switch (source)
            {
                case ToolStripTextBox textBox:
                    var clonedTextBox = (ToolStripTextBox)clone;
                    clonedTextBox.Text = textBox.Text;
                    clonedTextBox.TextBoxWidth = textBox.TextBoxWidth;
                    clonedTextBox.UseSystemPasswordChar = textBox.UseSystemPasswordChar;
                    break;
                case ToolStripButton button:
                    var clonedButton = (ToolStripButton)clone;
                    clonedButton.CheckOnClick = button.CheckOnClick;
                    clonedButton.Checked = button.Checked;
                    foreach (var dropItem in button.DropDownItems)
                        clonedButton.DropDownItems.Add(CloneItem(dropItem));
                    break;
                case ToolStripMenuItem menuItem:
                    var clonedMenuItem = (ToolStripMenuItem)clone;
                    foreach (var dropItem in menuItem.DropDownItems)
                        clonedMenuItem.DropDownItems.Add((ToolStripMenuItem)CloneItem(dropItem));
                    break;
                case ToolStripLabel label:
                    var clonedLabel = (ToolStripLabel)clone;
                    clonedLabel.IsLink = label.IsLink;
                    clonedLabel.LinkColor = label.LinkColor;
                    clonedLabel.VisitedLinkColor = label.VisitedLinkColor;
                    break;
            }

            return clone;
        }

        /// <summary>
        /// Gets the text shown for an item in the list grid, honoring the shadowed
        /// <see cref="ToolStripTextBox.Text"/> property.
        /// </summary>
        /// <param name="item">The item to describe.</param>
        /// <returns>The display text for the list row.</returns>
        public static string GetItemDisplayText(ToolStripItem item)
            => item is ToolStripTextBox textBox ? textBox.Text : item.Text;

        private void PopulateGrid()
        {
            if (_grid == null) return;

            _grid.Rows.Clear();
            foreach (var item in _working)
            {
                var row = new DataGridViewRow();
                row.Cells.Add(new DataGridViewCell { Value = item.GetType().Name });
                row.Cells.Add(new DataGridViewCell { Value = GetItemDisplayText(item) });
                row.DataBoundItem = item;
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

            if (e.PropertyName == nameof(ToolStripItem.Text))
            {
                RefreshRowFor(_selectedItem);
            }
        }

        private void RefreshRowFor(ToolStripItem item)
        {
            if (_grid == null) return;

            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                if (!ReferenceEquals(_grid.Rows[i].DataBoundItem, item)) continue;

                _grid.Rows[i].Cells[1].Value = GetItemDisplayText(item);
                _grid.Invalidate();
                return;
            }
        }

        protected void AddItem(ToolStripItem item)
        {
            if (_grid == null) return;

            _working.Add(item);

            var row = new DataGridViewRow();
            row.Cells.Add(new DataGridViewCell { Value = item.GetType().Name });
            row.Cells.Add(new DataGridViewCell { Value = GetItemDisplayText(item) });
            row.DataBoundItem = item;
            _grid.Rows.Add(row);

            _grid.SelectedRowIndex = _working.Count - 1;
            BindSelected();
        }

        protected void DeleteSelectedItem()
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

        protected void MoveSelected(int delta)
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
        /// Pushes the working copy of the items onto the target <see cref="ToolStrip"/>,
        /// replacing its existing item set with the edited clones.
        /// </summary>
        protected void ApplyChanges()
        {
            _target.Items.Clear();
            foreach (var item in _working)
            {
                _target.Items.Add(item);
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
