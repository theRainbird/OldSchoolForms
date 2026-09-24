using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using OldSchoolForms.Ui.Core;

namespace OldSchoolForms.Ui.Designer;

/// <summary>
/// Maps control types to their <see cref="IControlDesignAdapter"/> factories.
/// The <see cref="DesignSurface"/> uses this registry to find design-mode interaction
/// adapters for placed controls without hardcoding individual control types.
/// Adapter instances are cached per control instance so adapters may hold state.
/// </summary>
public sealed class ControlDesignAdapterRegistry
{
    private readonly Dictionary<Type, Func<Control, IControlDesignAdapter>> _factories = new();
    private readonly ConditionalWeakTable<Control, IControlDesignAdapter> _instances = new();

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
    /// The adapter is created once per control instance and cached afterwards.
    /// </summary>
    /// <param name="control">The control to get an adapter for.</param>
    /// <returns>The adapter, or <see langword="null"/>.</returns>
    public IControlDesignAdapter? GetAdapter(Control control)
    {
        if (control == null) return null;
        if (_instances.TryGetValue(control, out var cached))
            return cached;
        if (!_factories.TryGetValue(control.GetType(), out var factory))
            return null;
        var adapter = factory(control);
        _instances.Add(control, adapter);
        return adapter;
    }
}
