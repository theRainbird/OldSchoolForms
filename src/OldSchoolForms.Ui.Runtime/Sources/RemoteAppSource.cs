using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OldSchoolForms.Ui.Runtime.Sources;

/// <summary>
/// Netzwerkquelle: liest das Server-Manifest und lädt Modul-Bundles (ZIP) von
/// einem zentralen OldSchoolForms-Server. Wird typischerweise von
/// <see cref="CachedAppSource"/> für Offline-Fähigkeit umschlossen.
/// </summary>
public sealed class RemoteAppSource : IDisposable
{
    private readonly Uri _baseUri;
    private readonly HttpClient _http;

    /// <summary>
    /// Erzeugt eine Netzwerkquelle.
    /// </summary>
    /// <param name="baseUri">Basis-URL des App-Servers (z. B. <c>https://host/app</c>).</param>
    /// <param name="http">Optionaler HttpClient.</param>
    public RemoteAppSource(Uri baseUri, HttpClient? http = null)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        _baseUri = baseUri;
        _http = http ?? new HttpClient();
    }

    /// <summary>Die Basis-URL.</summary>
    public Uri BaseUri => _baseUri;

    /// <summary>
    /// Lädt das Server-Manifest (<c>manifest.json</c>).
    /// </summary>
    /// <param name="cancellationToken">Abbruchtoken.</param>
    /// <returns>Das Server-Manifest.</returns>
    /// <exception cref="InvalidOperationException">Manifest fehlt oder ist ungültig.</exception>
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
            throw new InvalidOperationException($"Server-Manifest nicht erreichbar: {url} ({ex.Message})", ex);
        }

        try
        {
            return RemoteManifest.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Ungültiges Server-Manifest: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Lädt eine Bundle-Datei (ZIP) als Bytefolge.
    /// </summary>
    /// <param name="url">Absolute oder relative URL des Bundles.</param>
    /// <param name="cancellationToken">Abbruchtoken.</param>
    /// <returns>Die Bundle-Bytes.</returns>
    public async Task<byte[]> FetchBundleAsync(string url, CancellationToken cancellationToken = default)
    {
        var full = IsAbsolute(url) ? url : Combine(url);
        try
        {
            return await _http.GetByteArrayAsync(full, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Bundle nicht erreichbar: {full} ({ex.Message})", ex);
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
/// Repräsentation des Server-Manifests (<c>manifest.json</c>).
/// </summary>
public sealed class RemoteManifest
{
    /// <summary>App-Name.</summary>
    public string Name { get; set; } = "OldSchoolForms App";

    /// <summary>App-Version.</summary>
    public string Version { get; set; } = "0.0.0";

    /// <summary>Shell-Bundle-Referenz.</summary>
    public RemoteBundleRef Shell { get; set; } = new();

    /// <summary>Einstiegs-Skript (optional).</summary>
    public string? Entry { get; set; }

    /// <summary>Modul-Bundle-Referenzen.</summary>
    public List<RemoteModuleRef> Modules { get; } = new();

    /// <summary>
    /// Parst ein Server-Manifest aus JSON.
    /// </summary>
    /// <param name="json">Das JSON.</param>
    /// <returns>Das geparste Manifest.</returns>
    public static RemoteManifest Parse(string json)
    {
        var doc = JsonNode.Parse(json) as JsonObject
            ?? throw new InvalidOperationException("Server-Manifest-Wurzel ist kein Objekt.");

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
/// Referenz auf ein Bundle (URL + Prüfsumme).
/// </summary>
public sealed class RemoteBundleRef
{
    /// <summary>URL des Bundles.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>SHA-256-Prüfsumme (hex), optional.</summary>
    public string? Sha256 { get; set; }
}

/// <summary>
/// Referenz auf ein Modul-Bundle.
/// </summary>
public sealed class RemoteModuleRef
{
    /// <summary>Modul-ID.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Modul-Version.</summary>
    public string? Version { get; set; }

    /// <summary>URL des Modul-Bundles.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>SHA-256-Prüfsumme (hex), optional.</summary>
    public string? Sha256 { get; set; }
}
