using System.Text.Json.Nodes;
using OldSchoolForms.Ui.Runtime.Sources;

namespace OldSchoolForms.Ui.Runtime.Modules;

/// <summary>
/// App-Manifest (<c>app.json</c>): Name, Version, Shell-Layout, Einstiegs-Skript
/// und die Liste der Funktionsmodule.
/// </summary>
public sealed class AppManifest
{
    /// <summary>Der Name der App.</summary>
    public string Name { get; set; } = "OldSchoolForms App";

    /// <summary>Die App-Version.</summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>Relativer Pfad des Shell-Layouts.</summary>
    public string Shell { get; set; } = "layouts/shell.json";

    /// <summary>Relativer Pfad des Einstiegs-Skripts (optional).</summary>
    public string? Entry { get; set; }

    /// <summary>Die Funktionsmodule, sortiert nach <see cref="ModuleDescriptor.Order"/>.</summary>
    public List<ModuleDescriptor> Modules { get; } = new();

    /// <summary>
    /// Lädt das App-Manifest aus einer Paketquelle.
    /// </summary>
    /// <param name="source">Die Paketquelle.</param>
    /// <param name="path">Relativer Pfad zum Manifest (Standard: <c>app.json</c>).</param>
    /// <returns>Das geparste Manifest.</returns>
    /// <exception cref="InvalidOperationException">Das Manifest fehlt oder ist ungültig.</exception>
    public static AppManifest Load(IAppSource source, string path = "app.json")
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.Exists(path))
            throw new InvalidOperationException($"App-Manifest fehlt: {path}");

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
