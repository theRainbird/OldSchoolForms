namespace OldSchoolForms.Ui.Runtime.Modules;

/// <summary>
/// Registry aller Moduldeskriptoren eines Apps, lookup nach ID.
/// </summary>
public sealed class ModuleRegistry
{
    private readonly Dictionary<string, ModuleDescriptor> _byId = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ModuleDescriptor> _all;

    /// <summary>
    /// Erzeugt die Registry aus einer Liste von Deskriptoren.
    /// </summary>
    /// <param name="modules">Die Moduldeskriptoren.</param>
    public ModuleRegistry(IEnumerable<ModuleDescriptor> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        _all = modules.ToList();
        foreach (var module in _all)
        {
            if (string.IsNullOrEmpty(module.Id))
                throw new InvalidOperationException("Moduldeskriptor ohne ID.");
            _byId[module.Id] = module;
        }
    }

    /// <summary>Alle Module in Sortierreihenfolge.</summary>
    public IReadOnlyList<ModuleDescriptor> All => _all;

    /// <summary>
    /// Sucht einen Deskriptor nach ID.
    /// </summary>
    /// <param name="id">Die Modul-ID.</param>
    /// <returns>Der Deskriptor oder <see langword="null"/>.</returns>
    public ModuleDescriptor? Find(string id) =>
        !string.IsNullOrEmpty(id) && _byId.TryGetValue(id, out var m) ? m : null;
}
