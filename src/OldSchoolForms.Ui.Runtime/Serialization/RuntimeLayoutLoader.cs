using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Jint;
using Jint.Native.Object;
using OldSchoolForms.Ui.Controls;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Designer.Serialization;
using OldSchoolForms.Ui.Resources;
using OldSchoolForms.Ui.Runtime.Localization;

namespace OldSchoolForms.Ui.Runtime.Serialization;

/// <summary>
/// Result of a layout load operation: the controls findable by name
/// plus warnings about skipped types/properties.
/// </summary>
public sealed class LayoutLoadResult
{
    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="controls">Controls keyed by <see cref="Control.Name"/> (case-insensitive).</param>
    /// <param name="warnings">Warnings from the load operation.</param>
    /// <param name="translations">Translation bindings captured from <c>tr:</c> references.</param>
    /// <param name="stripItems">Named tool strip items keyed by <see cref="ToolStripItem.Name"/> (case-insensitive).</param>
    public LayoutLoadResult(
        IReadOnlyDictionary<string, Control> controls,
        IReadOnlyList<string> warnings,
        IReadOnlyList<TranslationRef>? translations = null,
        IReadOnlyDictionary<string, ToolStripItem>? stripItems = null)
    {
        Controls = controls;
        Warnings = warnings;
        Translations = translations ?? new List<TranslationRef>();
        StripItems = stripItems ?? new Dictionary<string, ToolStripItem>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The loaded, named controls.</summary>
    public IReadOnlyDictionary<string, Control> Controls { get; }

    /// <summary>Warnings about unknown types or properties.</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>
    /// Named tool strip items (buttons, labels) captured from tool strips in the layout,
    /// keyed by <see cref="ToolStripItem.Name"/> (case-insensitive). These are not
    /// <see cref="Control"/> instances, so they are tracked separately from
    /// <see cref="Controls"/>.
    /// </summary>
    public IReadOnlyDictionary<string, ToolStripItem> StripItems { get; }

    /// <summary>
    /// Translation bindings captured from <c>tr:</c> property values.
    /// Re-applying them re-localizes the targets after a culture change.
    /// </summary>
    public IReadOnlyList<TranslationRef> Translations { get; }

    /// <summary>
    /// Finds a control by its name.
    /// </summary>
    /// <param name="name">The control name.</param>
    /// <returns>The found control or <see langword="null"/>.</returns>
    public Control? Find(string name) =>
        !string.IsNullOrEmpty(name) && Controls.TryGetValue(name, out var c) ? c : null;
}

/// <summary>
/// Loads layout documents in the <c>OldSchoolForms.Layout</c> format directly into a
/// runtime control hierarchy (without a <c>DesignSurface</c>). The format and value
/// conversion are shared with the designer.
/// </summary>
public sealed class RuntimeLayoutLoader
{
    private readonly Dictionary<string, Type> _customTypes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Optional localization service. When set, string property values with the
    /// <c>tr:</c> prefix are resolved to localized texts during layout loading.
    /// </summary>
    public RuntimeI18n? I18n { get; set; }

    /// <summary>
    /// Registers an additional control type under a simple name.
    /// </summary>
    /// <param name="typeName">The type name used in the layout.</param>
    /// <param name="type">The CLR type (must derive from <see cref="Control"/> or <see cref="ToolStripItem"/>).</param>
    public void Register(string typeName, Type type)
    {
        if (string.IsNullOrWhiteSpace(typeName)) throw new ArgumentException(LangRes.GetEnglish("Runtime_TypeNameMissing"), nameof(typeName));
        ArgumentNullException.ThrowIfNull(type);
        _customTypes[typeName] = type;
    }

    /// <summary>
    /// Resolves a type name: first own registrations, then the known
    /// designer types.
    /// </summary>
    /// <param name="typeName">The simple type name.</param>
    /// <returns>The type or <see langword="null"/>.</returns>
    public Type? ResolveType(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName)) return null;
        if (_customTypes.TryGetValue(typeName, out var custom)) return custom;
        return LayoutJsonSerializer.ResolveType(typeName);
    }

    /// <summary>
    /// Creates a control instance from a type name (same resolution as layout loading).
    /// Used for building layouts programmatically from JavaScript.
    /// </summary>
    /// <param name="typeName">The simple control type name (e.g. <c>"Button"</c>).</param>
    /// <returns>The created control or <c>null</c> if the type is unknown or not a control.</returns>
    public Control? CreateControl(string typeName)
    {
        var type = ResolveType(typeName);
        if (type == null || !typeof(Control).IsAssignableFrom(type))
            return null;
        try
        {
            return (Control)Activator.CreateInstance(type)!;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Applies properties from a JavaScript object onto a CLR target. The JS object is
    /// converted to JSON and fed through the same value converter as layout loading,
    /// so colors, points, sizes, padding, fonts and enums are supported.
    /// </summary>
    /// <param name="engine">The Jint engine (unused placeholder for symmetry).</param>
    /// <param name="target">The target object (control or strip item).</param>
    /// <param name="props">The JS properties object.</param>
    /// <param name="warnings">Collects warnings for unknown/unsupported properties.</param>
    public void ApplyPropsFromJs(Engine engine, object target, ObjectInstance props, List<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(props);

        JsonNode? node;
        try
        {
            var clr = props.ToObject();
            node = clr == null ? null : JsonSerializer.SerializeToNode(clr);
        }
        catch (Exception ex)
        {
            warnings.Add($"JS properties could not be converted: {ex.Message}");
            return;
        }

        if (node is not JsonObject obj) return;
        foreach (var kvp in obj)
            ApplyProperty(target, kvp.Key, kvp.Value, warnings, null);
    }

    /// <summary>
    /// Loads a layout document into a target container.
    /// </summary>
    /// <param name="target">The container to which the layout roots are added.</param>
    /// <param name="json">The layout JSON.</param>
    /// <returns>The load result with named controls and warnings.</returns>
    /// <exception cref="InvalidOperationException">The JSON is invalid.</exception>
    public LayoutLoadResult LoadInto(ContainerControl target, string json)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(json);

        var warnings = new List<string>();
        var controls = new Dictionary<string, Control>(StringComparer.OrdinalIgnoreCase);
        var translations = new List<TranslationRef>();
        var stripItems = new Dictionary<string, ToolStripItem>(StringComparer.OrdinalIgnoreCase);

        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(string.Format(LangRes.GetEnglish("Runtime_InvalidJsonDocument"), ex.Message), ex);
        }

        if (parsed is not JsonObject doc)
            throw new InvalidOperationException(LangRes.GetEnglish("Runtime_JsonRootMustBeObject"));

        var format = GetString(doc, "format");
        if (format != LayoutJsonSerializer.FormatId)
            warnings.Add($"Unexpected document format: '{format}'.");

        var version = GetInt(doc, "version", LayoutJsonSerializer.FormatVersion);
        if (version > LayoutJsonSerializer.FormatVersion)
            warnings.Add($"Format version {version} is newer than the supported version {LayoutJsonSerializer.FormatVersion}.");

        if (doc["rootSize"] is JsonObject rootSize)
        {
            var width = GetInt(rootSize, "width", target.Width);
            var height = GetInt(rootSize, "height", target.Height);
            target.Size = new Size(width, height);
        }

        if (doc["controls"] is JsonArray roots)
        {
            foreach (var node in roots)
            {
                if (node is JsonObject controlNode)
                    LoadControl(controlNode, target, warnings, controls, translations, stripItems);
            }
        }

        return new LayoutLoadResult(controls, warnings, translations, stripItems);
    }

    private void LoadControl(
        JsonObject node,
        Control parent,
        List<string> warnings,
        Dictionary<string, Control> controls,
        List<TranslationRef> translations,
        Dictionary<string, ToolStripItem> stripItems)
    {
        var typeName = GetString(node, "type");
        var type = ResolveType(typeName ?? string.Empty);
        if (type == null || !typeof(Control).IsAssignableFrom(type))
        {
            warnings.Add($"Unknown control type '{typeName}' was skipped.");
            return;
        }

        Control control;
        try
        {
            control = (Control)Activator.CreateInstance(type)!;
        }
        catch (Exception ex)
        {
            warnings.Add($"Control '{typeName}' could not be created: {ex.Message}");
            return;
        }

        var declaredName = GetString(node, "name");
        if (!string.IsNullOrEmpty(declaredName))
            control.Name = declaredName;

        ApplyProperties(control, node["props"] as JsonObject, warnings, translations);

        if (!string.IsNullOrEmpty(control.Name))
            controls[control.Name] = control;

        if (parent is TabControl tabControl && control is TabPage page)
            tabControl.AddTabPage(page);
        else
            parent.Controls.Add(control);

        if (control is DataGridView grid && node["columns"] is JsonArray columns)
        {
            foreach (var columnNode in columns)
            {
                if (columnNode is JsonObject columnObj)
                    grid.Columns.Add(LoadColumn(columnObj, warnings, translations));
            }
        }

        if (control is ToolStrip toolStrip && node["items"] is JsonArray stripItemsNode)
        {
            foreach (var itemNode in stripItemsNode)
            {
                if (itemNode is JsonObject itemObj)
                {
                    var stripItem = LoadStripItem(itemObj, warnings, translations);
                    if (stripItem != null)
                    {
                        if (!string.IsNullOrEmpty(stripItem.Name))
                            stripItems[stripItem.Name] = stripItem;
                        toolStrip.Items.Add(stripItem);
                    }
                }
            }
        }

        if (control is StatusStrip statusStrip && node["items"] is JsonArray statusItems)
        {
            foreach (var itemNode in statusItems)
            {
                if (itemNode is JsonObject itemObj)
                {
                    var label = LoadStatusLabel(itemObj, warnings, translations);
                    if (label != null)
                        statusStrip.Items.Add(label);
                }
            }
        }

        if (node["children"] is JsonArray children)
        {
            foreach (var childNode in children)
            {
                if (childNode is JsonObject childObj)
                {
                    if (control is ContainerControl container)
                        LoadControl(childObj, container, warnings, controls, translations, stripItems);
                    else
                        warnings.Add($"Control '{typeName}' cannot accept children.");
                }
            }
        }
    }

    private DataGridViewColumn LoadColumn(JsonObject node, List<string> warnings, List<TranslationRef> translations)
    {
        var column = new DataGridViewColumn();

        foreach (var kvp in node)
        {
            switch (kvp.Key.ToLowerInvariant())
            {
                case "valuetype":
                    column.ValueType = JsonValueConverter.StringToType(kvp.Value?.GetValue<string>());
                    break;
                case "items":
                    if (kvp.Value is JsonArray items)
                    {
                        column.Items ??= new List<object>();
                        foreach (var item in items)
                        {
                            var value = JsonValueConverter.FromNode(item, typeof(object));
                            if (value != null)
                                column.Items.Add(value);
                        }
                    }
                    break;
                case "truevalue":
                    column.TrueValue = JsonValueConverter.FromNode(kvp.Value, typeof(object));
                    break;
                case "falsevalue":
                    column.FalseValue = JsonValueConverter.FromNode(kvp.Value, typeof(object));
                    break;
                default:
                    ApplyProperty(column, kvp.Key, kvp.Value, warnings, translations);
                    break;
            }
        }

        return column;
    }

    private ToolStripItem? LoadStripItem(JsonObject node, List<string> warnings, List<TranslationRef> translations)
    {
        var typeName = GetString(node, "type");
        var type = ResolveType(typeName ?? string.Empty);
        if (type == null || !typeof(ToolStripItem).IsAssignableFrom(type))
        {
            warnings.Add($"Unknown strip item type '{typeName}' was skipped.");
            return null;
        }

        ToolStripItem item;
        try
        {
            item = (ToolStripItem)Activator.CreateInstance(type)!;
        }
        catch (Exception ex)
        {
            warnings.Add($"Strip item '{typeName}' could not be created: {ex.Message}");
            return null;
        }

        var declaredItemName = GetString(node, "name");
        if (!string.IsNullOrEmpty(declaredItemName))
            item.Name = declaredItemName;

        ApplyProperties(item, node["props"] as JsonObject, warnings, translations);

        var iconKey = GetString(node, "icon");
        if (!string.IsNullOrEmpty(iconKey))
        {
            var icon = ResolveIcon(iconKey);
            if (icon != null)
                item.Image = icon;
            else
                warnings.Add($"Icon '{iconKey}' could not be resolved for strip item '{declaredItemName}'.");
        }

        if (node["dropDownItems"] is JsonArray dropDownNodes && GetDropDownItems(item) is System.Collections.IList dropDowns)
        {
            foreach (var dropDownNode in dropDownNodes)
            {
                if (dropDownNode is JsonObject dropDownObj)
                {
                    var child = LoadStripItem(dropDownObj, warnings, translations);
                    if (child != null)
                        dropDowns.Add(child);
                }
            }
        }

        return item;
    }

    private ToolStripStatusLabel LoadStatusLabel(JsonObject node, List<string> warnings, List<TranslationRef> translations)
    {
        var label = new ToolStripStatusLabel();
        ApplyProperties(label, node["props"] as JsonObject, warnings, translations);
        return label;
    }

    private void ApplyProperties(object target, JsonObject? props, List<string> warnings, List<TranslationRef>? translations)
    {
        if (props == null) return;
        foreach (var kvp in props)
            ApplyProperty(target, kvp.Key, kvp.Value, warnings, translations);
    }

    private void ApplyProperty(object target, string name, JsonNode? value, List<string> warnings, List<TranslationRef>? translations)
    {
        var prop = FindProperty(target.GetType(), name);
        if (prop == null || !prop.CanWrite)
        {
            warnings.Add($"Unknown property '{name}' for '{target.GetType().Name}' was skipped.");
            return;
        }

        if (!JsonValueConverter.IsSupportedType(prop.PropertyType))
        {
            warnings.Add($"Property '{name}' of type '{prop.PropertyType.Name}' is not supported.");
            return;
        }

        if (value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text) && RuntimeI18n.IsReference(text))
        {
            var key = text[RuntimeI18n.Prefix.Length..];
            if (I18n == null)
            {
                warnings.Add($"Translation reference '{text}' was kept because no localization service is configured.");
            }
            else
            {
                value = JsonValue.Create(I18n.T(key));
                translations?.Add(new TranslationRef(target, prop, key));
            }
        }

        var converted = JsonValueConverter.FromNode(value, prop.PropertyType, warnings);
        if (converted == null) return;

        try
        {
            prop.SetValue(target, converted);
        }
        catch (Exception ex)
        {
            warnings.Add($"Property '{name}' on '{target.GetType().Name}' could not be set: {ex.Message}");
        }
    }

    private static System.Collections.IList? GetDropDownItems(ToolStripItem item)
    {
        var prop = item.GetType().GetProperty("DropDownItems", BindingFlags.Public | BindingFlags.Instance);
        return prop?.GetValue(item) as System.Collections.IList;
    }

    private static PropertyInfo? FindProperty(Type type, string name)
    {
        for (var t = type; t != null && t != typeof(object); t = t.BaseType)
        {
            foreach (var prop in t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase) &&
                    prop.GetIndexParameters().Length == 0)
                {
                    return prop;
                }
            }
        }

        return null;
    }

    private static string? GetString(JsonObject obj, string key)
    {
        foreach (var kvp in obj)
        {
            if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                return kvp.Value?.GetValue<string>();
        }

        return null;
    }

    private static int GetInt(JsonObject obj, string key, int fallback)
    {
        foreach (var kvp in obj)
        {
            if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    return kvp.Value?.GetValue<int>() ?? fallback;
                }
                catch
                {
                    return fallback;
                }
            }
        }

        return fallback;
    }

    /// <summary>
    /// Resolves a core icon key (for example <c>nav_first</c> or <c>NavFirst</c>) to a
    /// rasterizable <see cref="SvgImage"/> exposed by the <see cref="Icons"/> catalog.
    /// Underscores and hyphens are ignored and matching is case-insensitive.
    /// </summary>
    /// <param name="key">The icon key to resolve.</param>
    /// <returns>The resolved <see cref="SvgImage"/>, or <c>null</c> when the key is empty or unknown.</returns>
    public static SvgImage? ResolveIcon(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        var normalized = key.Replace("_", string.Empty).Replace("-", string.Empty);
        foreach (var prop in typeof(Icons).GetProperties(BindingFlags.Public | BindingFlags.Static))
        {
            if (prop.PropertyType == typeof(SvgImage) && prop.CanRead &&
                string.Equals(prop.Name, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return prop.GetValue(null) as SvgImage;
            }
        }

        return null;
    }
}
