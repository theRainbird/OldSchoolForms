using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Core;

namespace OldSchoolForms.Ui.Designer.Reports;

/// <summary>
/// Modal dialog for choosing or typing a data field name. Shows the currently bound
/// field in an editable text box plus a list of fields available in the report
/// (declared design fields and fields referenced by existing controls). Selecting a
/// list entry copies it into the text box.
/// </summary>
public sealed class DataFieldDialog : Form
{
    private readonly TextBox _fieldBox = null!;
    private readonly ListBox _listBox = null!;
    private string _result = string.Empty;

    /// <summary>
    /// Shows the data field picker.
    /// </summary>
    /// <param name="current">The currently bound field name (may be empty).</param>
    /// <param name="fields">The available field names to offer for selection.</param>
    /// <param name="owner">The owner form, or <see langword="null"/>.</param>
    /// <returns>The chosen field name when confirmed; <see langword="null"/> when cancelled.</returns>
    public static string? ShowDialog(string? current, IReadOnlyList<string> fields, Form? owner = null)
    {
        var dialog = new DataFieldDialog(current, fields);
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog._result : null;
    }

    private DataFieldDialog(string? current, IReadOnlyList<string> fields)
    {
        Text = LangRes.GetString("DataFieldDialog_Title");
        Width = 380;
        Height = 340;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Zoom = 1f;

        Controls.Add(new Label { Text = LangRes.GetString("DataFieldDialog_FieldName"), X = 20, Y = 24, Width = 120 });
        _fieldBox = new TextBox { X = 20, Y = 48, Width = 324, Text = current ?? string.Empty };
        Controls.Add(_fieldBox);

        Controls.Add(new Label { Text = LangRes.GetString("DataFieldDialog_AvailableFields"), X = 20, Y = 88, Width = 160 });
        _listBox = new ListBox { X = 20, Y = 112, Width = 324, Height = 150 };
        foreach (var field in fields)
            _listBox.Items.Add(field);
        _listBox.SelectedIndexChanged += (_, _) =>
        {
            if (_listBox.SelectedIndex >= 0 && _listBox.SelectedIndex < fields.Count)
                _fieldBox.Text = fields[_listBox.SelectedIndex];
        };
        Controls.Add(_listBox);

        var buttonPanel = new Panel { Dock = DockStyle.Bottom, Height = 50, BackColor = Color.Transparent };
        var okButton = new Button { Text = LangRes.GetString("DataFieldDialog_Ok"), Width = 75, Height = 30, X = 72, Y = 10 };
        okButton.Click += (_, _) => CloseDialog(true);
        var cancelButton = new Button { Text = LangRes.GetString("DataFieldDialog_Cancel"), Width = 90, Height = 30, X = 157, Y = 10 };
        cancelButton.Click += (_, _) => CloseDialog(false);
        buttonPanel.Controls.AddRange([okButton, cancelButton]);
        Controls.Add(buttonPanel);
    }

    private void CloseDialog(bool ok)
    {
        if (ok) _result = _fieldBox.Text?.Trim() ?? string.Empty;
        DialogResult = ok ? DialogResult.OK : DialogResult.Cancel;
        _modal = false;
    }

    /// <summary>
    /// Handles keyboard shortcuts. Enter confirms (OK) and Escape cancels the dialog.
    /// </summary>
    /// <param name="e">The key event data.</param>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            CloseDialog(true);
            e.Handled = true;
            return;
        }

        if (e.KeyCode == Keys.Escape)
        {
            CloseDialog(false);
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }
}
