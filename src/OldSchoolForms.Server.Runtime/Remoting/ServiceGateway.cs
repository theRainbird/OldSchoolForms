using OldSchoolForms.Server.Runtime.Services;
using OldSchoolForms.Server.Runtime.SharedCode;
using OldSchoolForms.Shared.Contracts;

namespace OldSchoolForms.Server.Runtime.Remoting;

/// <summary>
/// Implementation of the generic <see cref="IServiceGateway"/> contract.
/// Translates gateway calls into <see cref="ServiceRegistry"/> invocations
/// and maps errors into <see cref="RemoteCallResult"/> error codes so the
/// client never sees raw exceptions across the wire.
/// </summary>
public sealed class ServiceGateway : IServiceGateway
{
    private readonly ServiceRegistry _registry;
    private readonly SharedCodeProvider _sharedCode;

    /// <summary>
    /// Creates the gateway.
    /// </summary>
    /// <param name="registry">The service registry.</param>
    /// <param name="sharedCode">The shared code provider.</param>
    public ServiceGateway(ServiceRegistry registry, SharedCodeProvider sharedCode)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(sharedCode);
        _registry = registry;
        _sharedCode = sharedCode;
    }

    /// <inheritdoc />
    public RemoteCallResult Invoke(string serviceId, string method, object?[] args)
    {
        try
        {
            var result = _registry.Invoke(serviceId, method, args);
            return RemoteCallResult.Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return RemoteCallResult.Fail("unknown_service", ex.Message);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("no method", StringComparison.Ordinal))
        {
            return RemoteCallResult.Fail("unknown_method", ex.Message);
        }
        catch (Exception ex)
        {
            return RemoteCallResult.Fail("invocation_error", ex.Message);
        }
    }

    /// <inheritdoc />
    public Task<RemoteCallResult> InvokeAsync(string serviceId, string method, object?[] args) =>
        Task.Run(() => Invoke(serviceId, method, args));

    /// <inheritdoc />
    public ServiceInfo[] DescribeServices() => _registry.Describe();

    /// <inheritdoc />
    public SharedCodeManifest GetSharedCodeManifest() => _sharedCode.GetManifest();

    /// <inheritdoc />
    public SharedCodeFile GetSharedModule(string path, string? expectedVersion)
    {
        try
        {
            return _sharedCode.GetFile(path, expectedVersion);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException($"Shared module error: {ex.Message}", ex);
        }
    }
}
