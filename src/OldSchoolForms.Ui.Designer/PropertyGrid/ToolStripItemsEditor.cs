using System.Reflection;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Design;
using OldSchoolForms.Ui.Designer.Dialogs;

namespace OldSchoolForms.Ui.Designer.PropertyGrid;

/// <summary>
/// Value editor that opens the <see cref="ToolStripItemsEditorDialog"/> for the
/// <see cref="ToolStrip.Items"/> property. It mutates the control directly and
/// therefore works for the read-only collection property.
/// </summary>
public sealed class ToolStripItemsEditor : IValueEditor
{
    /// <inheritdoc />
    public string ButtonText => "…";

    /// <inheritdoc />
    public bool Handles(PropertyInfo property)
        => property.DeclaringType == typeof(ToolStrip) && property.Name == nameof(ToolStrip.Items);

    /// <inheritdoc />
    public void Open(object target)
    {
        if (target is not ToolStrip toolStrip) return;
        ToolStripItemsEditorDialog.ShowDialog(toolStrip, toolStrip.FindForm());
    }
}
