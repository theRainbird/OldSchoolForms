using Jint;
using Jint.Native;
using Jint.Native.Object;
using OldSchoolForms.Ui.Runtime.Sources;

namespace OldSchoolForms.Ui.Runtime.Scripting;

/// <summary>
/// Zentrale Jint-Engine-Instanz für die JavaScript-Laufzeit. Konfiguriert die
/// Engine mit dem <see cref="AppSourceModuleLoader"/>, stellt Browser-ähnliche
/// Globals (<c>setTimeout</c>, <c>console</c>) bereit und marshallt Aufrufe.
/// </summary>
public sealed class JsEngineHost : IDisposable
{
    private readonly IAppSource _source;
    private bool _disposed;

    /// <summary>
    /// Erzeugt die JS-Engine für eine Paketquelle.
    /// </summary>
    /// <param name="source">Die Paketquelle für Modul-Importe.</param>
    /// <param name="devMode">Im Dev-Modus: längere Timeouts und detaillierte Fehler.</param>
    public JsEngineHost(IAppSource source, bool devMode = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
        DevMode = devMode;
        Timers = new TimerScheduler(this);

        Engine = new Engine(options =>
        {
            options.Strict = true;
            options.TimeoutInterval(TimeSpan.FromSeconds(devMode ? 120 : 10));
            options.Modules.ModuleLoader = new AppSourceModuleLoader(source);
            options.Modules.RegisterRequire = false;

            // camelCase-Fallback: JS greift mit Kleinbuchstaben zu, die CLR-Fassade
            // expose PascalCase-Mitglieder.
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

        InstallGlobals();
    }

    /// <summary>Die zugrunde liegende Jint-Engine.</summary>
    public Engine Engine { get; }

    /// <summary>Der Zeitgeber-Scheduler (wird pro Frame gepumpt).</summary>
    public TimerScheduler Timers { get; }

    /// <summary><see langword="true"/>, wenn im Entwicklungsmodus gestartet.</summary>
    public bool DevMode { get; }

    /// <summary>
    /// Fehler aus der JS-Ausführung (Kontext + Nachricht).
    /// </summary>
    public event Action<string, Exception>? Error;

    /// <summary>
    /// Importiert ein ES-Modul aus dem Paket.
    /// </summary>
    /// <param name="specifier">Der Modul-Spezifizierer (z. B. <c>app.js</c> oder <c>modules/customers/index.js</c>).</param>
    /// <returns>Das Modul-Namespace-Objekt.</returns>
    public ObjectInstance ImportModule(string specifier)
    {
        var key = ModulePath.Normalize(specifier);
        return Engine.Modules.Import(key);
    }

    /// <summary>
    /// Ruft eine JavaScript-Funktion mit der übergebenen <c>this</c>-Referenz auf.
    /// </summary>
    /// <param name="function">Der Funktionswert.</param>
    /// <param name="thisArg">Das <c>this</c>-Argument (oder <see cref="JsValue.Undefined"/>).</param>
    /// <param name="args">Die Argumente.</param>
    /// <returns>Der Rückgabewert.</returns>
    public JsValue CallFunction(JsValue function, JsValue thisArg, params JsValue[] args)
    {
        if (function is not Jint.Native.Function.Function)
            throw new InvalidOperationException("Aufgerufener Wert ist keine Funktion.");
        return Engine.Call(function, thisArg, args);
    }

    /// <summary>
    /// Ruft eine Funktion ohne spezifisches <c>this</c> auf.
    /// </summary>
    /// <param name="function">Der Funktionswert.</param>
    /// <param name="args">Die Argumente.</param>
    /// <returns>Der Rückgabewert.</returns>
    public JsValue CallFunction(JsValue function, params JsValue[] args)
        => CallFunction(function, JsValue.Undefined, args);

    /// <summary>
    /// Ruft einen Export eines Modul-Namespace auf, falls vorhanden.
    /// </summary>
    /// <param name="moduleNamespace">Der Namespace des Moduls.</param>
    /// <param name="exportName">Der Exportname (z. B. <c>init</c>).</param>
    /// <param name="args">Die Argumente als CLR-Werte.</param>
    /// <returns>Der Rückgabewert oder <see cref="JsValue.Undefined"/>.</returns>
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
    /// Erzeugt ein leeres JS-Objekt.
    /// </summary>
    /// <returns>Ein neues <see cref="ObjectInstance"/>.</returns>
    public ObjectInstance NewObject() => Engine.Intrinsics.Object.Construct(new JsValue[0]);

    /// <summary>
    /// Meldet einen Fehler aus der JS-Ausführung.
    /// </summary>
    /// <param name="context">Beschreibung des Fehlerkontexts.</param>
    /// <param name="ex">Die Ausnahme.</param>
    public void ReportError(string context, Exception ex)
    {
        Error?.Invoke(context, ex);
    }

    /// <summary>
    /// Pumpt die Zeitgeber (einmal pro Frame aufrufen).
    /// </summary>
    public void PumpTimers()
    {
        if (_disposed) return;
        Timers.Pump();
    }

    private void InstallGlobals()
    {
        Engine.SetValue("setTimeout", (Func<JsValue, double, int>)((cb, ms) => Timers.SetTimeout(cb, ms)));
        Engine.SetValue("setInterval", (Func<JsValue, double, int>)((cb, ms) => Timers.SetInterval(cb, ms)));
        Engine.SetValue("clearTimeout", (Action<int>)Timers.Clear);
        Engine.SetValue("clearInterval", (Action<int>)Timers.Clear);

        // Jint packt keine params-Delegates; daher ein echtes JS-Array via Rest-Parameter
        // an eine CLR-Funktion mit JsValue[]-Parameter weiterreichen.
        Engine.SetValue("__jsConsole", (Action<string, JsValue[]>)((level, msgs) => WriteConsole(level, msgs)));
        Engine.Evaluate(
            "var console = {" +
            "  log: function (...a) { __jsConsole('log', a); }," +
            "  info: function (...a) { __jsConsole('info', a); }," +
            "  warn: function (...a) { __jsConsole('warn', a); }," +
            "  error: function (...a) { __jsConsole('error', a); }" +
            "};");
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
