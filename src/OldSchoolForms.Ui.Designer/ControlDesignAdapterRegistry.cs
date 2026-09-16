using System;
using System.Collections.Generic;
using OldSchoolForms.Ui.Core;

namespace OldSchoolForms.Ui.Designer;

/// <summary>
/// Maps control types to their <see cref="IControlDesignAdapter"/> factories.
/// The <see cref="DesignSurface"/> uses this registry to find design-mode interaction
/// adapters for placed controls without hardcoding individual control types.
/// </summary>
public sealed class ControlDesignAdapterRegistry
{
    private readonly Dictionary<Type, Func<Control, IControlDesignAdapter>> _factories = new();

    /// <summary>
    /// Registers an adapter factory for the specified control type.
    /// </summary>
    /// <typeparam name="TControl">The control type the adapter handles.</typeparam>
    /// <param name="factory">The factory that creates the adapter for a given control instance.</param>
    public void Register<TControl>(Func<TControl, IControlDesignAdapter> factory)
        where TControl : Control
    {
        _factories[typeof(TControl)] = c => factory((TControl)c);
    }

    /// <summary>
    /// Gets the adapter for the specified control, or <see langword="null"/> if none is registered.
    /// </summary>
    /// <param name="control">The control to get an adapter for.</param>
    /// <returns>The adapter, or <see langword="null"/>.</returns>
    public IControlDesignAdapter? GetAdapter(Control control)
    {
        if (control == null) return null;
        return _factories.TryGetValue(control.GetType(), out var factory) ? factory(control) : null;
    }
}
