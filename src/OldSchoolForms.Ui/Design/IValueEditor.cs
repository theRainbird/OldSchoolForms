using System.Reflection;
using OldSchoolForms.Ui.Core;

namespace OldSchoolForms.Ui.Design;

/// <summary>
/// Provides a dedicated value editor for a specific property, opened from the
/// property browser via an ellipsis button. Implementations mutate the target
/// control directly (they are not bound to reflection set access) and therefore
/// also work for read-only / collection properties.
/// </summary>
public interface IValueEditor
{
    /// <summary>
    /// Gets the caption displayed on the ellipsis button in the property browser.
    /// </summary>
    string ButtonText { get; }

    /// <summary>
    /// Gets whether this editor is responsible for the given property.
    /// </summary>
    /// <param name="property">The property to test.</param>
    /// <returns>True if this editor handles the property.</returns>
    bool Handles(PropertyInfo property);

    /// <summary>
    /// Opens the dedicated editor for the given target object's property.
    /// </summary>
    /// <param name="target">The object that owns the property being edited. This may be a
    /// <see cref="Control"/> or any other object the property grid is bound to (for example a
    /// component such as a tool strip item).</param>
    void Open(object target);

    /// <summary>
    /// Opens the dedicated editor for the given target, additionally supplying a context object
    /// that describes the specific property being edited (typically the property grid's property
    /// descriptor). The default implementation ignores the context and delegates to
    /// <see cref="Open(object)"/>. Editors that need to read the current value and write the
    /// edited value back through the descriptor - so that undo, dirty tracking and repaint work -
    /// override this overload and cast <paramref name="context"/> to their descriptor type.
    /// </summary>
    /// <param name="target">The object that owns the property being edited.</param>
    /// <param name="context">An optional descriptor/context object supplied by the property grid,
    /// or <c>null</c> when no context is available.</param>
    void Open(object target, object? context) => Open(target);
}
