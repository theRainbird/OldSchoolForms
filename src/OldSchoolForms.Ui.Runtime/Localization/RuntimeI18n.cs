using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Runtime.Sources;

namespace OldSchoolForms.Ui.Runtime.Localization;

/// <summary>
/// Reusable localization service for the app runtime. Loads flat string bundles
/// (<c>&lt;i18nDir&gt;/&lt;culture&gt;.json</c>) from the app package and resolves
/// translation keys with a fallback chain: current UI culture → parent culture →
/// default culture → English → the key itself.
/// </summary>
public sealed class RuntimeI18n
{
    /// <summary>Prefix that marks a string as a translation reference (e.g. <c>tr:Menu_File</c>).</summary>
    public const string Prefix = "tr:";

    private static readonly string[] ProbeCultures = { "de", "en" };

    private readonly IAppSource _source;
    private readonly string _i18nDir;
    private readonly Dictionary<string, Dictionary<string, string>?> _bundles = new(StringComparer.OrdinalIgnoreCase);
    private List<string>? _available;

    /// <summary>
    /// Creates the localization service for a package source.
    /// </summary>
    /// <param name="source">The package source to read the bundles from.</param>
    /// <param name="i18nDir">Relative directory of the bundles (default: <c>i18n</c>).</param>
    /// <param name="defaultCulture">Fallback culture when a key is missing in the UI culture (default: <c>en</c>).</param>
    /// <param name="availableCultures">Explicit list of supported cultures; auto-detected when <c>null</c>.</param>
    public RuntimeI18n(
        IAppSource source,
        string i18nDir = "i18n",
        string defaultCulture = "en",
        IReadOnlyList<string>? availableCultures = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
        _i18nDir = string.IsNullOrWhiteSpace(i18nDir) ? "i18n" : i18nDir.Trim('/');
        DefaultCulture = string.IsNullOrWhiteSpace(defaultCulture) ? "en" : defaultCulture;

        if (availableCultures is { Count: > 0 })
        {
            _available = availableCultures
                .Select(c => c.Trim())
                .Where(c => c.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    /// <summary>The fallback culture used when a key is missing in the current UI culture.</summary>
    public string DefaultCulture { get; }

    /// <summary>The name of the currently active UI culture.</summary>
    public string Locale => LocalizationManager.CurrentCulture.Name;

    /// <summary>The cultures available as bundles in the app package.</summary>
    public IReadOnlyList<string> AvailableCultures => _available ??= ProbeAvailableCultures();

    /// <summary>
    /// Translates a key using the current UI culture with the fallback chain and
    /// formats the result with <c>{0}</c>-style placeholders.
    /// </summary>
    /// <param name="key">The translation key.</param>
    /// <param name="args">Optional format arguments.</param>
    /// <returns>The localized string, or the key itself when no bundle provides it.</returns>
    public string T(string key, params object?[] args)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;

        var template = Lookup(key) ?? key;
        if (args.Length == 0) return template;

        try
        {
            return string.Format(CultureInfo.CurrentCulture, template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }

    /// <summary>
    /// Resolves a layout string: values carrying the <c>tr:</c> prefix are translated,
    /// all other values are returned unchanged.
    /// </summary>
    /// <param name="value">The raw string from a layout or JS property.</param>
    /// <returns>The resolved string.</returns>
    public string Resolve(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return IsReference(value) ? T(value![Prefix.Length..]) : value!;
    }

    /// <summary>
    /// Checks whether a string is a translation reference (starts with <c>tr:</c>).
    /// </summary>
    /// <param name="value">The string to check.</param>
    /// <returns><see langword="true"/> if the string is a translation reference.</returns>
    public static bool IsReference(string? value) =>
        value != null && value.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>
    /// Checks whether a bundle exists for the given culture name or its parent culture.
    /// The default fallback culture is intentionally <em>not</em> considered, so this
    /// answers "does the app ship a bundle specific to this culture?".
    /// </summary>
    /// <param name="cultureName">The culture name (e.g. <c>de-DE</c>).</param>
    /// <returns><see langword="true"/> if a culture-specific bundle exists.</returns>
    public bool Supports(string cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName)) return false;
        try
        {
            var culture = new CultureInfo(cultureName);
            if (!string.IsNullOrEmpty(culture.Name) && GetBundle(culture.Name) != null)
                return true;
            if (!string.IsNullOrEmpty(culture.Parent.Name) && GetBundle(culture.Parent.Name) != null)
                return true;
        }
        catch (CultureNotFoundException)
        {
            return false;
        }

        return false;
    }

    /// <summary>
    /// Switches the UI culture via <see cref="LocalizationManager"/>.
    /// Does not persist the choice; call <see cref="LocalizationManager.SaveCurrentCulture"/> for that.
    /// </summary>
    /// <param name="cultureName">The culture name (e.g. <c>de</c>).</param>
    /// <exception cref="ArgumentException">The culture name is empty.</exception>
    /// <exception cref="CultureNotFoundException">The culture name is invalid.</exception>
    public void SetCulture(string cultureName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cultureName);
        LocalizationManager.SetCulture(new CultureInfo(cultureName));
    }

    private string? Lookup(string key)
    {
        foreach (var name in CandidateCultures(LocalizationManager.CurrentCulture))
        {
            var bundle = GetBundle(name);
            if (bundle != null && bundle.TryGetValue(key, out var value))
                return value;
        }

        return null;
    }

    private IEnumerable<string> CandidateCultures(CultureInfo culture)
    {
        var seen = new List<string>(4);
        if (!string.IsNullOrEmpty(culture.Name)) seen.Add(culture.Name);
        if (!string.IsNullOrEmpty(culture.Parent.Name) && culture.Parent.Name != culture.Name) seen.Add(culture.Parent.Name);
        if (!seen.Contains(DefaultCulture, StringComparer.OrdinalIgnoreCase)) seen.Add(DefaultCulture);
        if (!seen.Contains("en", StringComparer.OrdinalIgnoreCase)) seen.Add("en");
        return seen;
    }

    private Dictionary<string, string>? GetBundle(string culture)
    {
        if (_bundles.TryGetValue(culture, out var cached)) return cached;

        Dictionary<string, string>? bundle = null;
        var path = _i18nDir + "/" + culture + ".json";
        if (_source.Exists(path))
        {
            try
            {
                if (JsonNode.Parse(_source.ReadText(path)) is JsonObject obj)
                {
                    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var kvp in obj)
                    {
                        if (kvp.Value is JsonValue v && v.TryGetValue<string>(out var text))
                            map[kvp.Key] = text;
                    }

                    bundle = map;
                }
            }
            catch (JsonException)
            {
                // A broken bundle is treated as missing so the fallback chain continues.
                bundle = null;
            }
        }

        _bundles[culture] = bundle;
        return bundle;
    }

    private List<string> ProbeAvailableCultures()
    {
        var list = new List<string>();
        foreach (var culture in ProbeCultures)
        {
            if (!list.Contains(culture, StringComparer.OrdinalIgnoreCase) &&
                _source.Exists(_i18nDir + "/" + culture + ".json"))
            {
                list.Add(culture);
            }
        }

        if (!list.Contains(DefaultCulture, StringComparer.OrdinalIgnoreCase) &&
            _source.Exists(_i18nDir + "/" + DefaultCulture + ".json"))
        {
            list.Add(DefaultCulture);
        }

        if (list.Count == 0) list.Add(DefaultCulture);
        return list;
    }
}
