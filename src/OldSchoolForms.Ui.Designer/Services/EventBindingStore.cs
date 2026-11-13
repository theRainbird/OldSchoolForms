using System;
using System.Collections.Generic;

namespace OldSchoolForms.Ui.Designer.Services;

/// <summary>
/// Holds the designer-side mapping between a designed object (a <see cref="Core.Control"/> or a
/// <see cref="Controls.Containers.ToolStripItem"/>) and the names of the event handler methods that
/// are wired to its events. The store is the single source of truth for the designer's events view:
/// the property grid reads and writes handler names through it, and both the JSON layout serializer and
/// the C# code generator persist the recorded bindings.
/// <para>
/// A binding is a plain <c>eventName -&gt; handlerName</c> string pair. Setting a handler name to null
/// or whitespace removes the binding, mirroring the way the WinForms designer deletes a handler by
/// clearing the name in the property grid.
/// </para>
/// </summary>
public sealed class EventBindingStore
{
    private readonly Dictionary<object, Dictionary<string, string>> _bindings = new();

    private static readonly IReadOnlyDictionary<string, string> EmptyBindings =
        new Dictionary<string, string>();

    /// <summary>
    /// Raised whenever a binding is created, renamed or removed. Subscribers can use this to refresh
    /// dependent views or mark the document dirty.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Gets the handler name currently wired to <paramref name="eventName"/> on <paramref name="target"/>.
    /// </summary>
    /// <param name="target">The designed object to query.</param>
    /// <param name="eventName">The event whose handler name is requested.</param>
    /// <returns>The wired handler name, or an empty string when no handler is bound.</returns>
    public string GetHandler(object? target, string eventName)
    {
        if (target == null || string.IsNullOrEmpty(eventName))
            return string.Empty;
        return _bindings.TryGetValue(target, out var map) && map.TryGetValue(eventName, out var handler)
            ? handler
            : string.Empty;
    }

    /// <summary>
    /// Wires <paramref name="handlerName"/> to <paramref name="eventName"/> on <paramref name="target"/>.
    /// Passing null or whitespace removes the binding instead.
    /// </summary>
    /// <param name="target">The designed object to modify.</param>
    /// <param name="eventName">The event to bind.</param>
    /// <param name="handlerName">The handler method name, or null/whitespace to remove the binding.</param>
    public void SetHandler(object? target, string eventName, string? handlerName)
    {
        if (target == null || string.IsNullOrEmpty(eventName))
            return;

        var trimmed = handlerName?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            ClearHandler(target, eventName);
            return;
        }

        if (!_bindings.TryGetValue(target, out var map))
        {
            map = new Dictionary<string, string>();
            _bindings[target] = map;
        }

        if (map.TryGetValue(eventName, out var existing) && existing == trimmed)
            return;

        map[eventName] = trimmed;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Removes the handler bound to <paramref name="eventName"/> on <paramref name="target"/>.
    /// </summary>
    /// <param name="target">The designed object to modify.</param>
    /// <param name="eventName">The event whose binding should be removed.</param>
    public void ClearHandler(object? target, string eventName)
    {
        if (target == null || string.IsNullOrEmpty(eventName))
            return;
        if (_bindings.TryGetValue(target, out var map) && map.Remove(eventName))
        {
            if (map.Count == 0)
                _bindings.Remove(target);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Gets all event-to-handler bindings recorded for <paramref name="target"/>.
    /// </summary>
    /// <param name="target">The designed object to query.</param>
    /// <returns>A read-only view of the bindings, empty when the target has none.</returns>
    public IReadOnlyDictionary<string, string> GetBindings(object? target)
    {
        if (target != null && _bindings.TryGetValue(target, out var map))
            return map;
        return EmptyBindings;
    }

    /// <summary>
    /// Gets whether <paramref name="target"/> has at least one event binding.
    /// </summary>
    /// <param name="target">The designed object to query.</param>
    /// <returns><see langword="true"/> when the target has one or more bindings.</returns>
    public bool HasBindings(object? target) => target != null && _bindings.ContainsKey(target);

    /// <summary>
    /// Removes every binding recorded for <paramref name="target"/>.
    /// </summary>
    /// <param name="target">The designed object whose bindings should be dropped.</param>
    public void ClearObject(object? target)
    {
        if (target != null && _bindings.Remove(target))
            Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Removes all bindings from the store.
    /// </summary>
    public void Clear()
    {
        if (_bindings.Count > 0)
        {
            _bindings.Clear();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
