using System;

namespace OldSchoolForms.Ui.Designer.Services;

/// <summary>
/// Describes a pluggable document kind that the designer can create, open, and save
/// for a specific file extension (for example <c>.report</c> for report documents).
/// Implementations own the mapping between a file on disk and the design surface root.
/// </summary>
public interface IDesignDocumentKind
{
    /// <summary>
    /// Gets the file extension handled by this kind, including the leading dot
    /// (compared case-insensitively).
    /// </summary>
    string Extension { get; }

    /// <summary>
    /// Gets the human-readable display name of this document kind.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Creates a fresh, empty document of this kind on the given surface,
    /// replacing any existing content.
    /// </summary>
    /// <param name="surface">The design surface to install the new document root on.</param>
    void CreateNew(DesignSurface surface);

    /// <summary>
    /// Loads a document of this kind from the given file path and installs its
    /// design root on the surface, replacing any existing content.
    /// </summary>
    /// <param name="surface">The design surface to install the document root on.</param>
    /// <param name="path">The file to load.</param>
    /// <exception cref="System.IO.InvalidDataException">Thrown when the file is not a valid document of this kind.</exception>
    void Load(DesignSurface surface, string path);

    /// <summary>
    /// Saves the current document of this kind from the surface to the given file path.
    /// </summary>
    /// <param name="surface">The design surface holding the document.</param>
    /// <param name="path">The file to write.</param>
    /// <exception cref="InvalidOperationException">Thrown when the surface does not host a root of this kind.</exception>
    void Save(DesignSurface surface, string path);
}
