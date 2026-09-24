using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Layout;

namespace OldSchoolForms.Ui.Designer.Roslyn;

/// <summary>
/// Parses a Windows Forms-style InitializeComponent method from C# source code
/// and reconstructs the control hierarchy on a <see cref="Designer.DesignSurface"/>.
/// </summary>
public class FormParser
{
    /// <summary>
    /// Delegate used to resolve type names to <see cref="Type"/> instances.
    /// Receives the type name, an optional namespace hint, and the full source code.
    /// Returns the resolved type, or null if the type cannot be found.
    /// </summary>
    /// <param name="typeName">The simple type name (e.g. "Button").</param>
    /// <param name="namespaceHint">An optional namespace hint for disambiguation.</param>
    /// <param name="sourceCode">The full source code of the file being parsed.</param>
    /// <returns>The resolved <see cref="Type"/>, or null if not found.</returns>
    public delegate Type? TypeResolver(string typeName, string? namespaceHint, string sourceCode);

    /// <summary>
    /// Gets or sets the type resolver delegate used to create control instances.
    /// The default resolver handles all built-in OldSchoolForms controls.
    /// Set this to resolve custom controls or <see cref="UserControl"/> types from external assemblies.
    /// </summary>
    public TypeResolver? TypeResolverCallback { get; set; }

    private static readonly Dictionary<string, Type> _builtinTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Button", typeof(Button) },
        { "TextBox", typeof(TextBox) },
        { "Label", typeof(Label) },
        { "CheckBox", typeof(CheckBox) },
        { "RadioButton", typeof(RadioButton) },
        { "ListBox", typeof(ListBox) },
        { "ComboBox", typeof(ComboBox) },
        { "ProgressBar", typeof(ProgressBar) },
        { "FontPicker", typeof(FontPicker) },
        { "TabControl", typeof(TabControl) },
        { "TabPage", typeof(TabPage) },
        { "StatusStrip", typeof(StatusStrip) },
        { "MenuStrip", typeof(MenuStrip) },
        { "ToolStrip", typeof(ToolStrip) },
        { "Panel", typeof(Panel) },
        { "FlowLayoutPanel", typeof(FlowLayoutPanel) },
        { "TableLayoutPanel", typeof(TableLayoutPanel) },
        { "CalendarView", typeof(CalendarView) },
        { "DataGridView", typeof(DataGridView) },
        { "ToolStripButton", typeof(ToolStripButton) },
        { "ToolStripLabel", typeof(ToolStripLabel) },
        { "ToolStripSeparator", typeof(ToolStripSeparator) },
        { "ToolStripTextBox", typeof(ToolStripTextBox) },
        { "ToolStripMenuItem", typeof(ToolStripMenuItem) },
    };

    private static readonly Dictionary<string, Color> _namedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        // Color static fields
        { "White", Color.White },
        { "Black", Color.Black },
        { "Red", Color.Red },
        { "Green", Color.Green },
        { "Blue", Color.Blue },
        { "Yellow", Color.Yellow },
        { "Transparent", Color.Transparent },
        { "Empty", Color.Empty },

        // SystemColors fields
        { "Control", SystemColors.Control },
        { "ControlText", SystemColors.ControlText },
        { "Window", SystemColors.Window },
        { "WindowText", SystemColors.WindowText },
        { "Highlight", SystemColors.Highlight },
        { "HighlightText", SystemColors.HighlightText },
        { "ActiveCaption", SystemColors.ActiveCaption },
        { "ActiveCaptionText", SystemColors.ActiveCaptionText },
        { "InactiveCaption", SystemColors.InactiveCaption },
        { "ControlLight", SystemColors.ControlLight },
        { "ControlDark", SystemColors.ControlDark },
        { "GrayText", SystemColors.GrayText },
    };

    /// <summary>
    /// Parses the given source code and populates the design surface with the reconstructed controls.
    /// </summary>
    /// <param name="sourceCode">The full C# source code of the form file (e.g., MainForm.cs or MainForm.Designer.cs).</param>
    /// <param name="surface">The design surface to populate.</param>
    /// <returns>A list of warnings or informational messages generated during parsing.</returns>
    /// <exception cref="ArgumentNullException">Thrown if sourceCode or surface is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if InitializeComponent cannot be found or parsed.</exception>
    public List<string> Parse(string sourceCode, Designer.DesignSurface surface)
    {
        if (sourceCode == null) throw new ArgumentNullException(nameof(sourceCode));
        if (surface == null) throw new ArgumentNullException(nameof(surface));

        var warnings = new List<string>();
        surface.ClearAll();

        // ClearAll detaches the previous root from the surface's render tree without
        // replacing it. Re-establish a fresh root so the controls added below are
        // parented to a control that is actually part of the surface (mirrors the
        // NewForm idiom of ClearAll followed by BeginDesignX). The root type (form vs.
        // user control) is chosen based on the loaded source's base class.
        if (IsUserControlSource(sourceCode))
            surface.BeginDesignUserControl();
        else
            surface.BeginDesignForm();

        try
        {
            var body = ExtractInitializeComponentBody(sourceCode);
            if (body == null)
            {
                throw new InvalidOperationException("Could not find InitializeComponent method in source code.");
            }

            var instances = ParseControlInstances(body);
            var assignments = ParsePropertyAssignments(body);
            var hierarchy = ParseControlHierarchy(body);

            foreach (var (fieldName, typeName, instance) in instances)
            {
                warnings.Add($"Parsed instance: {fieldName} ({typeName})");
            }

            var controlMap = new Dictionary<string, Core.Control>();
            var typeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (fieldName, typeName, instance) in instances)
            {
                typeMap[fieldName] = typeName;
                controlMap[fieldName] = instance;
            }

            foreach (var (fieldName, propMap) in assignments)
            {
                if (!controlMap.TryGetValue(fieldName, out var control))
                {
                    warnings.Add($"Warning: Could not find control '{fieldName}' for property assignment.");
                    continue;
                }

                ApplyPropertyAssignments(control, propMap, warnings);
            }

            foreach (var (parentFieldName, childFieldName) in hierarchy)
            {
                if (controlMap.TryGetValue(parentFieldName, out var parentControl) &&
                    controlMap.TryGetValue(childFieldName, out var childControl))
                {
                    parentControl.Controls.Add(childControl);
                    childControl.Parent = parentControl;
                }
                else
                {
                    warnings.Add($"Warning: Could not resolve hierarchy parent '{parentFieldName}' -> child '{childFieldName}'.");
                }
            }

            foreach (var (parentFieldName, item) in ParseToolStripItemAdds(body))
            {
                if (controlMap.TryGetValue(parentFieldName, out var parentControl) && parentControl is ToolStrip strip)
                {
                    strip.Items.Add(item);
                    warnings.Add($"Parsed tool strip item: {item.GetType().Name} on {parentFieldName}");
                }
                else
                {
                    warnings.Add($"Warning: Could not resolve tool strip parent '{parentFieldName}' for item '{item.GetType().Name}'.");
                }
            }

            // Register every control as a design item so that controls nested inside
            // containers become selectable too. Parents must be placed before their
            // children so each child can resolve its parent design item (mirrors the
            // AddControl(control, parent, localPoint) used by the toolbox drag-drop path).
            // Top-level controls are parented to the surface root; nested ones to their
            // container.
            var pending = new Dictionary<string, Core.Control>(controlMap, StringComparer.OrdinalIgnoreCase);
            var placed = new HashSet<Core.Control>();
            if (surface.RootControl != null)
                placed.Add(surface.RootControl);

            bool madeProgress = true;
            while (pending.Count > 0 && madeProgress)
            {
                madeProgress = false;
                foreach (var (fieldName, control) in pending.ToArray())
                {
                    ContainerControl? parent = control.Parent as ContainerControl;
                    bool topLevel = parent == null || ReferenceEquals(parent, surface.RootControl);
                    if (topLevel || placed.Contains(parent!))
                    {
                        DesignItem item = topLevel
                            ? surface.AddControl(control, control.Location)
                            : surface.AddControl(control, parent!, control.Location);
                        item.Selected = false;
                        warnings.Add($"Added to surface: {fieldName} at ({control.X}, {control.Y})");
                        pending.Remove(fieldName);
                        placed.Add(control);
                        madeProgress = true;
                    }
                }
            }

            // Any controls left after the ordering pass (e.g. a parent/child cycle) fall
            // back to top-level placement so nothing is silently dropped.
            foreach (var (fieldName, control) in pending.ToArray())
            {
                var item = surface.AddControl(control, control.Location);
                item.Selected = false;
                warnings.Add($"Added to surface (fallback): {fieldName}");
            }

            surface.Invalidate();
        }
        catch (Exception ex)
        {
            warnings.Add($"Parsing error: {ex.Message}");
        }

        return warnings;
    }

    /// <summary>
    /// Extracts the body of the InitializeComponent method from the source code.
    /// </summary>
    /// <param name="sourceCode">The full C# source code.</param>
    /// <returns>The method body as a string, or null if not found.</returns>
    private static string? ExtractInitializeComponentBody(string sourceCode)
    {
        var pattern = @"private\s+void\s+InitializeComponent\s*\(\s*\)\s*\{";
        var match = System.Text.RegularExpressions.Regex.Match(sourceCode, pattern);
        if (!match.Success) return null;

        int start = match.Index + match.Length;
        int depth = 1;
        int i = start;

        while (i < sourceCode.Length && depth > 0)
        {
            if (sourceCode[i] == '{') depth++;
            else if (sourceCode[i] == '}') depth--;
            i++;
        }

        if (depth != 0) return null;
        return sourceCode.Substring(start, i - start - 1);
    }

    private List<(string fieldName, string typeName, Core.Control instance)> ParseControlInstances(string body)
    {
        var result = new List<(string fieldName, string typeName, Core.Control instance)>();
        var pattern = @"(\w+)\s*=\s*new\s+(\w+)\s*\(\s*\)\s*;";
        var matches = System.Text.RegularExpressions.Regex.Matches(body, pattern);

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var fieldName = match.Groups[1].Value;
            var typeName = match.Groups[2].Value;

            Type? controlType = ResolveControlType(typeName);
            if (controlType == null)
            {
                continue;
            }

            var instance = (Core.Control)Activator.CreateInstance(controlType)!;
            result.Add((fieldName, typeName, instance));
        }

        return result;
    }

    private static Dictionary<string, Dictionary<string, string>> ParsePropertyAssignments(string body)
    {
        var result = new Dictionary<string, Dictionary<string, string>>();
        var pattern = @"(\w+)\.(\w+)\s*=\s*([^;]+)\s*;";
        var matches = System.Text.RegularExpressions.Regex.Matches(body, pattern);

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var fieldName = match.Groups[1].Value;
            var propName = match.Groups[2].Value;
            var rawValue = match.Groups[3].Value.Trim();

            if (rawValue.StartsWith("//") || rawValue.StartsWith("this."))
            {
                continue;
            }

            if (!result.TryGetValue(fieldName, out var propMap))
            {
                propMap = new Dictionary<string, string>();
                result[fieldName] = propMap;
            }

            propMap[propName] = rawValue;
        }

        return result;
    }

    private static List<(string parentFieldName, string childFieldName)> ParseControlHierarchy(string body)
    {
        var result = new List<(string parentFieldName, string childFieldName)>();
        var pattern = @"(\w+)\.Controls\.Add\((\w+)\)\s*;";
        var matches = System.Text.RegularExpressions.Regex.Matches(body, pattern);

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var parentFieldName = match.Groups[1].Value;
            var childFieldName = match.Groups[2].Value;
            result.Add((parentFieldName, childFieldName));
        }

        return result;
    }

    /// <summary>
    /// Finds every <c>toolStrip.Items.Add(new ToolStripXxx { ... })</c> call in the method body
    /// and reconstructs the corresponding <see cref="ToolStripItem"/> instances, including
    /// nested <c>DropDownItems</c> collection initializers.
    /// </summary>
    /// <param name="body">The InitializeComponent method body.</param>
    /// <returns>A list of (parent field name, reconstructed item) pairs in source order.</returns>
    private List<(string parentFieldName, ToolStripItem item)> ParseToolStripItemAdds(string body)
    {
        var result = new List<(string, ToolStripItem)>();
        var pattern = @"(\w+)\.Items\.Add\(\s*new\s+(\w+)";
        var matches = System.Text.RegularExpressions.Regex.Matches(body, pattern);

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var parentFieldName = match.Groups[1].Value;
            var typeName = match.Groups[2].Value;

            Type? itemType = ResolveControlType(typeName);
            if (itemType == null || !typeof(ToolStripItem).IsAssignableFrom(itemType))
                continue;

            int openParen = body.IndexOf('(', match.Index);
            if (openParen < 0) continue;
            int closeParen = FindMatchingClose(body, openParen, '(', ')');
            if (closeParen < 0) continue;

            string inner = body.Substring(openParen + 1, closeParen - openParen - 1).Trim();
            var item = ParseItemExpression(inner);
            if (item != null)
                result.Add((parentFieldName, item));
        }

        return result;
    }

    /// <summary>
    /// Parses a single item expression of the form <c>new Type</c>, <c>new Type()</c> or
    /// <c>new Type { Prop = value, Nested = { new Other { ... } } }</c> into a live item.
    /// </summary>
    /// <param name="expr">The expression text to parse.</param>
    /// <returns>The reconstructed item, or <c>null</c> when the expression is not a known item type.</returns>
    private ToolStripItem? ParseItemExpression(string expr)
    {
        var m = System.Text.RegularExpressions.Regex.Match(expr, @"^new\s+(\w+)");
        if (!m.Success) return null;

        Type? itemType = ResolveControlType(m.Groups[1].Value);
        if (itemType == null || !typeof(ToolStripItem).IsAssignableFrom(itemType))
            return null;

        var item = (ToolStripItem)Activator.CreateInstance(itemType)!;

        string rest = expr.Substring(m.Index + m.Length).Trim();
        if (!rest.StartsWith("{")) return item;

        int openBrace = rest.IndexOf('{');
        int closeBrace = FindMatchingClose(rest, openBrace, '{', '}');
        if (closeBrace < 0) return item;

        string initContent = rest.Substring(openBrace + 1, closeBrace - openBrace - 1);
        foreach (var member in SplitTopLevel(initContent))
        {
            int eq = member.IndexOf('=');
            if (eq < 0) continue;
            string propName = member.Substring(0, eq).Trim();
            string rawValue = member.Substring(eq + 1).Trim();

            var prop = itemType.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
            if (prop == null) continue;

            try
            {
                if (rawValue.StartsWith("{"))
                {
                    // Nested collection initializer: DropDownItems = { new X { ... }, ... }
                    int nbOpen = rawValue.IndexOf('{');
                    int nbClose = FindMatchingClose(rawValue, nbOpen, '{', '}');
                    if (nbClose < 0) continue;
                    if (prop.GetValue(item) is not System.Collections.IList list) continue;

                    foreach (var nestedExpr in SplitTopLevel(rawValue.Substring(nbOpen + 1, nbClose - nbOpen - 1)))
                    {
                        var nested = ParseItemExpression(nestedExpr.Trim());
                        if (nested != null) list.Add(nested);
                    }
                }
                else
                {
                    var value = ParsePropertyValue(prop.PropertyType, rawValue);
                    if (value != null) prop.SetValue(item, value);
                }
            }
            catch
            {
                // Silently skip members that cannot be applied
            }
        }

        return item;
    }

    /// <summary>
    /// Finds the index of the closing bracket matching the opening bracket at
    /// <paramref name="openIndex"/>, skipping string and char literals.
    /// </summary>
    /// <param name="s">The text to scan.</param>
    /// <param name="openIndex">Index of the opening bracket.</param>
    /// <param name="open">The opening bracket character.</param>
    /// <param name="closeChar">The closing bracket character.</param>
    /// <returns>The index of the matching closing bracket, or -1 when unbalanced.</returns>
    private static int FindMatchingClose(string s, int openIndex, char open, char closeChar)
    {
        int depth = 0;
        bool inString = false;
        bool inChar = false;

        for (int i = openIndex; i < s.Length; i++)
        {
            char c = s[i];
            if (inString)
            {
                if (c == '\\') { i++; continue; }
                if (c == '"') inString = false;
                continue;
            }
            if (inChar)
            {
                if (c == '\\') { i++; continue; }
                if (c == '\'') inChar = false;
                continue;
            }
            if (c == '"') { inString = true; continue; }
            if (c == '\'') { inChar = true; continue; }
            if (c == open) depth++;
            else if (c == closeChar)
            {
                depth--;
                if (depth == 0) return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Splits a text on top-level commas, ignoring commas nested inside braces,
    /// parentheses, strings or char literals.
    /// </summary>
    /// <param name="s">The text to split.</param>
    /// <returns>The top-level segments with surrounding whitespace trimmed.</returns>
    private static List<string> SplitTopLevel(string s)
    {
        var parts = new List<string>();
        int depth = 0;
        bool inString = false;
        bool inChar = false;
        int start = 0;

        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (inString)
            {
                if (c == '\\') { i++; continue; }
                if (c == '"') inString = false;
                continue;
            }
            if (inChar)
            {
                if (c == '\\') { i++; continue; }
                if (c == '\'') inChar = false;
                continue;
            }
            if (c == '"') { inString = true; continue; }
            if (c == '\'') { inChar = true; continue; }
            if (c == '{' || c == '(') depth++;
            else if (c == '}' || c == ')') depth--;
            else if (c == ',' && depth == 0)
            {
                parts.Add(s.Substring(start, i - start).Trim());
                start = i + 1;
            }
        }

        var last = s.Substring(start).Trim();
        if (last.Length > 0) parts.Add(last);
        return parts;
    }

    private static void ApplyPropertyAssignments(Core.Control control, Dictionary<string, string> propMap, List<string> warnings)
    {
        var controlType = control.GetType();

        foreach (var (propName, rawValue) in propMap)
        {
            try
            {
                var prop = controlType.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
                if (prop == null)
                {
                    continue;
                }

                var value = ParsePropertyValue(prop.PropertyType, rawValue);
                if (value != null)
                {
                    prop.SetValue(control, value);
                }
            }
            catch
            {
                // Silently skip properties that cannot be set
            }
        }
    }

    private static object? ParsePropertyValue(Type propType, string rawValue)
    {
        rawValue = rawValue.Trim();

        if (string.IsNullOrEmpty(rawValue)) return null;

        // String literal
        if (rawValue.StartsWith("\"") && rawValue.EndsWith("\""))
        {
            var inner = rawValue.Substring(1, rawValue.Length - 2);
            inner = inner.Replace("\\\"", "\"").Replace("\\\\", "\\").Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t");
            return inner;
        }

        // Boolean
        if (rawValue == "true") return true;
        if (rawValue == "false") return false;

        // Integer
        if (int.TryParse(rawValue, out var intVal)) return intVal;

        // new Point(...)
        if (rawValue.StartsWith("new Point("))
        {
            var m = System.Text.RegularExpressions.Regex.Match(rawValue, @"new\s+Point\s*\(\s*(-?\d+)\s*,\s*(-?\d+)\s*\)");
            if (m.Success)
            {
                return new Core.Point(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value));
            }
        }

        // new Size(...)
        if (rawValue.StartsWith("new Size("))
        {
            var m = System.Text.RegularExpressions.Regex.Match(rawValue, @"new\s+Size\s*\(\s*(-?\d+)\s*,\s*(-?\d+)\s*\)");
            if (m.Success)
            {
                return new Core.Size(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value));
            }
        }

        // Color.FromArgb(...)
        if (rawValue.StartsWith("Color.FromArgb("))
        {
            // Color.FromArgb(R, G, B) — opaque (3 args)
            var m = System.Text.RegularExpressions.Regex.Match(rawValue, @"Color\.FromArgb\s*\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)");
            if (m.Success)
            {
                return Core.Color.FromArgb(
                    int.Parse(m.Groups[1].Value),
                    int.Parse(m.Groups[2].Value),
                    int.Parse(m.Groups[3].Value));
            }

            // Color.FromArgb(R, G, B, A) — transparent (4 args)
            m = System.Text.RegularExpressions.Regex.Match(rawValue, @"Color\.FromArgb\s*\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)");
            if (m.Success)
            {
                return Core.Color.FromArgb(
                    int.Parse(m.Groups[1].Value),
                    int.Parse(m.Groups[2].Value),
                    int.Parse(m.Groups[3].Value),
                    int.Parse(m.Groups[4].Value));
            }

            // Legacy variants (ArgbFrom, Argb) — kept for backward compatibility
            m = System.Text.RegularExpressions.Regex.Match(rawValue, @"Color\.ArgbFrom\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)");
            if (m.Success)
            {
                return Core.Color.FromArgb(
                    int.Parse(m.Groups[1].Value),
                    int.Parse(m.Groups[2].Value),
                    int.Parse(m.Groups[3].Value),
                    int.Parse(m.Groups[4].Value));
            }

            m = System.Text.RegularExpressions.Regex.Match(rawValue, @"Color\.Argb\s*\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)");
            if (m.Success)
            {
                return Core.Color.FromArgb(
                    int.Parse(m.Groups[1].Value),
                    int.Parse(m.Groups[2].Value),
                    int.Parse(m.Groups[3].Value),
                    int.Parse(m.Groups[4].Value));
            }
        }

        // SvgImage.FromSvgResource("name", size)
        if (rawValue.StartsWith("SvgImage.FromSvgResource("))
        {
            var m = System.Text.RegularExpressions.Regex.Match(rawValue, @"SvgImage\.FromSvgResource\s*\(\s*""([^""]*)""\s*,\s*(\d+)\s*\)");
            if (m.Success)
            {
                return SvgImage.FromSvgResource(m.Groups[1].Value, int.Parse(m.Groups[2].Value));
            }
        }

        // this.someProperty (skip)
        if (rawValue.StartsWith("this.")) return null;

        // Enum (e.g., DockStyle.Fill)
        if (rawValue.Contains("."))
        {
            var parts = rawValue.Split('.');
            if (parts.Length == 2 && propType.IsEnum)
            {
                try
                {
                    return Enum.Parse(propType, parts[1]);
                }
                catch { /* ignore */ }
            }
        }

        // Named color references (e.g., Color.White, SystemColors.Control)
        if (propType == typeof(Color) && rawValue.Contains('.'))
        {
            var parts = rawValue.Split('.');
            if (parts.Length == 2)
            {
                string typeName = parts[0];
                string memberName = parts[1];

                if (typeName == "Color" || typeName == "SystemColors")
                {
                    if (_namedColors.TryGetValue(memberName, out var namedColor))
                        return namedColor;
                }
            }
        }

        return null;
    }

    private Type? ResolveControlType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return null;

        if (_builtinTypes.TryGetValue(typeName, out var builtinType))
        {
            return builtinType;
        }

        if (TypeResolverCallback != null)
        {
            var resolved = TypeResolverCallback(typeName, null, string.Empty);
            if (resolved != null) return resolved;
        }

        return null;
    }

    /// <summary>
    /// Attempts to detect whether the given source code contains a OldSchoolForms form definition.
    /// </summary>
    /// <param name="sourceCode">The C# source code to inspect.</param>
    /// <returns>True if the source appears to be a OldSchoolForms form.</returns>
    public static bool IsFormFile(string sourceCode)
    {
        if (string.IsNullOrEmpty(sourceCode)) return false;

        return sourceCode.Contains("class ") &&
               (sourceCode.Contains(" : Form") || sourceCode.Contains(" : ContainerControl")) &&
               (sourceCode.Contains("InitializeComponent") ||
                sourceCode.Contains("SuspendLayout") ||
                sourceCode.Contains("Controls.Add"));
    }

    /// <summary>
    /// Determines whether the given source declares a user control (derives from
    /// <c>ContainerControl</c> or <c>UserControl</c>) rather than a form (<c>Form</c>).
    /// Used to select the correct design root when loading a form file.
    /// </summary>
    /// <param name="sourceCode">The full C# source code of the file being loaded.</param>
    /// <returns>True if the source derives from a user-control base type; otherwise false.</returns>
    private static bool IsUserControlSource(string sourceCode)
    {
        if (string.IsNullOrEmpty(sourceCode)) return false;

        // A base-class clause looks like "class X : Form" or "class X : ContainerControl".
        // \b keeps identifiers such as "MyForm" from matching "Form".
        if (System.Text.RegularExpressions.Regex.IsMatch(sourceCode, @":\s*Form\b"))
            return false;

        return System.Text.RegularExpressions.Regex.IsMatch(sourceCode, @":\s*(ContainerControl|UserControl)\b");
    }
}
