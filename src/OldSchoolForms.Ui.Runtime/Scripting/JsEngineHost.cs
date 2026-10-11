using Jint;
using Jint.Native;
using Jint.Native.Object;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Runtime.Facade;
using OldSchoolForms.Ui.Runtime.Localization;
using OldSchoolForms.Ui.Runtime.Sources;

namespace OldSchoolForms.Ui.Runtime.Scripting;

/// <summary>
/// Central Jint engine instance for the JavaScript runtime. Configures the
/// engine with the <see cref="AppSourceModuleLoader"/>, provides browser-like
/// globals (<c>setTimeout</c>, <c>console</c>) and marshalls calls.
/// </summary>
public sealed class JsEngineHost : IDisposable
{
    private readonly IAppSource _source;
    private bool _disposed;

    /// <summary>
    /// Creates the JS engine for a package source.
    /// </summary>
    /// <param name="source">The package source for module imports.</param>
    /// <param name="devMode">In dev mode: longer timeouts and detailed errors.</param>
    /// <param name="moduleLoader">
    /// Optional custom module loader. When <c>null</c>, an
    /// <see cref="AppSourceModuleLoader"/> for <paramref name="source"/> is used.
    /// Pass a <see cref="CompositeModuleLoader"/> to additionally resolve
    /// <c>@shared/</c> imports from the server-provided shared code cache.
    /// </param>
    public JsEngineHost(IAppSource source, bool devMode = false, Jint.Runtime.Modules.IModuleLoader? moduleLoader = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
        DevMode = devMode;
        Timers = new TimerScheduler(this);

        Engine = new Engine(options =>
        {
            options.Strict = true;
            options.TimeoutInterval(TimeSpan.FromSeconds(devMode ? 120 : 10));
            options.Modules.ModuleLoader = moduleLoader ?? new AppSourceModuleLoader(source);
            options.Modules.RegisterRequire = false;

            // camelCase fallback: JS accesses with lowercase names while the CLR facade
            // exposes PascalCase members.
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

        JsFacadeRegistry.RegisterDefaults();
        InstallGlobals();
    }

    /// <summary>The underlying Jint engine.</summary>
    public Engine Engine { get; }

    /// <summary>The timer scheduler (pumped once per frame).</summary>
    public TimerScheduler Timers { get; }

    /// <summary><see langword="true"/> when started in development mode.</summary>
    public bool DevMode { get; }

    /// <summary>
    /// The localization service backing the JS <c>t()</c> / <c>i18n</c> globals.
    /// When <c>null</c>, translation calls return the key unchanged.
    /// </summary>
    public RuntimeI18n? I18n { get; set; }

    /// <summary>
    /// The ID of the module whose code is currently executing (set via
    /// <see cref="UseModule"/>). Timers created while this is set become owned by
    /// that module and are cleared when the module closes.
    /// </summary>
    public string? CurrentModuleId { get; private set; }

    /// <summary>
    /// Marks the current synchronous execution scope as belonging to the given
    /// module. Dispose the returned token to restore the previous scope, so nested
    /// module activations (e.g. a module opening another module) stay correct.
    /// </summary>
    /// <param name="moduleId">The module ID, or <see langword="null"/> for no owner.</param>
    /// <returns>A token that restores the previous module scope on dispose.</returns>
    public IDisposable UseModule(string? moduleId)
    {
        var previous = CurrentModuleId;
        CurrentModuleId = moduleId;
        return new ModuleScope(this, previous);
    }

    private sealed class ModuleScope : IDisposable
    {
        private JsEngineHost? _host;
        private readonly string? _previous;

        public ModuleScope(JsEngineHost host, string? previous)
        {
            _host = host;
            _previous = previous;
        }

        public void Dispose()
        {
            if (_host == null) return;
            _host.CurrentModuleId = _previous;
            _host = null;
        }
    }

    /// <summary>
    /// Errors from JS execution (context + message).
    /// </summary>
    public event Action<string, Exception>? Error;

    /// <summary>
    /// Imports an ES module from the package.
    /// </summary>
    /// <param name="specifier">The module specifier (e.g. <c>app.js</c> or <c>modules/customers/index.js</c>).</param>
    /// <returns>The module namespace object.</returns>
    public ObjectInstance ImportModule(string specifier)
    {
        var key = ModulePath.Normalize(specifier);
        return Engine.Modules.Import(key);
    }

    /// <summary>
    /// Calls a JavaScript function with the given <c>this</c> reference.
    /// </summary>
    /// <param name="function">The function value.</param>
    /// <param name="thisArg">The <c>this</c> argument (or <see cref="JsValue.Undefined"/>).</param>
    /// <param name="args">The arguments.</param>
    /// <returns>The return value.</returns>
    public JsValue CallFunction(JsValue function, JsValue thisArg, params JsValue[] args)
    {
        if (function is not Jint.Native.Function.Function)
            throw new InvalidOperationException(LangRes.GetEnglish("Runtime_ValueNotFunction"));
        return Engine.Call(function, thisArg, args);
    }

    /// <summary>
    /// Calls a function without a specific <c>this</c>.
    /// </summary>
    /// <param name="function">The function value.</param>
    /// <param name="args">The arguments.</param>
    /// <returns>The return value.</returns>
    public JsValue CallFunction(JsValue function, params JsValue[] args)
        => CallFunction(function, JsValue.Undefined, args);

    /// <summary>
    /// Calls an export of a module namespace, if present.
    /// </summary>
    /// <param name="moduleNamespace">The namespace of the module.</param>
    /// <param name="exportName">The export name (e.g. <c>init</c>).</param>
    /// <param name="args">The arguments as CLR values.</param>
    /// <returns>The return value or <see cref="JsValue.Undefined"/>.</returns>
    public JsValue CallExport(ObjectInstance moduleNamespace, string exportName, params object?[] args)
    {
        var value = moduleNamespace.Get(exportName);
        if (value is not Jint.Native.Function.Function)
            return JsValue.Undefined;

        var jsArgs = new JsValue[args.Length];
        for (var i = 0; i < args.Length; i++)
            jsArgs[i] = JsValue.FromObject(Engine, args[i]);

        try
        {
            return Engine.Call(value, JsValue.Undefined, jsArgs);
        }
        catch (Exception ex)
        {
            ReportError($"Export '{exportName}'", ex);
            return JsValue.Undefined;
        }
    }

    /// <summary>
    /// Creates an empty JS object.
    /// </summary>
    /// <returns>A new <see cref="ObjectInstance"/>.</returns>
    public ObjectInstance NewObject() => Engine.Intrinsics.Object.Construct(new JsValue[0]);

    /// <summary>
    /// Reports an error from JS execution.
    /// </summary>
    /// <param name="context">Description of the error context.</param>
    /// <param name="ex">The exception.</param>
    public void ReportError(string context, Exception ex)
    {
        Error?.Invoke(context, ex);
    }

    /// <summary>
    /// Pumps the timers (call once per frame).
    /// </summary>
    public void PumpTimers()
    {
        if (_disposed) return;
        Timers.Pump();
    }

    private void InstallGlobals()
    {
        Engine.SetValue("setTimeout", (Func<JsValue, double, int>)((cb, ms) => Timers.SetTimeout(cb, ms, CurrentModuleId)));
        Engine.SetValue("setInterval", (Func<JsValue, double, int>)((cb, ms) => Timers.SetInterval(cb, ms, CurrentModuleId)));
        Engine.SetValue("clearTimeout", (Action<int>)Timers.Clear);
        Engine.SetValue("clearInterval", (Action<int>)Timers.Clear);

        // Jint does not wrap params delegates; therefore pass a real JS array via a rest parameter
        // to a CLR function with a JsValue[] parameter.
        Engine.SetValue("__jsConsole", (Action<string, JsValue[]>)((level, msgs) => WriteConsole(level, msgs)));
        Engine.Evaluate(
            "var console = {" +
            "  log: function (...a) { __jsConsole('log', a); }," +
            "  info: function (...a) { __jsConsole('info', a); }," +
            "  warn: function (...a) { __jsConsole('warn', a); }," +
            "  error: function (...a) { __jsConsole('error', a); }" +
            "};");

        InstallI18nGlobals();
        InstallFormBase();
    }

    /// <summary>
    /// Injects the localization globals <c>t(key, ...args)</c> and the
    /// <c>i18n</c> object (<c>t</c>, <c>locale</c>, <c>languages</c>) that are
    /// available to every module and to <c>FormBase</c>.
    /// </summary>
    private void InstallI18nGlobals()
    {
        Engine.SetValue("__jsTranslate", (Func<JsValue, JsValue[], JsValue>)((keyJs, argsJs) =>
        {
            var key = keyJs.ToString();
            if (I18n == null || string.IsNullOrEmpty(key)) return keyJs;

            var args = new object?[argsJs.Length];
            for (var i = 0; i < argsJs.Length; i++)
                args[i] = argsJs[i].ToObject();

            return JsValue.FromObject(Engine, I18n.T(key, args));
        }));
        Engine.SetValue("__i18nLocale", (Func<string>)(() => I18n?.Locale ?? "en"));
        Engine.SetValue("__i18nLanguages", (Func<string[]>)(() =>
        {
            var cultures = I18n?.AvailableCultures;
            return cultures is { Count: > 0 } ? cultures.ToArray() : new[] { "en" };
        }));

        Engine.Evaluate(
            "var t = function (key) { return __jsTranslate(key, Array.prototype.slice.call(arguments, 1)); };" +
            "var i18n = { t: t };" +
            "Object.defineProperty(i18n, 'locale', { get: function () { return __i18nLocale(); } });" +
            "Object.defineProperty(i18n, 'languages', { get: function () { return __i18nLanguages(); } });");
    }

    /// <summary>
    /// Injects the global base class <c>FormBase</c> that form/module classes
    /// can inherit from. The constructor receives the context (<c>ctx</c>) and provides
    /// access to controls, host services, form, tab and app.
    /// The constructor serves as initialization; <c>activate</c>/<c>deactivate</c>/
    /// <c>dispose</c> are called by the host during the lifecycle.
    /// </summary>
    private void InstallFormBase()
    {
        Engine.Evaluate(
            "globalThis.FormBase = class FormBase {" +
            "  constructor(ctx) {" +
            "    this.ctx = ctx;" +
            "    this.__loadLayout = ctx ? ctx.__loadLayout : undefined;" +
            "    this.__createControl = ctx ? ctx.__createControl : undefined;" +
            "    this.controls = ctx ? ctx.controls : undefined;" +
            "    this.host = ctx ? ctx.host : undefined;" +
            "    this.form = ctx ? ctx.form : undefined;" +
            "    this.tab = ctx ? ctx.tab : undefined;" +
            "    this.app = ctx ? ctx.app : undefined;" +
            "  }" +
            "  loadLayout(path) {" +
            "    if (!this.__loadLayout) throw new Error('loadLayout is not available here.');" +
            "    var c = this.__loadLayout(path);" +
            "    this.controls = c;" +
            "    return c;" +
            "  }" +
            "  createControl(typeName, props) {" +
            "    if (!this.__createControl) throw new Error('createControl is not available here.');" +
            "    var c = this.__createControl(typeName, props || {});" +
            "    if (props && props.name) this.controls[props.name] = c;" +
            "    return c;" +
            "  }" +
            "  find(name) { return this.controls ? this.controls[name] : null; }" +
            "  t(key) { return globalThis.t.apply(globalThis, arguments); }" +
            "  get locale() { return globalThis.i18n.locale; }" +
            "  setStatus(text) { if (this.host) this.host.setStatus(text); }" +
            "  openModule(id) { if (this.host) this.host.openModule(id); }" +
            "  closeModule(id) { if (this.host) this.host.closeModule(id); }" +
            "  activate() { }" +
            "  deactivate() { }" +
            "  dispose() { }" +
            "};");
    }

    /// <summary>
    /// Tries to instantiate a module's <c>default</c> export as an ES6 class.
    /// </summary>
    /// <param name="moduleNamespace">The module namespace.</param>
    /// <param name="context">The context passed to the constructor.</param>
    /// <param name="instance">The created class instance, if successful.</param>
    /// <returns><see langword="true"/> if the <c>default</c> export is a constructible class.</returns>
    public bool TryConstructDefault(ObjectInstance moduleNamespace, JsValue context, out ObjectInstance instance)
    {
        instance = null!;
        var def = moduleNamespace.Get("default");
        if (def is Jint.Native.Function.Function f && f.IsConstructor())
        {
            instance = Engine.Construct(f, new JsValue[] { context });
            return true;
        }

        return false;
    }

    private void WriteConsole(string level, JsValue[] messages)
    {
        var text = string.Join(" ", messages.Select(FormatJs));
        Console.WriteLine($"[js:{level}] {text}");
    }

    private string FormatJs(JsValue value)
    {
        try
        {
            if (value is null || value.IsUndefined()) return "undefined";
            if (value.IsNull()) return "null";
            return value.ToString();
        }
        catch
        {
            return "<unprintable>";
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Timers.ClearAll();
        Engine.Dispose();
    }
}
