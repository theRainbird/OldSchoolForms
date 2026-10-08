namespace OldSchoolForms.Server.Runtime;

/// <summary>
/// Entry point of the server runtime. Parses the command line, starts the
/// <see cref="ServerRuntime"/> and runs until the process is terminated.
/// </summary>
public static class Program
{
    /// <summary>
    /// Starts the server runtime.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Exit code.</returns>
    public static int Main(string[] args)
    {
        try
        {
            var options = ServerOptions.Parse(args);
            if (options.ShowHelp)
            {
                PrintUsage();
                return 0;
            }

            using var runtime = new ServerRuntime(options.PackageDir, options.Dev, options.Host, options.Port);
            runtime.Start();

            var exit = new ManualResetEventSlim(false);
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                exit.Set();
            };

            Console.WriteLine("Press Ctrl+C to stop.");
            exit.Wait();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[server] Error: {ex.Message}");
            return 1;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("OldSchoolForms Server Runtime");
        Console.WriteLine();
        Console.WriteLine("Usage: OldSchoolForms.Server.Runtime <packageDir> [options]");
        Console.WriteLine();
        Console.WriteLine("Arguments:");
        Console.WriteLine("  <packageDir>   Directory containing server.json");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --host <addr>  Override the manifest host");
        Console.WriteLine("  --port <port>  Override the manifest port");
        Console.WriteLine("  --dev          Development mode (longer script timeouts)");
        Console.WriteLine("  -h, --help     Show this help");
    }
}

/// <summary>
/// Parsed server command-line options.
/// </summary>
public sealed class ServerOptions
{
    /// <summary>Package directory containing <c>server.json</c>.</summary>
    public string PackageDir { get; set; } = ".";

    /// <summary>Host override (--host).</summary>
    public string? Host { get; set; }

    /// <summary>Port override (--port).</summary>
    public int? Port { get; set; }

    /// <summary>Development mode (--dev).</summary>
    public bool Dev { get; set; }

    /// <summary>Show help (--help).</summary>
    public bool ShowHelp { get; set; }

    /// <summary>
    /// Parses the command-line arguments.
    /// </summary>
    /// <param name="args">The arguments.</param>
    /// <returns>The parsed options.</returns>
    public static ServerOptions Parse(string[] args)
    {
        var options = new ServerOptions();
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
                case "--host":
                    if (i + 1 < args.Length) options.Host = args[++i];
                    break;
                case "--port":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out var port)) options.Port = port;
                    else if (i + 1 < args.Length) throw new ArgumentException($"Invalid port: {args[i + 1]}");
                    break;
                default:
                    if (arg.StartsWith("--host=", StringComparison.Ordinal))
                        options.Host = arg["--host=".Length..];
                    else if (arg.StartsWith("--port=", StringComparison.Ordinal))
                        options.Port = int.Parse(arg["--port=".Length..]);
                    else if (string.IsNullOrEmpty(options.PackageDir) || options.PackageDir == ".")
                        options.PackageDir = arg;
                    break;
            }
        }

        return options;
    }
}
