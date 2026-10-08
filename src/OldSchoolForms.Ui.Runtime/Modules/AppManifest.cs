using System.Text.Json.Nodes;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Runtime.Sources;

namespace OldSchoolForms.Ui.Runtime.Modules;

/// <summary>
/// App manifest (<c>app.json</c>): name, version, shell layout, entry script,
/// and the list of feature modules.
/// </summary>
public sealed class AppManifest
{
    /// <summary>The name of the app.</summary>
    public string Name { get; set; } = "OldSchoolForms App";

    /// <summary>The app version.</summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>Relative path of the shell layout.</summary>
    public string Shell { get; set; } = "layouts/shell.json";

    /// <summary>Relative path of the entry script (optional).</summary>
    public string? Entry { get; set; }

    /// <summary>Fallback UI culture when a translation key is missing in the current culture.</summary>
    public string DefaultCulture { get; set; } = "en";

    /// <summary>Relative directory of the i18n bundles within the package.</summary>
    public string I18nPath { get; set; } = "i18n";

    /// <summary>
    /// The cultures supported by the app. Empty means auto-detection
    /// (probe for <c>de</c> and <c>en</c> bundles in the package).
    /// </summary>
    public List<string> Cultures { get; } = new();

    /// <summary>The feature modules, sorted by <see cref="ModuleDescriptor.Order"/>.</summary>
    public List<ModuleDescriptor> Modules { get; } = new();

    /// <summary>
    /// The optional backend endpoint (<c>server</c> section). When present, the
    /// runtime connects to the CoreRemoting server and exposes <c>ctx.remote</c>
    /// plus <c>@shared/</c> module resolution.
    /// </summary>
    public ServerEndpoint? Server { get; set; }

    /// <summary>
    /// Loads the app manifest from a package source.
    /// </summary>
    /// <param name="source">The package source.</param>
    /// <param name="path">Relative path to the manifest (default: <c>app.json</c>).</param>
    /// <returns>The parsed manifest.</returns>
    /// <exception cref="InvalidOperationException">The manifest is missing or invalid.</exception>
    public static AppManifest Load(IAppSource source, string path = "app.json")
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.Exists(path))
            throw new InvalidOperationException(string.Format(LangRes.GetEnglish("Runtime_AppManifestMissing"), path));

        var doc = ModuleDescriptor.ParseObject(source.ReadText(path), path);
        var manifest = new AppManifest
        {
            Name = ModuleDescriptor.GetString(doc, "name") ?? "OldSchoolForms App",
            Version = ModuleDescriptor.GetString(doc, "version") ?? "1.0.0",
            Shell = ModuleDescriptor.GetString(doc, "shell") ?? "layouts/shell.json",
            Entry = ModuleDescriptor.GetString(doc, "entry"),
            DefaultCulture = ModuleDescriptor.GetString(doc, "defaultCulture") ?? "en",
            I18nPath = ModuleDescriptor.GetString(doc, "i18n") ?? "i18n"
        };

        if (doc["cultures"] is JsonArray cultures)
        {
            foreach (var item in cultures)
            {
                if (item is JsonValue val && val.GetValueKind() == System.Text.Json.JsonValueKind.String)
                    manifest.Cultures.Add(val.GetValue<string>());
            }
        }

        if (doc["server"] is JsonObject serverObj)
        {
            manifest.Server = new ServerEndpoint
            {
                Host = ModuleDescriptor.GetString(serverObj, "host") ?? "localhost",
                Port = serverObj["port"] is JsonValue pv && pv.TryGetValue(out int port) ? port : 9090,
                Encryption = !(serverObj["encryption"] is JsonValue ev && ev.TryGetValue(out bool enc) && !enc)
            };
        }

        if (doc["modules"] is JsonArray modules)
        {
            foreach (var item in modules)
            {
                if (item is JsonValue val && val.GetValueKind() == System.Text.Json.JsonValueKind.String)
                {
                    var dir = val.GetValue<string>();
                    manifest.Modules.Add(ModuleDescriptor.FromDirectory(source, dir));
                }
                else if (item is JsonObject obj)
                {
                    var dir = ModuleDescriptor.GetString(obj, "dir");
                    manifest.Modules.Add(ModuleDescriptor.FromNode(obj, dir));
                }
            }
        }

        manifest.Modules.Sort(static (a, b) =>
        {
            var c = a.Order.CompareTo(b.Order);
            return c != 0 ? c : string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase);
        });

        return manifest;
    }
}

/// <summary>
/// Backend endpoint configuration from the <c>server</c> section of
/// <c>app.json</c>: where the CoreRemoting server listens.
/// </summary>
public sealed class ServerEndpoint
{
    /// <summary>The server host name or address.</summary>
    public string Host { get; set; } = "localhost";

    /// <summary>The server port.</summary>
    public int Port { get; set; } = 9090;

    /// <summary>Whether transport encryption (RSA+AES) is used.</summary>
    public bool Encryption { get; set; } = true;
}
