using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using OldSchoolForms.Ui.Designer.Serialization;
using OldSchoolForms.Ui.Designer.Services;

namespace OldSchoolForms.Ui.Designer.Serialization;

/// <summary>
/// JSON-backed implementation of <see cref="IJsonLayoutService"/> that persists the
/// designed layout in the <c>OldSchoolForms.Layout</c> format using
/// <see cref="System.Text.Json"/>. Works with files, streams and strings.
/// </summary>
public sealed class JsonDocumentService : IJsonLayoutService
{
    /// <summary>
    /// Initializes a new instance with the default namespace and class name.
    /// </summary>
    public JsonDocumentService()
    {
    }

    /// <summary>
    /// Initializes a new instance with an explicit namespace and class name.
    /// </summary>
    /// <param name="namespaceName">The namespace to record in saved documents.</param>
    /// <param name="className">The class name to record in saved documents.</param>
    public JsonDocumentService(string namespaceName, string className)
    {
        Namespace = namespaceName ?? string.Empty;
        ClassName = className ?? string.Empty;
    }

    /// <inheritdoc />
    public string Namespace { get; set; } = "OldSchoolForms.Ui.Demo";

    /// <inheritdoc />
    public string ClassName { get; set; } = "MainForm";

    /// <inheritdoc />
    public string SaveToJson(DesignSurface surface)
    {
        if (surface == null) throw new ArgumentNullException(nameof(surface));
        return LayoutJsonSerializer.SaveToJson(surface, Namespace, ClassName);
    }

    /// <inheritdoc />
    public List<string> LoadFromJson(DesignSurface surface, string json)
    {
        if (surface == null) throw new ArgumentNullException(nameof(surface));
        if (json == null) throw new ArgumentNullException(nameof(json));

        RestoreIdentifiers(json);
        return LayoutJsonSerializer.LoadFromJson(surface, json);
    }

    /// <inheritdoc />
    public void Save(DesignSurface surface, Stream stream)
    {
        if (surface == null) throw new ArgumentNullException(nameof(surface));
        if (stream == null) throw new ArgumentNullException(nameof(stream));

        var json = SaveToJson(surface);
        var bytes = new UTF8Encoding(false).GetBytes(json);
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush();
    }

    /// <inheritdoc />
    public List<string> Load(DesignSurface surface, Stream stream)
    {
        if (surface == null) throw new ArgumentNullException(nameof(surface));
        if (stream == null) throw new ArgumentNullException(nameof(stream));

        using var reader = new StreamReader(stream, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true,
            bufferSize: 4096, leaveOpen: true);
        var json = reader.ReadToEnd();
        return LoadFromJson(surface, json);
    }

    /// <inheritdoc />
    public void SaveToFile(DesignSurface surface, string path)
    {
        if (surface == null) throw new ArgumentNullException(nameof(surface));
        if (path == null) throw new ArgumentNullException(nameof(path));

        var json = SaveToJson(surface);
        File.WriteAllText(path, json, new UTF8Encoding(false));
    }

    /// <inheritdoc />
    public List<string> LoadFromFile(DesignSurface surface, string path)
    {
        if (surface == null) throw new ArgumentNullException(nameof(surface));
        if (path == null) throw new ArgumentNullException(nameof(path));

        var json = File.ReadAllText(path);
        return LoadFromJson(surface, json);
    }

    private void RestoreIdentifiers(string json)
    {
        try
        {
            if (JsonNode.Parse(json) is JsonObject doc)
            {
                var ns = doc["namespace"]?.GetValue<string>();
                var cls = doc["className"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(ns)) Namespace = ns;
                if (!string.IsNullOrEmpty(cls)) ClassName = cls;
            }
        }
        catch
        {
            // Malformed JSON is reported by the serializer itself.
        }
    }
}
