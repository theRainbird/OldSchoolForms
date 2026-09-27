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
    /// A modal dialog that lets the user view, create, edit, reorder and delete the
    /// <see cref="ToolStripMenuItem"/> entries of a <see cref="MenuStrip"/>, including
    /// nested submenu commands. The items are shown on the left as an indented tree
    /// (top-level menus and their sub-items) while the full property set of the selected
    /// item is edited in a <see cref="PropertyGrid"/> on the right.
    /// Changes are applied to the control only when the dialog is confirmed with OK;
    /// cancelling discards all edits.
    /// </summary>
    public class MenuStripItemsEditorDialog : Form
    {
        /// <summary>
        /// A flattened view of one menu item within the tree, tracking its parent and depth
        /// so the grid can render indentation and the edit operations can target the correct
        /// sibling collection.
        /// </summary>
        private sealed class MenuRow
        {
            public MenuRow(ToolStripMenuItem item, ToolStripMenuItem? parent, int depth)
            {
                Item = item;
                Parent = parent;
                Depth = depth;
            }

            public ToolStripMenuItem Item { get; }
            public ToolStripMenuItem? Parent { get; }
            public int Depth { get; }
        }

        private readonly MenuStrip _target;
        private readonly List<ToolStripMenuItem> _working = new();
        private readonly List<MenuRow> _rows = new();
        private ToolStripMenuItem? _selectedItem;
        private DataGridView? _grid;
        private readonly PropertyGrid _propertyGrid;

        /// <summary>
        /// Opens the menu items editor as a separate top-level window and blocks until the
        /// user clicks OK or Cancel.
        /// </summary>
        /// <param name="menuStrip">The control whose items are edited.</param>
        /// <param name="owner">The owner form, or null.</param>
        /// <returns><see cref="DialogResult.OK"/> if confirmed, otherwise <see cref="DialogResult.Cancel"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="menuStrip"/> is null.</exception>
        public static DialogResult ShowDialog(MenuStrip menuStrip, Form? owner = null)
        {
            if (menuStrip == null) throw new ArgumentNullException(nameof(menuStrip));

            var dialog = new MenuStripItemsEditorDialog(menuStrip)
            {
                Text = LangRes.GetString("MenuStripItemsDialog_Title")
            };

            return dialog.ShowDialog(owner);
        }

        internal MenuStripItemsEditorDialog(MenuStrip menuStrip)
        {
            _target = menuStrip;

            Size = new Size(860, 520);
            FormBorderStyle = FormBorderStyle.Sizable;

            var toolbar = new ToolStrip
            {
                Dock = DockStyle.Top,
                Height = 30
            };

            var btnAddMenuItem = MakeToolButton("MenuStripItemsDialog_AddMenuItem", Icons.AddCircle16, AddTopLevel);
            var btnAddSubItem = MakeToolButton("MenuStripItemsDialog_AddSubItem", null, AddSubItem);
            var btnDelete = MakeToolButton("MenuStripItemsDialog_Delete", Icons.Trash, DeleteSelectedItem);
            var btnMoveUp = MakeToolButton("MenuStripItemsDialog_MoveUp", null, () => MoveSelected(-1));
            var btnMoveDown = MakeToolButton("MenuStripItemsDialog_MoveDown", null, () => MoveSelected(1));
            var btnOk = MakeToolButton("MenuStripItemsDialog_Ok", Icons.CheckmarkCircle16, () => CloseDialog(true));
            var btnCancel = MakeToolButton("MenuStripItemsDialog_Cancel", Icons.DismissCircle16, () => CloseDialog(false));

            toolbar.Items.Add(btnAddMenuItem);
            toolbar.Items.Add(btnAddSubItem);
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
                HeaderText = LangRes.GetString("MenuStripItemsDialog_ColType"),
                Width = 160,
                CellEditType = DataGridViewColumnEditType.None
            });
            _grid.Columns.Add(new DataGridViewColumn
            {
                Name = "Text",
                HeaderText = LangRes.GetString("MenuStripItemsDialog_ColText"),
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
            RebuildRows();
            PopulateGrid();

            if (_rows.Count > 0)
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
                _working.Add((ToolStripMenuItem)ToolStripItemsEditorDialog.CloneItem(item));
            }
        }

        /// <summary>
        /// Flattens the working tree into <see cref="_rows"/> in depth-first order, recording
        /// each item's parent and nesting depth.
        /// </summary>
        private void RebuildRows()
        {
            _rows.Clear();
            foreach (var top in _working)
            {
                Walk(top, null, 0);
            }

            void Walk(ToolStripMenuItem item, ToolStripMenuItem? parent, int depth)
            {
                _rows.Add(new MenuRow(item, parent, depth));
                foreach (var child in item.DropDownItems)
                {
                    Walk(child, item, depth + 1);
                }
            }
        }

        private void PopulateGrid()
        {
            if (_grid == null) return;

            _grid.Rows.Clear();
            foreach (var r in _rows)
            {
                var row = new DataGridViewRow();
                string typeLabel = r.Parent == null
                    ? LangRes.GetString("MenuStripItemsDialog_TypeTop")
                    : LangRes.GetString("MenuStripItemsDialog_TypeSub");
                row.Cells.Add(new DataGridViewCell { Value = typeLabel });
                row.Cells.Add(new DataGridViewCell { Value = Indent(r) });
                row.DataBoundItem = r.Item;
                _grid.Rows.Add(row);
            }
        }

        private static string Indent(MenuRow row)
            => new string(' ', row.Depth * 3) + ToolStripItemsEditorDialog.GetItemDisplayText(row.Item);

        private void OnGridSelectionChanged(object? sender, EventArgs e)
        {
            BindSelected();
        }

        private void BindSelected()
        {
            if (_grid == null) return;

            int index = _grid.SelectedRowIndex;
            var row = index >= 0 && index < _rows.Count ? _rows[index] : null;
            _selectedItem = row?.Item;
            _propertyGrid.BindObject(_selectedItem);
        }

        private void OnPropertyValueChanged(object? sender, PropertyValueChangedEventArgs e)
        {
            if (_selectedItem == null) return;

            if (e.PropertyName == nameof(ToolStripMenuItem.Text))
            {
                RefreshRowFor(_selectedItem);
            }
        }

        private void RefreshRowFor(ToolStripMenuItem item)
        {
            if (_grid == null) return;

            var menuRow = _rows.FirstOrDefault(r => ReferenceEquals(r.Item, item));
            if (menuRow == null) return;

            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                if (!ReferenceEquals(_grid.Rows[i].DataBoundItem, item)) continue;

                _grid.Rows[i].Cells[1].Value = Indent(menuRow);
                _grid.Invalidate();
                return;
            }
        }

        /// <summary>
        /// Adds a new top-level menu item to the working set and selects it.
        /// </summary>
        private void AddTopLevel()
        {
            var item = new ToolStripMenuItem(LangRes.GetString("MenuStripItemsDialog_NewMenuItem"));
            _working.Add(item);
            RebuildAndSelect(item);
        }

        /// <summary>
        /// Adds a new sub-item to the currently selected menu item's dropdown. If no item is
        /// selected, the new item is added at the top level instead.
        /// </summary>
        private void AddSubItem()
        {
            if (_selectedItem == null)
            {
                AddTopLevel();
                return;
            }

            var child = new ToolStripMenuItem(LangRes.GetString("MenuStripItemsDialog_NewSubItem"));
            _selectedItem.DropDownItems.Add(child);
            RebuildAndSelect(child);
        }

        private void RebuildAndSelect(ToolStripMenuItem item)
        {
            if (_grid == null) return;

            RebuildRows();
            PopulateGrid();

            int idx = _rows.FindIndex(r => ReferenceEquals(r.Item, item));
            if (idx >= 0)
            {
                _grid.SelectedRowIndex = idx;
            }
            BindSelected();
        }

        private void DeleteSelectedItem()
        {
            if (_grid == null) return;

            int index = _grid.SelectedRowIndex;
            if (index < 0 || index >= _rows.Count) return;

            var row = _rows[index];
            if (row.Parent == null)
            {
                _working.Remove(row.Item);
            }
            else
            {
                row.Parent.DropDownItems.Remove(row.Item);
            }

            RebuildRows();
            PopulateGrid();

            int newIdx = Math.Min(index, _rows.Count - 1);
            if (newIdx >= 0)
            {
                _grid.SelectedRowIndex = newIdx;
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
            if (index < 0 || index >= _rows.Count) return;

            var row = _rows[index];
            var siblings = row.Parent == null ? _working : row.Parent.DropDownItems;
            int pos = siblings.IndexOf(row.Item);
            int target = pos + delta;
            if (target < 0 || target >= siblings.Count) return;

            siblings.RemoveAt(pos);
            siblings.Insert(target, row.Item);

            RebuildRows();
            PopulateGrid();

            int newIdx = _rows.FindIndex(r => ReferenceEquals(r.Item, row.Item));
            if (newIdx >= 0)
            {
                _grid.SelectedRowIndex = newIdx;
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
        /// Pushes the working copy of the items onto the target <see cref="MenuStrip"/>,
        /// replacing its existing item set with the edited clones (including nested dropdowns).
        /// </summary>
        private void ApplyChanges()
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
