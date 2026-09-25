using System.Reflection;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Design;

namespace OldSchoolForms.Ui.Designer.PropertyGrid;

/// <summary>
/// Value editor that opens the <see cref="Dialogs.TabPagesEditorDialog"/> for the
/// <see cref="TabControl.TabPages"/> property. It mutates the control directly and
/// therefore works for the read-only collection property.
/// </summary>
public sealed class TabPagesEditor : IValueEditor
{
    /// <inheritdoc />
    public string ButtonText => "…";

    /// <inheritdoc />
    public bool Handles(PropertyInfo property)
        => property.DeclaringType == typeof(TabControl) && property.Name == nameof(TabControl.TabPages);

    /// <inheritdoc />
    public void Open(object target)
    {
        if (target is not TabControl tabControl) return;
        OldSchoolForms.Ui.Dialogs.TabPagesEditorDialog.ShowDialog(tabControl, tabControl.FindForm());
    }
}
