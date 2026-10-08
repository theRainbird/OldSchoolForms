namespace OldSchoolForms.Shared.Contracts;

/// <summary>
/// Generic CoreRemoting service contract between the client runtime and the
/// server runtime. Instead of one compile-time interface per business service,
/// all business-logic services are addressed dynamically by their service ID
/// through this single gateway. The gateway also serves the shared ES6 code
/// modules that clients download for local validation logic.
/// </summary>
public interface IServiceGateway
{
    /// <summary>
    /// Invokes a method of a business-logic service synchronously on the server.
    /// </summary>
    /// <param name="serviceId">The service ID as declared in <c>server.json</c>.</param>
    /// <param name="method">The method name exported by the service's ES6 class.</param>
    /// <param name="args">The arguments. <see cref="System.Data.DataTable"/> and
    /// <see cref="System.Data.DataSet"/> instances are transferred with full
    /// DiffGram fidelity (row states and original values preserved).</param>
    /// <returns>The call result carrying the return value or the error.</returns>
    RemoteCallResult Invoke(string serviceId, string method, object?[] args);

    /// <summary>
    /// Invokes a method of a business-logic service asynchronously on the server.
    /// </summary>
    /// <param name="serviceId">The service ID as declared in <c>server.json</c>.</param>
    /// <param name="method">The method name exported by the service's ES6 class.</param>
    /// <param name="args">The arguments (see <see cref="Invoke"/>).</param>
    /// <returns>A task carrying the call result.</returns>
    Task<RemoteCallResult> InvokeAsync(string serviceId, string method, object?[] args);

    /// <summary>
    /// Describes all services registered on the server.
    /// </summary>
    /// <returns>One <see cref="ServiceInfo"/> per registered service.</returns>
    ServiceInfo[] DescribeServices();

    /// <summary>
    /// Gets the manifest of the shared ES6 code modules that clients can
    /// download for local (client-side) validation logic.
    /// </summary>
    /// <returns>The shared code manifest with per-file hashes.</returns>
    SharedCodeManifest GetSharedCodeManifest();

    /// <summary>
    /// Downloads a single shared ES6 module from the server.
    /// </summary>
    /// <param name="path">The module path relative to the shared directory (e.g. <c>validation/customers.js</c>).</param>
    /// <param name="expectedVersion">Optional version the client already has;
    /// <see langword="null"/> when the client has no cached copy.</param>
    /// <returns>The shared module file including its content.</returns>
    SharedCodeFile GetSharedModule(string path, string? expectedVersion);
}
