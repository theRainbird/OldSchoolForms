using System.Collections.Generic;
using System.Reflection;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Designer.PropertyGrid;
using Xunit;

namespace OldSchoolForms.Ui.Designer.Tests;

/// <summary>
/// Verifies that <see cref="PropertyGridRow.CommitEdit"/> correctly parses and assigns the
/// complex property types used by <see cref="DataGridViewColumn"/>: <see cref="int"/>,
/// <see cref="System.Type"/>, <see cref="List{T}"/> of object, and boxed scalar objects.
/// </summary>
public class PropertyGridRowComplexTypeTests
{
    private static void SetEditBuffer(PropertyGridRow row, string value)
    {
        typeof(PropertyGridRow)
            .GetField("_editBuffer", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(row, value);
    }

    [Fact]
    public void CommitEdit_ParsesIntWidth()
    {
        var column = new DataGridViewColumn { Width = 100 };
        var property = typeof(DataGridViewColumn).GetProperty(nameof(DataGridViewColumn.Width))!;
        var descriptor = new PropertyDescriptor(property, column);
        var row = new PropertyGridRow(descriptor, 140);

        row.StartEdit();
        SetEditBuffer(row, "250");
        row.CommitEdit();

        Assert.Equal(250, column.Width);
    }

    [Fact]
    public void CommitEdit_ParsesTypeValueType()
    {
        var column = new DataGridViewColumn();
        var property = typeof(DataGridViewColumn).GetProperty(nameof(DataGridViewColumn.ValueType))!;
        var descriptor = new PropertyDescriptor(property, column);
        var row = new PropertyGridRow(descriptor, 140);

        row.StartEdit();
        SetEditBuffer(row, "int");
        row.CommitEdit();

        Assert.Equal(typeof(int), column.ValueType);
    }

    [Fact]
    public void CommitEdit_ParsesItemsList()
    {
        var column = new DataGridViewColumn();
        var property = typeof(DataGridViewColumn).GetProperty(nameof(DataGridViewColumn.Items))!;
        var descriptor = new PropertyDescriptor(property, column);
        var row = new PropertyGridRow(descriptor, 140);

        row.StartEdit();
        SetEditBuffer(row, "A, B, C");
        row.CommitEdit();

        Assert.NotNull(column.Items);
        Assert.Equal(new List<object> { "A", "B", "C" }, column.Items);
    }

    [Fact]
    public void CommitEdit_ParsesTrueValueAsBool()
    {
        var column = new DataGridViewColumn();
        var property = typeof(DataGridViewColumn).GetProperty(nameof(DataGridViewColumn.TrueValue))!;
        var descriptor = new PropertyDescriptor(property, column);
        var row = new PropertyGridRow(descriptor, 140);

        row.StartEdit();
        SetEditBuffer(row, "true");
        row.CommitEdit();

        Assert.IsType<bool>(column.TrueValue);
        Assert.Equal(true, column.TrueValue);
    }

    [Fact]
    public void CommitEdit_ParsesTrueValueAsInt()
    {
        var column = new DataGridViewColumn();
        var property = typeof(DataGridViewColumn).GetProperty(nameof(DataGridViewColumn.TrueValue))!;
        var descriptor = new PropertyDescriptor(property, column);
        var row = new PropertyGridRow(descriptor, 140);

        row.StartEdit();
        SetEditBuffer(row, "42");
        row.CommitEdit();

        Assert.IsType<int>(column.TrueValue);
        Assert.Equal(42, column.TrueValue);
    }
}
