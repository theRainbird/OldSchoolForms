using Jint;
using Jint.Native;
using OldSchoolForms.Ui.Runtime.Scripting;
using OldSchoolForms.Ui.Runtime.Sources;
using Xunit;

namespace OldSchoolForms.Ui.Runtime.Tests;

/// <summary>
/// Headless-Tests für die Jint-Engine-Host: ES-Modul-Import, Export-Aufrufe,
/// Timer-Scheduling und Console-Marshalling.
/// </summary>
public class JsEngineHostTests
{
    private static JsEngineHost CreateSampleEngine() =>
        new JsEngineHost(new FileSystemAppSource(TestPaths.SampleApp));

    [Fact]
    public void ImportSharedUtil_CallsExports()
    {
        using var engine = CreateSampleEngine();
        var ns = engine.ImportModule("shared/util.js");

        var currency = engine.CallExport(ns, "formatCurrency", 1234.5).ToString();
        Assert.Equal("1234.50 EUR", currency);

        var greet = engine.CallExport(ns, "greet", "Welt").ToString();
        Assert.Equal("Hallo, Welt!", greet);
    }

    [Fact]
    public void ImportEntry_ExportsConstructibleClass()
    {
        using var engine = CreateSampleEngine();
        var ns = engine.ImportModule("app.js");
        var def = ns.Get("default");
        var ctor = Assert.IsAssignableFrom<Jint.Native.Function.Function>(def);
        Assert.True(ctor.IsConstructor());
    }

    [Fact]
    public void CallExport_MissingReturnsUndefined()
    {
        using var engine = CreateSampleEngine();
        var ns = engine.ImportModule("shared/util.js");
        var value = engine.CallExport(ns, "doesNotExist");
        Assert.True(value.IsUndefined());
    }

    [Fact]
    public void SetTimeout_RunsOnPump()
    {
        using var engine = CreateSampleEngine();
        var ran = false;
        engine.Engine.SetValue("markRan", (Action)(() => ran = true));
        engine.Engine.Evaluate("setTimeout(markRan, 0);");

        engine.PumpTimers();

        Assert.True(ran);
    }

    [Fact]
    public void SetInterval_RepeatsUntilCleared()
    {
        using var engine = CreateSampleEngine();
        var count = 0;
        engine.Engine.SetValue("bump", (Action)(() => count++));
        var id = (int)engine.Engine.Evaluate("setInterval(bump, 0)").AsNumber();

        for (var i = 0; i < 10 && count < 3; i++)
        {
            engine.PumpTimers();
            Thread.Sleep(5);
        }

        engine.Engine.Evaluate($"clearInterval({id})");
        Assert.True(count >= 3, $"Erwartet >= 3 Aufrufe, war {count}.");
    }

    [Fact]
    public void ClearTimeout_PreventsExecution()
    {
        using var engine = CreateSampleEngine();
        var ran = false;
        engine.Engine.SetValue("markRan", (Action)(() => ran = true));
        var id = (int)engine.Engine.Evaluate("setTimeout(markRan, 50)").AsNumber();
        engine.Engine.Evaluate($"clearTimeout({id})");

        engine.PumpTimers();

        Assert.False(ran);
    }

    [Fact]
    public void ConsoleLog_IsCapturedWithLevel()
    {
        using var engine = CreateSampleEngine();
        var buffer = new StringWriter();
        var original = Console.Out;
        try
        {
            Console.SetOut(buffer);
            engine.Engine.Evaluate("console.log('hello', 42);");
        }
        finally
        {
            Console.SetOut(original);
        }

        Assert.Contains("[js:log] hello 42", buffer.ToString());
    }

    [Fact]
    public void CallFunction_NonFunctionThrows()
    {
        using var engine = CreateSampleEngine();
        Assert.Throws<InvalidOperationException>(
            () => engine.CallFunction(JsValue.Undefined));
    }
}
