using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Reports;

namespace OldSchoolForms.Ui.Designer.Reports;

/// <summary>
/// Modal dialog for editing the field list of a cross-tab report control. Each field defines
/// a data field name, an optional header caption, its usage (row, column or value) and the
/// aggregation applied to value fields.
/// </summary>
public sealed class CrossTabFieldsEditorDialog : Form
{
    private readonly string[] _usageLabels =
        [LangRes.GetString("CrossTabFields_UsageRow"), LangRes.GetString("CrossTabFields_UsageColumn"), LangRes.GetString("CrossTabFields_UsageValue")];
    private static readonly FieldUsage[] UsageValues = [FieldUsage.RowField, FieldUsage.ColumnField, FieldUsage.ValueField];
    private readonly string[] _aggregationLabels =
        [LangRes.GetString("CrossTabFields_AggSum"), LangRes.GetString("CrossTabFields_AggCount"), LangRes.GetString("CrossTabFields_AggAvg"), LangRes.GetString("CrossTabFields_AggMin"), LangRes.GetString("CrossTabFields_AggMax")];
    private static readonly CrossTabAggregation[] AggregationValues =
        [CrossTabAggregation.Sum, CrossTabAggregation.Count, CrossTabAggregation.Avg, CrossTabAggregation.Min, CrossTabAggregation.Max];

    private readonly ListBox _fieldList = null!;
    private readonly TextBox _dataFieldBox = null!;
    private readonly TextBox _headerBox = null!;
    private readonly ComboBox _usageCombo = null!;
    private readonly ComboBox _aggregationCombo = null!;
    private readonly TextBox _formatBox = null!;
    private readonly List<CrossTabField> _drafts = new();
    private bool _loading;

    /// <summary>
    /// Shows the cross-tab field editor for the given cross-tab control.
    /// </summary>
    /// <param name="crossTab">The cross-tab control whose fields are edited.</param>
    /// <param name="owner">The owner form, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the user confirmed the changes; otherwise <see langword="false"/>.</returns>
    public static bool ShowDialog(ReportCrossTab crossTab, Form? owner = null)
    {
        var dialog = new CrossTabFieldsEditorDialog(crossTab);
        if (dialog.ShowDialog(owner) != DialogResult.OK) return false;
        dialog.ApplyTo(crossTab);
        return true;
    }

    private CrossTabFieldsEditorDialog(ReportCrossTab crossTab)
    {
        Text = LangRes.GetString("CrossTabFieldsDialog_Title");
        Width = 560;
        Height = 420;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Zoom = 1f;

        foreach (var field in crossTab.Fields)
        {
            _drafts.Add(new CrossTabField
            {
                DataField = field.DataField,
                HeaderText = field.HeaderText,
                Usage = field.Usage,
                Aggregation = field.Aggregation,
                Format = field.Format
            });
        }

        Controls.Add(new Label { Text = LangRes.GetString("CrossTabFieldsDialog_Fields"), X = 20, Y = 16, Width = 120 });
        _fieldList = new ListBox { X = 20, Y = 40, Width = 170, Height = 280 };
        _fieldList.SelectedIndexChanged += (_, _) => LoadSelectedField();
        Controls.Add(_fieldList);

        var addButton = new Button { Text = LangRes.GetString("CrossTabFieldsDialog_AddField"), Width = 170, Height = 26, X = 20, Y = 326 };
        addButton.Click += (_, _) => AddField();
        Controls.Add(addButton);

        var removeButton = new Button { Text = LangRes.GetString("CrossTabFieldsDialog_RemoveField"), Width = 170, Height = 26, X = 20, Y = 356 };
        removeButton.Click += (_, _) => RemoveSelectedField();
        Controls.Add(removeButton);

        Controls.Add(new Label { Text = LangRes.GetString("CrossTabFieldsDialog_DataField"), X = 210, Y = 16, Width = 140 });
        _dataFieldBox = new TextBox { X = 210, Y = 40, Width = 314 };
        Controls.Add(_dataFieldBox);

        Controls.Add(new Label { Text = LangRes.GetString("CrossTabFieldsDialog_HeaderText"), X = 210, Y = 72, Width = 140 });
        _headerBox = new TextBox { X = 210, Y = 96, Width = 314 };
        Controls.Add(_headerBox);

        Controls.Add(new Label { Text = LangRes.GetString("CrossTabFieldsDialog_Usage"), X = 210, Y = 128, Width = 140 });
        _usageCombo = new ComboBox { X = 210, Y = 152, Width = 140, DropDownStyle = DropDownStyle.DropDownList };
        foreach (var label in _usageLabels)
            _usageCombo.Items.Add(label);
        Controls.Add(_usageCombo);

        Controls.Add(new Label { Text = LangRes.GetString("CrossTabFieldsDialog_Aggregation"), X = 370, Y = 128, Width = 140 });
        _aggregationCombo = new ComboBox { X = 370, Y = 152, Width = 154, DropDownStyle = DropDownStyle.DropDownList };
        foreach (var label in _aggregationLabels)
            _aggregationCombo.Items.Add(label);
        Controls.Add(_aggregationCombo);

        Controls.Add(new Label { Text = LangRes.GetString("CrossTabFieldsDialog_Format"), X = 210, Y = 184, Width = 140 });
        _formatBox = new TextBox { X = 210, Y = 208, Width = 314 };
        Controls.Add(_formatBox);

        var buttonPanel = new Panel { Dock = DockStyle.Bottom, Height = 50, BackColor = Color.Transparent };
        var okButton = new Button { Text = LangRes.GetString("CrossTabFieldsDialog_Ok"), Width = 75, Height = 30, X = 162, Y = 10 };
        okButton.Click += (_, _) => CloseDialog(true);
        var cancelButton = new Button { Text = LangRes.GetString("CrossTabFieldsDialog_Cancel"), Width = 90, Height = 30, X = 247, Y = 10 };
        cancelButton.Click += (_, _) => CloseDialog(false);
        buttonPanel.Controls.AddRange([okButton, cancelButton]);
        Controls.Add(buttonPanel);

        RefreshFieldList();
        if (_drafts.Count > 0)
            _fieldList.SelectedIndex = 0;
    }

    private void RefreshFieldList()
    {
        _loading = true;
        var selected = _fieldList.SelectedIndex;
        _fieldList.Items.Clear();
        foreach (var draft in _drafts)
            _fieldList.Items.Add(string.IsNullOrWhiteSpace(draft.DataField) ? LangRes.GetString("CrossTabFieldsDialog_UnnamedField") : draft.DisplayHeader);
        if (selected >= 0 && selected < _drafts.Count)
            _fieldList.SelectedIndex = selected;
        _loading = false;
    }

    private void LoadSelectedField()
    {
        if (_loading) return;
        CommitCurrentEdits();
        var index = _fieldList.SelectedIndex;
        if (index < 0 || index >= _drafts.Count) return;
        var draft = _drafts[index];
        _dataFieldBox.Text = draft.DataField;
        _headerBox.Text = draft.HeaderText ?? string.Empty;
        _usageCombo.SelectedIndex = Array.IndexOf(UsageValues, draft.Usage);
        _aggregationCombo.SelectedIndex = Array.IndexOf(AggregationValues, draft.Aggregation);
        _formatBox.Text = draft.Format ?? string.Empty;
    }

    private void CommitCurrentEdits()
    {
        var index = _fieldList.SelectedIndex;
        if (index < 0 || index >= _drafts.Count) return;
        var draft = _drafts[index];
        draft.DataField = _dataFieldBox.Text?.Trim() ?? string.Empty;
        var header = _headerBox.Text?.Trim();
        draft.HeaderText = string.IsNullOrEmpty(header) ? null : header;
        if (_usageCombo.SelectedIndex >= 0 && _usageCombo.SelectedIndex < UsageValues.Length)
            draft.Usage = UsageValues[_usageCombo.SelectedIndex];
        if (_aggregationCombo.SelectedIndex >= 0 && _aggregationCombo.SelectedIndex < AggregationValues.Length)
            draft.Aggregation = AggregationValues[_aggregationCombo.SelectedIndex];
        var format = _formatBox.Text?.Trim();
        draft.Format = string.IsNullOrEmpty(format) ? null : format;
        RefreshFieldList();
    }

    private void AddField()
    {
        CommitCurrentEdits();
        _drafts.Add(new CrossTabField { Usage = FieldUsage.ValueField, Aggregation = CrossTabAggregation.Sum });
        RefreshFieldList();
        _fieldList.SelectedIndex = _drafts.Count - 1;
    }

    private void RemoveSelectedField()
    {
        var index = _fieldList.SelectedIndex;
        if (index < 0 || index >= _drafts.Count) return;
        _drafts.RemoveAt(index);
        ClearEditFields();
        RefreshFieldList();
        if (_drafts.Count > 0)
            _fieldList.SelectedIndex = Math.Min(index, _drafts.Count - 1);
    }

    private void ClearEditFields()
    {
        _dataFieldBox.Text = string.Empty;
        _headerBox.Text = string.Empty;
        _usageCombo.SelectedIndex = -1;
        _aggregationCombo.SelectedIndex = -1;
        _formatBox.Text = string.Empty;
    }

    private void ApplyTo(ReportCrossTab crossTab)
    {
        crossTab.Fields.Clear();
        foreach (var draft in _drafts)
        {
            crossTab.Fields.Add(new CrossTabField
            {
                DataField = draft.DataField,
                HeaderText = draft.HeaderText,
                Usage = draft.Usage,
                Aggregation = draft.Aggregation,
                Format = draft.Format
            });
        }
    }

    private void CloseDialog(bool ok)
    {
        if (ok) CommitCurrentEdits();
        DialogResult = ok ? DialogResult.OK : DialogResult.Cancel;
        _modal = false;
    }

    /// <summary>
    /// Handles keyboard shortcuts. Escape cancels the dialog.
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
