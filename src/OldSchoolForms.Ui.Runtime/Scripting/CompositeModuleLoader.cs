using Jint;
using Jint.Runtime.Modules;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Runtime.Remote;

namespace OldSchoolForms.Ui.Runtime.Scripting;

/// <summary>
/// Jint <see cref="IModuleLoader"/> that routes <c>@shared/...</c> imports to
/// the locally cached shared ES6 code (downloaded from the server) and every
/// other specifier to the regular app package source. This keeps shared
/// validation logic strictly separated from the app package while allowing a
/// single import syntax in module code.
/// </summary>
public sealed class CompositeModuleLoader : IModuleLoader
{
    /// <summary>Prefix for shared code imports (<c>@shared/validation/customers.js</c>).</summary>
    public const string SharedPrefix = "@shared/";

    private const string SharedKeyPrefix = "shared:";

    private readonly AppSourceModuleLoader _appLoader;
    private readonly SharedCodeCache? _sharedCache;

    /// <summary>
    /// Creates the composite loader.
    /// </summary>
    /// <param name="appLoader">The loader for the app package.</param>
    /// <param name="sharedCache">The shared code cache, or <c>null</c> when no server is configured.</param>
    public CompositeModuleLoader(AppSourceModuleLoader appLoader, SharedCodeCache? sharedCache)
    {
        ArgumentNullException.ThrowIfNull(appLoader);
        _appLoader = appLoader;
        _sharedCache = sharedCache;
    }

    /// <inheritdoc />
    public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest request)
    {
        var specifier = request.Specifier;
        if (specifier.StartsWith(SharedPrefix, StringComparison.Ordinal))
        {
            try
            {
                var path = ModulePath.Normalize(specifier[SharedPrefix.Length..]);
                var key = SharedKeyPrefix + path;
                var uri = new Uri("oldschoolforms://shared/" + path);
                return new ResolvedSpecifier(request, key, uri, SpecifierType.RelativeOrAbsolute);
            }
            catch (InvalidOperationException)
            {
                return new ResolvedSpecifier(request, specifier, null!, SpecifierType.Error);
            }
        }

        return _appLoader.Resolve(referencingModuleLocation, request);
    }

    /// <inheritdoc />
    public Module LoadModule(Engine engine, ResolvedSpecifier resolved)
    {
        if (resolved.Key.StartsWith(SharedKeyPrefix, StringComparison.Ordinal))
        {
            if (resolved.Type == SpecifierType.Error)
                throw new InvalidOperationException(string.Format(LangRes.GetEnglish("Runtime_ModuleResolveFailed"), resolved.ModuleRequest.Specifier));

            if (_sharedCache == null)
                throw new InvalidOperationException("Shared code is not available: no server connection configured.");

            var path = resolved.Key[SharedKeyPrefix.Length..];
            var code = _sharedCache.TryRead(path)
                ?? throw new InvalidOperationException($"Shared module not cached: {path}");

            return ModuleFactory.BuildSourceTextModule(engine, resolved, code);
        }

        return _appLoader.LoadModule(engine, resolved);
    }
}
