using System.Data;
using CoreRemoting;
using CoreRemoting.Serialization.NeoBinary;
using OldSchoolForms.Shared.Contracts;
using Xunit;

namespace OldSchoolForms.Server.Runtime.Tests;

/// <summary>
/// End-to-end integration: real <see cref="ServerRuntime"/> over a real
/// CoreRemoting TCP connection with the NeoBinary serializer.
/// </summary>
public class GatewayIntegrationTests : IDisposable
{
    private readonly TestPackage _pkg = new();
    private readonly ServerRuntime _runtime;
    private readonly RemotingClient _client;
    private readonly IServiceGateway _gateway;

    public GatewayIntegrationTests()
    {
        _pkg.WriteDefaultPackage();
        var port = 24000 + Random.Shared.Next(1000);
        _runtime = new ServerRuntime(_pkg.Root, devMode: false, hostOverride: "127.0.0.1", portOverride: port);
        _runtime.Start();

        var config = new ClientConfig
        {
            ServerHostName = "127.0.0.1",
            ServerPort = port,
            MessageEncryption = false,
            Serializer = new NeoBinarySerializerAdapter(new NeoBinarySerializerConfig())
        };
        _client = new RemotingClient(config);
        _client.Connect();
        _gateway = _client.CreateProxy<IServiceGateway>();
    }

    [Fact]
    public void Invoke_PrimitiveCall_Works()
    {
        var result = _gateway.Invoke("math", "add", new object?[] { 20, 22 });
        Assert.True(result.Success);
        Assert.Equal(42, Convert.ToInt32(result.ReturnValue));
    }

    [Fact]
    public void Invoke_UnknownService_FailsGracefully()
    {
        var result = _gateway.Invoke("nope", "x", Array.Empty<object?>());
        Assert.False(result.Success);
        Assert.Equal("unknown_service", result.ErrorCode);
    }

    [Fact]
    public void Invoke_UnknownMethod_FailsGracefully()
    {
        var result = _gateway.Invoke("math", "nope", Array.Empty<object?>());
        Assert.False(result.Success);
        Assert.Equal("unknown_method", result.ErrorCode);
    }

    [Fact]
    public void Invoke_ReturnsDataTable_WithSchema()
    {
        var result = _gateway.Invoke("customers", "list", Array.Empty<object?>());
        Assert.True(result.Success);
        var table = Assert.IsType<DataTable>(result.ReturnValue);
        Assert.Equal("Customers", table.TableName);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal("Bob", table.Rows[1]["name"]);
    }

    [Fact]
    public void Invoke_SendsModifiedDataTable_ChangesSurviveWire()
    {
        var table = new DataTable("Customers");
        table.Columns.Add("id", typeof(long));
        table.Columns.Add("name", typeof(string));
        table.Rows.Add(1L, "Ada");
        table.Rows.Add(2L, "Bob");
        table.AcceptChanges();
        table.Rows[0]["name"] = "Changed";
        table.Rows[1].Delete();

        var result = _gateway.Invoke("customers", "countChanges", new object?[] { table });
        Assert.True(result.Success, result.Error);
        Assert.Equal(2, Convert.ToInt32(result.ReturnValue));
    }

    [Fact]
    public void InvokeAsync_Works()
    {
        var result = _gateway.InvokeAsync("math", "add", new object?[] { 1, 2 }).GetAwaiter().GetResult();
        Assert.True(result.Success);
        Assert.Equal(3, Convert.ToInt32(result.ReturnValue));
    }

    [Fact]
    public void DescribeServices_ListsAll()
    {
        var infos = _gateway.DescribeServices();
        Assert.Equal(3, infos.Length);
        var customers = infos.Single(i => i.Id == "customers");
        Assert.Equal("Customer services", customers.Description);
        Assert.Contains("list", customers.Methods);
        Assert.Equal("singlecall", infos.Single(i => i.Id == "counter").Lifetime);
    }

    [Fact]
    public void SharedCode_ManifestAndDownload_Work()
    {
        var manifest = _gateway.GetSharedCodeManifest();
        Assert.Equal("TestServer", manifest.AppName);
        Assert.Single(manifest.Files);

        var file = _gateway.GetSharedModule("validation/customers.js", null);
        Assert.Contains("validateName", file.Content);
        Assert.Equal(manifest.Files[0].Sha256, file.Sha256);
    }

    [Fact]
    public void SharedCode_TraversalRejected()
    {
        Assert.ThrowsAny<Exception>(() => _gateway.GetSharedModule("../server.json", null));
    }

    public void Dispose()
    {
        try { _client.ShutdownProxy(_gateway); } catch { /* ignore */ }
        _client.Disconnect(true);
        _client.Dispose();
        _runtime.Dispose();
        _pkg.Dispose();
    }
}
