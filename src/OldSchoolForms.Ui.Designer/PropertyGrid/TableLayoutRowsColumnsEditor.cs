using System.Reflection;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Design;
using OldSchoolForms.Ui.Designer.Dialogs;
using OldSchoolForms.Ui.Layout;

namespace OldSchoolForms.Ui.Designer.PropertyGrid;

/// <summary>
/// Value editor that opens the <see cref="TableLayoutRowsColumnsEditorDialog"/> for the
/// <see cref="TableLayoutPanel.RowStyles"/> and <see cref="TableLayoutPanel.ColumnStyles"/>
/// properties. Like the Windows Forms designer, both properties open the same dialog which
/// edits rows and columns together and applies the result to the panel on OK.
/// </summary>
public sealed class TableLayoutRowsColumnsEditor : IValueEditor
{
    /// <inheritdoc />
    public string ButtonText => "…";

    /// <inheritdoc />
    public bool Handles(PropertyInfo property)
        => property.DeclaringType == typeof(TableLayoutPanel)
           && (property.Name == nameof(TableLayoutPanel.RowStyles)
               || property.Name == nameof(TableLayoutPanel.ColumnStyles));

    /// <inheritdoc />
    public void Open(object target)
    {
        if (target is not TableLayoutPanel panel) return;
        TableLayoutRowsColumnsEditorDialog.ShowDialog(panel, panel.FindForm());
    }
}
