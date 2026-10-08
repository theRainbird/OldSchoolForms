using OldSchoolForms.Server.Runtime.Manifest;
using OldSchoolForms.Server.Runtime.Scripting;
using OldSchoolForms.Shared.Contracts;

namespace OldSchoolForms.Server.Runtime.Services;

/// <summary>
/// Instantiates and owns the hosted ES6 services according to the server
/// manifest. Singleton services get one long-lived instance; single-call
/// (and scoped, which maps to single-call here) services are created fresh
/// per invocation. All invocations are serialized per service through the
/// <see cref="ServiceDispatcher"/>.
/// </summary>
public sealed class ServiceRegistry : IDisposable
{
    private sealed class RegisteredService
    {
        public ServiceDescriptor Descriptor = null!;
        public JsServiceInstance? Singleton;
    }

    private readonly Dictionary<string, RegisteredService> _services = new(StringComparer.OrdinalIgnoreCase);
    private readonly ServiceDispatcher _dispatcher = new();
    private readonly ServerManifest _manifest;
    private readonly string _packageRoot;
    private readonly string _sharedRoot;
    private readonly bool _devMode;
    private readonly Action<string, string> _log;

    /// <summary>
    /// Creates the registry and instantiates all singleton services.
    /// </summary>
    /// <param name="manifest">The server manifest.</param>
    /// <param name="packageRoot">Absolute package directory.</param>
    /// <param name="sharedRoot">Absolute shared code directory.</param>
    /// <param name="devMode">Development mode flag.</param>
    /// <param name="log">Log sink for service output.</param>
    public ServiceRegistry(ServerManifest manifest, string packageRoot, string sharedRoot, bool devMode, Action<string, string> log)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        _manifest = manifest;
        _packageRoot = packageRoot;
        _sharedRoot = sharedRoot;
        _devMode = devMode;
        _log = log;

        foreach (var descriptor in manifest.Services)
        {
            if (_services.ContainsKey(descriptor.Id))
                throw new InvalidOperationException($"Duplicate service id '{descriptor.Id}' in server.json.");

            var registered = new RegisteredService { Descriptor = descriptor };
            if (IsSingleton(descriptor.Lifetime))
                registered.Singleton = CreateInstance(descriptor);

            _services[descriptor.Id] = registered;
            _log("info", $"Registered service '{descriptor.Id}' ({descriptor.Lifetime}) from {descriptor.Script}");
        }
    }

    /// <summary>The registered service IDs.</summary>
    public IReadOnlyCollection<string> ServiceIds => _services.Keys;

    /// <summary>
    /// Invokes a service method.
    /// </summary>
    /// <param name="serviceId">The service ID.</param>
    /// <param name="method">The method name.</param>
    /// <param name="args">The arguments.</param>
    /// <returns>The CLR return value.</returns>
    /// <exception cref="KeyNotFoundException">The service is unknown.</exception>
    /// <exception cref="InvalidOperationException">The method is unknown or failed.</exception>
    public object? Invoke(string serviceId, string method, object?[]? args)
    {
        var registered = Find(serviceId);
        return _dispatcher.Run(serviceId, () =>
        {
            if (registered.Singleton != null)
                return registered.Singleton.Invoke(method, args);

            using var instance = CreateInstance(registered.Descriptor);
            return instance.Invoke(method, args);
        });
    }

    /// <summary>
    /// Describes all registered services for the gateway.
    /// </summary>
    /// <returns>Service metadata.</returns>
    public ServiceInfo[] Describe()
    {
        var infos = new List<ServiceInfo>();
        foreach (var registered in _services.Values)
        {
            var methods = Array.Empty<string>();
            try
            {
                if (registered.Singleton != null)
                {
                    methods = registered.Singleton.GetMethods();
                }
                else
                {
                    using var probe = CreateInstance(registered.Descriptor);
                    methods = probe.GetMethods();
                }
            }
            catch (Exception ex)
            {
                _log("warn", $"Could not enumerate methods of service '{registered.Descriptor.Id}': {ex.Message}");
            }

            infos.Add(new ServiceInfo
            {
                Id = registered.Descriptor.Id,
                Description = registered.Descriptor.Description,
                Methods = methods,
                Lifetime = registered.Descriptor.Lifetime
            });
        }

        return infos.ToArray();
    }

    private RegisteredService Find(string serviceId) =>
        _services.TryGetValue(serviceId ?? string.Empty, out var registered)
            ? registered
            : throw new KeyNotFoundException($"Unknown service '{serviceId}'.");

    private static bool IsSingleton(string lifetime) =>
        !string.Equals(lifetime, "singlecall", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(lifetime, "scoped", StringComparison.OrdinalIgnoreCase);

    private JsServiceInstance CreateInstance(ServiceDescriptor descriptor) =>
        new(descriptor, _manifest, _packageRoot, _sharedRoot, _devMode,
            (level, msg) => _log(level, $"[{descriptor.Id}] {msg}"));

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var registered in _services.Values)
            registered.Singleton?.Dispose();
        _services.Clear();
        _dispatcher.Dispose();
    }
}
