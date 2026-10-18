using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Designer.PropertyGrid;
using OldSchoolForms.Ui.Reports;

namespace OldSchoolForms.Ui.Designer.Reports;

/// <summary>
/// Modal dialog for editing the page setup of a report: paper size, orientation,
/// and the four margins in centimeters. Applying the dialog mutates the given
/// <see cref="ReportPageSetup"/> in place.
/// </summary>
public sealed class PageSetupDialog : Form
{
    private static readonly (string Name, (Cm Width, Cm Height) Size)[] PaperPresets =
    {
        ("A3", ReportPaperSize.A3),
        ("A4", ReportPaperSize.A4),
        ("A5", ReportPaperSize.A5),
        ("Letter", ReportPaperSize.Letter),
        ("Legal", ReportPaperSize.Legal)
    };

    private readonly ComboBox _paperCombo = null!;
    private readonly CheckBox _landscapeCheck = null!;
    private readonly TextBox _leftBox = null!;
    private readonly TextBox _topBox = null!;
    private readonly TextBox _rightBox = null!;
    private readonly TextBox _bottomBox = null!;

    /// <summary>
    /// Shows the page setup editor and applies the result to the given setup on OK.
    /// </summary>
    /// <param name="setup">The page setup to edit in place.</param>
    /// <param name="owner">The owner form, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the user confirmed; otherwise <see langword="false"/>.</returns>
    public static bool ShowDialog(ReportPageSetup setup, Form? owner = null)
    {
        var dialog = new PageSetupDialog(setup);
        var ok = dialog.ShowDialog(owner) == DialogResult.OK;
        if (ok) dialog.ApplyTo(setup);
        return ok;
    }

    private PageSetupDialog(ReportPageSetup setup)
    {
        Text = LangRes.GetString("PageSetupDialog_Title");
        Width = 360;
        Height = 330;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Zoom = 1f;

        Controls.Add(new Label { Text = LangRes.GetString("PageSetupDialog_PaperFormat"), X = 20, Y = 26, Width = 100 });
        _paperCombo = new ComboBox { X = 130, Y = 22, Width = 120 };
        foreach (var preset in PaperPresets)
            _paperCombo.Items.Add(preset.Name);
        _paperCombo.Items.Add(LangRes.GetString("PageSetupDialog_Custom"));
        _paperCombo.SelectedIndex = MatchPresetIndex(setup);
        Controls.Add(_paperCombo);

        _landscapeCheck = new CheckBox { Text = LangRes.GetString("PageSetupDialog_Landscape"), X = 260, Y = 24, Width = 90, Checked = setup.Landscape };
        Controls.Add(_landscapeCheck);

        Controls.Add(new Label { Text = LangRes.GetString("PageSetupDialog_MarginsLabel"), X = 20, Y = 62, Width = 140 });

        Controls.Add(new Label { Text = LangRes.GetString("PageSetupDialog_Left"), X = 20, Y = 94, Width = 60 });
        _leftBox = new TextBox { X = 84, Y = 90, Width = 70, Text = setup.LeftMargin.Value.ToString("0.##") };
        Controls.Add(_leftBox);

        Controls.Add(new Label { Text = LangRes.GetString("PageSetupDialog_Right"), X = 184, Y = 94, Width = 60 });
        _rightBox = new TextBox { X = 248, Y = 90, Width = 70, Text = setup.RightMargin.Value.ToString("0.##") };
        Controls.Add(_rightBox);

        Controls.Add(new Label { Text = LangRes.GetString("PageSetupDialog_Top"), X = 20, Y = 126, Width = 60 });
        _topBox = new TextBox { X = 84, Y = 122, Width = 70, Text = setup.TopMargin.Value.ToString("0.##") };
        Controls.Add(_topBox);

        Controls.Add(new Label { Text = LangRes.GetString("PageSetupDialog_Bottom"), X = 184, Y = 126, Width = 60 });
        _bottomBox = new TextBox { X = 248, Y = 122, Width = 70, Text = setup.BottomMargin.Value.ToString("0.##") };
        Controls.Add(_bottomBox);

        var buttonPanel = new Panel { Dock = DockStyle.Bottom, Height = 50, BackColor = Color.Transparent };
        var okButton = new Button { Text = LangRes.GetString("PageSetupDialog_Ok"), Width = 75, Height = 30, X = 72, Y = 10 };
        okButton.Click += (_, _) => CloseDialog(true);
        var cancelButton = new Button { Text = LangRes.GetString("PageSetupDialog_Cancel"), Width = 90, Height = 30, X = 157, Y = 10 };
        cancelButton.Click += (_, _) => CloseDialog(false);
        buttonPanel.Controls.AddRange([okButton, cancelButton]);
        Controls.Add(buttonPanel);
    }

    private static int MatchPresetIndex(ReportPageSetup setup)
    {
        for (var i = 0; i < PaperPresets.Length; i++)
        {
            if (Math.Abs(PaperPresets[i].Size.Width.Value - setup.PortraitWidth.Value) < 0.001 &&
                Math.Abs(PaperPresets[i].Size.Height.Value - setup.PortraitHeight.Value) < 0.001)
                return i;
        }

        return PaperPresets.Length;
    }

    private void ApplyTo(ReportPageSetup setup)
    {
        var index = _paperCombo.SelectedIndex;
        if (index >= 0 && index < PaperPresets.Length)
            setup.SetPaperSize(PaperPresets[index].Size);

        setup.Landscape = _landscapeCheck.Checked;
        setup.LeftMargin = ParseMargin(_leftBox.Text, setup.LeftMargin);
        setup.RightMargin = ParseMargin(_rightBox.Text, setup.RightMargin);
        setup.TopMargin = ParseMargin(_topBox.Text, setup.TopMargin);
        setup.BottomMargin = ParseMargin(_bottomBox.Text, setup.BottomMargin);
    }

    private static Cm ParseMargin(string? text, Cm fallback)
        => PropertyGridRow.TryParseCm(text, out var cm) ? cm : fallback;

    private void CloseDialog(bool ok)
    {
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
