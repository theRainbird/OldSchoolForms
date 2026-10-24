using System.Reflection;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Design;

namespace OldSchoolForms.Ui.Designer.PropertyGrid;

/// <summary>
/// Value editor that opens the <see cref="Dialogs.FontEditorDialog"/> for any property of type
/// <see cref="Font"/> - for example <c>Control.Font</c> or <c>ReportControl.Font</c>. It works for
/// both designer controls and report controls: the owner form is resolved from the target control
/// when possible, otherwise from the active <see cref="Reports.ReportDesignContext"/>. The edited
/// font is written back through the property descriptor so that undo, dirty tracking and repaint
/// work exactly like an inline property edit.
/// </summary>
public sealed class FontEditor : IValueEditor
{
    /// <inheritdoc />
    public string ButtonText => "…";

    /// <inheritdoc />
    public bool Handles(PropertyInfo property)
        => property.PropertyType == typeof(Font);

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

        var current = descriptor.GetValue() as Font;
        var owner = (target as Control)?.FindForm() ?? Reports.ReportDesignContext.Current?.FindForm();
        var result = Dialogs.FontEditorDialog.ShowDialog(current, owner);
        if (result != null)
            descriptor.SetValue(result);
    }
}
