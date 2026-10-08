using Jint;
using Jint.Native;
using Jint.Native.Object;
using Xunit;
using OldSchoolForms.Server.Runtime;
using OldSchoolForms.Ui.Runtime.Remote;
using OldSchoolForms.Ui.Runtime.Scripting;
using OldSchoolForms.Ui.Runtime.Sources;

namespace OldSchoolForms.Client.Runtime.Tests;

/// <summary>
/// End-to-end tests for the client runtime's remote integration: shared code
/// download + <c>@shared/</c> module resolution, synchronous and asynchronous
/// (<c>Promise</c>) service calls through the CoreRemoting gateway.
/// </summary>
public sealed class ClientRemoteTests : IDisposable
{
    private readonly string _serverRoot;
    private readonly string _clientRoot;
    private readonly ServerRuntime _server;
    private readonly RemoteGatewayClient _gateway;
    private readonly SharedCodeCache _sharedCache;
    private readonly JsEngineHost _engine;
    private readonly RemoteServices _remote;
    private readonly int _port;

    public ClientRemoteTests()
    {
        _port = 25000 + Random.Shared.Next(1000);

        _serverRoot = Path.Combine(Path.GetTempPath(), "osf-cli-server", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_serverRoot);
        WriteServerFile("server.json", $$"""
        {
          "name": "TestServer",
          "version": "1.0.0",
          "host": "127.0.0.1",
          "port": {{_port}},
          "encryption": false,
          "services": [
            { "id": "math", "script": "services/math/index.js", "lifetime": "singleton" },
            { "id": "customers", "script": "services/customers/index.js", "lifetime": "singleton" }
          ],
          "shared": { "dir": "shared", "version": "1.0.0" }
        }
        """);
        WriteServerFile("services/math/index.js", """
        export default class MathService {
          constructor(ctx) { this.ctx = ctx; }
          add(a, b) { return a + b; }
        }
        """);
        WriteServerFile("services/customers/index.js", """
        import { validateName } from '@shared/validation/customers.js';

        export default class CustomersService {
          constructor(ctx) { this.ctx = ctx; }
          list() {
            return this.ctx.data.tableFromRows('Customers', [
              { id: 1, name: 'Ada' },
              { id: 2, name: 'Bob' }
            ]);
          }
          validate(name) { return validateName(name); }
        }
        """);
        WriteServerFile("shared/validation/customers.js", """
        export function validateName(name) {
          if (!name || String(name).trim().length < 2) return 'Name too short';
          return null;
        }
        """);

        _server = new ServerRuntime(_serverRoot, devMode: false);
        _server.Start();

        _gateway = new RemoteGatewayClient("127.0.0.1", _port, encryption: false);
        _gateway.Connect();

        _sharedCache = new SharedCodeCache("TestApp-" + Guid.NewGuid().ToString("n"));
        _sharedCache.Sync(_gateway);

        _clientRoot = Path.Combine(Path.GetTempPath(), "osf-cli-client", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_clientRoot);
        var appSource = new FileSystemAppSource(_clientRoot);
        var loader = new CompositeModuleLoader(new AppSourceModuleLoader(appSource), _sharedCache);
        _engine = new JsEngineHost(appSource, devMode: false, moduleLoader: loader);
        _remote = new RemoteServices(_engine, _gateway);
    }

    [Fact]
    public void SharedCode_SyncedFromServer_CacheHasVersion()
    {
        Assert.Equal("1.0.0", _sharedCache.Version);
        Assert.NotNull(_sharedCache.Manifest);
        Assert.Contains(_sharedCache.Manifest!.Files, f => f.Path == "validation/customers.js");
    }

    [Fact]
    public void SharedModule_ImportResolvesViaCache()
    {
        WriteClientFile("sharedmod.js", """
        import { validateName } from '@shared/validation/customers.js';
        export function check(name) { return validateName(name); }
        """);

        var ns = _engine.ImportModule("sharedmod.js");
        var fn = ns.Get("check");
        var result = _engine.CallFunction(fn, JsValue.Undefined, JsValue.FromObject(_engine.Engine, "A"));
        Assert.Equal("Name too short", result.ToString());

        var ok = _engine.CallFunction(fn, JsValue.Undefined, JsValue.FromObject(_engine.Engine, "Ada"));
        Assert.True(ok == JsValue.Null);
    }

    [Fact]
    public void RemoteSync_Invoke_ReturnsValue()
    {
        var result = _remote.Invoke("math", "add", JsValue.FromObject(_engine.Engine, new object[] { 2, 3 }));
        Assert.Equal(5d, Convert.ToDouble(result.ToObject()));
    }

    [Fact]
    public void RemoteSync_InvokeDataTable_ReturnsFacade()
    {
        var result = _remote.Invoke("customers", "list", JsValue.Undefined);
        Assert.IsType<OldSchoolForms.Shared.Scripting.JsDataTable>(result.ToObject());
        var table = (OldSchoolForms.Shared.Scripting.JsDataTable)result.ToObject()!;
        Assert.Equal("Customers", table.Name);
        Assert.Equal(2, table.RowCount);
    }

    [Fact]
    public void RemoteAsync_Invoke_PromiseResolvesOnPump()
    {
        var promise = _remote.InvokeAsync("math", "add", JsValue.FromObject(_engine.Engine, new object[] { 10, 20 }));
        Assert.True(promise.IsPromise());

        var settled = JsValue.Undefined;
        _engine.Engine.SetValue("__testResolve", (Action<JsValue>)(v => settled = v));
        var thenFn = ((ObjectInstance)promise).Get("then");
        _engine.Engine.Call(thenFn, promise, new JsValue[]
        {
            JsValue.FromObject(_engine.Engine, (Action<JsValue>)(v => settled = v))
        });

        // Pump until the continuation resolves the promise.
        for (var i = 0; i < 200 && settled == JsValue.Undefined; i++)
        {
            _remote.Pump();
            Thread.Sleep(10);
        }

        Assert.Equal(30d, Convert.ToDouble(settled.ToObject()));
    }

    [Fact]
    public void RemoteAsync_Failure_PromiseRejects()
    {
        var promise = _remote.InvokeAsync("nonexistent", "x", JsValue.Undefined);
        var rejected = false;
        JsValue? err = null;

        var thenFn = ((ObjectInstance)promise).Get("then");
        _engine.Engine.Call(thenFn, promise, new JsValue[]
        {
            JsValue.FromObject(_engine.Engine, (Action<JsValue>)(_ => { })),
            JsValue.FromObject(_engine.Engine, (Action<JsValue>)(e => { rejected = true; err = e; }))
        });

        for (var i = 0; i < 200 && !rejected; i++)
        {
            _remote.Pump();
            Thread.Sleep(10);
        }

        Assert.True(rejected);
        Assert.NotNull(err);
    }

    [Fact]
    public void RemoteServices_ListsCatalog()
    {
        var arr = _remote.GetServices();
        Assert.True(arr.IsArray());
        var list = (Jint.Native.Array.ArrayInstance)arr;
        Assert.Equal(2d, Convert.ToDouble(list.Get("length").ToObject()));
    }

    private void WriteServerFile(string rel, string content)
    {
        var full = Path.Combine(_serverRoot, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    private void WriteClientFile(string rel, string content)
    {
        var full = Path.Combine(_clientRoot, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    public void Dispose()
    {
        _remote.Dispose();
        _engine.Dispose();
        _gateway.Dispose();
        _server.Stop();
        try { Directory.Delete(_serverRoot, true); } catch { /* best effort */ }
        try { Directory.Delete(_clientRoot, true); } catch { /* best effort */ }
    }
}
