using System.Reflection;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Designer.Services;
using PGRow = OldSchoolForms.Ui.Designer.PropertyGrid.PropertyGridRow;
using PG = OldSchoolForms.Ui.Designer.PropertyGrid.PropertyGrid;
using Xunit;

namespace OldSchoolForms.Ui.Designer.Tests;

/// <summary>
/// Verifies that selecting an enum value through the property grid dropdown commits the
/// chosen value to the underlying property instead of falling back to inline text editing.
/// </summary>
public class PropertyGridEnumSelectionTests
{
    private const int RowHeight = 26;
    private const int DropdownItemHeight = 22;
    private const int ScrollBarSize = 16;

    private sealed class TestablePropertyGrid : PG
    {
        public TestablePropertyGrid(SelectionService selectionService) : base(selectionService) { }

        public int FindRowIndex(string name)
        {
            var fi = typeof(PG).GetField("_rows", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var rows = (System.Collections.Generic.List<PGRow>)fi.GetValue(this)!;
            return rows.FindIndex(r => r.Name == name);
        }

        public PGRow FindRow(int index)
        {
            var fi = typeof(PG).GetField("_rows", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var rows = (System.Collections.Generic.List<PGRow>)fi.GetValue(this)!;
            return rows[index];
        }

        public bool IsDropdownOpen
        {
            get
            {
                FieldInfo fi = typeof(PG).GetField("_dropdownOpen", BindingFlags.NonPublic | BindingFlags.Instance)!;
                return (bool)fi.GetValue(this)!;
            }
        }

        public void SimulateClick(int x, int y)
            => OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, x, y, 0f));
    }

    [Fact]
    public void PropertyGrid_EnumDropdown_ClickArrow_OpensDropdown()
    {
        var selection = new SelectionService();
        var grid = new TestablePropertyGrid(selection) { Width = 276, Height = 200 };
        var button = new Button { Location = new Point(10, 10), Size = new Size(100, 30) };
        button.Dock = DockStyle.None;
        selection.Select(new DesignItem(button));

        int dockRow = grid.FindRowIndex("Dock");
        Assert.True(dockRow >= 0);

        int rowY = grid.GetRowY(dockRow);

        // Click the dropdown arrow at the right edge of the row content, just left of the scrollbar.
        grid.SimulateClick(grid.Width - ScrollBarSize - 6, rowY + 10);

        Assert.True(grid.IsDropdownOpen);
    }

    [Fact]
    public void PropertyGrid_EnumDropdown_ClickItem_CommitsValue()
    {
        var selection = new SelectionService();
        var grid = new TestablePropertyGrid(selection) { Width = 276, Height = 200 };
        var button = new Button { Location = new Point(10, 10), Size = new Size(100, 30) };
        button.Dock = DockStyle.None;
        selection.Select(new DesignItem(button));

        int dockRow = grid.FindRowIndex("Dock");
        Assert.True(dockRow >= 0);

        int rowY = grid.GetRowY(dockRow);
        int valueX = 140;

        // Open the dropdown via the arrow.
        grid.SimulateClick(grid.Width - ScrollBarSize - 6, rowY + 10);
        Assert.True(grid.IsDropdownOpen);

        // Select the item at index 1 ("Top" in the custom DockStyle declaration order).
        grid.SimulateClick(valueX + 20, rowY + RowHeight + 1 * DropdownItemHeight);

        Assert.False(grid.IsDropdownOpen);
        Assert.Equal(DockStyle.Top, button.Dock);
    }

    [Fact]
    public void PropertyGrid_EnumDropdown_ClickText_OpensDropdown_NotEditing()
    {
        var selection = new SelectionService();
        var grid = new TestablePropertyGrid(selection) { Width = 276, Height = 200 };
        var button = new Button { Location = new Point(10, 10), Size = new Size(100, 30) };
        button.Dock = DockStyle.None;
        selection.Select(new DesignItem(button));

        int dockRow = grid.FindRowIndex("Dock");
        Assert.True(dockRow >= 0);

        int rowY = grid.GetRowY(dockRow);
        int valueX = 140;

        // Click in the text portion of the enum value (left side of the value column),
        // away from the dropdown arrow. Free-text editing must not start for enum values.
        grid.SimulateClick(valueX + 20, rowY + 10);

        Assert.True(grid.IsDropdownOpen);
        Assert.False(grid.FindRow(dockRow).IsEditing);
    }
}
