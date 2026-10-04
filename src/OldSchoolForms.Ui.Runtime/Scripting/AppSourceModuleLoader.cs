using Jint;
using Jint.Runtime.Modules;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Runtime.Sources;

namespace OldSchoolForms.Ui.Runtime.Scripting;

/// <summary>
/// Jint <see cref="IModuleLoader"/> that loads ES modules from an <see cref="IAppSource"/>.
/// Supports relative imports (<c>./</c>, <c>../</c>), absolute
/// package imports (<c>@app/...</c>) and bare specifiers (relative to the package root).
/// </summary>
public sealed class AppSourceModuleLoader : IModuleLoader
{
    private readonly IAppSource _source;

    /// <summary>
    /// Creates a module loader for the given package source.
    /// </summary>
    /// <param name="source">The source from which module files are read.</param>
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
            throw new InvalidOperationException(string.Format(LangRes.GetString("Runtime_ModuleResolveFailed"), resolved.ModuleRequest.Specifier));

        var key = resolved.Key;
        if (!_source.Exists(key))
            throw new InvalidOperationException(string.Format(LangRes.GetString("Runtime_ModuleFileMissing"), key));

        var code = _source.ReadText(key);
        return ModuleFactory.BuildSourceTextModule(engine, resolved, code);
    }
}
