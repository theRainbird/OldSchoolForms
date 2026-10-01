using System;
using System.Collections.Generic;
using System.IO;

namespace OldSchoolForms.Ui.Designer.Services;

/// <summary>
/// Abstraction for saving and loading designed layouts as JSON documents
/// (<c>OldSchoolForms.Layout</c> format). Unlike <see cref="IDesignDocumentService"/>,
/// which round-trips through generated C# source code, this service persists the
/// layout as a platform-neutral JSON file or stream while keeping the namespace
/// and class name so C# code can still be regenerated afterwards.
/// </summary>
public interface IJsonLayoutService
{
    /// <summary>
    /// Gets or sets the namespace recorded in saved documents and restored on load.
    /// Used for later C# code regeneration.
    /// </summary>
    string Namespace { get; set; }

    /// <summary>
    /// Gets or sets the class name recorded in saved documents and restored on load.
    /// Used for later C# code regeneration.
    /// </summary>
    string ClassName { get; set; }

    /// <summary>
    /// Serializes the current surface state to an indented JSON string.
    /// </summary>
    /// <param name="surface">The design surface to serialize.</param>
    /// <returns>The layout JSON document.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="surface"/> is null.</exception>
    string SaveToJson(DesignSurface surface);

    /// <summary>
    /// Replaces the surface content with the layout described by the given JSON string.
    /// </summary>
    /// <param name="surface">The design surface to populate.</param>
    /// <param name="json">The layout JSON document.</param>
    /// <returns>A list of warnings produced while loading (unknown types or properties).</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="surface"/> or <paramref name="json"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the JSON document is malformed.</exception>
    List<string> LoadFromJson(DesignSurface surface, string json);

    /// <summary>
    /// Writes the current surface state as a JSON document to the given stream.
    /// </summary>
    /// <param name="surface">The design surface to serialize.</param>
    /// <param name="stream">The target stream; not closed by this method.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="surface"/> or <paramref name="stream"/> is null.</exception>
    void Save(DesignSurface surface, Stream stream);

    /// <summary>
    /// Reads a JSON layout document from the given stream and populates the surface.
    /// </summary>
    /// <param name="surface">The design surface to populate.</param>
    /// <param name="stream">The source stream; not closed by this method.</param>
    /// <returns>A list of warnings produced while loading (unknown types or properties).</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="surface"/> or <paramref name="stream"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the JSON document is malformed.</exception>
    List<string> Load(DesignSurface surface, Stream stream);

    /// <summary>
    /// Writes the current surface state as a UTF-8 JSON file.
    /// </summary>
    /// <param name="surface">The design surface to serialize.</param>
    /// <param name="path">The file path to write to.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="surface"/> or <paramref name="path"/> is null.</exception>
    void SaveToFile(DesignSurface surface, string path);

    /// <summary>
    /// Reads a UTF-8 JSON layout file and populates the surface.
    /// </summary>
    /// <param name="surface">The design surface to populate.</param>
    /// <param name="path">The file path to read from.</param>
    /// <returns>A list of warnings produced while loading (unknown types or properties).</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="surface"/> or <paramref name="path"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the JSON document is malformed.</exception>
    List<string> LoadFromFile(DesignSurface surface, string path);
}
