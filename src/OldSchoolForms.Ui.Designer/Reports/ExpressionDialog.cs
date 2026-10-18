using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Core;

namespace OldSchoolForms.Ui.Designer.Reports;

/// <summary>
/// Modal dialog for editing a report text box expression. The expression uses the
/// <c>{Field}</c> / <c>{Field:format}</c> template syntax. A list of available
/// fields allows inserting a placeholder at the current caret position.
/// </summary>
public sealed class ExpressionDialog : Form
{
    private readonly MemoBox _memo = null!;
    private readonly ListBox _listBox = null!;
    private string _result = string.Empty;

    /// <summary>
    /// Shows the expression editor.
    /// </summary>
    /// <param name="current">The current expression (may be empty).</param>
    /// <param name="fields">The available field names to offer for insertion.</param>
    /// <param name="owner">The owner form, or <see langword="null"/>.</param>
    /// <returns>The edited expression when confirmed; <see langword="null"/> when cancelled.</returns>
    public static string? ShowDialog(string? current, IReadOnlyList<string> fields, Form? owner = null)
    {
        var dialog = new ExpressionDialog(current, fields);
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog._result : null;
    }

    private ExpressionDialog(string? current, IReadOnlyList<string> fields)
    {
        Text = LangRes.GetString("ExpressionDialog_Title");
        Width = 460;
        Height = 360;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Zoom = 1f;

        Controls.Add(new Label { Text = LangRes.GetString("ExpressionDialog_ExpressionLabel"), X = 20, Y = 24, Width = 280 });
        _memo = new MemoBox { X = 20, Y = 48, Width = 260, Height = 220, Text = current ?? string.Empty };
        Controls.Add(_memo);

        Controls.Add(new Label { Text = LangRes.GetString("ExpressionDialog_AvailableFields"), X = 292, Y = 24, Width = 140 });
        _listBox = new ListBox { X = 292, Y = 48, Width = 148, Height = 190 };
        foreach (var field in fields)
            _listBox.Items.Add(field);
        Controls.Add(_listBox);

        var insertButton = new Button { Text = LangRes.GetString("ExpressionDialog_Insert"), Width = 148, Height = 26, X = 292, Y = 244 };
        insertButton.Click += (_, _) => InsertSelectedField();
        Controls.Add(insertButton);

        var buttonPanel = new Panel { Dock = DockStyle.Bottom, Height = 50, BackColor = Color.Transparent };
        var okButton = new Button { Text = LangRes.GetString("ExpressionDialog_Ok"), Width = 75, Height = 30, X = 112, Y = 10 };
        okButton.Click += (_, _) => CloseDialog(true);
        var cancelButton = new Button { Text = LangRes.GetString("ExpressionDialog_Cancel"), Width = 90, Height = 30, X = 197, Y = 10 };
        cancelButton.Click += (_, _) => CloseDialog(false);
        buttonPanel.Controls.AddRange([okButton, cancelButton]);
        Controls.Add(buttonPanel);
    }

    private void InsertSelectedField()
    {
        if (_listBox.SelectedIndex < 0) return;
        var field = _listBox.Items[_listBox.SelectedIndex]?.ToString();
        if (string.IsNullOrEmpty(field)) return;

        var text = _memo.Text ?? string.Empty;
        var index = Math.Clamp(_memo.SelectionStart, 0, text.Length);
        _memo.Text = text.Insert(index, "{" + field + "}");
    }

    private void CloseDialog(bool ok)
    {
        if (ok) _result = _memo.Text ?? string.Empty;
        DialogResult = ok ? DialogResult.OK : DialogResult.Cancel;
        _modal = false;
    }

    /// <summary>
    /// Handles keyboard shortcuts. Escape cancels the dialog. Enter is not confirmed
    /// here because the expression field is multi-line.
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
