using System.Linq;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Designer.PropertyGrid;
using Xunit;

namespace OldSchoolForms.Ui.Designer.Tests;

/// <summary>
/// Verifies that the <see cref="DataGridViewColumnsEditor"/> targets only the
/// <see cref="DataGridView.Columns"/> property and that the <see cref="PropertyService"/>
/// keeps that read-only collection property in the grid with the editor attached.
/// </summary>
public class DataGridViewColumnsEditorTests
{
    [Fact]
    public void DataGridViewColumnsEditor_MatchesColumnsProperty_Only()
    {
        var editor = new DataGridViewColumnsEditor();
        Assert.Equal("…", editor.ButtonText);

        var columns = typeof(DataGridView).GetProperty(nameof(DataGridView.Columns));
        var rows = typeof(DataGridView).GetProperty(nameof(DataGridView.Rows));

        Assert.True(editor.Handles(columns!));
        Assert.False(editor.Handles(rows!));
    }

    [Fact]
    public void PropertyService_KeepsColumnsProperty_WithEditor()
    {
        var service = new PropertyService();
        var grid = new DataGridView();

        var descriptor = service.GetProperties(grid).FirstOrDefault(p => p.Name == nameof(DataGridView.Columns));

        Assert.NotNull(descriptor);
        Assert.True(descriptor!.IsReadOnly);
        Assert.NotNull(descriptor.Editor);
    }
}
