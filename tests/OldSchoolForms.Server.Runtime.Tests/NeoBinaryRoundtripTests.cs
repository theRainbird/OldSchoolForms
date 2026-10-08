using System.Data;
using CoreRemoting.Serialization.NeoBinary;
using Xunit;

namespace OldSchoolForms.Server.Runtime.Tests;

/// <summary>
/// Verifies that the NeoBinary serializer (default config = DiffGram path)
/// preserves full DataTable/DataSet fidelity across the wire, which is the
/// foundation of the optimistic-concurrency data exchange.
/// </summary>
public class NeoBinaryRoundtripTests
{
    private static readonly NeoBinarySerializerAdapter Serializer = new(new NeoBinarySerializerConfig());

    [Fact]
    public void DataTable_ModifiedAndDeletedRows_Roundtrip()
    {
        var table = new DataTable("Customers");
        table.Columns.Add("id", typeof(long));
        table.Columns.Add("name", typeof(string));
        table.PrimaryKey = new[] { table.Columns["id"]! };
        table.Rows.Add(1L, "Ada");
        table.Rows.Add(2L, "Bob");
        table.Rows.Add(3L, "Cy");
        table.AcceptChanges();

        table.Rows[0]["name"] = "Ada L.";
        table.Rows[1].Delete();

        var bytes = Serializer.Serialize(table);
        var restored = (DataTable)Serializer.Deserialize(typeof(DataTable), bytes)!;

        Assert.Equal("Customers", restored.TableName);
        Assert.Single(restored.PrimaryKey);
        Assert.Equal(3, restored.Rows.Count); // Rows includes the deleted row; DefaultView excludes it
        Assert.Equal(2, restored.DefaultView.Count);
        Assert.Equal(DataRowState.Modified, restored.Rows[0].RowState);
        Assert.Equal("Ada L.", restored.Rows[0]["name"]);
        Assert.Equal("Ada", restored.Rows[0]["name", DataRowVersion.Original]);
        Assert.Equal(DataRowState.Deleted, restored.Rows[1].RowState);

        var deleted = restored.GetChanges(DataRowState.Deleted);
        Assert.NotNull(deleted);
        Assert.Single(deleted.Rows);
        Assert.Equal(2L, deleted.Rows[0]["id", DataRowVersion.Original]);
    }

    [Fact]
    public void DataSet_WithRelation_Roundtrip()
    {
        var set = new DataSet("Shop");
        var customers = new DataTable("Customers");
        customers.Columns.Add("id", typeof(long));
        customers.Columns.Add("name", typeof(string));
        customers.PrimaryKey = new[] { customers.Columns["id"]! };
        customers.Rows.Add(1L, "Ada");

        var orders = new DataTable("Orders");
        orders.Columns.Add("id", typeof(long));
        orders.Columns.Add("customerId", typeof(long));
        orders.PrimaryKey = new[] { orders.Columns["id"]! };
        orders.Rows.Add(10L, 1L);

        set.Tables.Add(customers);
        set.Tables.Add(orders);
        set.Relations.Add("CustomerOrders", customers.Columns["id"]!, orders.Columns["customerId"]!);

        var bytes = Serializer.Serialize(set);
        var restored = (DataSet)Serializer.Deserialize(typeof(DataSet), bytes)!;

        Assert.Equal(2, restored.Tables.Count);
        Assert.Single(restored.Relations);
        Assert.Single(restored.Tables["Customers"]!.Rows[0].GetChildRows("CustomerOrders"));
    }

    [Fact]
    public void ObjectArrayPayload_Roundtrip()
    {
        var payload = new object?[] { 42, "hello", true, null, 3.5 };
        var bytes = Serializer.Serialize(payload);
        var restored = (object?[])Serializer.Deserialize(typeof(object?[]), bytes)!;

        Assert.Equal(42, Convert.ToInt32(restored[0]));
        Assert.Equal("hello", restored[1]);
        Assert.Equal(true, restored[2]);
        Assert.Null(restored[3]);
        Assert.Equal(3.5, Convert.ToDouble(restored[4]));
    }
}
