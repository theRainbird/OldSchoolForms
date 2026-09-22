using System;
using System.Linq;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Designer.Dialogs;
using Xunit;

namespace OldSchoolForms.Ui.Designer.Tests;

/// <summary>
/// Verifies the working-copy semantics of the <see cref="DataGridViewColumnsEditorDialog"/>:
/// edits to the working column list do not touch the target control until OK is confirmed,
/// the left grid exposes exactly three columns, and add/delete operations keep the working
/// list and the grid in sync.
/// </summary>
public class DataGridViewColumnsEditorDialogTests
{
    private sealed class TestableDialog : DataGridViewColumnsEditorDialog
    {
        public TestableDialog(DataGridView dataGridView) : base(dataGridView)
        {
        }

        public DataGridView Grid => _grid!;
        public System.Collections.Generic.List<DataGridViewColumn> Working => _working;
        public void TestApplyChanges() => ApplyChanges();
        public void TestAddColumn() => AddColumn();
        public void TestDeleteSelected() => DeleteSelectedColumn();
    }

    [Fact]
    public void WorkingCopy_IsIsolatedFromTarget_UntilApplyChanges()
    {
        var target = new DataGridView();
        target.Columns.Add(new DataGridViewColumn { Name = "A", HeaderText = "Alpha" });

        var dialog = new TestableDialog(target);

        dialog.Working[0].Name = "A-edited";

        Assert.Equal("A", target.Columns[0].Name);

        dialog.TestApplyChanges();

        Assert.Equal("A-edited", target.Columns[0].Name);
    }

    [Fact]
    public void ApplyChanges_ReplacesTargetColumns_WithWorkingClones()
    {
        var target = new DataGridView();
        var original0 = new DataGridViewColumn { Name = "A" };
        var original1 = new DataGridViewColumn { Name = "B" };
        target.Columns.Add(original0);
        target.Columns.Add(original1);

        var dialog = new TestableDialog(target);

        Assert.Equal(2, dialog.Working.Count);

        dialog.TestApplyChanges();

        Assert.Equal(2, target.Columns.Count);
        Assert.NotSame(original0, target.Columns[0]);
        Assert.NotSame(original1, target.Columns[1]);
        Assert.Equal("A", target.Columns[0].Name);
        Assert.Equal("B", target.Columns[1].Name);
    }

    [Fact]
    public void Grid_HasThreeColumns()
    {
        var target = new DataGridView();
        target.Columns.Add(new DataGridViewColumn { Name = "A" });

        var dialog = new TestableDialog(target);

        Assert.Equal(3, dialog.Grid.Columns.Count);
    }

    [Fact]
    public void AddColumn_AppendsToWorkingAndGrid()
    {
        var target = new DataGridView();
        target.Columns.Add(new DataGridViewColumn { Name = "A" });

        var dialog = new TestableDialog(target);

        dialog.TestAddColumn();

        Assert.Equal(2, dialog.Working.Count);
        Assert.Equal(2, dialog.Grid.Rows.Count);
    }

    [Fact]
    public void DeleteSelectedColumn_RemovesFromWorkingAndGrid()
    {
        var target = new DataGridView();
        target.Columns.Add(new DataGridViewColumn { Name = "A" });
        target.Columns.Add(new DataGridViewColumn { Name = "B" });

        var dialog = new TestableDialog(target);
        dialog.Grid.SelectedRowIndex = 1;

        dialog.TestDeleteSelected();

        Assert.Single(dialog.Working);
        Assert.Equal("A", dialog.Working[0].Name);
        Assert.Equal(1, dialog.Grid.Rows.Count);
    }

    [Fact]
    public void ShowDialog_NullDataGridView_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => DataGridViewColumnsEditorDialog.ShowDialog(null!));
    }
}
