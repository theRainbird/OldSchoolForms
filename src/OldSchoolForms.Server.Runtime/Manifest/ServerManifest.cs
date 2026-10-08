using System.Text.Json;

namespace OldSchoolForms.Server.Runtime.Manifest;

/// <summary>
/// Server manifest (<c>server.json</c>): declares the network endpoint, the
/// business-logic services (ES6 scripts) and the shared code directory that
/// clients can download.
/// </summary>
public sealed class ServerManifest
{
    /// <summary>Name of the server application.</summary>
    public string Name { get; set; } = "OldSchoolForms Server";

    /// <summary>Server version.</summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>Host/address to bind to (default: all interfaces).</summary>
    public string Host { get; set; } = "0.0.0.0";

    /// <summary>TCP port for CoreRemoting (default: 9090).</summary>
    public int Port { get; set; } = 9090;

    /// <summary>Whether to encrypt the CoreRemoting channel (RSA + AES).</summary>
    public bool Encryption { get; set; } = true;

    /// <summary>The business-logic services hosted by this server.</summary>
    public List<ServiceDescriptor> Services { get; } = new();

    /// <summary>The shared ES6 code bundle (optional).</summary>
    public SharedDescriptor? Shared { get; set; }

    /// <summary>
    /// Loads the manifest from a package directory.
    /// </summary>
    /// <param name="packageDir">Absolute or relative directory containing <c>server.json</c>.</param>
    /// <param name="fileName">Manifest file name (default <c>server.json</c>).</param>
    /// <returns>The parsed manifest.</returns>
    /// <exception cref="InvalidOperationException">The manifest is missing or invalid.</exception>
    public static ServerManifest Load(string packageDir, string fileName = "server.json")
    {
        var path = Path.Combine(packageDir, fileName);
        if (!File.Exists(path))
            throw new InvalidOperationException($"Server manifest not found: {path}");

        var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        });

        var root = doc.RootElement;
        var manifest = new ServerManifest
        {
            Name = GetString(root, "name") ?? "OldSchoolForms Server",
            Version = GetString(root, "version") ?? "1.0.0",
            Host = GetString(root, "host") ?? "0.0.0.0",
            Port = root.TryGetProperty("port", out var port) ? port.GetInt32() : 9090,
            Encryption = !root.TryGetProperty("encryption", out var enc) || enc.GetBoolean()
        };

        if (root.TryGetProperty("services", out var services) && services.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in services.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                manifest.Services.Add(ServiceDescriptor.FromJson(item));
            }
        }

        if (root.TryGetProperty("shared", out var shared) && shared.ValueKind == JsonValueKind.Object)
            manifest.Shared = SharedDescriptor.FromJson(shared);

        return manifest;
    }

    internal static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
}

/// <summary>
/// Describes one business-logic service: its ID, the ES6 script that
/// implements it and its lifetime.
/// </summary>
public sealed class ServiceDescriptor
{
    /// <summary>Service ID used in gateway calls.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Script path relative to the package directory.</summary>
    public string Script { get; set; } = string.Empty;

    /// <summary>Lifetime: <c>singleton</c>, <c>singlecall</c> or <c>scoped</c>.</summary>
    public string Lifetime { get; set; } = "singleton";

    /// <summary>Optional human-readable description.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// Parses a service descriptor from a JSON object.
    /// </summary>
    /// <param name="json">The JSON object element.</param>
    /// <returns>The parsed descriptor.</returns>
    public static ServiceDescriptor FromJson(JsonElement json) => new()
    {
        Id = ServerManifest.GetString(json, "id") ?? throw new InvalidOperationException("Service is missing 'id'."),
        Script = ServerManifest.GetString(json, "script") ?? throw new InvalidOperationException("Service is missing 'script'."),
        Lifetime = ServerManifest.GetString(json, "lifetime") ?? "singleton",
        Description = ServerManifest.GetString(json, "description")
    };
}

/// <summary>
/// Describes the shared ES6 code bundle served to clients.
/// </summary>
public sealed class SharedDescriptor
{
    /// <summary>Directory (relative to the package) containing the shared modules.</summary>
    public string Dir { get; set; } = "shared";

    /// <summary>Version of the shared bundle.</summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Parses the shared descriptor from a JSON object.
    /// </summary>
    /// <param name="json">The JSON object element.</param>
    /// <returns>The parsed descriptor.</returns>
    public static SharedDescriptor FromJson(JsonElement json) => new()
    {
        Dir = ServerManifest.GetString(json, "dir") ?? "shared",
        Version = ServerManifest.GetString(json, "version") ?? "1.0.0"
    };
}
