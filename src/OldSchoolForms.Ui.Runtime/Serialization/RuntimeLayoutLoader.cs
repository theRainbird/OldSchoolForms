using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using OldSchoolForms.Ui.Controls;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Designer.Serialization;

namespace OldSchoolForms.Ui.Runtime.Serialization;

/// <summary>
/// Ergebnis eines Layout-Ladevorgangs: die am Namen auffindbaren Steuerelemente
/// sowie Warnungen über übersprungene Typen/Eigenschaften.
/// </summary>
public sealed class LayoutLoadResult
{
    /// <summary>
    /// Initialisiert eine neue Instanz.
    /// </summary>
    /// <param name="controls">Steuerelemente, keyed nach <see cref="Control.Name"/> (case-insensitive).</param>
    /// <param name="warnings">Warnungen aus dem Ladevorgang.</param>
    public LayoutLoadResult(IReadOnlyDictionary<string, Control> controls, IReadOnlyList<string> warnings)
    {
        Controls = controls;
        Warnings = warnings;
    }

    /// <summary>Die geladenen, benannten Steuerelemente.</summary>
    public IReadOnlyDictionary<string, Control> Controls { get; }

    /// <summary>Warnungen über unbekannte Typen oder Eigenschaften.</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>
    /// Sucht ein Steuerelement anhand seines Namens.
    /// </summary>
    /// <param name="name">Der Steuername.</param>
    /// <returns>Das gefundene Steuerelement oder <see langword="null"/>.</returns>
    public Control? Find(string name) =>
        !string.IsNullOrEmpty(name) && Controls.TryGetValue(name, out var c) ? c : null;
}

/// <summary>
/// Lädt Layout-Dokumente im <c>OldSchoolForms.Layout</c>-Format direkt in eine
/// Laufzeit-Steuerelementhierarchie (ohne <c>DesignSurface</c>). Das Format und die
/// Wertkonvertierung werden mit dem Designer geteilt.
/// </summary>
public sealed class RuntimeLayoutLoader
{
    private readonly Dictionary<string, Type> _customTypes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registriert einen zusätzlichen Steuerelementtyp unter einem einfachen Namen.
    /// </summary>
    /// <param name="typeName">Der im Layout verwendete Typname.</param>
    /// <param name="type">Der CLR-Typ (muss von <see cref="Control"/> oder <see cref="ToolStripItem"/> abstammen).</param>
    public void Register(string typeName, Type type)
    {
        if (string.IsNullOrWhiteSpace(typeName)) throw new ArgumentException("Typname fehlt.", nameof(typeName));
        ArgumentNullException.ThrowIfNull(type);
        _customTypes[typeName] = type;
    }

    /// <summary>
    /// Löst einen Typnamen auf: zuerst eigene Registrierung, dann die bekannten
    /// Designer-Typen.
    /// </summary>
    /// <param name="typeName">Der einfache Typname.</param>
    /// <returns>Der Typ oder <see langword="null"/>.</returns>
    public Type? ResolveType(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName)) return null;
        if (_customTypes.TryGetValue(typeName, out var custom)) return custom;
        return LayoutJsonSerializer.ResolveType(typeName);
    }

    /// <summary>
    /// Lädt ein Layout-Dokument in einen Zielcontainer.
    /// </summary>
    /// <param name="target">Der Container, dem die Wurzeln des Layouts hinzugefügt werden.</param>
    /// <param name="json">Das Layout-JSON.</param>
    /// <returns>Das Ladeergebnis mit benannten Steuerelementen und Warnungen.</returns>
    /// <exception cref="InvalidOperationException">Das JSON ist ungültig.</exception>
    public LayoutLoadResult LoadInto(ContainerControl target, string json)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(json);

        var warnings = new List<string>();
        var controls = new Dictionary<string, Control>(StringComparer.OrdinalIgnoreCase);

        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Ungültiges JSON-Dokument: {ex.Message}", ex);
        }

        if (parsed is not JsonObject doc)
            throw new InvalidOperationException("Die JSON-Wurzel muss ein Objekt sein.");

        var format = GetString(doc, "format");
        if (format != LayoutJsonSerializer.FormatId)
            warnings.Add($"Unerwartetes Dokumentformat: '{format}'.");

        var version = GetInt(doc, "version", LayoutJsonSerializer.FormatVersion);
        if (version > LayoutJsonSerializer.FormatVersion)
            warnings.Add($"Formatversion {version} ist neuer als die unterstützte Version {LayoutJsonSerializer.FormatVersion}.");

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
                    LoadControl(controlNode, target, warnings, controls);
            }
        }

        return new LayoutLoadResult(controls, warnings);
    }

    private void LoadControl(JsonObject node, Control parent, List<string> warnings, Dictionary<string, Control> controls)
    {
        var typeName = GetString(node, "type");
        var type = ResolveType(typeName ?? string.Empty);
        if (type == null || !typeof(Control).IsAssignableFrom(type))
        {
            warnings.Add($"Unbekannter Steuerelementtyp '{typeName}' wurde übersprungen.");
            return;
        }

        Control control;
        try
        {
            control = (Control)Activator.CreateInstance(type)!;
        }
        catch (Exception ex)
        {
            warnings.Add($"Steuerelement '{typeName}' konnte nicht erstellt werden: {ex.Message}");
            return;
        }

        var declaredName = GetString(node, "name");
        if (!string.IsNullOrEmpty(declaredName))
            control.Name = declaredName;

        ApplyProperties(control, node["props"] as JsonObject, warnings);

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
                    grid.Columns.Add(LoadColumn(columnObj, warnings));
            }
        }

        if (control is ToolStrip toolStrip && node["items"] is JsonArray stripItems)
        {
            foreach (var itemNode in stripItems)
            {
                if (itemNode is JsonObject itemObj)
                {
                    var stripItem = LoadStripItem(itemObj, warnings);
                    if (stripItem != null)
                        toolStrip.Items.Add(stripItem);
                }
            }
        }

        if (control is StatusStrip statusStrip && node["items"] is JsonArray statusItems)
        {
            foreach (var itemNode in statusItems)
            {
                if (itemNode is JsonObject itemObj)
                {
                    var label = LoadStatusLabel(itemObj, warnings);
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
                        LoadControl(childObj, container, warnings, controls);
                    else
                        warnings.Add($"Steuerelement '{typeName}' kann keine Kinder aufnehmen.");
                }
            }
        }
    }

    private DataGridViewColumn LoadColumn(JsonObject node, List<string> warnings)
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
                    ApplyProperty(column, kvp.Key, kvp.Value, warnings);
                    break;
            }
        }

        return column;
    }

    private ToolStripItem? LoadStripItem(JsonObject node, List<string> warnings)
    {
        var typeName = GetString(node, "type");
        var type = ResolveType(typeName ?? string.Empty);
        if (type == null || !typeof(ToolStripItem).IsAssignableFrom(type))
        {
            warnings.Add($"Unbekannter Strip-Item-Typ '{typeName}' wurde übersprungen.");
            return null;
        }

        ToolStripItem item;
        try
        {
            item = (ToolStripItem)Activator.CreateInstance(type)!;
        }
        catch (Exception ex)
        {
            warnings.Add($"Strip-Item '{typeName}' konnte nicht erstellt werden: {ex.Message}");
            return null;
        }

        ApplyProperties(item, node["props"] as JsonObject, warnings);

        if (node["dropDownItems"] is JsonArray dropDownNodes && GetDropDownItems(item) is System.Collections.IList dropDowns)
        {
            foreach (var dropDownNode in dropDownNodes)
            {
                if (dropDownNode is JsonObject dropDownObj)
                {
                    var child = LoadStripItem(dropDownObj, warnings);
                    if (child != null)
                        dropDowns.Add(child);
                }
            }
        }

        return item;
    }

    private static ToolStripStatusLabel LoadStatusLabel(JsonObject node, List<string> warnings)
    {
        var label = new ToolStripStatusLabel();
        ApplyProperties(label, node["props"] as JsonObject, warnings);
        return label;
    }

    private static void ApplyProperties(object target, JsonObject? props, List<string> warnings)
    {
        if (props == null) return;
        foreach (var kvp in props)
            ApplyProperty(target, kvp.Key, kvp.Value, warnings);
    }

    private static void ApplyProperty(object target, string name, JsonNode? value, List<string> warnings)
    {
        var prop = FindProperty(target.GetType(), name);
        if (prop == null || !prop.CanWrite)
        {
            warnings.Add($"Unbekannte Eigenschaft '{name}' für '{target.GetType().Name}' wurde übersprungen.");
            return;
        }

        if (!JsonValueConverter.IsSupportedType(prop.PropertyType))
        {
            warnings.Add($"Eigenschaft '{name}' vom Typ '{prop.PropertyType.Name}' wird nicht unterstützt.");
            return;
        }

        var converted = JsonValueConverter.FromNode(value, prop.PropertyType, warnings);
        if (converted == null) return;

        try
        {
            prop.SetValue(target, converted);
        }
        catch (Exception ex)
        {
            warnings.Add($"Eigenschaft '{name}' auf '{target.GetType().Name}' konnte nicht gesetzt werden: {ex.Message}");
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
}
