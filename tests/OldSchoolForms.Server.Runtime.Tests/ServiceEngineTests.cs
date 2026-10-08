using System.Data;
using OldSchoolForms.Server.Runtime.Manifest;
using OldSchoolForms.Server.Runtime.Scripting;
using Xunit;

namespace OldSchoolForms.Server.Runtime.Tests;

public class ServiceEngineTests
{
    private static JsServiceInstance CreateService(TestPackage pkg, string serviceId)
    {
        var manifest = ServerManifest.Load(pkg.Root);
        var descriptor = manifest.Services.First(s => s.Id == serviceId);
        var sharedRoot = Path.Combine(pkg.Root, "shared");
        return new JsServiceInstance(descriptor, manifest, pkg.Root, sharedRoot, false, (_, _) => { });
    }

    [Fact]
    public void ClassService_InvokeWithArgs()
    {
        using var pkg = new TestPackage();
        pkg.WriteDefaultPackage();
        using var svc = CreateService(pkg, "math");

        Assert.Equal(7, Convert.ToInt32(svc.Invoke("add", new object?[] { 3, 4 })));
        Assert.Equal("math", svc.Invoke("describe", Array.Empty<object?>()));
    }

    [Fact]
    public void ClassService_UnknownMethod_Throws()
    {
        using var pkg = new TestPackage();
        pkg.WriteDefaultPackage();
        using var svc = CreateService(pkg, "math");

        Assert.Throws<InvalidOperationException>(() => svc.Invoke("nope", Array.Empty<object?>()));
    }

    [Fact]
    public void ClassService_ImportSharedModule_Works()
    {
        using var pkg = new TestPackage();
        pkg.WriteDefaultPackage();
        using var svc = CreateService(pkg, "customers");

        Assert.Null(svc.Invoke("validate", new object?[] { "Ada" }));
        Assert.Equal("Name too short", svc.Invoke("validate", new object?[] { "A" }));
    }

    [Fact]
    public void ClassService_ReturnsDataTable_AsRealClrTable()
    {
        using var pkg = new TestPackage();
        pkg.WriteDefaultPackage();
        using var svc = CreateService(pkg, "customers");

        var result = svc.Invoke("list", Array.Empty<object?>());
        var table = Assert.IsType<DataTable>(result);
        Assert.Equal("Customers", table.TableName);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal("Ada", table.Rows[0]["name"]);
        Assert.Equal(1L, table.Rows[0]["id"]);
    }

    [Fact]
    public void ClassService_ReceivesDataTable_ChangesVisible()
    {
        using var pkg = new TestPackage();
        pkg.WriteDefaultPackage();
        using var svc = CreateService(pkg, "customers");

        var table = new DataTable("Customers");
        table.Columns.Add("id", typeof(long));
        table.Columns.Add("name", typeof(string));
        table.Rows.Add(1L, "Ada");
        table.Rows.Add(2L, "Bob");
        table.AcceptChanges();
        table.Rows[1]["name"] = "Bobby"; // Modified

        var result = svc.Invoke("countChanges", new object?[] { table });
        Assert.Equal(1, Convert.ToInt32(result));
    }

    [Fact]
    public void GetMethods_ListsClassMethods()
    {
        using var pkg = new TestPackage();
        pkg.WriteDefaultPackage();
        using var svc = CreateService(pkg, "customers");

        var methods = svc.GetMethods();
        Assert.Contains("list", methods);
        Assert.Contains("countChanges", methods);
        Assert.Contains("validate", methods);
    }
}
