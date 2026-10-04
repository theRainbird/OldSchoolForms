using OldSchoolForms.Ui.Core;

namespace OldSchoolForms.Ui.Runtime.Modules;

/// <summary>
/// Registry of all module descriptors of an app, lookup by ID.
/// </summary>
public sealed class ModuleRegistry
{
    private readonly Dictionary<string, ModuleDescriptor> _byId = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ModuleDescriptor> _all;

    /// <summary>
    /// Creates the registry from a list of descriptors.
    /// </summary>
    /// <param name="modules">The module descriptors.</param>
    public ModuleRegistry(IEnumerable<ModuleDescriptor> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        _all = modules.ToList();
        foreach (var module in _all)
        {
            if (string.IsNullOrEmpty(module.Id))
                throw new InvalidOperationException(LangRes.GetString("Runtime_DescriptorWithoutId"));
            _byId[module.Id] = module;
        }
    }

    /// <summary>All modules in sort order.</summary>
    public IReadOnlyList<ModuleDescriptor> All => _all;

    /// <summary>
    /// Finds a descriptor by ID.
    /// </summary>
    /// <param name="id">The module ID.</param>
    /// <returns>The descriptor, or <see langword="null"/>.</returns>
    public ModuleDescriptor? Find(string id) =>
        !string.IsNullOrEmpty(id) && _byId.TryGetValue(id, out var m) ? m : null;
}
