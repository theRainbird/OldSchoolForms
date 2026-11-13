using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using OldSchoolForms.Ui.Controls;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Controls.Separators;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Designer.Services;
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

            if (surface.EventBindings.HasBindings(root))
            {
                var rootEvents = new JsonObject();
                foreach (var kvp in surface.EventBindings.GetBindings(root))
                    rootEvents[kvp.Key] = kvp.Value;
                doc["rootEvents"] = rootEvents;
            }
        }

        var controls = new JsonArray();
        if (root != null)
        {
            foreach (Control child in root.Controls)
                controls.Add(SaveControl(child, surface.EventBindings));
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
        surface.EventBindings.Clear();
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

        if (doc["rootEvents"] is JsonObject rootEvents && surface.RootControl != null)
        {
            foreach (var kvp in rootEvents)
            {
                string? handler;
                try
                {
                    handler = kvp.Value?.GetValue<string>();
                }
                catch
                {
                    handler = null;
                }
                if (!string.IsNullOrWhiteSpace(handler))
                    surface.EventBindings.SetHandler(surface.RootControl, kvp.Key, handler);
            }
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

    private static JsonObject SaveControl(Control control, EventBindingStore? store)
    {
        var node = new JsonObject
        {
            ["type"] = control.GetType().Name
        };

        if (!string.IsNullOrEmpty(control.Name))
            node["name"] = control.Name;

        node["props"] = SaveProperties(control);

        AddEventsNode(node, control, store);

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
                items.Add(SaveStripItem(item, store));
            node["items"] = items;
        }

        if (control is StatusStrip statusStrip && statusStrip.Items.Count > 0)
        {
            var items = new JsonArray();
            foreach (var label in statusStrip.Items)
                items.Add(SaveStatusLabel(label, store));
            node["items"] = items;
        }

        if (control is TableLayoutPanel table)
        {
            if (table.RowStyles.Count > 0)
                node["rowStyles"] = SaveStyles(table.RowStyles);
            if (table.ColumnStyles.Count > 0)
                node["columnStyles"] = SaveStyles(table.ColumnStyles);

            JsonNode? cellMap = null;
            foreach (Control child in table.Controls)
            {
                var cell = table.GetCellPosition(child);
                if (cell.HasValue && !string.IsNullOrEmpty(child.Name))
                {
                    cellMap ??= new JsonObject();
                    cellMap.AsObject()[child.Name] = new JsonObject
                    {
                        ["row"] = cell.Value.Row,
                        ["col"] = cell.Value.Col
                    };
                }
            }
            if (cellMap != null)
                node["cellMap"] = cellMap;
        }

        var children = new JsonArray();
        foreach (Control child in control.Controls)
            children.Add(SaveControl(child, store));
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

    /// <summary>
    /// Writes an <c>events</c> object mapping event names to handler method names
    /// into the given node when the target has any event bindings recorded.
    /// </summary>
    /// <param name="node">The JSON object to enrich with an <c>events</c> member.</param>
    /// <param name="target">The control or strip item whose bindings are recorded.</param>
    /// <param name="store">The event binding store, or <c>null</c> to skip.</param>
    private static void AddEventsNode(JsonObject node, object target, EventBindingStore? store)
    {
        if (store == null || !store.HasBindings(target))
            return;

        var events = new JsonObject();
        foreach (var kvp in store.GetBindings(target))
            events[kvp.Key] = kvp.Value;

        if (events.Count > 0)
            node["events"] = events;
    }

    /// <summary>
    /// Reads an <c>events</c> object from the given node and registers each
    /// event-to-handler mapping against the freshly created target instance.
    /// </summary>
    /// <param name="node">The JSON object that may contain an <c>events</c> member.</param>
    /// <param name="target">The control or strip item to bind handlers to.</param>
    /// <param name="store">The event binding store, or <c>null</c> to skip.</param>
    private static void ApplyEventsNode(JsonObject node, object target, EventBindingStore? store)
    {
        if (store == null)
            return;

        if (node["events"] is not JsonObject events)
            return;

        foreach (var kvp in events)
        {
            string? handler;
            try
            {
                handler = kvp.Value?.GetValue<string>();
            }
            catch
            {
                handler = null;
            }

            if (!string.IsNullOrWhiteSpace(handler))
                store.SetHandler(target, kvp.Key, handler);
        }
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

    private static JsonObject SaveStripItem(ToolStripItem item, EventBindingStore? store)
    {
        var node = new JsonObject
        {
            ["type"] = item.GetType().Name,
            ["props"] = SaveProperties(item)
        };

        AddEventsNode(node, item, store);

        if (GetDropDownItems(item) is System.Collections.IList dropDowns && dropDowns.Count > 0)
        {
            var children = new JsonArray();
            foreach (var child in dropDowns)
            {
                if (child is ToolStripItem childItem)
                    children.Add(SaveStripItem(childItem, store));
            }
            if (children.Count > 0)
                node["dropDownItems"] = children;
        }

        return node;
    }

    private static JsonObject SaveStatusLabel(ToolStripStatusLabel label, EventBindingStore? store)
    {
        var node = new JsonObject
        {
            ["type"] = label.GetType().Name,
            ["props"] = SaveProperties(label)
        };

        AddEventsNode(node, label, store);

        return node;
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

        ApplyEventsNode(node, control, surface.EventBindings);

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
                    var stripItem = LoadStripItem(itemObj, warnings, surface.EventBindings);
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
                    var label = LoadStatusLabel(itemObj, warnings, surface.EventBindings);
                    if (label != null)
                        statusStrip.Items.Add(label);
                }
            }
        }

        if (control is TableLayoutPanel table)
        {
            LoadStyles(table.RowStyles, node["rowStyles"] as JsonArray, warnings);
            LoadStyles(table.ColumnStyles, node["columnStyles"] as JsonArray, warnings);
        }

        if (node["children"] is JsonArray children)
        {
            foreach (var childNode in children)
            {
                if (childNode is JsonObject childObj)
                    LoadControl(childObj, surface, control as ContainerControl, warnings);
            }
        }

        // Cell positions reference child controls by name, so they can only be
        // applied once the children exist.
        if (control is TableLayoutPanel tableWithCells && node["cellMap"] is JsonObject cellMap)
        {
            foreach (var kvp in cellMap)
            {
                if (kvp.Value is not JsonObject cellObj) continue;
                var child = tableWithCells.Controls.OfType<Control>()
                    .FirstOrDefault(c => c.Name == kvp.Key);
                if (child == null)
                {
                    warnings.Add($"Zelle für Control '{kvp.Key}' nicht gefunden (Name existiert nicht).");
                    continue;
                }
                var row = GetInt(cellObj, "row", 0);
                var col = GetInt(cellObj, "col", 0);
                tableWithCells.SetCellPosition(child, row, col);
            }
            tableWithCells.InvalidateTableLayout();
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

    private static JsonArray SaveStyles(System.Collections.Generic.IList<TableLayoutStyle> styles)
    {
        var array = new JsonArray();
        foreach (var style in styles)
        {
            array.Add(new JsonObject
            {
                ["sizeType"] = style.SizeType.ToString(),
                ["value"] = style.Value
            });
        }
        return array;
    }

    private static void LoadStyles(System.Collections.Generic.IList<TableLayoutStyle> target, JsonArray? array, List<string> warnings)
    {
        if (array == null) return;

        foreach (var node in array)
        {
            if (node is not JsonObject obj) continue;

            var style = new TableLayoutStyle();

            var sizeTypeText = GetString(obj, "sizeType");
            if (!string.IsNullOrEmpty(sizeTypeText) &&
                Enum.TryParse<SizeType>(sizeTypeText, true, out var parsedSizeType))
            {
                style.SizeType = parsedSizeType;
            }

            var valueNode = obj["value"];
            if (valueNode != null)
            {
                try
                {
                    style.Value = (float)valueNode.GetValue<double>();
                }
                catch
                {
                    warnings.Add($"Ungültiger Style-Wert '{valueNode}' wurde übersprungen.");
                }
            }

            target.Add(style);
        }
    }

    private static ToolStripItem? LoadStripItem(JsonObject node, List<string> warnings, EventBindingStore? store)
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

        ApplyEventsNode(node, item, store);

        if (node["dropDownItems"] is JsonArray dropDownNodes && GetDropDownItems(item) is System.Collections.IList dropDowns)
        {
            foreach (var dropDownNode in dropDownNodes)
            {
                if (dropDownNode is JsonObject dropDownObj)
                {
                    var child = LoadStripItem(dropDownObj, warnings, store);
                    if (child != null)
                        dropDowns.Add(child);
                }
            }
        }

        return item;
    }

    private static ToolStripStatusLabel? LoadStatusLabel(JsonObject node, List<string> warnings, EventBindingStore? store)
    {
        var label = new ToolStripStatusLabel();
        ApplyProperties(label, node["props"] as JsonObject, warnings);
        ApplyEventsNode(node, label, store);
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
