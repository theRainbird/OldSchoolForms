using System.Text.Json;
using System.Text.Json.Nodes;
using OldSchoolForms.Ui.Runtime.Sources;

namespace OldSchoolForms.Ui.Runtime.Modules;

/// <summary>
/// Beschreibt einen Funktionsbaustein (Modul) des Apps: Titel, Icon, Layout- und
/// Skriptdatei sowie die Reihenfolge in der Navigation.
/// </summary>
public sealed class ModuleDescriptor
{
    /// <summary>Eindeutige Modul-ID.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Anzeigename in der Navigation.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Optionaler Icon-Referenzname.</summary>
    public string? Icon { get; set; }

    /// <summary>Relativer Pfad des Layouts innerhalb des Modulverzeichnisses.</summary>
    public string Layout { get; set; } = "layout.json";

    /// <summary>Relativer Pfad des Skripts innerhalb des Modulverzeichnisses.</summary>
    public string Script { get; set; } = "index.js";

    /// <summary>Sortierordnung in der Navigation.</summary>
    public int Order { get; set; }

    /// <summary>Relatives Verzeichnis des Moduls im Paket.</summary>
    public string Directory { get; set; } = string.Empty;

    /// <summary>Optionale Versionsangabe (für Remote-Abgleich).</summary>
    public string? Version { get; set; }

    /// <summary>Absolute Paketpfad des Layouts.</summary>
    public string LayoutPath => Combine(Directory, Layout);

    /// <summary>Absolute Paketpfad des Skripts.</summary>
    public string ScriptPath => Combine(Directory, Script);

    private static string Combine(string dir, string file)
    {
        if (string.IsNullOrEmpty(dir)) return file;
        return dir.TrimEnd('/') + "/" + file.TrimStart('/');
    }

    /// <summary>
    /// Liest einen Moduldeskriptor aus einer <c>module.json</c> im angegebenen Verzeichnis.
    /// </summary>
    /// <param name="source">Die Paketquelle.</param>
    /// <param name="directory">Das relative Modulverzeichnis.</param>
    /// <returns>Der Deskriptor.</returns>
    /// <exception cref="InvalidOperationException">Die <c>module.json</c> fehlt oder ist ungültig.</exception>
    public static ModuleDescriptor FromDirectory(IAppSource source, string directory)
    {
        var manifestPath = Combine(directory, "module.json");
        if (!source.Exists(manifestPath))
            throw new InvalidOperationException($"Modul-Manifest fehlt: {manifestPath}");

        var node = ParseObject(source.ReadText(manifestPath), manifestPath);
        return FromNode(node, directory);
    }

    internal static ModuleDescriptor FromNode(JsonObject node, string? defaultDirectory = null)
    {
        var descriptor = new ModuleDescriptor
        {
            Id = GetString(node, "id") ?? string.Empty,
            Title = GetString(node, "title") ?? string.Empty,
            Icon = GetString(node, "icon"),
            Layout = GetString(node, "layout") ?? "layout.json",
            Script = GetString(node, "script") ?? "index.js",
            Order = GetInt(node, "order", 0),
            Directory = GetString(node, "dir") ?? defaultDirectory ?? string.Empty,
            Version = GetString(node, "version")
        };

        if (string.IsNullOrEmpty(descriptor.Id))
            throw new InvalidOperationException("Modul-Manifest ohne 'id'.");
        if (string.IsNullOrEmpty(descriptor.Title))
            descriptor.Title = descriptor.Id;

        return descriptor;
    }

    internal static JsonObject ParseObject(string json, string path)
    {
        try
        {
            if (JsonNode.Parse(json) is JsonObject obj) return obj;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Ungültiges JSON in '{path}': {ex.Message}", ex);
        }

        throw new InvalidOperationException($"JSON-Wurzel in '{path}' ist kein Objekt.");
    }

    internal static string? GetString(JsonObject obj, string key)
    {
        foreach (var kvp in obj)
            if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                return kvp.Value?.GetValue<string>();
        return null;
    }

    internal static int GetInt(JsonObject obj, string key, int fallback)
    {
        foreach (var kvp in obj)
        {
            if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                try { return kvp.Value?.GetValue<int>() ?? fallback; }
                catch { return fallback; }
            }
        }

        return fallback;
    }
}
