using System.Globalization;
using System.Text;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Reports;

namespace OldSchoolForms.Ui.Designer.Reports;

/// <summary>
/// Modal dialog for editing the data series of a report chart. Each series has a name and a
/// list of data points entered as one "label;value" pair per line.
/// </summary>
public sealed class ChartSeriesEditorDialog : Form
{
    private sealed class SeriesDraft
    {
        public string Name { get; set; } = string.Empty;
        public List<(string Label, double Value)> Points { get; set; } = new();
    }

    private readonly ListBox _seriesList = null!;
    private readonly TextBox _nameBox = null!;
    private readonly MemoBox _pointsBox = null!;
    private readonly List<SeriesDraft> _drafts = new();
    private bool _loading;

    /// <summary>
    /// Shows the chart series editor for the given chart control.
    /// </summary>
    /// <param name="chart">The chart control whose series are edited.</param>
    /// <param name="owner">The owner form, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the user confirmed the changes; otherwise <see langword="false"/>.</returns>
    public static bool ShowDialog(ReportChartControl chart, Form? owner = null)
    {
        var dialog = new ChartSeriesEditorDialog(chart);
        if (dialog.ShowDialog(owner) != DialogResult.OK) return false;
        dialog.ApplyTo(chart);
        return true;
    }

    private ChartSeriesEditorDialog(ReportChartControl chart)
    {
        Text = LangRes.GetString("ChartSeriesDialog_Title");
        Width = 560;
        Height = 420;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Zoom = 1f;

        foreach (var series in chart.Series)
        {
            var draft = new SeriesDraft { Name = series.Name };
            foreach (var point in series.Points)
                draft.Points.Add((point.Label ?? string.Empty, point.Value));
            _drafts.Add(draft);
        }

        Controls.Add(new Label { Text = LangRes.GetString("ChartSeriesDialog_Series"), X = 20, Y = 16, Width = 120 });
        _seriesList = new ListBox { X = 20, Y = 40, Width = 160, Height = 280 };
        _seriesList.SelectedIndexChanged += (_, _) => LoadSelectedSeries();
        Controls.Add(_seriesList);

        var addSeriesButton = new Button { Text = LangRes.GetString("ChartSeriesDialog_AddSeries"), Width = 160, Height = 26, X = 20, Y = 326 };
        addSeriesButton.Click += (_, _) => AddSeries();
        Controls.Add(addSeriesButton);

        var removeSeriesButton = new Button { Text = LangRes.GetString("ChartSeriesDialog_RemoveSeries"), Width = 160, Height = 26, X = 20, Y = 356 };
        removeSeriesButton.Click += (_, _) => RemoveSelectedSeries();
        Controls.Add(removeSeriesButton);

        Controls.Add(new Label { Text = LangRes.GetString("ChartSeriesDialog_Name"), X = 200, Y = 16, Width = 120 });
        _nameBox = new TextBox { X = 200, Y = 40, Width = 324 };
        Controls.Add(_nameBox);

        Controls.Add(new Label { Text = LangRes.GetString("ChartSeriesDialog_PointsLabel"), X = 200, Y = 72, Width = 240 });
        _pointsBox = new MemoBox { X = 200, Y = 96, Width = 324, Height = 286 };
        Controls.Add(_pointsBox);

        var buttonPanel = new Panel { Dock = DockStyle.Bottom, Height = 50, BackColor = Color.Transparent };
        var okButton = new Button { Text = LangRes.GetString("ChartSeriesDialog_Ok"), Width = 75, Height = 30, X = 162, Y = 10 };
        okButton.Click += (_, _) => CloseDialog(true);
        var cancelButton = new Button { Text = LangRes.GetString("ChartSeriesDialog_Cancel"), Width = 90, Height = 30, X = 247, Y = 10 };
        cancelButton.Click += (_, _) => CloseDialog(false);
        buttonPanel.Controls.AddRange([okButton, cancelButton]);
        Controls.Add(buttonPanel);

        RefreshSeriesList();
        if (_drafts.Count > 0)
            _seriesList.SelectedIndex = 0;
    }

    private void RefreshSeriesList()
    {
        _loading = true;
        var selected = _seriesList.SelectedIndex;
        _seriesList.Items.Clear();
        foreach (var draft in _drafts)
            _seriesList.Items.Add(string.IsNullOrWhiteSpace(draft.Name) ? LangRes.GetString("ChartSeriesDialog_UnnamedSeries") : draft.Name);
        if (selected >= 0 && selected < _drafts.Count)
            _seriesList.SelectedIndex = selected;
        _loading = false;
    }

    private void LoadSelectedSeries()
    {
        if (_loading) return;
        CommitCurrentEdits();
        var index = _seriesList.SelectedIndex;
        if (index < 0 || index >= _drafts.Count) return;
        var draft = _drafts[index];
        _nameBox.Text = draft.Name;
        var sb = new StringBuilder();
        foreach (var (label, value) in draft.Points)
        {
            if (sb.Length > 0) sb.AppendLine();
            sb.Append(label).Append(';').Append(value.ToString(CultureInfo.InvariantCulture));
        }
        _pointsBox.Text = sb.ToString();
    }

    private void CommitCurrentEdits()
    {
        var index = _seriesList.SelectedIndex;
        if (index < 0 || index >= _drafts.Count) return;
        var draft = _drafts[index];
        draft.Name = _nameBox.Text?.Trim() ?? string.Empty;
        draft.Points = ParsePoints(_pointsBox.Text);
        RefreshSeriesList();
    }

    private static List<(string Label, double Value)> ParsePoints(string? text)
    {
        var points = new List<(string Label, double Value)>();
        if (string.IsNullOrWhiteSpace(text)) return points;
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;
            var sep = line.IndexOf(';');
            string label;
            string valuePart;
            if (sep < 0)
            {
                label = line;
                valuePart = string.Empty;
            }
            else
            {
                label = line[..sep].Trim();
                valuePart = line[(sep + 1)..].Trim();
            }
            var value = double.TryParse(valuePart, NumberStyles.Float, CultureInfo.InvariantCulture, out var inv)
                ? inv
                : double.TryParse(valuePart, NumberStyles.Float, CultureInfo.CurrentCulture, out var cur)
                    ? cur
                    : 0d;
            points.Add((label, value));
        }
        return points;
    }

    private void AddSeries()
    {
        CommitCurrentEdits();
        _drafts.Add(new SeriesDraft { Name = string.Format(LangRes.GetString("ChartSeriesDialog_DefaultSeriesName"), _drafts.Count + 1) });
        RefreshSeriesList();
        _seriesList.SelectedIndex = _drafts.Count - 1;
    }

    private void RemoveSelectedSeries()
    {
        var index = _seriesList.SelectedIndex;
        if (index < 0 || index >= _drafts.Count) return;
        _drafts.RemoveAt(index);
        _nameBox.Text = string.Empty;
        _pointsBox.Text = string.Empty;
        RefreshSeriesList();
        if (_drafts.Count > 0)
            _seriesList.SelectedIndex = Math.Min(index, _drafts.Count - 1);
    }

    private void ApplyTo(ReportChartControl chart)
    {
        chart.Series.Clear();
        foreach (var draft in _drafts)
        {
            var series = new DiagramViewSeries { Name = draft.Name };
            foreach (var (label, value) in draft.Points)
                series.Add(label, value);
            chart.Series.Add(series);
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
