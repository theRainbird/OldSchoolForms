using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Dialogs;
using Xunit;

namespace OldSchoolForms.Ui.Tests;

/// <summary>
/// Drives the DataGridView columns editor reconcile logic (applied only on commit) headlessly
/// via a testable subclass so the target grid's column collection stays correct for
/// rename/add/remove.
/// </summary>
public class DataGridViewColumnsEditorDialogTests
{
    private sealed class TestableDialog : DataGridViewColumnsEditorDialog
    {
        public TestableDialog(DataGridView dataGridView) : base(dataGridView) { }

        public void TestApplyChanges() => ApplyChanges();

        public DataGridView Grid => _grid!;
    }

    [Fact]
    public void ApplyChanges_RenamesExistingColumn_InPlace_AndAppendsNew()
    {
        var grid = new DataGridView();
        var existing = new DataGridViewColumn { Name = "existing", HeaderText = "Existing" };
        grid.Columns.Add(existing);

        var dialog = new TestableDialog(grid);
        // The ctor already bound the existing column to row 0; rename it in place.
        dialog.Grid.Rows[0].Cells[0].Value = "existing";
        dialog.Grid.Rows[0].Cells[1].Value = "Existing Renamed";
        dialog.Grid.AddRow("Brand New", "New Header");

        dialog.TestApplyChanges();

        Assert.Equal(2, grid.Columns.Count);
        Assert.Same(existing, grid.Columns[0]);
        Assert.Equal("existing", existing.Name);
        Assert.Equal("Existing Renamed", existing.HeaderText);

        Assert.Equal("Brand New", grid.Columns[1].Name);
        Assert.Equal("New Header", grid.Columns[1].HeaderText);
    }

    [Fact]
    public void ApplyChanges_RemovesAbsentColumns()
    {
        var grid = new DataGridView();
        var keep = new DataGridViewColumn { Name = "keep", HeaderText = "Keep" };
        var drop = new DataGridViewColumn { Name = "drop", HeaderText = "Drop" };
        grid.Columns.Add(keep);
        grid.Columns.Add(drop);

        var dialog = new TestableDialog(grid);
        // The ctor bound keep to row 0 and drop to row 1; remove the drop row so it is absent.
        dialog.Grid.Rows.Remove(dialog.Grid.Rows[1]);

        dialog.TestApplyChanges();

        Assert.Single(grid.Columns);
        Assert.Same(keep, grid.Columns[0]);
        Assert.DoesNotContain(drop, grid.Columns);
    }

    [Fact]
    public void ApplyChanges_ParsesTypedProperties()
    {
        var grid = new DataGridView();
        var column = new DataGridViewColumn { Name = "c" };
        grid.Columns.Add(column);

        var dialog = new TestableDialog(grid);
        var row = dialog.Grid.Rows[0];
        row.Cells[3].Value = "250";            // Width
        row.Cells[4].Value = "True";           // ReadOnly
        row.Cells[5].Value = "int";            // ValueType
        row.Cells[12].Value = "ComboBox";      // CellEditType
        row.Cells[13].Value = "A, B, C";       // Items

        dialog.TestApplyChanges();

        Assert.Equal(250, column.Width);
        Assert.True(column.ReadOnly);
        Assert.Equal(typeof(int), column.ValueType);
        Assert.Equal(DataGridViewColumnEditType.ComboBox, column.CellEditType);
        Assert.Equal(new object[] { "A", "B", "C" }, column.Items);
    }

    [Fact]
    public void ShowDialog_NullDataGridView_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() => DataGridViewColumnsEditorDialog.ShowDialog(null!));
    }
}
