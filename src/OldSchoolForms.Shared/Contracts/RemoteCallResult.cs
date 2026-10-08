namespace OldSchoolForms.Shared.Contracts;

/// <summary>
/// Result of a remote service call through the <see cref="IServiceGateway"/>.
/// Carries either the return value (on success) or the error information
/// (on failure). Designed as a plain DTO so it serializes cleanly with the
/// NeoBinary serializer, including <see cref="System.Data.DataTable"/>
/// return values.
/// </summary>
public sealed class RemoteCallResult
{
    /// <summary><see langword="true"/> when the service method completed without an exception.</summary>
    public bool Success { get; set; }

    /// <summary>
    /// The return value of the service method. May be a primitive, a DTO,
    /// a <see cref="System.Data.DataTable"/> or a <see cref="System.Data.DataSet"/>.
    /// </summary>
    public object? ReturnValue { get; set; }

    /// <summary>The error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? Error { get; set; }

    /// <summary>
    /// A machine-readable error code (e.g. <c>unknown_service</c>,
    /// <c>unknown_method</c>, <c>invocation_error</c>).
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="returnValue">The return value of the service method.</param>
    /// <returns>A successful <see cref="RemoteCallResult"/>.</returns>
    public static RemoteCallResult Ok(object? returnValue) => new() { Success = true, ReturnValue = returnValue };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="errorCode">The machine-readable error code.</param>
    /// <param name="message">The human-readable error message.</param>
    /// <returns>A failed <see cref="RemoteCallResult"/>.</returns>
    public static RemoteCallResult Fail(string errorCode, string message) =>
        new() { Success = false, ErrorCode = errorCode, Error = message };
}
