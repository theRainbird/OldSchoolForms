namespace OldSchoolForms.Shared.Contracts;

/// <summary>
/// One shared ES6 module file. Used both inside a
/// <see cref="SharedCodeManifest"/> (without <see cref="Content"/>) and as
/// the download response of <see cref="IServiceGateway.GetSharedModule"/>
/// (with <see cref="Content"/>).
/// </summary>
public sealed class SharedCodeFile
{
    /// <summary>Module path relative to the shared directory (e.g. <c>validation/customers.js</c>).</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>SHA-256 hash of the file content (hex, lowercase).</summary>
    public string Sha256 { get; set; } = string.Empty;

    /// <summary>Size of the file content in bytes (UTF-8).</summary>
    public long Size { get; set; }

    /// <summary>Version of the shared code bundle this file belongs to.</summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// The file content. <see langword="null"/> in manifest entries;
    /// populated only in the download response.
    /// </summary>
    public string? Content { get; set; }
}
