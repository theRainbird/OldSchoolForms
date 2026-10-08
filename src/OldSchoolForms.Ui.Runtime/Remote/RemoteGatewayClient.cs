using CoreRemoting;
using CoreRemoting.Serialization.NeoBinary;
using OldSchoolForms.Shared.Contracts;

namespace OldSchoolForms.Ui.Runtime.Remote;

/// <summary>
/// Thin wrapper around the CoreRemoting <see cref="RemotingClient"/> that
/// connects to the server's <see cref="IServiceGateway"/>. Handles connection
/// lifecycle, lazy reconnect and exposes the gateway proxy to the runtime.
/// All calls use the NeoBinary serializer so <c>DataTable</c>/<c>DataSet</c>
/// payloads keep full DiffGram fidelity.
/// </summary>
public sealed class RemoteGatewayClient : IDisposable
{
    private readonly RemotingClient _client;
    private readonly object _sync = new();
    private IServiceGateway? _proxy;

    /// <summary>
    /// Creates the gateway client for the given endpoint.
    /// </summary>
    /// <param name="host">The server host name or address.</param>
    /// <param name="port">The server port.</param>
    /// <param name="encryption">Whether transport encryption (RSA+AES) is used.</param>
    public RemoteGatewayClient(string host, int port, bool encryption = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        Host = host;
        Port = port;

        var config = new ClientConfig
        {
            ServerHostName = host,
            ServerPort = port,
            MessageEncryption = encryption,
            Serializer = new NeoBinarySerializerAdapter(new NeoBinarySerializerConfig())
        };
        config.ConnectionTimeout = 15;
        config.InvocationTimeout = 60;

        _client = new RemotingClient(config);
        _client.AfterDisconnect += () =>
        {
            lock (_sync)
            {
                _proxy = null;
            }
        };
    }

    /// <summary>The configured server host.</summary>
    public string Host { get; }

    /// <summary>The configured server port.</summary>
    public int Port { get; }

    /// <summary><see langword="true"/> when the client is connected and has a session.</summary>
    public bool IsConnected => _client.IsConnected && _client.HasSession;

    /// <summary>
    /// Connects to the server and creates the gateway proxy.
    /// </summary>
    /// <exception cref="InvalidOperationException">The connection could not be established.</exception>
    public void Connect()
    {
        lock (_sync)
        {
            if (_proxy != null && _client.IsConnected && _client.HasSession)
                return;

            if (!_client.IsConnected)
                _client.Connect();

            _proxy = _client.CreateProxy<IServiceGateway>();
        }
    }

    /// <summary>
    /// Ensures an active connection (lazy reconnect) and returns the gateway proxy.
    /// </summary>
    /// <returns>The service gateway proxy.</returns>
    /// <exception cref="InvalidOperationException">The server is not reachable.</exception>
    public IServiceGateway Gateway
    {
        get
        {
            lock (_sync)
            {
                if (_proxy == null || !_client.IsConnected || !_client.HasSession)
                    Connect();
                return _proxy!;
            }
        }
    }

    /// <summary>
    /// Invokes a service method synchronously.
    /// </summary>
    /// <param name="serviceId">The service ID.</param>
    /// <param name="method">The method name.</param>
    /// <param name="args">The arguments.</param>
    /// <returns>The call result.</returns>
    public RemoteCallResult Invoke(string serviceId, string method, object?[] args)
        => Gateway.Invoke(serviceId, method, args);

    /// <summary>
    /// Invokes a service method asynchronously.
    /// </summary>
    /// <param name="serviceId">The service ID.</param>
    /// <param name="method">The method name.</param>
    /// <param name="args">The arguments.</param>
    /// <returns>A task with the call result.</returns>
    public Task<RemoteCallResult> InvokeAsync(string serviceId, string method, object?[] args)
        => Gateway.InvokeAsync(serviceId, method, args);

    /// <summary>
    /// Requests the service catalog from the server.
    /// </summary>
    /// <returns>The service descriptions.</returns>
    public ServiceInfo[] DescribeServices() => Gateway.DescribeServices();

    /// <summary>
    /// Requests the shared code manifest from the server.
    /// </summary>
    /// <returns>The manifest of shared ES6 files.</returns>
    public SharedCodeManifest GetSharedCodeManifest() => Gateway.GetSharedCodeManifest();

    /// <summary>
    /// Downloads a single shared code file.
    /// </summary>
    /// <param name="path">Relative path of the shared file.</param>
    /// <param name="expectedVersion">Optional expected version.</param>
    /// <returns>The file including its content.</returns>
    public SharedCodeFile GetSharedModule(string path, string? expectedVersion = null)
        => Gateway.GetSharedModule(path, expectedVersion);

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_sync)
        {
            if (_proxy != null)
            {
                try { _client.ShutdownProxy(_proxy); } catch { /* best effort */ }
                _proxy = null;
            }
        }

        try { _client.Disconnect(true); } catch { /* best effort */ }
        _client.Dispose();
    }
}
