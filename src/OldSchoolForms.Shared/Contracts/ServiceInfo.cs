namespace OldSchoolForms.Shared.Contracts;

/// <summary>
/// Describes one business-logic service registered on the server, as
/// returned by <see cref="IServiceGateway.DescribeServices"/>.
/// </summary>
public sealed class ServiceInfo
{
    /// <summary>The service ID used in <see cref="IServiceGateway.Invoke"/>.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Optional human-readable description of the service.</summary>
    public string? Description { get; set; }

    /// <summary>The names of the methods the service exposes.</summary>
    public string[] Methods { get; set; } = Array.Empty<string>();

    /// <summary>The service lifetime: <c>singleton</c>, <c>singlecall</c> or <c>scoped</c>.</summary>
    public string Lifetime { get; set; } = "singleton";
}
