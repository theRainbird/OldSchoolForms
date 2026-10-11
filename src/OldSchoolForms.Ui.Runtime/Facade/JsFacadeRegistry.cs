using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade;

/// <summary>
/// Central registry that maps CLR control and tool strip item types to their
/// typed JS facade factories. The lookup walks the type hierarchy so the most
/// specific registered facade is returned; unregistered types fall back to
/// the base <see cref="JsControl"/> or <see cref="JsToolStripItem"/>.
/// All facade creation for JavaScript must go through this registry so that
/// <c>find()</c>, layout loading and <c>createControl()</c> consistently
/// return the typed facades.
/// </summary>
public static class JsFacadeRegistry
{
    private static readonly Dictionary<Type, Func<JsEngineHost, Control, JsControl>> ControlFactories = new();
    private static readonly Dictionary<Type, Func<JsEngineHost, ToolStripItem, JsToolStripItem>> StripItemFactories = new();

    /// <summary>
    /// Registers a facade factory for a control type.
    /// </summary>
    /// <typeparam name="TControl">The CLR control type the facade wraps.</typeparam>
    /// <param name="factory">Factory creating the facade for a control instance.</param>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <see langword="null"/>.</exception>
    public static void Register<TControl>(Func<JsEngineHost, TControl, JsControl> factory)
        where TControl : Control
    {
        ArgumentNullException.ThrowIfNull(factory);
        ControlFactories[typeof(TControl)] = (host, control) => factory(host, (TControl)control);
    }

    /// <summary>
    /// Registers a facade factory for a tool strip item type.
    /// </summary>
    /// <typeparam name="TItem">The CLR tool strip item type the facade wraps.</typeparam>
    /// <param name="factory">Factory creating the facade for a strip item instance.</param>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <see langword="null"/>.</exception>
    public static void RegisterStripItem<TItem>(Func<JsEngineHost, TItem, JsToolStripItem> factory)
        where TItem : ToolStripItem
    {
        ArgumentNullException.ThrowIfNull(factory);
        StripItemFactories[typeof(TItem)] = (host, item) => factory(host, (TItem)item);
    }

    /// <summary>
    /// Wraps a control in the most specific registered facade,
    /// falling back to <see cref="JsControl"/>.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="control">The control to wrap.</param>
    /// <returns>The facade for the control.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="control"/> is <see langword="null"/>.</exception>
    public static JsControl WrapControl(JsEngineHost host, Control control)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(control);

        for (var type = control.GetType(); type != null && type != typeof(object); type = type.BaseType)
        {
            if (ControlFactories.TryGetValue(type, out var factory))
                return factory(host, control);
        }

        return new JsControl(host, control);
    }

    /// <summary>
    /// Wraps a tool strip item in the most specific registered facade,
    /// falling back to <see cref="JsToolStripItem"/>.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="item">The tool strip item to wrap.</param>
    /// <returns>The facade for the tool strip item.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="item"/> is <see langword="null"/>.</exception>
    public static JsToolStripItem WrapStripItem(JsEngineHost host, ToolStripItem item)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(item);

        for (var type = item.GetType(); type != null && type != typeof(object); type = type.BaseType)
        {
            if (StripItemFactories.TryGetValue(type, out var factory))
                return factory(host, item);
        }

        return new JsToolStripItem(host, item);
    }

    /// <summary>
    /// Removes all registrations. Intended for tests only.
    /// </summary>
    public static void Clear()
    {
        ControlFactories.Clear();
        StripItemFactories.Clear();
    }
}
