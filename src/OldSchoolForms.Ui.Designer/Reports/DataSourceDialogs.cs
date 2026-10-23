using System;
using System.Collections.Generic;
using System.Linq;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Reports;

namespace OldSchoolForms.Ui.Designer.Reports;

/// <summary>
/// A minimal modal text input dialog used to enter a data source name.
/// </summary>
public sealed class TextInputDialog : Form
{
    private readonly TextBox _input = null!;
    private string _result = string.Empty;

    /// <summary>
    /// Shows the input dialog.
    /// </summary>
    /// <param name="title">The dialog window title.</param>
    /// <param name="prompt">The label shown above the input field.</param>
    /// <param name="initial">The initial text value.</param>
    /// <param name="owner">The owner form, or <see langword="null"/>.</param>
    /// <returns>The entered text when confirmed; <see langword="null"/> when cancelled.</returns>
    public static string? ShowDialog(string title, string prompt, string initial, Form? owner = null)
    {
        var dialog = new TextInputDialog(title, prompt, initial);
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog._result : null;
    }

    private TextInputDialog(string title, string prompt, string initial)
    {
        Text = title;
        Width = 360;
        Height = 170;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Zoom = 1f;

        Controls.Add(new Label { Text = prompt, X = 20, Y = 20, Width = 300 });
        _input = new TextBox { X = 20, Y = 46, Width = 300, Text = initial };
        Controls.Add(_input);

        var okButton = new Button { Text = LangRes.GetString("Common_Ok"), Width = 90, Height = 28, X = 124, Y = 92 };
        okButton.Click += (_, _) => CloseDialog(true);
        var cancelButton = new Button { Text = LangRes.GetString("Common_Cancel"), Width = 90, Height = 28, X = 224, Y = 92 };
        cancelButton.Click += (_, _) => CloseDialog(false);
        Controls.Add(okButton);
        Controls.Add(cancelButton);
    }

    private void CloseDialog(bool ok)
    {
        if (ok)
        {
            var text = _input.Text?.Trim() ?? string.Empty;
            if (text.Length == 0) return;
            _result = text;
        }
        DialogResult = ok ? DialogResult.OK : DialogResult.Cancel;
        _modal = false;
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            CloseDialog(false);
            e.Handled = true;
            return;
        }
        if (e.KeyCode == Keys.Enter)
        {
            CloseDialog(true);
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }
}

/// <summary>
/// Modal dialog for defining a data source field: a name and a
/// <see cref="ReportDataType"/>.
/// </summary>
public sealed class FieldDefinitionDialog : Form
{
    private static readonly ReportDataType[] TypeOrder =
    {
        ReportDataType.String,
        ReportDataType.Int,
        ReportDataType.Long,
        ReportDataType.Decimal,
        ReportDataType.Double,
        ReportDataType.Boolean,
        ReportDataType.DateTime,
        ReportDataType.Guid
    };

    private readonly TextBox _nameBox = null!;
    private readonly ComboBox _typeCombo = null!;
    private string _name = string.Empty;
    private ReportDataType _type = ReportDataType.String;

    /// <summary>
    /// Shows the field definition dialog.
    /// </summary>
    /// <param name="title">The dialog window title.</param>
    /// <param name="initialName">The initial field name.</param>
    /// <param name="initialType">The initial field data type.</param>
    /// <param name="owner">The owner form, or <see langword="null"/>.</param>
    /// <returns>The tuple of name and type when confirmed; <see langword="null"/> when cancelled.</returns>
    public static (string Name, ReportDataType Type)? ShowDialog(
        string title, string initialName, ReportDataType initialType, Form? owner = null)
    {
        var dialog = new FieldDefinitionDialog(title, initialName, initialType);
        return dialog.ShowDialog(owner) == DialogResult.OK ? (dialog._name, dialog._type) : null;
    }

    private FieldDefinitionDialog(string title, string initialName, ReportDataType initialType)
    {
        Text = title;
        Width = 380;
        Height = 168;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Zoom = 1f;

        Controls.Add(new Label { Text = LangRes.GetString("DataSourceDialog_FieldName"), X = 20, Y = 16, Width = 120 });
        _nameBox = new TextBox { X = 20, Y = 40, Width = 320, Height = 24, Text = initialName };
        Controls.Add(_nameBox);

        Controls.Add(new Label { Text = LangRes.GetString("DataSourceDialog_FieldType"), X = 20, Y = 72, Width = 120 });
        _typeCombo = new ComboBox { X = 20, Y = 96, Width = 160, Height = 24, DropDownStyle = DropDownStyle.DropDownList };
        foreach (var t in TypeOrder)
            _typeCombo.Items.Add(t.ToJsonName());
        var idx = Array.IndexOf(TypeOrder, initialType);
        _typeCombo.SelectedIndex = idx >= 0 ? idx : 0;
        Controls.Add(_typeCombo);

        var okButton = new Button { Text = LangRes.GetString("Common_Ok"), Width = 90, Height = 24, X = 144, Y = 128 };
        okButton.Click += (_, _) => CloseDialog(true);
        var cancelButton = new Button { Text = LangRes.GetString("Common_Cancel"), Width = 90, Height = 24, X = 244, Y = 128 };
        cancelButton.Click += (_, _) => CloseDialog(false);
        Controls.Add(okButton);
        Controls.Add(cancelButton);
    }

    private void CloseDialog(bool ok)
    {
        if (ok)
        {
            var name = _nameBox.Text?.Trim() ?? string.Empty;
            if (name.Length == 0) return;
            _name = name;
            var idx = _typeCombo.SelectedIndex;
            _type = idx >= 0 && idx < TypeOrder.Length ? TypeOrder[idx] : ReportDataType.String;
        }
        DialogResult = ok ? DialogResult.OK : DialogResult.Cancel;
        _modal = false;
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            CloseDialog(false);
            e.Handled = true;
            return;
        }
        if (e.KeyCode == Keys.Enter)
        {
            CloseDialog(true);
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }
}

/// <summary>
/// Modal dialog for choosing the data source a band binds to. Offers a
/// <em>(default)</em> entry that clears the binding plus every declared source.
/// </summary>
public sealed class DataSourceNameDialog : Form
{
    private readonly ListBox _listBox = null!;
    private readonly List<string> _names;
    private string? _result;

    /// <summary>
    /// Shows the data source picker.
    /// </summary>
    /// <param name="current">The currently bound source name (may be null or empty).</param>
    /// <param name="sourceNames">The declared data source names.</param>
    /// <param name="owner">The owner form, or <see langword="null"/>.</param>
    /// <returns>The chosen source name, or <see langword="null"/> for the default binding.</returns>
    public static string? ShowDialog(string? current, IReadOnlyList<string> sourceNames, Form? owner = null)
    {
        var dialog = new DataSourceNameDialog(current, sourceNames);
        if (dialog.ShowDialog(owner) == DialogResult.OK)
            return dialog._result;
        return null;
    }

    private DataSourceNameDialog(string? current, IReadOnlyList<string> sourceNames)
    {
        Text = LangRes.GetString("DataSourceNameDialog_Title");
        Width = 360;
        Height = 360;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Zoom = 1f;
        _names = new List<string>(sourceNames);

        Controls.Add(new Label { Text = LangRes.GetString("DataSourceNameDialog_Prompt"), X = 20, Y = 16, Width = 300 });

        _listBox = new ListBox { X = 20, Y = 42, Width = 300, Height = 230 };
        _listBox.Items.Add(LangRes.GetString("DataSourceNameDialog_Default"));
        foreach (var name in _names)
            _listBox.Items.Add(name);

        var matchIdx = 0;
        if (!string.IsNullOrEmpty(current))
        {
            for (int i = 0; i < _names.Count; i++)
            {
                if (string.Equals(_names[i], current, StringComparison.OrdinalIgnoreCase))
                {
                    matchIdx = i + 1;
                    break;
                }
            }
        }
        _listBox.SelectedIndex = matchIdx;
        Controls.Add(_listBox);

        var okButton = new Button { Text = LangRes.GetString("Common_Ok"), Width = 90, Height = 28, X = 104, Y = 284 };
        okButton.Click += (_, _) => CloseDialog(true);
        var cancelButton = new Button { Text = LangRes.GetString("Common_Cancel"), Width = 90, Height = 28, X = 204, Y = 284 };
        cancelButton.Click += (_, _) => CloseDialog(false);
        Controls.Add(okButton);
        Controls.Add(cancelButton);
    }

    private void CloseDialog(bool ok)
    {
        if (ok)
        {
            var index = _listBox.SelectedIndex;
            // Index 0 is the "(default)" entry -> clear the binding.
            _result = index >= 1 && index - 1 < _names.Count ? _names[index - 1] : null;
        }
        DialogResult = ok ? DialogResult.OK : DialogResult.Cancel;
        _modal = false;
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            CloseDialog(false);
            e.Handled = true;
            return;
        }
        if (e.KeyCode == Keys.Enter)
        {
            CloseDialog(true);
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }
}
