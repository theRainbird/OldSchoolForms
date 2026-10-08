using OldSchoolForms.Ui.Core;

namespace OldSchoolForms.Ui.Runtime.Scripting;

/// <summary>
/// Helper functions for normalizing module paths within an app package.
/// All internal module keys are relative paths with forward slashes,
/// e.g. <c>modules/customers/index.js</c>.
/// </summary>
public static class ModulePath
{
    /// <summary>Prefix for absolute references to the package root (<c>@app/lib/util.js</c>).</summary>
    public const string AppPrefix = "@app/";

    /// <summary>
    /// Normalizes a relative path: resolves <c>.</c> and <c>..</c> segments and
    /// prevents the path from pointing outside the package root.
    /// </summary>
    /// <param name="path">The path to normalize.</param>
    /// <returns>The normalized path.</returns>
    /// <exception cref="InvalidOperationException">The path points outside the package root.</exception>
    public static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException(LangRes.GetEnglish("Runtime_ModulePathEmpty"));

        var parts = path.Replace('\\', '/').Split('/');
        var stack = new List<string>();
        foreach (var part in parts)
        {
            if (part.Length == 0 || part == ".") continue;
            if (part == "..")
            {
                if (stack.Count == 0)
                    throw new InvalidOperationException(string.Format(LangRes.GetEnglish("Runtime_ModulePathEscapesRoot"), path));
                stack.RemoveAt(stack.Count - 1);
                continue;
            }

            stack.Add(part);
        }

        if (stack.Count == 0)
            throw new InvalidOperationException(string.Format(LangRes.GetEnglish("Runtime_ModulePathEmptyAfterNormalize"), path));

        return string.Join("/", stack);
    }

    /// <summary>
    /// Resolves an import specifier relative to the directory of the referencing module.
    /// </summary>
    /// <param name="referencingKey">The key of the referencing module (empty = package root).</param>
    /// <param name="specifier">The imported specifier.</param>
    /// <returns>The normalized package-relative key.</returns>
    /// <exception cref="InvalidOperationException">The specifier cannot be resolved.</exception>
    public static string ResolveRelative(string referencingKey, string specifier)
    {
        if (specifier.StartsWith(AppPrefix, StringComparison.Ordinal))
            return Normalize(specifier[AppPrefix.Length..]);

        if (specifier.StartsWith("./", StringComparison.Ordinal) ||
            specifier.StartsWith("../", StringComparison.Ordinal))
        {
            var dir = GetDirectory(referencingKey);
            var combined = string.IsNullOrEmpty(dir) ? specifier : dir + "/" + specifier;
            return Normalize(combined);
        }

        if (specifier.StartsWith("/", StringComparison.Ordinal))
            return Normalize(specifier);

        // Bare specifier: relative to the package root (shared libraries).
        return Normalize(specifier);
    }

    /// <summary>
    /// Returns the directory of a key (without the file name).
    /// </summary>
    /// <param name="key">The module key.</param>
    /// <returns>The directory, or an empty string for the root level.</returns>
    public static string GetDirectory(string key)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;
        var idx = key.LastIndexOf('/');
        return idx < 0 ? string.Empty : key[..idx];
    }
}
