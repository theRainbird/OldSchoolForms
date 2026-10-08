using System.Collections.Concurrent;
using Jint.Native;
using Jint.Native.Object;
using OldSchoolForms.Shared.Contracts;
using OldSchoolForms.Shared.Scripting;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Remote;

/// <summary>
/// Remote services facade for JavaScript (<c>ctx.remote</c>). Calls business
/// services on the server through the CoreRemoting gateway. Synchronous calls
/// block the calling thread; asynchronous calls return a native JS
/// <c>Promise</c> that is resolved on the UI thread via the frame pump
/// (<see cref="Pump"/>), so callbacks never touch worker threads.
/// </summary>
public sealed class RemoteServices : IDisposable
{
    private readonly JsEngineHost _engine;
    private readonly RemoteGatewayClient _gateway;
    private readonly ConcurrentQueue<Action> _continuations = new();

    /// <summary>
    /// Creates the remote facade.
    /// </summary>
    /// <param name="engine">The JS engine host.</param>
    /// <param name="gateway">The connected gateway client.</param>
    public RemoteServices(JsEngineHost engine, RemoteGatewayClient gateway)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(gateway);
        _engine = engine;
        _gateway = gateway;
        InstallDeferredHelper();
    }

    /// <summary>The underlying gateway client.</summary>
    public RemoteGatewayClient Gateway => _gateway;

    /// <summary>
    /// Invokes a server service method synchronously. Blocks until the server
    /// responds. Prefer <see cref="InvokeAsync"/> for UI code.
    /// </summary>
    /// <param name="serviceId">The service ID (e.g. <c>customers</c>).</param>
    /// <param name="method">The method name.</param>
    /// <param name="args">The arguments as a JS array (or <c>undefined</c> for none).</param>
    /// <returns>The return value marshalled to JS (DataTable arrives as a facade).</returns>
    /// <exception cref="Exception">A JS-visible error when the call fails.</exception>
    public JsValue Invoke(string serviceId, string method, JsValue? args = null)
    {
        var clrArgs = ToClrArgs(args);
        var result = _gateway.Invoke(serviceId, method, clrArgs);
        if (!result.Success)
            throw new InvalidOperationException(result.Error ?? "Remote call failed.");
        return JsMarshal.ToJs(_engine.Engine, result.ReturnValue);
    }

    /// <summary>
    /// Invokes a server service method asynchronously and returns a JS
    /// <c>Promise</c>. The result is delivered on the UI thread when
    /// <see cref="Pump"/> runs (once per frame).
    /// </summary>
    /// <param name="serviceId">The service ID.</param>
    /// <param name="method">The method name.</param>
    /// <param name="args">The arguments as a JS array (or <c>undefined</c> for none).</param>
    /// <returns>A JS <c>Promise</c> resolving to the marshalled return value.</returns>
    public JsValue InvokeAsync(string serviceId, string method, JsValue? args = null)
    {
        var deferred = CreateDeferred();
        var clrArgs = ToClrArgs(args);

        Task.Run(async () =>
        {
            RemoteCallResult result;
            try
            {
                result = await _gateway.InvokeAsync(serviceId, method, clrArgs).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                result = RemoteCallResult.Fail("invocation_error", ex.Message);
            }

            _continuations.Enqueue(() =>
            {
                if (result.Success)
                    deferred.Resolve(JsMarshal.ToJs(_engine.Engine, result.ReturnValue));
                else
                    deferred.Reject(CreateJsError(result.Error ?? "Remote call failed.", result.ErrorCode));
            });
        });

        return deferred.Promise;
    }

    /// <summary>
    /// Lists the services available on the server.
    /// </summary>
    /// <returns>A JS array of <c>{ id, description, methods, lifetime }</c> objects.</returns>
    public JsValue GetServices()
    {
        var infos = _gateway.DescribeServices();
        var arr = (Jint.Native.Array.ArrayInstance)_engine.Engine.Intrinsics.Array.Construct(new JsValue[0]);
        foreach (var info in infos)
        {
            var obj = _engine.NewObject();
            obj.Set("id", JsValue.FromObject(_engine.Engine, info.Id));
            obj.Set("description", JsValue.FromObject(_engine.Engine, info.Description));
            obj.Set("lifetime", JsValue.FromObject(_engine.Engine, info.Lifetime));
            var methods = (Jint.Native.Array.ArrayInstance)_engine.Engine.Intrinsics.Array.Construct(new JsValue[0]);
            foreach (var m in info.Methods)
                methods.Push(JsValue.FromObject(_engine.Engine, m));
            obj.Set("methods", methods);
            arr.Push(obj);
        }

        return arr;
    }

    /// <summary>
    /// Checks whether the client currently has a live session with the server.
    /// </summary>
    /// <returns><see langword="true"/> when connected.</returns>
    public bool IsConnected() => _gateway.IsConnected;

    /// <summary>
    /// Drains pending remote continuations. Call once per UI frame
    /// (wired to <c>Platform.OnFrame</c>).
    /// </summary>
    public void Pump()
    {
        var hadWork = false;
        while (_continuations.TryDequeue(out var action))
        {
            hadWork = true;
            try
            {
                action();
            }
            catch (Exception ex)
            {
                _engine.ReportError("remote continuation", ex);
            }
        }

        // Jint drains the promise microtask queue only at the end of a top-level
        // Evaluate, so flush settled promises with a trivial script.
        if (hadWork)
        {
            try
            {
                _engine.Engine.Evaluate("void 0");
            }
            catch (Exception ex)
            {
                _engine.ReportError("remote promise flush", ex);
            }
        }
    }

    private static object?[] ToClrArgs(JsValue? args)
    {
        if (args is null || args == JsValue.Undefined || args == JsValue.Null)
            return Array.Empty<object?>();

        if (args is Jint.Native.Array.ArrayInstance)
            return JsMarshal.ToClr(args) as object?[] ?? Array.Empty<object?>();

        return new[] { JsMarshal.ToClr(args) };
    }

    private void InstallDeferredHelper()
    {
        // JS-side deferred: captures the native resolve/reject functions of a
        // Promise so the CLR side can settle it later on the UI thread.
        _engine.Engine.Evaluate(
            "globalThis.__remoteDeferred = function () {" +
            "  var d = {};" +
            "  var p = new Promise(function (res, rej) { d.resolve = res; d.reject = rej; });" +
            "  d.promise = p;" +
            "  return d;" +
            "};");
    }

    private (JsValue Promise, Action<JsValue> Resolve, Action<JsValue> Reject) CreateDeferred()
    {
        var engine = _engine.Engine;
        var helper = engine.GetValue("__remoteDeferred");
        var d = engine.Call(helper, JsValue.Undefined, new JsValue[0]);
        var obj = (ObjectInstance)d;
        var promise = obj.Get("promise");
        var resolveFn = obj.Get("resolve");
        var rejectFn = obj.Get("reject");

        return (
            promise,
            v => engine.Call(resolveFn, JsValue.Undefined, new JsValue[] { v ?? JsValue.Undefined }),
            v => engine.Call(rejectFn, JsValue.Undefined, new JsValue[] { v ?? JsValue.Undefined }));
    }

    private JsValue CreateJsError(string message, string? code)
    {
        var error = _engine.Engine.Intrinsics.Error.Construct(new JsValue[] { JsValue.FromObject(_engine.Engine, message) }, JsValue.Undefined);
        if (!string.IsNullOrEmpty(code))
            ((ObjectInstance)error).Set("code", JsValue.FromObject(_engine.Engine, code));
        return error;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _continuations.Clear();
    }
}
