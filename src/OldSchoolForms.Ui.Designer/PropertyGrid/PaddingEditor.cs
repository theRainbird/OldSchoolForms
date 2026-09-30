using System.Reflection;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Design;

namespace OldSchoolForms.Ui.Designer.PropertyGrid;

/// <summary>
/// Value editor that opens the <see cref="Dialogs.PaddingEditorDialog"/> for any property of
/// type <see cref="Padding"/> - for example <c>Control.Padding</c>, <c>ToolStripItem.Padding</c>
/// or <c>DiagramView.DiagramPadding</c>. It reads the current value and writes the edited value
/// back through the property descriptor so that undo, dirty tracking and repaint work exactly
/// like an inline property edit.
/// </summary>
public sealed class PaddingEditor : IValueEditor
{
    /// <inheritdoc />
    public string ButtonText => "…";

    /// <inheritdoc />
    public bool Handles(PropertyInfo property)
        => property.PropertyType == typeof(Padding);

    /// <inheritdoc />
    public void Open(object target)
    {
        // Without a descriptor the current value cannot be read and the edited value cannot be
        // written back through the property grid's undo pipeline; nothing to do here.
    }

    /// <inheritdoc />
    public void Open(object target, object? context)
    {
        if (context is not PropertyDescriptor descriptor)
        {
            Open(target);
            return;
        }

        var current = descriptor.GetValue() is Padding padding ? padding : Padding.Empty;
        var owner = (target as Control)?.FindForm();
        var result = Dialogs.PaddingEditorDialog.ShowDialog(current, owner);
        if (result.HasValue)
            descriptor.SetValue(result.Value);
    }
}
