using System.ComponentModel;
using System.Data;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Data;
using Xunit;

namespace OldSchoolForms.Ui.Tests;

public class DataTableBindingTests
{
    private static DataTable CreateCustomers()
    {
        var dt = new DataTable("Customers");
        dt.Columns.Add("Id", typeof(long));
        dt.Columns.Add("Name", typeof(string));
        dt.Columns.Add("Age", typeof(long));
        dt.PrimaryKey = new[] { dt.Columns["Id"]! };
        dt.Rows.Add(1L, "Ada", 36L);
        dt.Rows.Add(2L, "Bob", 17L);
        dt.Rows.Add(3L, "Amy", 22L);
        dt.AcceptChanges();
        return dt;
    }

    #region ReflectionPropertyDescriptor (custom type descriptor)

    [Fact]
    public void Descriptor_ResolvesDataRowViewColumn()
    {
        var dt = CreateCustomers();
        var drv = dt.DefaultView[0];

        var desc = ReflectionPropertyDescriptor.GetProperty(drv, "Name");

        Assert.NotNull(desc);
        Assert.Equal("Ada", desc!.GetValue(drv));
        Assert.True(desc.CanWrite);
    }

    [Fact]
    public void Descriptor_WritesThroughToDataRow()
    {
        var dt = CreateCustomers();
        var drv = dt.DefaultView[0];

        var desc = ReflectionPropertyDescriptor.GetProperty(drv, "Name")!;
        desc.SetValue(drv, "Ada Lovelace");

        Assert.Equal("Ada Lovelace", dt.Rows[0]["Name"]);
        Assert.Equal(DataRowState.Modified, dt.Rows[0].RowState);
    }

    [Fact]
    public void Descriptor_ReturnsNull_ForUnknownColumn()
    {
        var dt = CreateCustomers();
        var drv = dt.DefaultView[0];

        Assert.Null(ReflectionPropertyDescriptor.GetProperty(drv, "Nope"));
    }

    #endregion

    #region BindingSource with DataTable / DataView

    [Fact]
    public void BindingSource_UsesDataView_ForDataTable()
    {
        var dt = CreateCustomers();
        var bs = new BindingSource { DataSource = dt };

        Assert.IsType<DataView>(bs.List);
        Assert.Equal(3, bs.Count);
        Assert.True(bs.SupportsFiltering);
    }

    [Fact]
    public void BindingSource_Filter_ReducesVisibleRows()
    {
        var dt = CreateCustomers();
        var bs = new BindingSource { DataSource = dt };

        bs.Filter = "Age >= 18";

        Assert.Equal(2, bs.Count);
        Assert.True(bs.List![0] is DataRowView);
    }

    [Fact]
    public void BindingSource_Sort_ReordersRows()
    {
        var dt = CreateCustomers();
        var bs = new BindingSource { DataSource = dt };

        bs.Sort = "Name ASC";

        Assert.True(bs.IsSorted);
        var first = (DataRowView)bs.List![0];
        Assert.Equal("Ada", first["Name"]);
    }

    [Fact]
    public void BindingSource_FilterAndSort_Combine()
    {
        var dt = CreateCustomers();
        var bs = new BindingSource { DataSource = dt };

        bs.Filter = "Age >= 18";
        bs.Sort = "Name DESC";

        Assert.Equal(2, bs.Count);
        Assert.Equal("Amy", ((DataRowView)bs.List![0])["Name"]);
    }

    [Fact]
    public void BindingSource_DataSet_WithDataMember_BindsTable()
    {
        var ds = new DataSet();
        ds.Tables.Add(CreateCustomers());

        var bs = new BindingSource { DataSource = ds, DataMember = "Customers" };

        Assert.Equal(3, bs.Count);
        Assert.True(bs.SupportsFiltering);
    }

    [Fact]
    public void BindingSource_Filter_RaisesReset()
    {
        var dt = CreateCustomers();
        var bs = new BindingSource { DataSource = dt };
        var resets = 0;
        bs.ListChanged += (_, e) => { if (e.ListChangedType == ListChangedType.Reset) resets++; };

        bs.Filter = "Age >= 18";

        Assert.True(resets >= 1);
    }

    [Fact]
    public void BindingSource_NavigatesWithMoveNextAndLast()
    {
        var dt = CreateCustomers();
        var bs = new BindingSource { DataSource = dt };

        Assert.Equal(0, bs.Position);
        bs.MoveNext();
        Assert.Equal(1, bs.Position);
        bs.MoveLast();
        Assert.Equal(2, bs.Position);
        bs.MoveFirst();
        Assert.Equal(0, bs.Position);
    }

    [Fact]
    public void BindingSource_Find_NormalizesNumericTypes()
    {
        var dt = CreateCustomers();
        var bs = new BindingSource { DataSource = dt };

        Assert.Equal(1, bs.Find("Id", 2));
        Assert.Equal(2, bs.Find("Name", "Amy"));
        Assert.Equal(-1, bs.Find("Name", "Zoe"));
    }

    #endregion

    #region Two-way binding to controls

    [Fact]
    public void TextBox_TwoWay_EditsFlowToDataRow()
    {
        var dt = CreateCustomers();
        var bs = new BindingSource { DataSource = dt };
        var box = new TextBox();
        box.DataBindings.Add("Text", bs, "Name");

        Assert.Equal("Ada", box.Text);

        box.Text = "Ada L.";

        Assert.Equal("Ada L.", dt.Rows[0]["Name"]);
        Assert.Equal(DataRowState.Modified, dt.Rows[0].RowState);
    }

    [Fact]
    public void TextBox_UpdatesOnNavigation()
    {
        var dt = CreateCustomers();
        var bs = new BindingSource { DataSource = dt };
        var box = new TextBox();
        box.DataBindings.Add("Text", bs, "Name");

        bs.Position = 2;

        Assert.Equal("Amy", box.Text);
    }

    [Fact]
    public void TextBox_NumericColumn_ConvertsString()
    {
        var dt = CreateCustomers();
        var bs = new BindingSource { DataSource = dt };
        var box = new TextBox();
        box.DataBindings.Add("Text", bs, "Age");

        box.Text = "99";

        Assert.Equal(99L, dt.Rows[0]["Age"]);
    }

    #endregion

    #region DataGridView binding + write-back

    [Fact]
    public void Grid_BoundToBindingSource_CreatesRowsFromView()
    {
        var dt = CreateCustomers();
        var bs = new BindingSource { DataSource = dt };
        var grid = new DataGridView();
        grid.Columns.Add(new DataGridViewColumn { DataPropertyName = "Name" });
        grid.Columns.Add(new DataGridViewColumn { DataPropertyName = "Age" });

        grid.DataSource = bs;

        Assert.Equal(3, grid.Rows.Count);
        Assert.Equal("Ada", grid.Rows[0].Cells[0].Value);
        Assert.Equal(36L, grid.Rows[0].Cells[1].Value);
    }

    [Fact]
    public void Grid_FilteredSource_ShowsOnlyMatchingRows()
    {
        var dt = CreateCustomers();
        var bs = new BindingSource { DataSource = dt };
        var grid = new DataGridView();
        grid.Columns.Add(new DataGridViewColumn { DataPropertyName = "Name" });
        grid.DataSource = bs;

        bs.Filter = "Age >= 18";

        Assert.Equal(2, grid.Rows.Count);
    }

    [Fact]
    public void Grid_CellEdit_WritesBackToDataRow()
    {
        var dt = CreateCustomers();
        var bs = new BindingSource { DataSource = dt };
        var grid = new DataGridView();
        grid.Columns.Add(new DataGridViewColumn { DataPropertyName = "Name" });
        grid.DataSource = bs;

        grid.NotifyCellValueChanged(0, 1, "Bobby");

        Assert.Equal("Bobby", dt.Rows[1]["Name"]);
        Assert.Equal(DataRowState.Modified, dt.Rows[1].RowState);
    }

    [Fact]
    public void Grid_CellEdit_NumericCoercion()
    {
        var dt = CreateCustomers();
        var bs = new BindingSource { DataSource = dt };
        var grid = new DataGridView();
        grid.Columns.Add(new DataGridViewColumn { DataPropertyName = "Age" });
        grid.DataSource = bs;

        grid.NotifyCellValueChanged(0, 0, "42");

        Assert.Equal(42L, dt.Rows[0]["Age"]);
    }

    #endregion
}
