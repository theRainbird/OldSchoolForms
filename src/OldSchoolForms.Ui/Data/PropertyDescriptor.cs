using System.ComponentModel;
using System.Reflection;

namespace OldSchoolForms.Ui.Data;

/// <summary>
/// Provides a lightweight property descriptor based on reflection.
/// Used by the binding infrastructure to get and set property values.
/// </summary>
public class ReflectionPropertyDescriptor
{
    /// <summary>
    /// Gets the name of the property.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the type of the property.
    /// </summary>
    public Type PropertyType { get; }

    private readonly PropertyInfo _property;

    /// <summary>
    /// Initializes a new instance for the specified property.
    /// </summary>
    /// <param name="property">The PropertyInfo to wrap.</param>
    /// <exception cref="ArgumentNullException">Thrown when property is null.</exception>
    public ReflectionPropertyDescriptor(PropertyInfo property)
    {
        _property = property ?? throw new ArgumentNullException(nameof(property));
        Name = property.Name;
        PropertyType = property.PropertyType;
    }

    /// <summary>
    /// Protected constructor for descriptor implementations that resolve
    /// members through other mechanisms (e.g. <see cref="ICustomTypeDescriptor"/>).
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="propertyType">The property type.</param>
    protected ReflectionPropertyDescriptor(string name, Type propertyType)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        PropertyType = propertyType ?? throw new ArgumentNullException(nameof(propertyType));
        _property = null!;
    }

    /// <summary>
    /// Gets a value indicating whether the property can be written.
    /// </summary>
    public virtual bool CanWrite => _property != null && _property.CanWrite;

    /// <summary>
    /// Gets the property value from the specified component.
    /// </summary>
    /// <param name="component">The object to read from.</param>
    /// <returns>The property value.</returns>
    public virtual object? GetValue(object component)
    {
        return _property.GetValue(component);
    }

    /// <summary>
    /// Sets the property value on the specified component.
    /// </summary>
    /// <param name="component">The object to write to.</param>
    /// <param name="value">The value to set.</param>
    public virtual void SetValue(object component, object? value)
    {
        _property.SetValue(component, value);
    }

    /// <summary>
    /// Gets the property descriptor for the named property on the given type.
    /// </summary>
    /// <param name="type">The type to search.</param>
    /// <param name="propertyName">The name of the property.</param>
    /// <returns>The descriptor, or null if the property is not found.</returns>
    public static ReflectionPropertyDescriptor? GetProperty(Type type, string propertyName)
    {
        var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
        if (prop == null || !prop.CanRead)
            return null;
        return new ReflectionPropertyDescriptor(prop);
    }

    /// <summary>
    /// Gets the property descriptor for the named member of a concrete instance.
    /// Falls back to <see cref="ICustomTypeDescriptor"/> when the CLR type has no
    /// matching property, which makes data-wrapper types such as
    /// <see cref="System.Data.DataRowView"/> (whose columns are exposed through a
    /// custom type descriptor) bindable.
    /// </summary>
    /// <param name="instance">The instance whose member should be described.</param>
    /// <param name="propertyName">The name of the property or custom member.</param>
    /// <returns>The descriptor, or null if no member with that name exists.</returns>
    public static ReflectionPropertyDescriptor? GetProperty(object instance, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var clr = GetProperty(instance.GetType(), propertyName);
        if (clr != null)
            return clr;

        if (instance is ICustomTypeDescriptor ctd)
        {
            foreach (System.ComponentModel.PropertyDescriptor custom in ctd.GetProperties())
            {
                if (string.Equals(custom.Name, propertyName, StringComparison.Ordinal))
                    return new CustomTypePropertyDescriptor(custom);
            }
        }

        return null;
    }
}

/// <summary>
/// A <see cref="ReflectionPropertyDescriptor"/> that reads and writes members
/// exposed through <see cref="ICustomTypeDescriptor"/> instead of CLR reflection.
/// Used for data wrapper objects like <see cref="System.Data.DataRowView"/>.
/// </summary>
public sealed class CustomTypePropertyDescriptor : ReflectionPropertyDescriptor
{
    private readonly PropertyDescriptor _descriptor;

    /// <summary>
    /// Initializes a new instance wrapping a custom type descriptor property.
    /// </summary>
    /// <param name="descriptor">The property descriptor from the custom type descriptor.</param>
    /// <exception cref="ArgumentNullException">Thrown when descriptor is null.</exception>
    public CustomTypePropertyDescriptor(PropertyDescriptor descriptor)
        : base(descriptor?.Name ?? throw new ArgumentNullException(nameof(descriptor)), descriptor.PropertyType)
    {
        _descriptor = descriptor;
    }

    /// <inheritdoc />
    public override bool CanWrite => !_descriptor.IsReadOnly;

    /// <inheritdoc />
    public override object? GetValue(object component)
    {
        return _descriptor.GetValue(component);
    }

    /// <inheritdoc />
    public override void SetValue(object component, object? value)
    {
        if (!_descriptor.IsReadOnly)
            _descriptor.SetValue(component, value);
    }
}
