using System.Globalization;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Controls.Containers;

namespace OldSchoolForms.Ui.Designer.Dialogs;

/// <summary>
/// A modal dialog that edits a <see cref="Padding"/> value through four numeric fields
/// (Left, Top, Right, Bottom), mirroring the Windows Forms designer experience. The dialog
/// returns the edited padding when confirmed with OK and <see langword="null"/> when cancelled.
/// Each field accepts an optional leading minus sign followed by digits; unparsable input
/// falls back to zero.
/// </summary>
public sealed class PaddingEditorDialog : Form
{
    private readonly TextBox _leftBox = null!;
    private readonly TextBox _topBox = null!;
    private readonly TextBox _rightBox = null!;
    private readonly TextBox _bottomBox = null!;
    private Padding _result;

    /// <summary>
    /// Shows the padding editor and blocks until the user confirms or cancels.
    /// </summary>
    /// <param name="initial">The padding value preselected when the dialog opens.</param>
    /// <param name="owner">The owner form, or <see langword="null"/>.</param>
    /// <returns>The edited padding when confirmed; <see langword="null"/> when cancelled.</returns>
    public static Padding? ShowDialog(Padding initial, Form? owner = null)
    {
        var dialog = new PaddingEditorDialog(initial);
        var result = dialog.ShowDialog(owner);
        return result == DialogResult.OK ? dialog._result : null;
    }

    private PaddingEditorDialog(Padding initial)
    {
        Text = LangRes.GetString("PaddingDialog_Title");
        Width = 320;
        Height = 250;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Zoom = 1f;

        Controls.Add(new Label { Text = LangRes.GetString("PaddingDialog_Left"), X = 24, Y = 31, Width = 90 });
        Controls.Add(new Label { Text = LangRes.GetString("PaddingDialog_Top"), X = 24, Y = 63, Width = 90 });
        Controls.Add(new Label { Text = LangRes.GetString("PaddingDialog_Right"), X = 24, Y = 95, Width = 90 });
        Controls.Add(new Label { Text = LangRes.GetString("PaddingDialog_Bottom"), X = 24, Y = 127, Width = 90 });

        _leftBox = new NumericTextBox { X = 120, Y = 28, Width = 80, Text = initial.Left.ToString(CultureInfo.InvariantCulture) };
        _topBox = new NumericTextBox { X = 120, Y = 60, Width = 80, Text = initial.Top.ToString(CultureInfo.InvariantCulture) };
        _rightBox = new NumericTextBox { X = 120, Y = 92, Width = 80, Text = initial.Right.ToString(CultureInfo.InvariantCulture) };
        _bottomBox = new NumericTextBox { X = 120, Y = 124, Width = 80, Text = initial.Bottom.ToString(CultureInfo.InvariantCulture) };

        Controls.Add(_leftBox);
        Controls.Add(_topBox);
        Controls.Add(_rightBox);
        Controls.Add(_bottomBox);

        var buttonPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            BackColor = Color.Transparent
        };
        var okButton = new Button
        {
            Text = LangRes.GetString("PaddingDialog_OK"),
            Width = 75,
            Height = 30,
            X = 72,
            Y = 10
        };
        okButton.Click += (_, _) => CloseDialog(true);

        var cancelButton = new Button
        {
            Text = LangRes.GetString("PaddingDialog_Cancel"),
            Width = 75,
            Height = 30,
            X = 157,
            Y = 10
        };
        cancelButton.Click += (_, _) => CloseDialog(false);

        buttonPanel.Controls.AddRange([okButton, cancelButton]);
        Controls.Add(buttonPanel);
    }

    private void CloseDialog(bool ok)
    {
        if (ok)
        {
            _result = new Padding(
                ParseSide(_leftBox.Text),
                ParseSide(_topBox.Text),
                ParseSide(_rightBox.Text),
                ParseSide(_bottomBox.Text));
        }

        DialogResult = ok ? DialogResult.OK : DialogResult.Cancel;
        _modal = false;
    }

    private static int ParseSide(string? text)
    {
        return int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
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

    /// <summary>
    /// A <see cref="TextBox"/> that only accepts an optional leading minus sign followed by
    /// digits, so the padding fields cannot receive non-numeric typed input.
    /// </summary>
    private sealed class NumericTextBox : TextBox
    {
        protected override void OnTextInput(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            foreach (var c in text)
            {
                if (!char.IsDigit(c) && c != '-') return;
            }

            base.OnTextInput(text);
        }
    }
}
