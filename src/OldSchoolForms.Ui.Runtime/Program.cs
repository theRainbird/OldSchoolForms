using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Platform;
using OldSchoolForms.Ui.Runtime.Dev;
using OldSchoolForms.Ui.Runtime.Facade;
using OldSchoolForms.Ui.Runtime.Hosting;
using OldSchoolForms.Ui.Runtime.Modules;
using OldSchoolForms.Ui.Runtime.Scripting;
using OldSchoolForms.Ui.Runtime.Serialization;
using OldSchoolForms.Ui.Runtime.Shell;
using OldSchoolForms.Ui.Runtime.Sources;

namespace OldSchoolForms.Ui.Runtime;

/// <summary>
/// Entry point of the client runtime environment. Parses the command line,
/// builds the package source, the JS engine, the shell and the module host,
/// and starts the UI message loop.
/// </summary>
public static class Program
{
    /// <summary>
    /// Starts the runtime environment.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Exit code.</returns>
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            var options = CliOptions.Parse(args);
            if (options.ShowHelp)
            {
                PrintUsage();
                return 0;
            }

            var source = BuildSource(options);
            Console.WriteLine($"[runtime] Source: {source.Describe}");

            if (source is CachedAppSource)
            {
                var progress = new Progress<string>(p => Console.WriteLine($"[sync] {p}"));
                source.SyncAsync(progress).GetAwaiter().GetResult();
            }

            var manifest = AppManifest.Load(source);
            Console.WriteLine($"[runtime] App: {manifest.Name} v{manifest.Version} ({manifest.Modules.Count} modules)");

            using var engine = new JsEngineHost(source, options.Dev);
            engine.Error += (context, ex) =>
            {
                Console.Error.WriteLine($"[js-error] {context}: {ex.Message}");
                if (options.Dev)
                    Console.Error.WriteLine(ex.StackTrace);
            };

            var loader = new RuntimeLayoutLoader();
            var registry = new ModuleRegistry(manifest.Modules);
            var host = new HostServices(source, manifest.Name, manifest.Version);

            var shell = ShellBuilder.Build(source, manifest, engine, loader, registry, host);
            var shellForm = new JsForm(engine, shell.Form);
            var moduleHost = new ModuleHost(source, engine, loader, registry, shell.ContentTabs, shellForm, host);

            host.ModuleHost = moduleHost;
            host.AttachStatusLabel(shell.StatusLabel);

            if (shell.NavTree != null)
                ShellBuilder.WireNavTree(shell.NavTree, moduleHost);
            moduleHost.AttachTabActivation();

            RunEntryScript(engine, manifest, source, shellForm, host);

            Platform.Platform.OnFrame += engine.PumpTimers;

            HotReloadWatcher? watcher = null;
            if (options.Dev && source is FileSystemAppSource fileSource)
            {
                watcher = new HotReloadWatcher(fileSource.Root);
                watcher.Changed += path => OnFileChanged(path, fileSource.Root, manifest, moduleHost);
                watcher.Start();
                Console.WriteLine("[runtime] Hot Reload enabled.");
            }

            LocalizationManager.LoadSavedCulture();
            Application.Run(shell.Form);
            watcher?.Dispose();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[runtime] Error: {ex.Message}");
            return 1;
        }
    }

    private static void RunEntryScript(
        JsEngineHost engine,
        AppManifest manifest,
        IAppSource source,
        JsForm shellForm,
        HostServices host)
    {
        if (string.IsNullOrEmpty(manifest.Entry) || !source.Exists(manifest.Entry))
            return;

        try
        {
            var ns = engine.ImportModule(manifest.Entry);
            var ctx = engine.NewObject();
            var app = engine.NewObject();
            app.Set("name", Jint.Native.JsValue.FromObject(engine.Engine, manifest.Name));
            app.Set("version", Jint.Native.JsValue.FromObject(engine.Engine, manifest.Version));
            ctx.Set("form", Jint.Native.JsValue.FromObject(engine.Engine, shellForm));
            ctx.Set("host", Jint.Native.JsValue.FromObject(engine.Engine, host));
            ctx.Set("app", app);
            if (!engine.TryConstructDefault(ns, ctx, out _))
                engine.CallExport(ns, "init", ctx);
        }
        catch (Exception ex)
        {
            engine.ReportError($"Entry '{manifest.Entry}'", ex);
        }
    }

    private static void OnFileChanged(
        string absolutePath,
        string root,
        AppManifest manifest,
        ModuleHost moduleHost)
    {
        var relative = Path.GetRelativePath(root, absolutePath).Replace('\\', '/');
        Console.WriteLine($"[hotreload] changed: {relative}");

        foreach (var module in manifest.Modules)
        {
            if (!moduleHost.OpenModuleIds.Contains(module.Id, StringComparer.OrdinalIgnoreCase))
                continue;

            if (string.Equals(relative, module.ScriptPath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(relative, module.LayoutPath, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[hotreload] Reload module '{module.Id}'");
                moduleHost.ReloadModule(module.Id);
                return;
            }
        }
    }

    private static IAppSource BuildSource(CliOptions options)
    {
        if (!string.IsNullOrEmpty(options.PositionalPath))
            return new FileSystemAppSource(options.PositionalPath);

        if (!string.IsNullOrEmpty(options.Source))
            return RemoteAppSourceFactory.Create(options.Source);

        if (File.Exists("runtime.json"))
        {
            var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText("runtime.json"));
            if (doc.RootElement.TryGetProperty("source", out var src))
                return RemoteAppSourceFactory.Create(src.GetString() ?? "./app");
        }

        return new FileSystemAppSource("./app");
    }

    private static void PrintUsage()
    {
        Console.WriteLine(LangRes.GetString("Runtime_Cli_Title"));
        Console.WriteLine();
        Console.WriteLine(LangRes.GetString("Runtime_Cli_Usage"));
        Console.WriteLine(LangRes.GetString("Runtime_Cli_UsageLine"));
        Console.WriteLine();
        Console.WriteLine(LangRes.GetString("Runtime_Cli_Arguments"));
        Console.WriteLine(LangRes.GetString("Runtime_Cli_ArgPath"));
        Console.WriteLine();
        Console.WriteLine(LangRes.GetString("Runtime_Cli_Options"));
        Console.WriteLine(LangRes.GetString("Runtime_Cli_OptSource"));
        Console.WriteLine(LangRes.GetString("Runtime_Cli_OptDev"));
        Console.WriteLine(LangRes.GetString("Runtime_Cli_OptHelp"));
        Console.WriteLine();
        Console.WriteLine(LangRes.GetString("Runtime_Cli_Examples"));
        Console.WriteLine("  OldSchoolForms.Ui.Runtime ./app");
        Console.WriteLine("  OldSchoolForms.Ui.Runtime /pfad/zum/checkout --dev");
        Console.WriteLine("  OldSchoolForms.Ui.Runtime --source https://server/app --dev");
    }
}

/// <summary>
/// Parsed command-line options.
/// </summary>
public sealed class CliOptions
{
    /// <summary>Positional package path.</summary>
    public string? PositionalPath { get; set; }

    /// <summary>Network source (--source).</summary>
    public string? Source { get; set; }

    /// <summary>Dev mode (--dev).</summary>
    public bool Dev { get; set; }

    /// <summary>Show help (--help).</summary>
    public bool ShowHelp { get; set; }

    /// <summary>
    /// Parses the command-line arguments.
    /// </summary>
    /// <param name="args">The arguments.</param>
    /// <returns>The parsed options.</returns>
    public static CliOptions Parse(string[] args)
    {
        var options = new CliOptions();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--dev":
                    options.Dev = true;
                    break;
                case "-h":
                case "--help":
                    options.ShowHelp = true;
                    break;
                case "--source":
                    if (i + 1 < args.Length)
                        options.Source = args[++i];
                    break;
                default:
                    if (arg.StartsWith("--source=", StringComparison.Ordinal))
                        options.Source = arg["--source=".Length..];
                    else if (string.IsNullOrEmpty(options.PositionalPath))
                        options.PositionalPath = arg;
                    break;
            }
        }

        return options;
    }
}
