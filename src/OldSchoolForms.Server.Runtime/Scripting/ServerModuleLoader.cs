using Jint;
using Jint.Runtime.Modules;

namespace OldSchoolForms.Server.Runtime.Scripting;

/// <summary>
/// Jint <see cref="IModuleLoader"/> for the server package. Resolves
/// relative imports (<c>./</c>, <c>../</c>), package-root imports
/// (leading <c>/</c> or bare specifiers) and shared-code imports
/// (<c>@shared/...</c>) which map into the shared directory declared in
/// <c>server.json</c>.
/// </summary>
public sealed class ServerModuleLoader : IModuleLoader
{
    /// <summary>Prefix for imports that resolve into the shared directory.</summary>
    public const string SharedPrefix = "@shared/";

    private readonly string _packageRoot;
    private readonly string _sharedRoot;

    /// <summary>
    /// Creates the loader.
    /// </summary>
    /// <param name="packageRoot">Absolute path of the server package directory.</param>
    /// <param name="sharedRoot">Absolute path of the shared code directory (may not exist).</param>
    public ServerModuleLoader(string packageRoot, string sharedRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedRoot);
        _packageRoot = Path.GetFullPath(packageRoot);
        _sharedRoot = Path.GetFullPath(sharedRoot);
    }

    /// <inheritdoc />
    public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest request)
    {
        var specifier = request.Specifier;
        try
        {
            if (specifier.StartsWith(SharedPrefix, StringComparison.Ordinal))
            {
                var key = "shared/" + Normalize(specifier[SharedPrefix.Length..]);
                return new ResolvedSpecifier(request, key, new Uri("oldschoolforms-server://shared/" + key), SpecifierType.RelativeOrAbsolute);
            }

            var resolved = ResolveRelative(referencingModuleLocation ?? string.Empty, specifier);
            return new ResolvedSpecifier(request, resolved, new Uri("oldschoolforms-server://app/" + resolved), SpecifierType.RelativeOrAbsolute);
        }
        catch (InvalidOperationException)
        {
            return new ResolvedSpecifier(request, specifier, null!, SpecifierType.Error);
        }
    }

    /// <inheritdoc />
    public Module LoadModule(Engine engine, ResolvedSpecifier resolved)
    {
        if (resolved.Type == SpecifierType.Error)
            throw new InvalidOperationException($"Cannot resolve module '{resolved.ModuleRequest.Specifier}'.");

        var path = ToAbsolutePath(resolved.Key);
        if (!File.Exists(path))
            throw new InvalidOperationException($"Module file not found: {resolved.Key}");

        var code = File.ReadAllText(path);
        return ModuleFactory.BuildSourceTextModule(engine, resolved, code);
    }

    /// <summary>
    /// Maps a resolved module key to an absolute file path, guarding against
    /// path traversal outside the package or shared roots.
    /// </summary>
    /// <param name="key">The resolved module key.</param>
    /// <returns>The absolute file path.</returns>
    /// <exception cref="InvalidOperationException">The key escapes the allowed roots.</exception>
    public string ToAbsolutePath(string key)
    {
        var relative = key.StartsWith("shared/", StringComparison.Ordinal) ? key["shared/".Length..] : key;
        var baseDir = key.StartsWith("shared/", StringComparison.Ordinal) ? _sharedRoot : _packageRoot;
        var full = Path.GetFullPath(Path.Combine(baseDir, relative));

        if (!full.StartsWith(baseDir + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
            full != baseDir)
        {
            throw new InvalidOperationException($"Module key escapes its root: {key}");
        }

        return full;
    }

    private static string ResolveRelative(string referencingLocation, string specifier)
    {
        if (string.IsNullOrEmpty(specifier))
            throw new InvalidOperationException("Empty module specifier.");

        if (specifier.StartsWith("/", StringComparison.Ordinal))
            return Normalize(specifier);

        if (specifier.StartsWith("./", StringComparison.Ordinal) || specifier.StartsWith("../", StringComparison.Ordinal))
        {
            var dir = string.IsNullOrEmpty(referencingLocation)
                ? string.Empty
                : referencingLocation.Contains('/') ? referencingLocation[..referencingLocation.LastIndexOf('/')] : string.Empty;
            return Normalize(string.IsNullOrEmpty(dir) ? specifier : dir + "/" + specifier);
        }

        // Bare specifier: relative to the package root.
        return Normalize(specifier);
    }

    private static string Normalize(string path)
    {
        var parts = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var stack = new List<string>();
        foreach (var part in parts)
        {
            if (part == ".") continue;
            if (part == "..")
            {
                if (stack.Count == 0)
                    throw new InvalidOperationException($"Module path escapes the root: {path}");
                stack.RemoveAt(stack.Count - 1);
                continue;
            }

            stack.Add(part);
        }

        return string.Join("/", stack);
    }
}
