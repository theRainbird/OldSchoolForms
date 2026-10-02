namespace OldSchoolForms.Ui.Runtime.Scripting;

/// <summary>
/// Hilfsfunktionen zur Normalisierung von Modulpfaden innerhalb eines App-Pakets.
/// Alle internen Modulschlüssel sind relative Pfade mit Vorwärtsschrägstrichen,
/// z. B. <c>modules/customers/index.js</c>.
/// </summary>
public static class ModulePath
{
    /// <summary>Präfix für absolute Referenzen auf die Paketwurzel (<c>@app/lib/util.js</c>).</summary>
    public const string AppPrefix = "@app/";

    /// <summary>
    /// Normalisiert einen relativen Pfad: löst <c>.</c> und <c>..</c>-Segmente auf
    /// und verhindert, dass der Pfad über die Paketwurzel hinauszeigt.
    /// </summary>
    /// <param name="path">Der zu normalisierende Pfad.</param>
    /// <returns>Der normalisierte Pfad.</returns>
    /// <exception cref="InvalidOperationException">Der Pfad zeigt über die Paketwurzel hinaus.</exception>
    public static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException("Modulpfad darf nicht leer sein.");

        var parts = path.Replace('\\', '/').Split('/');
        var stack = new List<string>();
        foreach (var part in parts)
        {
            if (part.Length == 0 || part == ".") continue;
            if (part == "..")
            {
                if (stack.Count == 0)
                    throw new InvalidOperationException($"Modulpfad zeigt über die Paketwurzel: {path}");
                stack.RemoveAt(stack.Count - 1);
                continue;
            }

            stack.Add(part);
        }

        if (stack.Count == 0)
            throw new InvalidOperationException($"Modulpfad ist leer nach Normalisierung: {path}");

        return string.Join("/", stack);
    }

    /// <summary>
    /// Löst einen Import-Spezifizierer relativ zum Verzeichnis des referenzierenden Moduls auf.
    /// </summary>
    /// <param name="referencingKey">Der Schlüssel des referenzierenden Moduls (leer = Paketwurzel).</param>
    /// <param name="specifier">Der importierte Spezifizierer.</param>
    /// <returns>Der normalisierte Paketre relative Schlüssel.</returns>
    /// <exception cref="InvalidOperationException">Der Spezifizierer kann nicht aufgelöst werden.</exception>
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

        // Karger Spezifizierer: relativ zur Paketwurzel (Shared-Libraries).
        return Normalize(specifier);
    }

    /// <summary>
    /// Liefert das Verzeichnis eines Schlüssels (ohne Dateinamen).
    /// </summary>
    /// <param name="key">Der Modulschlüssel.</param>
    /// <returns>Das Verzeichnis oder leerer String für Wurzelebene.</returns>
    public static string GetDirectory(string key)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;
        var idx = key.LastIndexOf('/');
        return idx < 0 ? string.Empty : key[..idx];
    }
}
