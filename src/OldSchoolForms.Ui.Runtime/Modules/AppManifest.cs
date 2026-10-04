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

    /// <summary>The feature modules, sorted by <see cref="ModuleDescriptor.Order"/>.</summary>
    public List<ModuleDescriptor> Modules { get; } = new();

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
            throw new InvalidOperationException(string.Format(LangRes.GetString("Runtime_AppManifestMissing"), path));

        var doc = ModuleDescriptor.ParseObject(source.ReadText(path), path);
        var manifest = new AppManifest
        {
            Name = ModuleDescriptor.GetString(doc, "name") ?? "OldSchoolForms App",
            Version = ModuleDescriptor.GetString(doc, "version") ?? "1.0.0",
            Shell = ModuleDescriptor.GetString(doc, "shell") ?? "layouts/shell.json",
            Entry = ModuleDescriptor.GetString(doc, "entry")
        };

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
