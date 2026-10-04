namespace OldSchoolForms.Ui.Runtime.Sources;

/// <summary>
/// Abstraction over the location from which an OldSchoolForms app package (manifest, layouts,
/// JavaScript modules) is read. All paths are relative to the package root and
/// use forward slashes (<c>/</c>).
/// </summary>
public interface IAppSource
{
    /// <summary>
    /// Checks whether the specified relative file exists in the package.
    /// </summary>
    /// <param name="relativePath">Relative path to the package root, e.g. <c>modules/customers/index.js</c>.</param>
    /// <returns><see langword="true"/> if the file is present.</returns>
    bool Exists(string relativePath);

    /// <summary>
    /// Reads the content of a file as a byte array.
    /// </summary>
    /// <param name="relativePath">Relative path to the package root.</param>
    /// <returns>The file content.</returns>
    /// <exception cref="FileNotFoundException">The file does not exist in the package.</exception>
    byte[] ReadBytes(string relativePath);

    /// <summary>
    /// Reads the content of a file as UTF-8 text.
    /// </summary>
    /// <param name="relativePath">Relative path to the package root.</param>
    /// <returns>The file content as text.</returns>
    /// <exception cref="FileNotFoundException">The file does not exist in the package.</exception>
    string ReadText(string relativePath);

    /// <summary>
    /// Provides a version marker (ETag) for the specified file, if the source
    /// offers such information.
    /// </summary>
    /// <param name="relativePath">Relative path to the package root.</param>
    /// <returns>The version marker or <see langword="null"/>.</returns>
    string? GetETag(string relativePath);

    /// <summary>
    /// Updates the package from the remote source (e.g. server synchronization).
    /// Local sources implement this as a no-op.
    /// </summary>
    /// <param name="progress">Optional progress report.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes the synchronization.</returns>
    Task SyncAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Human-readable description of the source for logging and status displays.
    /// </summary>
    string Describe { get; }
}
