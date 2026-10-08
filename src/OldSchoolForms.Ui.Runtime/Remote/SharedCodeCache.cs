using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OldSchoolForms.Shared.Contracts;

namespace OldSchoolForms.Ui.Runtime.Remote;

/// <summary>
/// Local cache for shared ES6 code downloaded from the server. Files are stored
/// under <c>LocalApplicationData/OldSchoolForms.Ui.Runtime/shared-cache/&lt;app&gt;/&lt;version&gt;/</c>
/// and verified with SHA-256 after download. If the server is unreachable, the
/// most recently cached version is used (offline fallback).
/// </summary>
public sealed class SharedCodeCache
{
    private readonly string _appRoot;
    private string? _versionDir;

    /// <summary>
    /// Creates the cache for an app.
    /// </summary>
    /// <param name="appName">The app name (used for the cache folder).</param>
    public SharedCodeCache(string appName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appName);
        _appRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OldSchoolForms.Ui.Runtime",
            "shared-cache",
            Sanitize(appName));
    }

    /// <summary>The currently active shared code version, or <c>null</c> when nothing is loaded.</summary>
    public string? Version { get; private set; }

    /// <summary>The manifest of the currently active version.</summary>
    public SharedCodeManifest? Manifest { get; private set; }

    /// <summary>
    /// Synchronizes the cache with the server. Downloads all shared files of the
    /// server's current version when the cached version differs.
    /// </summary>
    /// <param name="gateway">The connected gateway client.</param>
    /// <param name="progress">Optional progress callback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">A downloaded file failed its SHA-256 check.</exception>
    public void Sync(RemoteGatewayClient gateway, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gateway);

        var manifest = gateway.GetSharedCodeManifest();
        var targetDir = Path.Combine(_appRoot, Sanitize(manifest.Version));
        var marker = Path.Combine(targetDir, ".synced");

        if (File.Exists(marker) && File.ReadAllText(marker) == manifest.Version)
        {
            Activate(manifest, targetDir);
            progress?.Report($"Shared code up to date (v{manifest.Version}).");
            return;
        }

        if (Directory.Exists(targetDir))
            Directory.Delete(targetDir, true);
        Directory.CreateDirectory(targetDir);

        foreach (var file in manifest.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report($"Downloading shared module '{file.Path}'...");

            var downloaded = gateway.GetSharedModule(file.Path, manifest.Version);
            var content = downloaded.Content
                ?? throw new InvalidOperationException($"Shared module '{file.Path}' has no content.");

            var bytes = Encoding.UTF8.GetBytes(content);
            var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (!string.Equals(hash, file.Sha256?.Trim().ToLowerInvariant(), StringComparison.Ordinal))
                throw new InvalidOperationException($"SHA-256 check failed for shared module '{file.Path}'.");

            var dest = Path.Combine(targetDir, file.Path.Replace('/', Path.DirectorySeparatorChar));
            var destDir = Path.GetDirectoryName(dest);
            if (!string.IsNullOrEmpty(destDir))
                Directory.CreateDirectory(destDir);
            File.WriteAllText(dest, content, Encoding.UTF8);
        }

        File.WriteAllText(Path.Combine(targetDir, "manifest.json"), JsonSerializer.Serialize(manifest), Encoding.UTF8);
        File.WriteAllText(marker, manifest.Version);

        Activate(manifest, targetDir);
        progress?.Report($"Shared code synced (v{manifest.Version}, {manifest.Files.Length} files).");
    }

    /// <summary>
    /// Loads the most recently cached version from disk without contacting the
    /// server. Used as offline fallback.
    /// </summary>
    /// <returns><see langword="true"/> if a cached version was found and loaded.</returns>
    public bool TryLoadCached()
    {
        if (!Directory.Exists(_appRoot)) return false;

        foreach (var dir in Directory.GetDirectories(_appRoot).OrderByDescending(d => d, StringComparer.Ordinal))
        {
            var marker = Path.Combine(dir, ".synced");
            var manifestFile = Path.Combine(dir, "manifest.json");
            if (!File.Exists(marker) || !File.Exists(manifestFile)) continue;

            try
            {
                var manifest = JsonSerializer.Deserialize<SharedCodeManifest>(File.ReadAllText(manifestFile, Encoding.UTF8));
                if (manifest == null) continue;
                Activate(manifest, dir);
                return true;
            }
            catch
            {
                // Corrupt cache entry: skip and try the next one.
            }
        }

        return false;
    }

    /// <summary>
    /// Reads a cached shared file by its relative path.
    /// </summary>
    /// <param name="relativePath">Relative path within the shared directory (e.g. <c>validation/customers.js</c>).</param>
    /// <returns>The file content, or <c>null</c> when not cached.</returns>
    public string? TryRead(string relativePath)
    {
        if (_versionDir == null || string.IsNullOrWhiteSpace(relativePath)) return null;
        if (relativePath.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(relativePath)) return null;

        var full = Path.GetFullPath(Path.Combine(_versionDir, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var rootWithSep = _versionDir.EndsWith(Path.DirectorySeparatorChar)
            ? _versionDir
            : _versionDir + Path.DirectorySeparatorChar;
        if (!full.StartsWith(rootWithSep, StringComparison.Ordinal)) return null;

        return File.Exists(full) ? File.ReadAllText(full, Encoding.UTF8) : null;
    }

    /// <summary>
    /// Checks whether a shared file is present in the active version.
    /// </summary>
    /// <param name="relativePath">Relative path within the shared directory.</param>
    /// <returns><see langword="true"/> if the file is cached.</returns>
    public bool Contains(string relativePath) => TryRead(relativePath) != null;

    private void Activate(SharedCodeManifest manifest, string versionDir)
    {
        Manifest = manifest;
        Version = manifest.Version;
        _versionDir = versionDir;
    }

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
