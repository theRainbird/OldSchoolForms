using OldSchoolForms.Ui.Controls;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Runtime.Facade.Typed;
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
    /// Registers the built-in typed facades for the basic controls. Idempotent;
    /// called automatically when a <see cref="JsEngineHost"/> is created.
    /// Custom registrations made after this call override the defaults.
    /// </summary>
    public static void RegisterDefaults()
    {
        Register<Button>((host, c) => new JsButton(host, c));
        Register<CheckBox>((host, c) => new JsCheckBox(host, c));
        Register<RadioButton>((host, c) => new JsRadioButton(host, c));
        Register<TextBox>((host, c) => new JsTextBox(host, c));
        Register<MemoBox>((host, c) => new JsMemoBox(host, c));
        Register<ComboBox>((host, c) => new JsComboBox(host, c));
        Register<ListBox>((host, c) => new JsListBox(host, c));
        Register<ProgressBar>((host, c) => new JsProgressBar(host, c));
        Register<Spinner>((host, c) => new JsSpinner(host, c));
        Register<FontPicker>((host, c) => new JsFontPicker(host, c));
        Register<Label>((host, c) => new JsLabel(host, c));
        Register<PictureBox>((host, c) => new JsPictureBox(host, c));
        Register<DateTimePicker>((host, c) => new JsDateTimePicker(host, c));

        Register<Panel>((host, c) => new JsPanel(host, c));
        Register<SplitPanel>((host, c) => new JsSplitPanel(host, c));
        Register<TabControl>((host, c) => new JsTabControl(host, c));
        Register<TreeView>((host, c) => new JsTreeView(host, c));
        Register<MenuStrip>((host, c) => new JsMenuStrip(host, c));
        Register<ToolStrip>((host, c) => new JsToolStrip(host, c));
        Register<StatusStrip>((host, c) => new JsStatusStrip(host, c));

        RegisterStripItem<ToolStripMenuItem>((host, i) => new JsToolStripMenuItem(host, i));
        RegisterStripItem<ToolStripButton>((host, i) => new JsToolStripButton(host, i));
        RegisterStripItem<ToolStripLabel>((host, i) => new JsToolStripLabel(host, i));
        RegisterStripItem<ToolStripTextBox>((host, i) => new JsToolStripTextBox(host, i));
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
