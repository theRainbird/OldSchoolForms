using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Designer.Services;

namespace OldSchoolForms.Ui.Designer.PropertyGrid;

/// <summary>
/// Enumerates the designer-browsable events of a control or tool strip item and produces
/// delegate-backed <see cref="PropertyDescriptor"/> instances whose string value is the name of the
/// event handler wired to that event, read from and written to an <see cref="EventBindingStore"/>.
/// <para>
/// Only a curated set of commonly used events is exposed; infrastructure and rarely-edited events
/// (such as <c>PropertyChanged</c> or <c>BoundsChanged</c>) are intentionally hidden so the events
/// view stays focused, mirroring the trimmed event list of the WinForms designer.
/// </para>
/// </summary>
public static class EventService
{
    /// <summary>
    /// The category name used for every event row in the grid.
    /// </summary>
    public const string EventCategory = "Ereignisse";

    /// <summary>
    /// The curated set of event names exposed in the designer's events view.
    /// </summary>
    private static readonly HashSet<string> BrowsableEvents = new(StringComparer.Ordinal)
    {
        "Click",
        "DoubleClick",
        "TextChanged",
        "CheckedChanged",
        "SelectedIndexChanged",
        "ValueChanged",
        "SelectedFontChanged",
        "ActiveChanged",
        "ImageChanged",
        "GotFocus",
        "LostFocus",
        "MouseDown",
        "MouseUp",
        "MouseMove",
        "MouseWheel",
        "MouseEnter",
        "MouseLeave",
        "KeyDown",
        "KeyPress",
        "KeyUp",
        "Shown",
        "Resize",
        "FormClosing"
    };

    /// <summary>
    /// Gets whether <paramref name="target"/> is a kind of object whose events can be edited in the
    /// designer (a <see cref="Control"/> or a <see cref="ToolStripItem"/>).
    /// </summary>
    /// <param name="target">The object to test.</param>
    /// <returns><see langword="true"/> when the object's events are designer-editable.</returns>
    public static bool IsEventTarget(object? target) => target is Control || target is ToolStripItem;

    /// <summary>
    /// Builds the event-binding descriptors for <paramref name="target"/>, walking the inheritance
    /// chain so inherited events are included, filtered to the curated set and sorted by name.
    /// </summary>
    /// <param name="target">The control or tool strip item to enumerate events for.</param>
    /// <param name="store">The store holding the current handler-name bindings.</param>
    /// <returns>A list of event-binding property descriptors, empty when none apply.</returns>
    public static List<PropertyDescriptor> GetEventDescriptors(object? target, EventBindingStore? store)
    {
        var result = new List<PropertyDescriptor>();
        if (target == null || store == null || !IsEventTarget(target))
            return result;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var t = target.GetType(); t != null && t != typeof(object); t = t.BaseType)
        {
            foreach (var ev in t.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (!BrowsableEvents.Contains(ev.Name))
                    continue;
                if (!seen.Add(ev.Name))
                    continue;
                result.Add(CreateDescriptor(target, store, ev.Name));
            }
        }

        return result.OrderBy(d => d.Name, StringComparer.Ordinal).ToList();
    }

    private static PropertyDescriptor CreateDescriptor(object target, EventBindingStore store, string eventName)
    {
        string suggested = BuildSuggestedName(target, eventName);
        return new PropertyDescriptor(
            name: eventName,
            category: EventCategory,
            propertyType: typeof(string),
            target: target,
            getter: () => store.GetHandler(target, eventName),
            setter: value => store.SetHandler(target, eventName, value as string ?? value?.ToString()),
            suggestedValue: suggested,
            isEventBinding: true);
    }

    /// <summary>
    /// Builds the default handler name for a freshly created binding, in the
    /// <c>controlName_EventName</c> style used by the WinForms designer. The base name is taken from
    /// the target's <c>Name</c> property and sanitized to a valid C# identifier.
    /// </summary>
    /// <param name="target">The control or tool strip item.</param>
    /// <param name="eventName">The event name.</param>
    /// <returns>A sanitized suggested handler name.</returns>
    private static string BuildSuggestedName(object target, string eventName)
    {
        string baseName = "handler";
        var nameProp = target.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
        if (nameProp?.GetValue(target) is string s && !string.IsNullOrWhiteSpace(s))
            baseName = s;

        baseName = SanitizeIdentifier(baseName);
        if (string.IsNullOrEmpty(baseName))
            baseName = "handler";

        return baseName + "_" + eventName;
    }

    /// <summary>
    /// Reduces a string to characters valid in a C# identifier and ensures it does not start with a digit.
    /// </summary>
    /// <param name="value">The raw string to sanitize.</param>
    /// <returns>The sanitized identifier (possibly empty).</returns>
    private static string SanitizeIdentifier(string value)
    {
        var chars = new List<char>(value.Length);
        foreach (var c in value)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
                chars.Add(c);
        }
        if (chars.Count > 0 && char.IsDigit(chars[0]))
            chars.Insert(0, '_');
        return new string(chars.ToArray());
    }
}
