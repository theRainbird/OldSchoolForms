namespace OldSchoolForms.Shared.Contracts;

/// <summary>
/// Manifest of the shared ES6 code modules a client can download from the
/// server. The shared code is strictly separated from the server-only
/// business-logic services: it contains pure logic (e.g. DataTable schema
/// validation) that runs inside the client's JS engine.
/// </summary>
public sealed class SharedCodeManifest
{
    /// <summary>Name of the app the shared code belongs to.</summary>
    public string AppName { get; set; } = string.Empty;

    /// <summary>Version of the shared code bundle.</summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>The shared files with their integrity hashes.</summary>
    public SharedCodeFile[] Files { get; set; } = Array.Empty<SharedCodeFile>();
}
