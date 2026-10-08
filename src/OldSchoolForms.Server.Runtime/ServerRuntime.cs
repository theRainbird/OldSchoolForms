using CoreRemoting;
using Microsoft.Extensions.DependencyInjection;
using CoreRemoting.DependencyInjection;
using CoreRemoting.Serialization.NeoBinary;
using OldSchoolForms.Server.Runtime.Manifest;
using OldSchoolForms.Server.Runtime.Remoting;
using OldSchoolForms.Server.Runtime.Services;
using OldSchoolForms.Server.Runtime.SharedCode;
using OldSchoolForms.Shared.Contracts;
using ServiceLifetime = CoreRemoting.DependencyInjection.ServiceLifetime;

namespace OldSchoolForms.Server.Runtime;

/// <summary>
/// The server runtime host: loads the manifest, instantiates the ES6
/// services, exposes them through the generic <see cref="IServiceGateway"/>
/// over CoreRemoting (plain TCP, no HTTP) and serves the shared ES6 code
/// bundle to clients.
/// </summary>
public sealed class ServerRuntime : IDisposable
{
    private readonly ServerManifest _manifest;
    private readonly ServiceRegistry _registry;
    private readonly SharedCodeProvider _sharedCode;
    private readonly ServiceGateway _gateway;
    private RemotingServer? _server;

    /// <summary>
    /// Creates the runtime for a package directory.
    /// </summary>
    /// <param name="packageRoot">Absolute directory containing <c>server.json</c>.</param>
    /// <param name="devMode">Development mode (longer script timeouts).</param>
    /// <param name="hostOverride">Overrides the manifest host when set.</param>
    /// <param name="portOverride">Overrides the manifest port when set.</param>
    public ServerRuntime(string packageRoot, bool devMode = false, string? hostOverride = null, int? portOverride = null)
    {
        packageRoot = Path.GetFullPath(packageRoot);
        _manifest = ServerManifest.Load(packageRoot);

        var sharedRoot = Path.GetFullPath(Path.Combine(packageRoot, _manifest.Shared?.Dir ?? "shared"));
        _sharedCode = new SharedCodeProvider(packageRoot, _manifest.Shared ?? new SharedDescriptor(), _manifest.Name);
        _registry = new ServiceRegistry(_manifest, packageRoot, sharedRoot, devMode, Log);
        _gateway = new ServiceGateway(_registry, _sharedCode);

        Host = hostOverride ?? _manifest.Host;
        Port = portOverride ?? _manifest.Port;
    }

    /// <summary>The bound host address.</summary>
    public string Host { get; }

    /// <summary>The bound TCP port.</summary>
    public int Port { get; }

    /// <summary>The loaded manifest.</summary>
    public ServerManifest Manifest => _manifest;

    /// <summary>
    /// Starts the CoreRemoting server and begins accepting client connections.
    /// </summary>
    public void Start()
    {
        var config = new ServerConfig
        {
            HostName = Host,
            NetworkPort = Port,
            MessageEncryption = _manifest.Encryption,
            Serializer = new NeoBinarySerializerAdapter(new NeoBinarySerializerConfig()),
            DependencyInjectionContainer = new MicrosoftDependencyInjectionContainer(new ServiceCollection()),
            RegisterServicesAction = container =>
                container.RegisterService<IServiceGateway>(() => _gateway, ServiceLifetime.Singleton)
        };

        _server = new RemotingServer(config);
        _server.Logon += (_, _) => Log("info", "Client logged on.");
        _server.Logoff += (_, _) => Log("info", "Client logged off.");
        _server.Error += (_, ex) => Log("error", $"Server error: {ex.Message}");

        _server.Start();
        Log("info", $"'{_manifest.Name}' v{_manifest.Version} listening on {Host}:{Port} (encryption: {(_manifest.Encryption ? "on" : "off")})");
        Log("info", $"Services: {string.Join(", ", _registry.ServiceIds)}");
    }

    /// <summary>
    /// Stops the server and releases all resources.
    /// </summary>
    public void Stop()
    {
        _server?.Stop();
        _server?.Dispose();
        _server = null;
        Log("info", "Server stopped.");
    }

    private static void Log(string level, string message) =>
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [{level}] {message}");

    /// <inheritdoc />
    public void Dispose()
    {
        Stop();
        _registry.Dispose();
    }
}
