using System.Data;
using Jint;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using OldSchoolForms.Server.Runtime.Manifest;
using OldSchoolForms.Shared.Scripting;

namespace OldSchoolForms.Server.Runtime.Scripting;

/// <summary>
/// Builds the <c>ctx</c> object injected into every ES6 service. Provides
/// logging (<c>ctx.log</c>), configuration (<c>ctx.config</c>), data
/// factories (<c>ctx.data</c>) and a clock (<c>ctx.clock</c>). Services
/// never see raw CLR types — only the JS facades from
/// <see cref="OldSchoolForms.Shared"/>.
/// </summary>
public static class ServiceContext
{
    /// <summary>
    /// Creates the context object for a service.
    /// </summary>
    /// <param name="engine">The service's JS engine.</param>
    /// <param name="manifest">The server manifest (for app name/version).</param>
    /// <param name="descriptor">The service descriptor (for the service ID).</param>
    /// <param name="log">Log sink receiving <c>(level, message)</c> pairs.</param>
    /// <returns>The <c>ctx</c> object instance.</returns>
    public static ObjectInstance Build(Engine engine, ServerManifest manifest, ServiceDescriptor descriptor, Action<string, string> log)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(descriptor);

        var ctx = engine.Intrinsics.Object.Construct(new JsValue[0]);

        // ctx.log
        var logObj = engine.Intrinsics.Object.Construct(new JsValue[0]);
        logObj.Set("info", JsValue.FromObject(engine, (Action<string>)((msg) => log("info", msg))));
        logObj.Set("warn", JsValue.FromObject(engine, (Action<string>)((msg) => log("warn", msg))));
        logObj.Set("error", JsValue.FromObject(engine, (Action<string>)((msg) => log("error", msg))));
        logObj.Set("debug", JsValue.FromObject(engine, (Action<string>)((msg) => log("debug", msg))));
        ctx.Set("log", logObj);

        // ctx.config
        var configObj = engine.Intrinsics.Object.Construct(new JsValue[0]);
        configObj.Set("appName", JsValue.FromObject(engine, manifest.Name));
        configObj.Set("appVersion", JsValue.FromObject(engine, manifest.Version));
        configObj.Set("serviceId", JsValue.FromObject(engine, descriptor.Id));
        ctx.Set("config", configObj);

        // ctx.data
        var dataObj = engine.Intrinsics.Object.Construct(new JsValue[0]);
        dataObj.Set("newTable", JsValue.FromObject(engine, (Func<string, JsValue>)(name =>
            JsValue.FromObject(engine, new JsDataTable(engine, new DataTable(name))))));
        dataObj.Set("newSet", JsValue.FromObject(engine, (Func<string, JsValue>)(name =>
            JsValue.FromObject(engine, new JsDataSet(engine, new DataSet(name))))));
        dataObj.Set("tableFromRows", JsValue.FromObject(engine, (Func<string, JsValue, JsValue>)((name, rows) =>
            JsValue.FromObject(engine, new JsDataTable(engine, TableFromRows(name, rows))))));
        ctx.Set("data", dataObj);

        // ctx.clock
        var clockObj = engine.Intrinsics.Object.Construct(new JsValue[0]);
        clockObj.Set("now", JsValue.FromObject(engine, (Func<JsValue>)(() => JsValue.FromObject(engine, DateTime.Now))));
        clockObj.Set("utcNow", JsValue.FromObject(engine, (Func<JsValue>)(() => JsValue.FromObject(engine, DateTime.UtcNow))));
        clockObj.Set("nowIso", JsValue.FromObject(engine, (Func<string>)(() => DateTime.Now.ToString("O"))));
        ctx.Set("clock", clockObj);

        return ctx;
    }

    /// <summary>
    /// Builds a <see cref="DataTable"/> from a JS array of plain objects.
    /// Columns are the union of all row keys (first-seen order); types are
    /// inferred from the first non-null value per column.
    /// </summary>
    /// <param name="name">The table name.</param>
    /// <param name="rows">The JS array of row objects.</param>
    /// <returns>The constructed table.</returns>
    /// <exception cref="InvalidOperationException">The argument is not an array.</exception>
    public static DataTable TableFromRows(string name, JsValue rows)
    {
        if (rows is not ArrayInstance arr)
            throw new InvalidOperationException("tableFromRows expects an array of objects.");

        var table = new DataTable(name);
        var items = arr.ToArray();

        var columnOrder = new List<string>();
        var columnTypes = new Dictionary<string, Type>(StringComparer.Ordinal);

        foreach (var item in items)
        {
            if (item is not ObjectInstance obj) continue;
            foreach (var prop in obj.GetOwnProperties())
            {
                var key = prop.Key.ToString();
                if (!columnTypes.ContainsKey(key))
                {
                    columnOrder.Add(key);
                    columnTypes[key] = typeof(string);
                }

                var clr = JsMarshal.ToClr(prop.Value.Value);
                if (clr != null && columnTypes[key] == typeof(string) && clr is not string)
                    columnTypes[key] = InferType(clr);
            }
        }

        foreach (var col in columnOrder)
            table.Columns.Add(col, columnTypes[col]);

        foreach (var item in items)
        {
            var row = table.NewRow();
            if (item is ObjectInstance obj)
            {
                foreach (var col in columnOrder)
                {
                    var val = obj.Get(col);
                    row[col] = JsMarshal.Coerce(JsMarshal.ToClr(val), table.Columns[col]!.DataType);
                }
            }

            table.Rows.Add(row);
        }

        // Loaded data represents a clean state so that client-side edits
        // (Modified/Added/Deleted) are the only pending changes on the wire.
        table.AcceptChanges();
        return table;
    }

    private static Type InferType(object clr) => clr switch
    {
        bool => typeof(bool),
        int or long => typeof(long),
        double dbl => Math.Abs(dbl % 1) < double.Epsilon ? typeof(long) : typeof(double),
        decimal dec => dec % 1 == 0m ? typeof(long) : typeof(double),
        DateTime => typeof(DateTime),
        Guid => typeof(Guid),
        _ => typeof(string)
    };
}
