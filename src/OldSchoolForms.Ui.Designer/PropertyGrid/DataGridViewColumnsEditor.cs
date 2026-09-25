using System.Reflection;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Design;
using OldSchoolForms.Ui.Designer.Dialogs;

namespace OldSchoolForms.Ui.Designer.PropertyGrid;

/// <summary>
/// Value editor that opens the <see cref="DataGridViewColumnsEditorDialog"/> for the
/// <see cref="DataGridView.Columns"/> property. It mutates the control directly and
/// therefore works for the read-only collection property.
/// </summary>
public sealed class DataGridViewColumnsEditor : IValueEditor
{
    /// <inheritdoc />
    public string ButtonText => "…";

    /// <inheritdoc />
    public bool Handles(PropertyInfo property)
        => property.DeclaringType == typeof(DataGridView) && property.Name == nameof(DataGridView.Columns);

    /// <inheritdoc />
    public void Open(object target)
    {
        if (target is not DataGridView dataGridView) return;
        DataGridViewColumnsEditorDialog.ShowDialog(dataGridView, dataGridView.FindForm());
    }
}
