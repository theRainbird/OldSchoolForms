using System.Reflection;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Design;
using OldSchoolForms.Ui.Designer.Dialogs;

namespace OldSchoolForms.Ui.Designer.PropertyGrid;

/// <summary>
/// Value editor that opens the <see cref="ImagePickerDialog"/> for the
/// <see cref="ToolStripItem.Image"/> property. It lets the user pick an image from the
/// framework's embedded icon resources or import one from disk, mirroring the
/// Windows Forms designer's image picker.
/// </summary>
public sealed class ToolStripItemImageEditor : IValueEditor
{
    /// <inheritdoc />
    public string ButtonText => "…";

    /// <inheritdoc />
    public bool Handles(PropertyInfo property)
        => property.Name == nameof(ToolStripItem.Image)
           && typeof(ToolStripItem).IsAssignableFrom(property.DeclaringType);

    /// <inheritdoc />
    public void Open(object target)
    {
        if (target is not ToolStripItem item) return;
        ImagePickerDialog.ShowDialog(item, item.OwnerControl?.FindForm());
    }
}
