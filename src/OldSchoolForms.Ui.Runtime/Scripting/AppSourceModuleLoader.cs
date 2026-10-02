using Jint;
using Jint.Runtime.Modules;
using OldSchoolForms.Ui.Runtime.Sources;

namespace OldSchoolForms.Ui.Runtime.Scripting;

/// <summary>
/// Jint-<see cref="IModuleLoader"/>, der ES-Module aus einer <see cref="IAppSource"/>
/// lädt. Unterstützt relative Importe (<c>./</c>, <c>../</c>), absolute
/// Paket-Importe (<c>@app/...</c>) und karge Spezifizierer (relativ zur Paketwurzel).
/// </summary>
public sealed class AppSourceModuleLoader : IModuleLoader
{
    private readonly IAppSource _source;

    /// <summary>
    /// Erzeugt einen Modul-Loader für die angegebene Paketquelle.
    /// </summary>
    /// <param name="source">Die Quelle, aus der Moduldateien gelesen werden.</param>
    public AppSourceModuleLoader(IAppSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    /// <inheritdoc />
    public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest request)
    {
        var specifier = request.Specifier;
        try
        {
            var key = ModulePath.ResolveRelative(referencingModuleLocation ?? string.Empty, specifier);
            var uri = new Uri("oldschoolforms://app/" + key);
            return new ResolvedSpecifier(request, key, uri, SpecifierType.RelativeOrAbsolute);
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
            throw new InvalidOperationException($"Modul konnte nicht aufgelöst werden: {resolved.ModuleRequest.Specifier}");

        var key = resolved.Key;
        if (!_source.Exists(key))
            throw new InvalidOperationException($"Moduldatei fehlt im Paket: {key}");

        var code = _source.ReadText(key);
        return ModuleFactory.BuildSourceTextModule(engine, resolved, code);
    }
}
