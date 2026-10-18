using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Core;

namespace OldSchoolForms.Ui.Designer.Reports;

/// <summary>
/// Modal dialog for managing the design-time field list of a report. The list
/// defines the field names offered by the data field picker and used to build the
/// synthetic sample record for the WYSIWYG preview.
/// </summary>
public sealed class DesignFieldsDialog : Form
{
    private readonly ListBox _listBox = null!;
    private readonly TextBox _newFieldBox = null!;
    private readonly List<string> _result;

    /// <summary>
    /// Shows the design fields editor.
    /// </summary>
    /// <param name="current">The currently declared field names.</param>
    /// <param name="owner">The owner form, or <see langword="null"/>.</param>
    /// <returns>The edited field list when confirmed; <see langword="null"/> when cancelled.</returns>
    public static List<string>? ShowDialog(IReadOnlyList<string> current, Form? owner = null)
    {
        var dialog = new DesignFieldsDialog(current);
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog._result : null;
    }

    private DesignFieldsDialog(IReadOnlyList<string> current)
    {
        Text = LangRes.GetString("DesignFieldsDialog_Title");
        Width = 380;
        Height = 380;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Zoom = 1f;
        _result = new List<string>(current);

        Controls.Add(new Label { Text = LangRes.GetString("DesignFieldsDialog_FieldNames"), X = 20, Y = 20, Width = 120 });
        _newFieldBox = new TextBox { X = 20, Y = 44, Width = 210 };
        Controls.Add(_newFieldBox);

        var addButton = new Button { Text = LangRes.GetString("DesignFieldsDialog_Add"), Width = 100, Height = 26, X = 240, Y = 44 };
        addButton.Click += (_, _) => AddField();
        Controls.Add(addButton);

        _listBox = new ListBox { X = 20, Y = 82, Width = 324, Height = 200 };
        foreach (var field in _result)
            _listBox.Items.Add(field);
        Controls.Add(_listBox);

        var removeButton = new Button { Text = LangRes.GetString("DesignFieldsDialog_Remove"), Width = 100, Height = 26, X = 244, Y = 288 };
        removeButton.Click += (_, _) => RemoveSelected();
        Controls.Add(removeButton);

        var buttonPanel = new Panel { Dock = DockStyle.Bottom, Height = 50, BackColor = Color.Transparent };
        var okButton = new Button { Text = LangRes.GetString("DesignFieldsDialog_Ok"), Width = 75, Height = 30, X = 72, Y = 10 };
        okButton.Click += (_, _) => CloseDialog(true);
        var cancelButton = new Button { Text = LangRes.GetString("DesignFieldsDialog_Cancel"), Width = 90, Height = 30, X = 157, Y = 10 };
        cancelButton.Click += (_, _) => CloseDialog(false);
        buttonPanel.Controls.AddRange([okButton, cancelButton]);
        Controls.Add(buttonPanel);
    }

    private void AddField()
    {
        var name = _newFieldBox.Text?.Trim() ?? string.Empty;
        if (name.Length == 0) return;
        if (_result.Any(f => string.Equals(f, name, StringComparison.OrdinalIgnoreCase))) return;
        _result.Add(name);
        _listBox.Items.Add(name);
        _newFieldBox.Text = string.Empty;
    }

    private void RemoveSelected()
    {
        var index = _listBox.SelectedIndex;
        if (index < 0 || index >= _result.Count) return;
        _result.RemoveAt(index);
        _listBox.Items.RemoveAt(index);
    }

    private void CloseDialog(bool ok)
    {
        DialogResult = ok ? DialogResult.OK : DialogResult.Cancel;
        _modal = false;
    }

    /// <summary>
    /// Handles keyboard shortcuts. Enter in the name field adds the field, Escape cancels the dialog.
    /// </summary>
    /// <param name="e">The key event data.</param>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            CloseDialog(false);
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }
}
