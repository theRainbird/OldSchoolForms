using System.Text.Json;
using System.Text.Json.Nodes;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Runtime.Sources;

namespace OldSchoolForms.Ui.Runtime.Modules;

/// <summary>
/// Describes a functional unit (module) of the app: title, icon, layout and
/// script file, and the order in the navigation.
/// </summary>
public sealed class ModuleDescriptor
{
    /// <summary>Unique module ID.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Display name in the navigation.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Optional i18n key for the display title. When set and a localization
    /// service is available, the resolved text overrides <see cref="Title"/>.
    /// </summary>
    public string TitleKey { get; set; } = string.Empty;

    /// <summary>Optional icon reference name.</summary>
    public string? Icon { get; set; }

    /// <summary>Relative path of the layout within the module directory.</summary>
    public string Layout { get; set; } = "layout.json";

    /// <summary>Relative path of the script within the module directory.</summary>
    public string Script { get; set; } = "index.js";

    /// <summary>Sort order in the navigation.</summary>
    public int Order { get; set; }

    /// <summary>The module's relative directory in the package.</summary>
    public string Directory { get; set; } = string.Empty;

    /// <summary>Optional version information (for remote synchronization).</summary>
    public string? Version { get; set; }

    /// <summary>Absolute package path of the layout.</summary>
    public string LayoutPath => Combine(Directory, Layout);

    /// <summary>Absolute package path of the script.</summary>
    public string ScriptPath => Combine(Directory, Script);

    private static string Combine(string dir, string file)
    {
        if (string.IsNullOrEmpty(dir)) return file;
        return dir.TrimEnd('/') + "/" + file.TrimStart('/');
    }

    /// <summary>
    /// Resolves the display title: the localized <see cref="TitleKey"/> when a
    /// localization service is available and the key is set, otherwise the
    /// literal <see cref="Title"/>.
    /// </summary>
    /// <param name="i18n">The localization service (may be <c>null</c>).</param>
    /// <returns>The display title for the current culture.</returns>
    public string GetTitle(Localization.RuntimeI18n? i18n)
    {
        if (i18n != null && !string.IsNullOrEmpty(TitleKey))
            return i18n.T(TitleKey);
        return Title;
    }

    /// <summary>
    /// Reads a module descriptor from a <c>module.json</c> in the specified directory.
    /// </summary>
    /// <param name="source">The package source.</param>
    /// <param name="directory">The relative module directory.</param>
    /// <returns>The descriptor.</returns>
    /// <exception cref="InvalidOperationException">The <c>module.json</c> is missing or invalid.</exception>
    public static ModuleDescriptor FromDirectory(IAppSource source, string directory)
    {
        var manifestPath = Combine(directory, "module.json");
        if (!source.Exists(manifestPath))
            throw new InvalidOperationException(string.Format(LangRes.GetEnglish("Runtime_ModuleManifestMissing"), manifestPath));

        var node = ParseObject(source.ReadText(manifestPath), manifestPath);
        return FromNode(node, directory);
    }

    internal static ModuleDescriptor FromNode(JsonObject node, string? defaultDirectory = null)
    {
        var descriptor = new ModuleDescriptor
        {
            Id = GetString(node, "id") ?? string.Empty,
            Title = GetString(node, "title") ?? string.Empty,
            TitleKey = GetString(node, "titleKey") ?? string.Empty,
            Icon = GetString(node, "icon"),
            Layout = GetString(node, "layout") ?? "layout.json",
            Script = GetString(node, "script") ?? "index.js",
            Order = GetInt(node, "order", 0),
            Directory = GetString(node, "dir") ?? defaultDirectory ?? string.Empty,
            Version = GetString(node, "version")
        };

        if (string.IsNullOrEmpty(descriptor.Id))
            throw new InvalidOperationException(LangRes.GetEnglish("Runtime_ModuleManifestWithoutId"));
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
            throw new InvalidOperationException(string.Format(LangRes.GetEnglish("Runtime_InvalidJsonInFile"), path, ex.Message), ex);
        }

        throw new InvalidOperationException(string.Format(LangRes.GetEnglish("Runtime_JsonRootNotObjectInFile"), path));
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
