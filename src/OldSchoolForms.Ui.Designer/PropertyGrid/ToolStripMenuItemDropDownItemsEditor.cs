using System.Reflection;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Design;
using OldSchoolForms.Ui.Designer.Dialogs;

namespace OldSchoolForms.Ui.Designer.PropertyGrid;

/// <summary>
/// Value editor that opens the <see cref="MenuStripItemsEditorDialog"/> for the
/// <see cref="ToolStripMenuItem.DropDownItems"/> property, so the submenu commands of a
/// selected menu item can be edited in situ from the property grid. It mutates the item
/// directly and therefore works for the read-only collection property.
/// </summary>
public sealed class ToolStripMenuItemDropDownItemsEditor : IValueEditor
{
    /// <inheritdoc />
    public string ButtonText => "…";

    /// <inheritdoc />
    public bool Handles(PropertyInfo property)
        => property.DeclaringType == typeof(ToolStripMenuItem) && property.Name == nameof(ToolStripMenuItem.DropDownItems);

    /// <inheritdoc />
    public void Open(object target)
    {
        if (target is not ToolStripMenuItem item) return;
        MenuStripItemsEditorDialog.ShowDialog(item, item.OwnerControl?.FindForm());
    }
}
