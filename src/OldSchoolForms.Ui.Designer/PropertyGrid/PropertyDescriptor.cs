using System;
using System.Collections.Generic;
using System.Reflection;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Design;

namespace OldSchoolForms.Ui.Designer.PropertyGrid;

/// <summary>
/// Describes a single property for display and editing in the PropertyGrid.
/// Wraps <see cref="PropertyInfo"/> and caches metadata attributes.
/// </summary>
public class PropertyDescriptor
{
    private readonly PropertyInfo? _property;
    private readonly object _target;
    private readonly bool _readOnlyOverride;
    private readonly Func<object?>? _getter;
    private readonly Action<object?>? _setter;
    private readonly Type? _explicitType;

    /// <summary>
    /// Gets the display name of the property.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the category for grouping in the grid.
    /// </summary>
    public string Category { get; }

    /// <summary>
    /// Gets the property type.
    /// </summary>
    public Type PropertyType => _property?.PropertyType ?? _explicitType ?? typeof(string);

    /// <summary>
    /// Gets whether this property is read-only. True when the property has no setter or when
    /// a read-only override was supplied at construction (used for properties that have a
    /// setter but cannot be edited inline, such as image or complex object properties).
    /// </summary>
    public bool IsReadOnly => _readOnlyOverride
        || (_property != null && !_property.CanWrite)
        || (_property == null && _setter == null);

    /// <summary>
    /// Gets the dedicated value editor for this property, or null if the property
    /// is edited inline.
    /// </summary>
    public IValueEditor? Editor { get; }

    /// <summary>
    /// Gets whether this descriptor represents an event-handler binding rather than a CLR property.
    /// Event-binding descriptors are backed by delegates into the <see cref="Services.EventBindingStore"/>
    /// and drive the create/rename/delete behavior of the designer's events view.
    /// </summary>
    public bool IsEventBinding { get; }

    /// <summary>
    /// Gets the value suggested for a freshly created binding, such as the default handler name
    /// <c>controlName_EventName</c>. Null for ordinary properties.
    /// </summary>
    public string? SuggestedValue { get; }

    /// <summary>
    /// Gets the object instance this property is read from and written to.
    /// </summary>
    internal object Target => _target;

    /// <summary>
    /// Occurs when a value has been written to the underlying property via <see cref="SetValue"/>,
    /// carrying the target control, the property name, and the previous and new values. This lets
    /// the <see cref="PropertyGrid"/> forward the change so the designer can push an undo entry,
    /// mark the document dirty, and repaint the surface.
    /// </summary>
    public event EventHandler<PropertyValueChangedEventArgs>? ValueChanged;

    /// <summary>
    /// Initializes a new instance backed by a reflection <see cref="PropertyInfo"/>.
    /// </summary>
    /// <param name="property">The reflection PropertyInfo.</param>
    /// <param name="target">The object instance to read/write values from.</param>
    /// <param name="editor">The dedicated value editor for the property, if any.</param>
    /// <param name="readOnlyOverride">When true, forces the property to be read-only even if it has a setter.</param>
    public PropertyDescriptor(PropertyInfo property, object target, IValueEditor? editor = null, bool readOnlyOverride = false)
    {
        _property = property;
        _target = target;
        Editor = editor;
        _readOnlyOverride = readOnlyOverride;
        Name = property.Name;

        Category = property.GetCustomAttribute<System.ComponentModel.CategoryAttribute>()?.Category
            ?? GuessCategory(property.Name);
    }

    /// <summary>
    /// Initializes a new instance backed by explicit getter/setter delegates instead of a
    /// <see cref="PropertyInfo"/>. Used for event-handler bindings whose value lives in the
    /// <see cref="Services.EventBindingStore"/> rather than on the target object itself.
    /// </summary>
    /// <param name="name">The display name (the event name).</param>
    /// <param name="category">The category to group the row under.</param>
    /// <param name="propertyType">The value type; typically <see cref="string"/> for handler names.</param>
    /// <param name="target">The object the binding belongs to (control or tool strip item).</param>
    /// <param name="getter">Delegate returning the current value.</param>
    /// <param name="setter">Delegate writing the value, or null for a read-only binding.</param>
    /// <param name="suggestedValue">The value suggested when creating a fresh binding.</param>
    /// <param name="isEventBinding">When true, marks the descriptor as an event-handler binding.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> or <paramref name="getter"/> is null.</exception>
    public PropertyDescriptor(
        string name,
        string category,
        Type propertyType,
        object target,
        Func<object?> getter,
        Action<object?>? setter,
        string? suggestedValue = null,
        bool isEventBinding = false)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Category = category ?? string.Empty;
        _explicitType = propertyType ?? typeof(string);
        _target = target;
        _getter = getter ?? throw new ArgumentNullException(nameof(getter));
        _setter = setter;
        SuggestedValue = suggestedValue;
        IsEventBinding = isEventBinding;
    }

    /// <summary>
    /// Gets the current value of the property on the target.
    /// </summary>
    public object? GetValue()
    {
        try
        {
            if (_property != null)
                return _property.GetValue(_target);
            return _getter?.Invoke();
        }
        catch { return null; }
    }

    /// <summary>
    /// Sets the value of the property on the target and raises <see cref="ValueChanged"/>
    /// with the previous and new values so the PropertyGrid can forward the change to the
    /// designer for undo, dirty tracking, and repainting.
    /// </summary>
    public void SetValue(object? value)
    {
        try
        {
            if (_property != null)
            {
                var oldValue = _property.GetValue(_target);
                var converted = ConvertValue(value, PropertyType);
                _property.SetValue(_target, converted);
                OnValueChanged(oldValue, converted);
            }
            else if (_setter != null)
            {
                var oldValue = _getter?.Invoke();
                var converted = ConvertValue(value, PropertyType);
                _setter(converted);
                OnValueChanged(oldValue, converted);
            }
        }
        catch { }
    }

    private void OnValueChanged(object? oldValue, object? newValue)
    {
        ValueChanged?.Invoke(this, new PropertyValueChangedEventArgs(
            _target as Control, Name, oldValue, newValue, _target, IsEventBinding));
    }

    private static object? ConvertValue(object? value, Type targetType)
    {
        if (value == null) return null;
        if (value.GetType() == targetType) return value;

        if (targetType == typeof(string))
            return value.ToString();

        if (targetType.IsEnum)
        {
            if (value is string str)
                return Enum.Parse(targetType, str);
            return Enum.ToObject(targetType, value);
        }

        if (targetType == typeof(int))
            return Convert.ToInt32(value);
        if (targetType == typeof(float))
            return Convert.ToSingle(value);
        if (targetType == typeof(bool))
            return Convert.ToBoolean(value);

        return value;
    }

    private static string GuessCategory(string propertyName)
    {
        var layoutProps = new HashSet<string>
        {
            "Location", "Size", "Width", "Height", "X", "Y",
            "Anchor", "Dock", "Padding", "Margin", "Bounds",
            "MinimumSize", "MaximumSize", "PreferredSize"
        };

        var appearanceProps = new HashSet<string>
        {
            "BackColor", "ForeColor", "Font", "Text", "TextAlign",
            "Image", "ImageAlign", "BackgroundImage", "Cursor",
            "FlatStyle", "BorderStyle", "RightToLeft"
        };

        var behaviorProps = new HashSet<string>
        {
            "Enabled", "Visible", "TabStop", "TabIndex",
            "ReadOnly", "AllowDrop", "AutoSize"
        };

        var designProps = new HashSet<string>
        {
            "Name", "Tag", "Modifiers", "GenerateMember", "Locked"
        };

        if (layoutProps.Contains(propertyName)) return "Layout";
        if (appearanceProps.Contains(propertyName)) return "Darstellung";
        if (behaviorProps.Contains(propertyName)) return "Verhalten";
        if (designProps.Contains(propertyName)) return "Design";
        return "Sonstiges";
    }
}
