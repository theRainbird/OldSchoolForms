using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Design;
using OldSchoolForms.Ui.Reports;

namespace OldSchoolForms.Ui.Designer.PropertyGrid;

/// <summary>
/// Uses reflection to enumerate public properties of a control
/// and wraps them in <see cref="PropertyDescriptor"/> instances
/// with categorization and filtering.
/// </summary>
public class PropertyService
{
    private static readonly HashSet<string> HiddenProperties = new()
    {
        "Controls", "Parent", "Site", "Handle", "WindowId",
        "CaptureControl", "IsOpaque", "Focused", "CapturingMouse",
        "EffectiveFont", "EffectiveZoom", "Dirty", "ClientSize",
        "ClientRectangle", "ClientSizePixels", "State", "ActiveControl",
        "BindingContext", "DataBindings", "Cursor", "CursorType",
        "Modal", "DialogResult", "WindowState", "FormBorderStyle",
        "RequiresRender", "HasDirtyDescendant", "CachedForm",
        "LayoutDrivenBoundsChange", "LayoutVersion", "LayoutSuspended",
        "LayoutDirty", "BackColorSet", "ForeColorSet", "Zoom",
        "SelectedItem", "SelectedIndex", "Items", "DropDownItems",
        "IsDropDownVisible", "IsSelected", "PreferredSize"
    };

    private static bool _editorsRegistered;
    private static readonly object _editorRegistrationLock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyService"/> and registers
    /// the built-in value editors.
    /// </summary>
    public PropertyService()
    {
        if (_editorsRegistered) return;
        lock (_editorRegistrationLock)
        {
            if (_editorsRegistered) return;
            ValueEditorRegistry.Register(new TabPagesEditor());
            ValueEditorRegistry.Register(new DataGridViewColumnsEditor());
            ValueEditorRegistry.Register(new ToolStripItemsEditor());
            ValueEditorRegistry.Register(new ToolStripItemImageEditor());
            ValueEditorRegistry.Register(new MenuStripItemsEditor());
            ValueEditorRegistry.Register(new StatusStripItemsEditor());
            ValueEditorRegistry.Register(new ToolStripMenuItemDropDownItemsEditor());
            ValueEditorRegistry.Register(new PaddingEditor());
            ValueEditorRegistry.Register(new FontEditor());
            _editorsRegistered = true;
        }
    }

    private static readonly HashSet<Type> SupportedTypes = new()
    {
        typeof(string), typeof(int), typeof(float), typeof(bool),
        typeof(Color), typeof(Point), typeof(Size), typeof(Padding),
        typeof(Font), typeof(FontStyle),
        typeof(AnchorStyles), typeof(DockStyle),
        typeof(FormBorderStyle),
        typeof(FormWindowState)
    };

    /// <summary>
    /// Gets all browsable property descriptors for the given control,
    /// grouped and sorted by category.
    /// </summary>
    /// <param name="control">The control to inspect.</param>
    /// <returns>A list of property descriptors.</returns>
    public List<PropertyDescriptor> GetProperties(Control control)
    {
        if (control == null) return new List<PropertyDescriptor>();

        var properties = control.GetType().GetProperties(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(p => p.CanRead)
            .Where(p => !HiddenProperties.Contains(p.Name) || HasEditorButton(p))
            .Where(p => IsSupportedType(p.PropertyType) || HasEditorButton(p))
            .Select(p => new PropertyDescriptor(p, control, ResolveEditor(p)))
            .OrderBy(p => GetCategoryOrder(p.Category))
            .ThenBy(p => p.Name)
            .ToList();

        // Also include inherited key properties from Control
        var inherited = typeof(Control).GetProperties(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(p => p.CanRead)
            .Where(p => !HiddenProperties.Contains(p.Name))
            .Where(p => IsSupportedType(p.PropertyType) || HasEditorButton(p))
            .Select(p => new PropertyDescriptor(p, control, ResolveEditor(p)))
            .OrderBy(p => GetCategoryOrder(p.Category))
            .ThenBy(p => p.Name);

        properties.AddRange(inherited);

        return properties.DistinctBy(p => p.Name).ToList();
    }

    /// <summary>
    /// Gets browsable property descriptors for an arbitrary bound object. When the target is a
    /// <see cref="Control"/> the control-specific enumeration is used; otherwise every declared
    /// readable property is returned regardless of type so the full property set is shown.
    /// Properties that cannot be edited inline are marked read-only.
    /// </summary>
    /// <param name="target">The object to inspect.</param>
    /// <returns>A list of property descriptors.</returns>
    public List<PropertyDescriptor> GetPropertiesFor(object target)
    {
        if (target == null) return new List<PropertyDescriptor>();
        if (target is Control control) return GetProperties(control);
        return GetPropertiesForObject(target);
    }

    /// <summary>
    /// Enumerates all declared readable properties of a non-control object (such as a
    /// <see cref="DataGridViewColumn"/>) without the supported-type filter.
    /// </summary>
    /// <param name="target">The object to inspect.</param>
    /// <returns>A list of property descriptors, sorted by category and name.</returns>
    public List<PropertyDescriptor> GetPropertiesForObject(object target)
    {
        if (target == null) return new List<PropertyDescriptor>();

        var readOnlyNames = GetReadOnlyPropertyNames(target);
        var noiseNames = GetNoisePropertyNames(target);
        var properties = target switch
        {
            ToolStripItem => EnumerateToolStripItemProperties(target.GetType()),
            ReportControl => EnumerateReportControlProperties(target.GetType()),
            _ => target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        };

        return properties
            .Where(p => p.CanRead && !noiseNames.Contains(p.Name))
            .Select(p => new PropertyDescriptor(p, target, ResolveEditor(p), readOnlyOverride: readOnlyNames.Contains(p.Name)))
            .OrderBy(p => GetCategoryOrder(p.Category))
            .ThenBy(p => p.Name)
            .ToList();
    }

    /// <summary>
    /// Enumerates the declared public properties of a <see cref="ToolStripItem"/>-derived type,
    /// walking up the inheritance chain until <see cref="ToolStripItem"/> itself (inclusive) so
    /// the shared item properties (Text, DisplayStyle, ...) are browsable as well.
    /// </summary>
    /// <param name="type">The concrete item type to enumerate.</param>
    /// <returns>The deduplicated property set from the type up to and including <see cref="ToolStripItem"/>.</returns>
    private static IEnumerable<PropertyInfo> EnumerateToolStripItemProperties(Type type)
    {
        var seen = new HashSet<string>();
        for (var t = type; t != null && typeof(ToolStripItem).IsAssignableFrom(t); t = t.BaseType)
        {
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (seen.Add(p.Name))
                    yield return p;
            }
        }
    }

    /// <summary>
    /// Enumerates the declared public properties of a <see cref="ReportControl"/>-derived type,
    /// walking up the inheritance chain until <see cref="ReportControl"/> itself (inclusive) so
    /// the shared report control properties (Left, Top, Width, Height, colors, font, ...) are
    /// browsable as well.
    /// </summary>
    /// <param name="type">The concrete report control type to enumerate.</param>
    /// <returns>The deduplicated property set from the type up to and including <see cref="ReportControl"/>.</returns>
    private static IEnumerable<PropertyInfo> EnumerateReportControlProperties(Type type)
    {
        var seen = new HashSet<string>();
        for (var t = type; t != null && typeof(ReportControl).IsAssignableFrom(t); t = t.BaseType)
        {
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (seen.Add(p.Name))
                    yield return p;
            }
        }
    }

    private static HashSet<string> GetReadOnlyPropertyNames(object target) => target switch
    {
        DataGridViewColumn => new HashSet<string> { "ButtonIcon", "HeaderCell" },
        ReportBand band when band.BandType is ReportBandType.GroupHeader or ReportBandType.GroupFooter
            => new HashSet<string> { "BandType", "DataSourceName" },
        ReportBand => new HashSet<string> { "BandType" },
        _ => new HashSet<string>()
    };

    /// <summary>
    /// Returns property names that should be hidden entirely from the grid for a given target,
    /// because they are runtime-only noise (hover/press state, derived display text) that a
    /// designer user cannot meaningfully edit.
    /// </summary>
    /// <param name="target">The object being inspected.</param>
    /// <returns>The set of property names to exclude from enumeration.</returns>
    private static HashSet<string> GetNoisePropertyNames(object target) => target switch
    {
        ToolStripItem => new HashSet<string> { "IsHovered", "IsPressed", "DisplayText", "Mnemonic" },
        Report => new HashSet<string>
        {
            "PageWidth", "PageHeight", "ReportHeader", "PageHeader", "Detail",
            "PageFooter", "ReportFooter", "Groups", "DataSource", "DataSources", "PageSettings"
        },
        ReportBand => new HashSet<string> { "Controls" },
        ReportChartControl => new HashSet<string>
        {
            "XAxis", "YAxis", "TrendLine", "Palette", "ChartPadding",
            "Right", "Bottom", "EffectiveFont"
        },
        ReportControl => new HashSet<string> { "Right", "Bottom", "EffectiveFont" },
        _ => new HashSet<string>()
    };

    private static bool HasEditorButton(PropertyInfo property)
        => property.GetCustomAttribute<EditorButtonAttribute>() != null;

    private static IValueEditor? ResolveEditor(PropertyInfo property)
        => ValueEditorRegistry.Find(property);

    private static bool IsSupportedType(Type type)
    {
        if (SupportedTypes.Contains(type)) return true;
        if (type.IsEnum) return true;
        if (type == typeof(string)) return true;
        return false;
    }

    private static int GetCategoryOrder(string category)
    {
        return category switch
        {
            "Darstellung" => 0,
            "Verhalten" => 1,
            "Layout" => 2,
            "Design" => 4,
            "Data" => 3,
            "Sonstiges" => 5,
            _ => 99
        };
    }
}
