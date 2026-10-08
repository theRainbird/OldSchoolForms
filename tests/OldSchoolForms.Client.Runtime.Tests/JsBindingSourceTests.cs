using System.Data;
using Jint;
using Jint.Native;
using OldSchoolForms.Server.Runtime;
using OldSchoolForms.Shared.Scripting;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Runtime.Facade;
using OldSchoolForms.Ui.Runtime.Remote;
using OldSchoolForms.Ui.Runtime.Scripting;
using OldSchoolForms.Ui.Runtime.Sources;
using Xunit;

namespace OldSchoolForms.Client.Runtime.Tests;

/// <summary>
/// Tests for the <see cref="JsBindingSource"/> facade over a remotely loaded
/// <see cref="JsDataTable"/>: live filtering, sorting, currency navigation,
/// two-way control binding and grid binding with write-back.
/// </summary>
public sealed class JsBindingSourceTests : IDisposable
{
    private readonly string _serverRoot;
    private readonly string _clientRoot;
    private readonly ServerRuntime _server;
    private readonly RemoteGatewayClient _gateway;
    private readonly SharedCodeCache _sharedCache;
    private readonly JsEngineHost _engine;
    private readonly int _port;

    public JsBindingSourceTests()
    {
        _port = 26000 + Random.Shared.Next(1000);

        _serverRoot = Path.Combine(Path.GetTempPath(), "osf-bs-server", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_serverRoot);
        WriteServerFile("server.json", $$"""
        {
          "name": "BsTestServer",
          "version": "1.0.0",
          "host": "127.0.0.1",
          "port": {{_port}},
          "encryption": false,
          "services": [
            { "id": "customers", "script": "services/customers/index.js", "lifetime": "singleton" }
          ],
          "shared": { "dir": "shared", "version": "1.0.0" }
        }
        """);
        WriteServerFile("services/customers/index.js", """
        export default class CustomersService {
          constructor(ctx) { this.ctx = ctx; }
          list() {
            return this.ctx.data.tableFromRows('Customers', [
              { id: 1, name: 'Ada', age: 36 },
              { id: 2, name: 'Bob', age: 17 },
              { id: 3, name: 'Amy', age: 22 }
            ]);
          }
        }
        """);
        WriteServerFile("shared/.keep", "// empty shared dir\n");

        _server = new ServerRuntime(_serverRoot, devMode: false);
        _server.Start();

        _gateway = new RemoteGatewayClient("127.0.0.1", _port, encryption: false);
        _gateway.Connect();

        _sharedCache = new SharedCodeCache("BsTestApp-" + Guid.NewGuid().ToString("n"));
        _sharedCache.Sync(_gateway);

        _clientRoot = Path.Combine(Path.GetTempPath(), "osf-bs-client", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_clientRoot);
        var appSource = new FileSystemAppSource(_clientRoot);
        var loader = new CompositeModuleLoader(new AppSourceModuleLoader(appSource), _sharedCache);
        _engine = new JsEngineHost(appSource, devMode: false, moduleLoader: loader);
    }

    private JsDataTable LoadTable()
    {
        var remote = new RemoteServices(_engine, _gateway);
        var result = remote.Invoke("customers", "list", JsValue.Undefined);
        return (JsDataTable)result.ToObject()!;
    }

    [Fact]
    public void BindingSource_OverRemoteTable_CountsAllRows()
    {
        var table = LoadTable();
        using var bs = new JsBindingSource(_engine, table);

        Assert.Equal(3, bs.Count);
        Assert.True(bs.SupportsFiltering);
    }

    [Fact]
    public void BindingSource_Filter_ReducesCount()
    {
        var table = LoadTable();
        using var bs = new JsBindingSource(_engine, table);

        bs.Filter = "age >= 18";

        Assert.Equal(2, bs.Count);
    }

    [Fact]
    public void BindingSource_Sort_ReordersView()
    {
        var table = LoadTable();
        using var bs = new JsBindingSource(_engine, table);

        bs.Sort = "name ASC";

        Assert.Equal("Ada", bs.GetRow(0).Get("name"));
        Assert.Equal("Amy", bs.GetRow(1).Get("name"));
    }

    [Fact]
    public void BindingSource_Navigation_UpdatesCurrent()
    {
        var table = LoadTable();
        using var bs = new JsBindingSource(_engine, table);

        bs.MoveLast();
        Assert.Equal(2, bs.Position);
        var cur = bs.Current.ToObject();
        Assert.IsType<JsDataRow>(cur);
        Assert.Equal("Amy", ((JsDataRow)cur!).Get("name"));
    }

    [Fact]
    public void BindingSource_BindTextBox_TwoWay()
    {
        var table = LoadTable();
        using var bs = new JsBindingSource(_engine, table);
        var box = new TextBox();
        var boxFacade = new JsControl(_engine, box);

        bs.Bind(boxFacade, "text", "name");
        Assert.Equal("Ada", box.Text);

        box.Text = "Ada Lovelace";
        Assert.Equal("Ada Lovelace", table.Table.Rows[0]["name"]);
        Assert.Equal(DataRowState.Modified, table.Table.Rows[0].RowState);

        bs.Position = 1;
        Assert.Equal("Bob", box.Text);
    }

    [Fact]
    public void BindingSource_BindGrid_LiveAndWritable()
    {
        var table = LoadTable();
        using var bs = new JsBindingSource(_engine, table);
        var grid = new DataGridView();
        grid.Columns.Add(new DataGridViewColumn { DataPropertyName = "name" });
        grid.Columns.Add(new DataGridViewColumn { DataPropertyName = "age" });
        var gridFacade = new JsControl(_engine, grid);

        gridFacade.BindSource(bs);
        Assert.Equal(3, grid.Rows.Count);
        Assert.Equal("Ada", grid.Rows[0].Cells[0].Value);

        bs.Filter = "age >= 18";
        Assert.Equal(2, grid.Rows.Count);

        bs.Filter = "";
        bs.Sort = "name ASC";
        Assert.Equal("Ada", grid.Rows[0].Cells[0].Value);
    }

    [Fact]
    public void BindingSource_Find_ReturnsViewIndex()
    {
        var table = LoadTable();
        using var bs = new JsBindingSource(_engine, table);

        Assert.Equal(2, bs.Find("name", "Amy"));
        Assert.Equal(1, bs.Find("id", 2));
    }

    [Fact]
    public void BindingSource_AddNew_AppendsAndMakesCurrent()
    {
        var table = LoadTable();
        using var bs = new JsBindingSource(_engine, table);

        var row = bs.AddNew();
        Assert.Equal(4, bs.Count);
        Assert.Equal(3, bs.Position);
        Assert.Equal(DataRowState.Detached, row.Row.RowState);
    }

    [Fact]
    public void BindingSource_OnCurrentChanged_FiresCallback()
    {
        var table = LoadTable();
        using var bs = new JsBindingSource(_engine, table);

        _engine.Engine.SetValue("__bsFired", 0);
        _engine.Engine.SetValue("__bsCb", (Action<JsValue>)(v =>
        {
            var cur = Convert.ToInt32(_engine.Engine.GetValue("__bsFired").ToObject());
            _engine.Engine.SetValue("__bsFired", cur + 1);
        }));
        var cb = _engine.Engine.GetValue("__bsCb");
        bs.OnCurrentChanged(cb);

        bs.Position = 2;
        bs.Position = 0;

        Assert.Equal(2, Convert.ToInt32(_engine.Engine.GetValue("__bsFired").ToObject()));
    }

    public void Dispose()
    {
        _engine.Dispose();
        _gateway.Dispose();
        _server.Stop();
        try { Directory.Delete(_serverRoot, true); } catch { /* best effort */ }
        try { Directory.Delete(_clientRoot, true); } catch { /* best effort */ }
    }

    private void WriteServerFile(string rel, string content)
    {
        var full = Path.Combine(_serverRoot, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }
}
