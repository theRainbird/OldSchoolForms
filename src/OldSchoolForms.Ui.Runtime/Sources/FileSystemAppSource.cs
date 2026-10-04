using OldSchoolForms.Ui.Core;

namespace OldSchoolForms.Ui.Runtime.Sources;

/// <summary>
/// <see cref="IAppSource"/> that reads an app package from a local directory
/// (e.g. a checked-out Git repository or an unpacked bundle).
/// </summary>
public sealed class FileSystemAppSource : IAppSource
{
    private readonly string _root;

    /// <summary>
    /// Creates a new local package source.
    /// </summary>
    /// <param name="rootPath">Absolute or relative base directory of the package.</param>
    /// <exception cref="DirectoryNotFoundException">The directory does not exist.</exception>
    public FileSystemAppSource(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentException(LangRes.GetString("Runtime_RootDirectoryEmpty"), nameof(rootPath));

        _root = Path.GetFullPath(rootPath);
        if (!Directory.Exists(_root))
            throw new DirectoryNotFoundException(string.Format(LangRes.GetString("Runtime_AppDirectoryNotFound"), _root));
    }

    /// <summary>
    /// The resolved absolute root directory of the package.
    /// </summary>
    public string Root => _root;

    /// <inheritdoc />
    public string Describe => $"File system: {_root}";

    /// <inheritdoc />
    public bool Exists(string relativePath) => File.Exists(Resolve(relativePath));

    /// <inheritdoc />
    public byte[] ReadBytes(string relativePath)
    {
        var full = Resolve(relativePath);
        if (!File.Exists(full))
            throw new FileNotFoundException(string.Format(LangRes.GetString("Runtime_FileMissingInPackage"), relativePath), full);
        return File.ReadAllBytes(full);
    }

    /// <inheritdoc />
    public string ReadText(string relativePath)
    {
        var full = Resolve(relativePath);
        if (!File.Exists(full))
            throw new FileNotFoundException(string.Format(LangRes.GetString("Runtime_FileMissingInPackage"), relativePath), full);
        return File.ReadAllText(full, System.Text.Encoding.UTF8);
    }

    /// <inheritdoc />
    public string? GetETag(string relativePath)
    {
        var full = Resolve(relativePath);
        if (!File.Exists(full)) return null;
        var stamp = File.GetLastWriteTimeUtc(full).Ticks.ToString();
        return $"local-{stamp}";
    }

    /// <inheritdoc />
    public Task SyncAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Resolves a relative path below the package root and prevents
    /// path traversal out of the root.
    /// </summary>
    /// <param name="relativePath">Relative path to the package root.</param>
    /// <returns>The absolute path.</returns>
    /// <exception cref="UnauthorizedAccessException">The path escapes the package root.</exception>
    private string Resolve(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException(LangRes.GetString("Runtime_PathEmpty"), nameof(relativePath));

        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        var full = Path.GetFullPath(Path.Combine(_root, normalized));

        var rootWithSep = _root.EndsWith(Path.DirectorySeparatorChar) ? _root : _root + Path.DirectorySeparatorChar;
        if (!full.StartsWith(rootWithSep, StringComparison.Ordinal) &&
            !string.Equals(full, _root, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(string.Format(LangRes.GetString("Runtime_PathEscapesPackage"), relativePath));
        }

        return full;
    }
}
