using System.Collections.Generic;
using System.Reflection;

namespace OldSchoolForms.Ui.Design;

/// <summary>
/// Holds the set of registered <see cref="IValueEditor"/> instances and resolves
/// the editor responsible for a given property. The registry is intentionally
/// empty by default; the property browser registers the built-in editors.
/// </summary>
public static class ValueEditorRegistry
{
    private static readonly object _sync = new();
    private static readonly List<IValueEditor> _editors = new();

    /// <summary>
    /// Registers a value editor. Editors are matched in registration order; the
    /// first editor whose <see cref="IValueEditor.Handles"/> returns true wins.
    /// </summary>
    /// <param name="editor">The editor to register.</param>
    public static void Register(IValueEditor editor)
    {
        if (editor == null) return;
        lock (_sync)
        {
            _editors.Add(editor);
        }
    }

    /// <summary>
    /// Gets the editor responsible for the given property, or null if none is registered.
    /// </summary>
    /// <param name="property">The property to resolve an editor for.</param>
    /// <returns>The matching editor, or null.</returns>
    public static IValueEditor? Find(PropertyInfo property)
    {
        if (property == null) return null;

        // Snapshot the editors under the lock so a concurrent Register (the designer can
        // build property grids on several threads) cannot invalidate the enumeration.
        IValueEditor[] snapshot;
        lock (_sync)
        {
            snapshot = _editors.ToArray();
        }

        foreach (var editor in snapshot)
        {
            if (editor.Handles(property))
                return editor;
        }

        return null;
    }
}
