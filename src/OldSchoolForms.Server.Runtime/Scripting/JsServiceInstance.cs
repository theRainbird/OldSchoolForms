using Jint;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using OldSchoolForms.Server.Runtime.Manifest;
using OldSchoolForms.Shared.Scripting;

namespace OldSchoolForms.Server.Runtime.Scripting;

/// <summary>
/// One hosted ES6 service: owns a dedicated Jint engine (fault isolation
/// between services), imports the service script, instantiates the
/// <c>export default class</c> (or uses exported functions) and marshalls
/// method calls between CLR arguments and JS values.
/// </summary>
public sealed class JsServiceInstance : IDisposable
{
    private readonly Engine _engine;
    private readonly ObjectInstance _namespace;
    private readonly ObjectInstance? _instance;
    private readonly bool _isClass;
    private readonly ServiceDescriptor _descriptor;

    /// <summary>
    /// Creates and initializes the service instance.
    /// </summary>
    /// <param name="descriptor">The service descriptor from <c>server.json</c>.</param>
    /// <param name="manifest">The server manifest.</param>
    /// <param name="packageRoot">Absolute package directory.</param>
    /// <param name="sharedRoot">Absolute shared code directory.</param>
    /// <param name="devMode">Extended timeouts and verbose errors in dev mode.</param>
    /// <param name="log">Log sink for <c>ctx.log</c> and console output.</param>
    public JsServiceInstance(
        ServiceDescriptor descriptor,
        ServerManifest manifest,
        string packageRoot,
        string sharedRoot,
        bool devMode,
        Action<string, string> log)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        _descriptor = descriptor;

        var loader = new ServerModuleLoader(packageRoot, sharedRoot);

        _engine = new Engine(options =>
        {
            options.Strict = true;
            options.TimeoutInterval(TimeSpan.FromSeconds(devMode ? 120 : 15));
            options.Modules.ModuleLoader = loader;
            options.Modules.RegisterRequire = false;

            var innerAccessor = options.Interop.MemberAccessor;
            options.Interop.MemberAccessor = (engine, target, member) =>
            {
                var direct = innerAccessor?.Invoke(engine, target, member);
                if (direct != null) return direct;

                if (!string.IsNullOrEmpty(member) && char.IsLower(member[0]))
                {
                    var pascal = char.ToUpperInvariant(member[0]) + member[1..];
                    return innerAccessor?.Invoke(engine, target, pascal);
                }

                return null;
            };
        });

        InstallConsole(log);

        var ctx = ServiceContext.Build(_engine, manifest, descriptor, log);
        _namespace = _engine.Modules.Import(descriptor.Script.Replace('\\', '/'));

        var def = _namespace.Get("default");
        if (def is Function f && f.IsConstructor())
        {
            _isClass = true;
            _instance = _engine.Construct(f, new JsValue[] { ctx });
        }
        else
        {
            _isClass = false;
            _instance = null;
            var init = _namespace.Get("init");
            if (init is Function initFn)
                _engine.Call(initFn, JsValue.Undefined, ctx);
        }
    }

    /// <summary>
    /// Invokes a service method with marshalled arguments and return value.
    /// </summary>
    /// <param name="method">The method name.</param>
    /// <param name="args">The CLR arguments (facade unwrapping already applied
    /// by the caller or plain values).</param>
    /// <returns>The CLR return value (facades unwrapped to real data objects).</returns>
    /// <exception cref="InvalidOperationException">The method does not exist or is not callable.</exception>
    public object? Invoke(string method, object?[]? args)
    {
        var target = _isClass ? _instance! : _namespace;
        var fn = target.Get(method);
        if (fn is not Function function)
            throw new InvalidOperationException($"Service '{_descriptor.Id}' has no method '{method}'.");

        var jsArgs = JsMarshal.ToJsArgs(_engine, args);
        var thisArg = _isClass ? _instance! : JsValue.Undefined;
        var result = _engine.Call(function, thisArg, jsArgs);
        return JsMarshal.ToClr(result);
    }

    /// <summary>
    /// Lists the callable method names of this service.
    /// </summary>
    /// <returns>The method names exposed by the service.</returns>
    public string[] GetMethods()
    {
        var names = new List<string>();
        if (_isClass && _instance != null)
        {
            foreach (var prop in _instance.GetOwnProperties())
            {
                if (prop.Value.Value is Function)
                    names.Add(prop.Key.ToString());
            }

            // Include prototype methods (class methods live on the prototype).
            var proto = _instance.Prototype;
            while (proto != null && !ReferenceEquals(proto, _engine.Intrinsics.Object.PrototypeObject))
            {
                foreach (var prop in proto.GetOwnProperties())
                {
                    if (prop.Value.Value is Function && !names.Contains(prop.Key.ToString()))
                        names.Add(prop.Key.ToString());
                }

                proto = proto.Prototype;
            }
        }
        else
        {
            foreach (var prop in _namespace.GetOwnProperties())
            {
                var name = prop.Key.ToString();
                if (name is "default" or "init" or "dispose") continue;
                if (prop.Value.Value is Function)
                    names.Add(name);
            }
        }

        return names.ToArray();
    }

    private void InstallConsole(Action<string, string> log)
    {
        _engine.SetValue("__jsConsole", (Action<string, JsValue[]>)((level, msgs) =>
        {
            var text = string.Join(" ", msgs.Select(m =>
            {
                try { return m.IsUndefined() ? "undefined" : m.ToString(); }
                catch { return "<unprintable>"; }
            }));
            log(level, text);
        }));

        _engine.Evaluate(
            "var console = {" +
            "  log: function (...a) { __jsConsole('log', a); }," +
            "  info: function (...a) { __jsConsole('info', a); }," +
            "  warn: function (...a) { __jsConsole('warn', a); }," +
            "  error: function (...a) { __jsConsole('error', a); }" +
            "};");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            var target = _isClass ? _instance : _namespace;
            if (target != null)
            {
                var dispose = target.Get("dispose");
                if (dispose is Function fn)
                    _engine.Call(fn, _isClass ? _instance! : JsValue.Undefined);
            }
        }
        catch
        {
            // Dispose errors are non-fatal during shutdown.
        }

        _engine.Dispose();
    }
}
