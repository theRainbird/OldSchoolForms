using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OldSchoolForms.Ui.Designer.Services;

/// <summary>
/// A registry of <see cref="IDesignDocumentKind"/> implementations keyed by file
/// extension. The designer queries this registry to dispatch open/save operations
/// for document kinds beyond the built-in C# and JSON form layouts.
/// </summary>
public static class DocumentKindRegistry
{
    private static readonly Dictionary<string, IDesignDocumentKind> Kinds = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a document kind for its <see cref="IDesignDocumentKind.Extension"/>.
    /// Registering a second kind for the same extension replaces the previous one.
    /// </summary>
    /// <param name="kind">The document kind to register.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="kind"/> is null.</exception>
    public static void Register(IDesignDocumentKind kind)
    {
        if (kind == null) throw new ArgumentNullException(nameof(kind));
        Kinds[kind.Extension] = kind;
    }

    /// <summary>
    /// Looks up the document kind that handles the given file path by its extension.
    /// </summary>
    /// <param name="path">The file path to inspect.</param>
    /// <param name="kind">The matching kind, when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when a registered kind handles the extension.</returns>
    public static bool TryGet(string path, out IDesignDocumentKind kind)
    {
        var ext = Path.GetExtension(path);
        if (!string.IsNullOrEmpty(ext) && Kinds.TryGetValue(ext, out var found))
        {
            kind = found;
            return true;
        }
        kind = null!;
        return false;
    }

    /// <summary>
    /// Gets all currently registered document kinds.
    /// </summary>
    public static IReadOnlyCollection<IDesignDocumentKind> All => Kinds.Values.ToList();

    /// <summary>
    /// Removes all registered document kinds. Intended for tests.
    /// </summary>
    public static void Clear() => Kinds.Clear();
}
