using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using OldSchoolForms.Ui.Core;

namespace OldSchoolForms.Ui.Runtime.Sources;

/// <summary>
/// Network source: reads the server manifest and downloads module bundles (ZIP) from
/// a central OldSchoolForms server. Typically wrapped by
/// <see cref="CachedAppSource"/> for offline capability.
/// </summary>
public sealed class RemoteAppSource : IDisposable
{
    private readonly Uri _baseUri;
    private readonly HttpClient _http;

    /// <summary>
    /// Creates a network source.
    /// </summary>
    /// <param name="baseUri">Base URL of the app server (e.g. <c>https://host/app</c>).</param>
    /// <param name="http">Optional HttpClient.</param>
    public RemoteAppSource(Uri baseUri, HttpClient? http = null)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        _baseUri = baseUri;
        _http = http ?? new HttpClient();
    }

    /// <summary>The base URL.</summary>
    public Uri BaseUri => _baseUri;

    /// <summary>
    /// Loads the server manifest (<c>manifest.json</c>).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The server manifest.</returns>
    /// <exception cref="InvalidOperationException">Manifest is missing or invalid.</exception>
    public async Task<RemoteManifest> FetchManifestAsync(CancellationToken cancellationToken = default)
    {
        var url = Combine("manifest.json");
        string json;
        try
        {
            json = await _http.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(string.Format(LangRes.GetEnglish("Runtime_ServerManifestUnreachable"), url, ex.Message), ex);
        }

        try
        {
            return RemoteManifest.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(string.Format(LangRes.GetEnglish("Runtime_InvalidServerManifest"), ex.Message), ex);
        }
    }

    /// <summary>
    /// Downloads a bundle file (ZIP) as a byte array.
    /// </summary>
    /// <param name="url">Absolute or relative URL of the bundle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The bundle bytes.</returns>
    public async Task<byte[]> FetchBundleAsync(string url, CancellationToken cancellationToken = default)
    {
        var full = IsAbsolute(url) ? url : Combine(url);
        try
        {
            return await _http.GetByteArrayAsync(full, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(string.Format(LangRes.GetEnglish("Runtime_BundleUnreachable"), full, ex.Message), ex);
        }
    }

    private string Combine(string relative)
    {
        var trimmed = _baseUri.ToString().TrimEnd('/');
        return trimmed + "/" + relative.TrimStart('/');
    }

    private static bool IsAbsolute(string url) =>
        url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public void Dispose() => _http.Dispose();
}

/// <summary>
/// Representation of the server manifest (<c>manifest.json</c>).
/// </summary>
public sealed class RemoteManifest
{
    /// <summary>App name.</summary>
    public string Name { get; set; } = "OldSchoolForms App";

    /// <summary>App version.</summary>
    public string Version { get; set; } = "0.0.0";

    /// <summary>Shell bundle reference.</summary>
    public RemoteBundleRef Shell { get; set; } = new();

    /// <summary>Entry script (optional).</summary>
    public string? Entry { get; set; }

    /// <summary>Module bundle references.</summary>
    public List<RemoteModuleRef> Modules { get; } = new();

    /// <summary>
    /// Parses a server manifest from JSON.
    /// </summary>
    /// <param name="json">The JSON.</param>
    /// <returns>The parsed manifest.</returns>
    public static RemoteManifest Parse(string json)
    {
        var doc = JsonNode.Parse(json) as JsonObject
            ?? throw new InvalidOperationException(LangRes.GetEnglish("Runtime_ServerManifestRootNotObject"));

        var manifest = new RemoteManifest
        {
            Name = GetStr(doc, "name") ?? "OldSchoolForms App",
            Version = GetStr(doc, "version") ?? "0.0.0",
            Entry = GetStr(doc, "entry")
        };

        if (GetNode(doc, "shell") is JsonObject shell)
            manifest.Shell = new RemoteBundleRef
            {
                Url = GetStr(shell, "url") ?? "shell.zip",
                Sha256 = GetStr(shell, "sha256")
            };

        if (GetNode(doc, "modules") is JsonArray modules)
        {
            foreach (var m in modules)
            {
                if (m is JsonObject mo)
                {
                    manifest.Modules.Add(new RemoteModuleRef
                    {
                        Id = GetStr(mo, "id") ?? string.Empty,
                        Version = GetStr(mo, "version"),
                        Url = GetStr(mo, "url") ?? string.Empty,
                        Sha256 = GetStr(mo, "sha256")
                    });
                }
            }
        }

        return manifest;
    }

    private static string? GetStr(JsonObject obj, string key)
    {
        foreach (var kvp in obj)
            if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                return kvp.Value?.GetValue<string>();
        return null;
    }

    private static JsonNode? GetNode(JsonObject obj, string key)
    {
        foreach (var kvp in obj)
            if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                return kvp.Value;
        return null;
    }
}

/// <summary>
/// Reference to a bundle (URL + checksum).
/// </summary>
public sealed class RemoteBundleRef
{
    /// <summary>URL of the bundle.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>SHA-256 checksum (hex), optional.</summary>
    public string? Sha256 { get; set; }
}

/// <summary>
/// Reference to a module bundle.
/// </summary>
public sealed class RemoteModuleRef
{
    /// <summary>Module ID.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Module version.</summary>
    public string? Version { get; set; }

    /// <summary>URL of the module bundle.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>SHA-256 checksum (hex), optional.</summary>
    public string? Sha256 { get; set; }
}
