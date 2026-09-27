using System.Reflection;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Design;
using OldSchoolForms.Ui.Designer.Dialogs;

namespace OldSchoolForms.Ui.Designer.PropertyGrid;

/// <summary>
/// Value editor that opens the <see cref="MenuStripItemsEditorDialog"/> for the
/// <see cref="MenuStrip.Items"/> property. It mutates the control directly and
/// therefore works for the read-only collection property.
/// </summary>
public sealed class MenuStripItemsEditor : IValueEditor
{
    /// <inheritdoc />
    public string ButtonText => "…";

    /// <inheritdoc />
    public bool Handles(PropertyInfo property)
        => property.DeclaringType == typeof(MenuStrip) && property.Name == nameof(MenuStrip.Items);

    /// <inheritdoc />
    public void Open(object target)
    {
        if (target is not MenuStrip menuStrip) return;
        MenuStripItemsEditorDialog.ShowDialog(menuStrip, menuStrip.FindForm());
    }
}
