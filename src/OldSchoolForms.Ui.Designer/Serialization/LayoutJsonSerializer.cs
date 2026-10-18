using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using OldSchoolForms.Ui.Controls;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Controls.Separators;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Layout;
using OldSchoolForms.Ui.Serialization;

namespace OldSchoolForms.Ui.Designer.Serialization;

/// <summary>
/// Serializes the control tree of a <see cref="DesignSurface"/> to the
/// <c>OldSchoolForms.Layout</c> JSON format and reconstructs a design surface
/// from such a document. The format records the root type (form or user control),
/// the namespace and class name (so C# code can still be regenerated after a
/// JSON round-trip) and the full control hierarchy including tab pages,
/// data grid columns and tool/menu/status strip items.
/// </summary>
internal static class LayoutJsonSerializer
{
    /// <summary>The format identifier written into every layout document.</summary>
    public const string FormatId = "OldSchoolForms.Layout";

    /// <summary>The current format version written into every layout document.</summary>
    public const int FormatVersion = 1;

    private static readonly HashSet<string> ExcludedProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "SelectionStart",
        "SelectionLength",
        "SelectedIndex"
    };

    private static readonly Dictionary<string, Type> KnownTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Button", typeof(Button) },
        { "Label", typeof(Label) },
        { "TextBox", typeof(TextBox) },
        { "CheckBox", typeof(CheckBox) },
        { "RadioButton", typeof(RadioButton) },
        { "ComboBox", typeof(ComboBox) },
        { "ListBox", typeof(ListBox) },
        { "ProgressBar", typeof(ProgressBar) },
        { "DateTimePicker", typeof(DateTimePicker) },
        { "PictureBox", typeof(PictureBox) },
        { "FontPicker", typeof(FontPicker) },
        { "MemoBox", typeof(MemoBox) },
        { "Spinner", typeof(Spinner) },
        { "Panel", typeof(Panel) },
        { "GroupBox", typeof(GroupBox) },
        { "TabControl", typeof(TabControl) },
        { "TabPage", typeof(TabPage) },
        { "SplitPanel", typeof(SplitPanel) },
        { "FlowLayoutPanel", typeof(FlowLayoutPanel) },
        { "TableLayoutPanel", typeof(TableLayoutPanel) },
        { "MenuStrip", typeof(MenuStrip) },
        { "ToolStrip", typeof(ToolStrip) },
        { "DataGridView", typeof(DataGridView) },
        { "TreeView", typeof(TreeView) },
        { "CalendarView", typeof(CalendarView) },
        { "HtmlBox", typeof(HtmlBox) },
        { "DiagramView", typeof(DiagramView) },
        { "PivotTable", typeof(PivotTable) },
        { "KanbanBoardView", typeof(KanbanBoardView) },
        { "StatusStrip", typeof(StatusStrip) },
        { "SeperatorControl", typeof(SeperatorControl) },
        { "ToolStripButton", typeof(ToolStripButton) },
        { "ToolStripLabel", typeof(ToolStripLabel) },
        { "ToolStripSeparator", typeof(ToolStripSeparator) },
        { "ToolStripTextBox", typeof(ToolStripTextBox) },
        { "ToolStripMenuItem", typeof(ToolStripMenuItem) },
        { "ToolStripStatusLabel", typeof(ToolStripStatusLabel) },
    };

    /// <summary>
    /// Resolves a control or strip item type by its simple name.
    /// </summary>
    /// <param name="typeName">The simple type name (e.g. <c>"Button"</c>).</param>
    /// <returns>The resolved type, or <see langword="null"/> when unknown.</returns>
    public static Type? ResolveType(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName)) return null;
        return KnownTypes.TryGetValue(typeName, out var type) ? type : null;
    }

    /// <summary>
    /// Serializes the current surface state into an indented layout JSON string.
    /// </summary>
    /// <param name="surface">The design surface to serialize.</param>
    /// <param name="namespaceName">The namespace to record for later C# regeneration.</param>
    /// <param name="className">The class name to record for later C# regeneration.</param>
    /// <returns>A JSON string describing the layout.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="surface"/> is null.</exception>
    public static string SaveToJson(DesignSurface surface, string namespaceName, string className)
    {
        if (surface == null) throw new ArgumentNullException(nameof(surface));
        return BuildDocument(surface, namespaceName, className)
            .ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Builds the layout document object for the current surface state.
    /// </summary>
    /// <param name="surface">The design surface to serialize.</param>
    /// <param name="namespaceName">The namespace to record for later C# regeneration.</param>
    /// <param name="className">The class name to record for later C# regeneration.</param>
    /// <returns>The root JSON object of the layout document.</returns>
    public static JsonObject BuildDocument(DesignSurface surface, string namespaceName, string className)
    {
        if (surface == null) throw new ArgumentNullException(nameof(surface));

        var doc = new JsonObject
        {
            ["format"] = FormatId,
            ["version"] = FormatVersion,
            ["rootType"] = surface.RootControl is DesignUserControl ? "UserControl" : "Form",
            ["namespace"] = namespaceName ?? string.Empty,
            ["className"] = className ?? string.Empty
        };

        var root = surface.RootControl;
        if (root != null)
        {
            doc["rootSize"] = new JsonObject { ["width"] = root.Width, ["height"] = root.Height };
        }

        var controls = new JsonArray();
        if (root != null)
        {
            foreach (Control child in root.Controls)
                controls.Add(SaveControl(child));
        }
        doc["controls"] = controls;

        return doc;
    }

    /// <summary>
    /// Loads a layout document into the given surface, replacing its current content.
    /// </summary>
    /// <param name="surface">The design surface to populate.</param>
    /// <param name="json">The layout JSON string.</param>
    /// <returns>A list of warnings produced while loading (unknown types/properties).</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="surface"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the JSON is malformed.</exception>
    public static List<string> LoadFromJson(DesignSurface surface, string json)
    {
        if (surface == null) throw new ArgumentNullException(nameof(surface));
        if (json == null) throw new ArgumentNullException(nameof(json));

        var warnings = new List<string>();

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
        if (format != FormatId)
            warnings.Add($"Unerwartetes Dokumentformat: '{format}'.");

        var version = GetInt(doc, "version", FormatVersion);
        if (version > FormatVersion)
            warnings.Add($"Formatversion {version} ist neuer als die unterstützte Version {FormatVersion}.");

        var rootType = GetString(doc, "rootType") ?? "Form";

        surface.ClearAll();
        if (string.Equals(rootType, "UserControl", StringComparison.OrdinalIgnoreCase))
            surface.BeginDesignUserControl();
        else
            surface.BeginDesignForm();

        if (doc["rootSize"] is JsonObject rootSize && surface.RootControl != null)
        {
            int width = GetInt(rootSize, "width", surface.RootControl.Width);
            int height = GetInt(rootSize, "height", surface.RootControl.Height);
            surface.RootControl.Size = new Size(width, height);
        }

        bool oldSnap = surface.SnapToGrid;
        surface.SnapToGrid = false;
        try
        {
            if (doc["controls"] is JsonArray controls)
            {
                foreach (var node in controls)
                {
                    if (node is JsonObject controlNode)
                        LoadControl(controlNode, surface, null, warnings);
                }
            }
        }
        finally
        {
            surface.SnapToGrid = oldSnap;
        }

        surface.Invalidate();
        return warnings;
    }

    private static JsonObject SaveControl(Control control)
    {
        var node = new JsonObject
        {
            ["type"] = control.GetType().Name
        };

        if (!string.IsNullOrEmpty(control.Name))
            node["name"] = control.Name;

        node["props"] = SaveProperties(control);

        if (control is DataGridView grid && grid.Columns.Count > 0)
        {
            var columns = new JsonArray();
            foreach (var column in grid.Columns)
                columns.Add(SaveColumn(column));
            node["columns"] = columns;
        }

        if (control is ToolStrip strip && strip.Items.Count > 0)
        {
            var items = new JsonArray();
            foreach (var item in strip.Items)
                items.Add(SaveStripItem(item));
            node["items"] = items;
        }

        if (control is StatusStrip statusStrip && statusStrip.Items.Count > 0)
        {
            var items = new JsonArray();
            foreach (var label in statusStrip.Items)
                items.Add(SaveStatusLabel(label));
            node["items"] = items;
        }

        var children = new JsonArray();
        foreach (Control child in control.Controls)
            children.Add(SaveControl(child));
        if (children.Count > 0)
            node["children"] = children;

        return node;
    }

    private static JsonObject SaveProperties(object target)
    {
        var obj = new JsonObject();

        object? defaults = null;
        try
        {
            defaults = Activator.CreateInstance(target.GetType());
        }
        catch
        {
            // Types without a parameterless constructor fall back to "no defaults".
        }

        foreach (var prop in target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanRead || !prop.CanWrite || prop.GetIndexParameters().Length > 0)
                continue;
            if (ExcludedProperties.Contains(prop.Name))
                continue;
            if (!JsonValueConverter.IsSupportedType(prop.PropertyType))
                continue;

            object? current;
            try
            {
                current = prop.GetValue(target);
            }
            catch
            {
                continue;
            }

            if (current == null)
                continue;

            object? defaultValue = null;
            if (defaults != null)
            {
                try
                {
                    defaultValue = prop.GetValue(defaults);
                }
                catch
                {
                    defaultValue = null;
                }
            }

            if (JsonValueConverter.ValuesEqual(current, defaultValue))
                continue;

            var jsonValue = JsonValueConverter.ToNode(current);
            if (jsonValue != null)
                obj[ToCamelCase(prop.Name)] = jsonValue;
        }

        return obj;
    }

    private static JsonObject SaveColumn(DataGridViewColumn column)
    {
        var obj = SaveProperties(column);

        if (column.ValueType != null)
            obj["valueType"] = JsonValueConverter.TypeToString(column.ValueType);

        if (column.Items != null && column.Items.Count > 0)
        {
            var items = new JsonArray();
            foreach (var item in column.Items)
                items.Add(JsonValueConverter.ToNode(item));
            obj["items"] = items;
        }

        if (column.TrueValue != null)
            obj["trueValue"] = JsonValueConverter.ToNode(column.TrueValue);

        if (column.FalseValue != null)
            obj["falseValue"] = JsonValueConverter.ToNode(column.FalseValue);

        return obj;
    }

    private static JsonObject SaveStripItem(ToolStripItem item)
    {
        var node = new JsonObject
        {
            ["type"] = item.GetType().Name,
            ["props"] = SaveProperties(item)
        };

        if (GetDropDownItems(item) is System.Collections.IList dropDowns && dropDowns.Count > 0)
        {
            var children = new JsonArray();
            foreach (var child in dropDowns)
            {
                if (child is ToolStripItem childItem)
                    children.Add(SaveStripItem(childItem));
            }
            if (children.Count > 0)
                node["dropDownItems"] = children;
        }

        return node;
    }

    private static JsonObject SaveStatusLabel(ToolStripStatusLabel label)
    {
        return new JsonObject
        {
            ["type"] = label.GetType().Name,
            ["props"] = SaveProperties(label)
        };
    }

    private static void LoadControl(JsonObject node, DesignSurface surface, ContainerControl? parent, List<string> warnings)
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

        ApplyProperties(control, node["props"] as JsonObject, warnings);

        if (parent is TabControl tabControl && control is TabPage page)
            tabControl.AddTabPage(page);

        var root = surface.RootControl;
        if (root == null)
        {
            warnings.Add("Keine Design-Root vorhanden; Steuerelement '" + typeName + "' nicht platziert.");
            return;
        }

        var item = surface.AddControl(control, parent ?? root, control.Location);
        item.Selected = false;

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
                    LoadControl(childObj, surface, control as ContainerControl, warnings);
            }
        }
    }

    private static DataGridViewColumn LoadColumn(JsonObject node, List<string> warnings)
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

    private static ToolStripItem? LoadStripItem(JsonObject node, List<string> warnings)
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

    private static ToolStripStatusLabel? LoadStatusLabel(JsonObject node, List<string> warnings)
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

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name) || char.IsLower(name[0]))
            return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
