namespace OldSchoolForms.Ui.Runtime.Sources;

/// <summary>
/// <see cref="IAppSource"/>, der ein App-Paket aus einem lokalen Verzeichnis liest
/// (z. B. ein ausgechecktes Git-Repository oder ein entpacktes Bundle).
/// </summary>
public sealed class FileSystemAppSource : IAppSource
{
    private readonly string _root;

    /// <summary>
    /// Erzeugt eine neue lokale Paketquelle.
    /// </summary>
    /// <param name="rootPath">Absolutes oder relatives Basisverzeichnis des Pakets.</param>
    /// <exception cref="DirectoryNotFoundException">Das Verzeichnis existiert nicht.</exception>
    public FileSystemAppSource(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentException("Wurzelverzeichnis darf nicht leer sein.", nameof(rootPath));

        _root = Path.GetFullPath(rootPath);
        if (!Directory.Exists(_root))
            throw new DirectoryNotFoundException($"App-Verzeichnis nicht gefunden: {_root}");
    }

    /// <summary>
    /// Das aufgelöste absolute Wurzelverzeichnis des Pakets.
    /// </summary>
    public string Root => _root;

    /// <inheritdoc />
    public string Describe => $"Dateisystem: {_root}";

    /// <inheritdoc />
    public bool Exists(string relativePath) => File.Exists(Resolve(relativePath));

    /// <inheritdoc />
    public byte[] ReadBytes(string relativePath)
    {
        var full = Resolve(relativePath);
        if (!File.Exists(full))
            throw new FileNotFoundException($"Datei fehlt im Paket: {relativePath}", full);
        return File.ReadAllBytes(full);
    }

    /// <inheritdoc />
    public string ReadText(string relativePath)
    {
        var full = Resolve(relativePath);
        if (!File.Exists(full))
            throw new FileNotFoundException($"Datei fehlt im Paket: {relativePath}", full);
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
    /// Löst einen relativen Pfad unterhalb der Paketwurzel auf und verhindert
    /// Pfad-Traversierung aus der Wurzel heraus.
    /// </summary>
    /// <param name="relativePath">Relativer Pfad zur Paketwurzel.</param>
    /// <returns>Der absolute Pfad.</returns>
    /// <exception cref="UnauthorizedAccessException">Der Pfad verlässt die Paketwurzel.</exception>
    private string Resolve(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("Pfad darf nicht leer sein.", nameof(relativePath));

        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        var full = Path.GetFullPath(Path.Combine(_root, normalized));

        var rootWithSep = _root.EndsWith(Path.DirectorySeparatorChar) ? _root : _root + Path.DirectorySeparatorChar;
        if (!full.StartsWith(rootWithSep, StringComparison.Ordinal) &&
            !string.Equals(full, _root, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException($"Pfad verlässt die Paketwurzel: {relativePath}");
        }

        return full;
    }
}
