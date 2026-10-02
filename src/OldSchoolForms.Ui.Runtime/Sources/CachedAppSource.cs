using System.IO.Compression;
using System.Security.Cryptography;

namespace OldSchoolForms.Ui.Runtime.Sources;

/// <summary>
/// Offline-fähige Quelle: kapselt eine <see cref="RemoteAppSource"/> und spiegelt
/// das App-Paket in einen lokalen Cache. Liest aus dem Cache; <see cref="SyncAsync"/>
/// gleicht Versionen ab und lädt geänderte Bundles (mit SHA-256-Verifikation).
/// </summary>
public sealed class CachedAppSource : IAppSource, IDisposable
{
    private readonly RemoteAppSource _remote;
    private readonly string _cacheRoot;
    private FileSystemAppSource? _local;

    /// <summary>
    /// Erzeugt eine gecachte Netzwerkquelle.
    /// <param name="remote">Die ferne Quelle.</param>
    /// <param name="cacheRoot">Das Cache-Basisverzeichnis.</param>
    /// </summary>
    public CachedAppSource(RemoteAppSource remote, string cacheRoot)
    {
        ArgumentNullException.ThrowIfNull(remote);
        ArgumentNullException.ThrowIfNull(cacheRoot);
        _remote = remote;
        _cacheRoot = cacheRoot;
    }

    /// <summary>Die aktuell gespiegelte App-Version (nach Sync).</summary>
    public string? SyncedVersion { get; private set; }

    /// <inheritdoc />
    public string Describe => $"Cache ({_remote.BaseUri}) → {_local?.Root ?? _cacheRoot}";

    /// <inheritdoc />
    public bool Exists(string relativePath) => EnsureLocal().Exists(relativePath);

    /// <inheritdoc />
    public byte[] ReadBytes(string relativePath) => EnsureLocal().ReadBytes(relativePath);

    /// <inheritdoc />
    public string ReadText(string relativePath) => EnsureLocal().ReadText(relativePath);

    /// <inheritdoc />
    public string? GetETag(string relativePath) => SyncedVersion ?? EnsureLocal().GetETag(relativePath);

    /// <inheritdoc />
    public async Task SyncAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        RemoteManifest manifest;
        try
        {
            progress?.Report("Server-Manifest wird geladen...");
            manifest = await _remote.FetchManifestAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (_local != null)
        {
            progress?.Report($"Offline: nutze Cache (Sync fehlgeschlagen: {ex.Message})");
            return;
        }

        var safeName = Sanitize(manifest.Name);
        var targetDir = Path.Combine(_cacheRoot, safeName, manifest.Version);
        var marker = Path.Combine(targetDir, ".synced");

        if (File.Exists(marker) && File.ReadAllText(marker) == manifest.Version)
        {
            _local = new FileSystemAppSource(targetDir);
            SyncedVersion = manifest.Version;
            progress?.Report($"Cache aktuell (v{manifest.Version}).");
            return;
        }

        if (Directory.Exists(targetDir))
            Directory.Delete(targetDir, true);
        Directory.CreateDirectory(targetDir);

        progress?.Report($"Shell-Bundle wird geladen...");
        await ExtractBundleAsync(manifest.Shell.Url, manifest.Shell.Sha256, targetDir, cancellationToken).ConfigureAwait(false);

        foreach (var module in manifest.Modules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report($"Modul '{module.Id}' wird geladen...");
            var moduleDir = Path.Combine(targetDir, "modules", module.Id);
            await ExtractBundleAsync(module.Url, module.Sha256, moduleDir, cancellationToken).ConfigureAwait(false);
        }

        WriteLocalManifest(targetDir, manifest);
        File.WriteAllText(marker, manifest.Version);

        _local = new FileSystemAppSource(targetDir);
        SyncedVersion = manifest.Version;
        progress?.Report($"Sync abgeschlossen (v{manifest.Version}).");
    }

    private async Task ExtractBundleAsync(string url, string? sha256, string targetDir, CancellationToken ct)
    {
        var bytes = await _remote.FetchBundleAsync(url, ct).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(sha256))
        {
            var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (!string.Equals(hash, sha256.Trim().ToLowerInvariant(), StringComparison.Ordinal))
                throw new InvalidOperationException($"SHA-256-Prüfung fehlgeschlagen für {url}.");
        }

        using var ms = new MemoryStream(bytes);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue; // directory entry
            var dest = Path.GetFullPath(Path.Combine(targetDir, entry.FullName));
            var destDir = Path.GetDirectoryName(dest);
            if (!string.IsNullOrEmpty(destDir))
                Directory.CreateDirectory(destDir);
            using var inStream = entry.Open();
            using var outStream = File.Create(dest);
            await inStream.CopyToAsync(outStream, ct).ConfigureAwait(false);
        }
    }

    private static void WriteLocalManifest(string targetDir, RemoteManifest remote)
    {
        var modules = remote.Modules
            .Select(m => $"\"modules/{m.Id}\"");
        var entry = remote.Entry == null ? "null" : $"\"{remote.Entry}\"";
        var json =
            "{\n" +
            $"  \"name\": \"{Escape(remote.Name)}\",\n" +
            $"  \"version\": \"{Escape(remote.Version)}\",\n" +
            "  \"shell\": \"layouts/shell.json\",\n" +
            $"  \"entry\": {entry},\n" +
            "  \"modules\": [" + string.Join(",", modules) + "]\n" +
            "}\n";
        File.WriteAllText(Path.Combine(targetDir, "app.json"), json, System.Text.Encoding.UTF8);
    }

    private FileSystemAppSource EnsureLocal()
    {
        if (_local != null) return _local;
        throw new InvalidOperationException("App-Paket wurde noch nicht synchronisiert. SyncAsync zuerst aufrufen.");
    }

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    /// <inheritdoc />
    public void Dispose() => _remote.Dispose();
}

/// <summary>
/// Erzeugt anhand einer URI die passende <see cref="IAppSource"/>.
/// </summary>
public static class RemoteAppSourceFactory
{
    /// <summary>
    /// Erstellt eine Quelle für die angegebene URI.
    /// </summary>
    /// <param name="uri">Pfad, <c>file://</c>- oder <c>http(s)://</c>-URI.</param>
    /// <returns>Die passende Quelle.</returns>
    public static IAppSource Create(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
            throw new ArgumentException("URI fehlt.", nameof(uri));

        if (uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            var cacheRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OldSchoolForms.Ui.Runtime",
                "cache");
            Directory.CreateDirectory(cacheRoot);
            return new CachedAppSource(new RemoteAppSource(new Uri(uri)), cacheRoot);
        }

        if (uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            return new FileSystemAppSource(new Uri(uri).LocalPath);

        return new FileSystemAppSource(uri);
    }
}
