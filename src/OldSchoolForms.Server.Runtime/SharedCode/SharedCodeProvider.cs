using System.Security.Cryptography;
using System.Text;
using OldSchoolForms.Server.Runtime.Manifest;
using OldSchoolForms.Shared.Contracts;

namespace OldSchoolForms.Server.Runtime.SharedCode;

/// <summary>
/// Serves the shared ES6 code bundle to clients over the CoreRemoting
/// gateway. Only files inside the declared shared directory are accessible;
/// path traversal is rejected. Each file is hashed with SHA-256 so clients
/// can verify integrity and cache by version.
/// </summary>
public sealed class SharedCodeProvider
{
    private static readonly string[] AllowedExtensions = { ".js", ".mjs", ".json" };

    private readonly string _sharedRoot;
    private readonly string _appName;
    private readonly string _version;

    /// <summary>
    /// Creates the provider.
    /// </summary>
    /// <param name="packageRoot">Absolute server package directory.</param>
    /// <param name="descriptor">The shared bundle descriptor from <c>server.json</c>.</param>
    /// <param name="appName">App name reported in the manifest.</param>
    public SharedCodeProvider(string packageRoot, SharedDescriptor descriptor, string appName)
    {
        ArgumentNullException.ThrowIfNull(packageRoot);
        ArgumentNullException.ThrowIfNull(descriptor);
        _sharedRoot = Path.GetFullPath(Path.Combine(packageRoot, descriptor.Dir));
        _appName = appName;
        _version = descriptor.Version;
    }

    /// <summary>
    /// Builds the manifest of all shared files with hashes and sizes
    /// (without content).
    /// </summary>
    /// <returns>The shared code manifest.</returns>
    public SharedCodeManifest GetManifest()
    {
        var manifest = new SharedCodeManifest { AppName = _appName, Version = _version };
        if (!Directory.Exists(_sharedRoot))
            return manifest;

        var files = new List<SharedCodeFile>();
        foreach (var absolute in Directory.EnumerateFiles(_sharedRoot, "*", SearchOption.AllDirectories)
                     .Where(f => AllowedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                     .OrderBy(f => f, StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(_sharedRoot, absolute).Replace('\\', '/');
            var content = File.ReadAllText(absolute);
            files.Add(new SharedCodeFile
            {
                Path = relative,
                Sha256 = ComputeSha256(content),
                Size = Encoding.UTF8.GetByteCount(content),
                Version = _version
            });
        }

        manifest.Files = files.ToArray();
        return manifest;
    }

    /// <summary>
    /// Gets one shared module file including its content.
    /// </summary>
    /// <param name="path">Relative module path.</param>
    /// <param name="expectedVersion">The version the client already has (informational).</param>
    /// <returns>The file with content.</returns>
    /// <exception cref="InvalidOperationException">The path is invalid or the file is missing.</exception>
    public SharedCodeFile GetFile(string path, string? expectedVersion)
    {
        var absolute = ResolveSafe(path);
        if (!File.Exists(absolute))
            throw new InvalidOperationException($"Shared module not found: {path}");

        var content = File.ReadAllText(absolute);
        return new SharedCodeFile
        {
            Path = path.Replace('\\', '/'),
            Sha256 = ComputeSha256(content),
            Size = Encoding.UTF8.GetByteCount(content),
            Version = _version,
            Content = content
        };
    }

    private string ResolveSafe(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException("Shared module path must not be empty.");

        var normalized = path.Replace('\\', '/');
        if (normalized.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(normalized))
            throw new InvalidOperationException($"Invalid shared module path: {path}");

        var extension = Path.GetExtension(normalized).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            throw new InvalidOperationException($"Shared module type not allowed: {extension}");

        var absolute = Path.GetFullPath(Path.Combine(_sharedRoot, normalized));
        if (!absolute.StartsWith(_sharedRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException($"Shared module path escapes the shared directory: {path}");

        return absolute;
    }

    private static string ComputeSha256(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
