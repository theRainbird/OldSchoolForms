using System.Reflection;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Design;
using OldSchoolForms.Ui.Designer.PropertyGrid;
using OldSchoolForms.Ui.Reports;

namespace OldSchoolForms.Ui.Designer.Reports;

/// <summary>
/// Value editor for the <c>DataField</c> property of report controls
/// (<see cref="ReportTextBox"/>, <see cref="ReportCheckBox"/>). Opens the
/// <see cref="DataFieldDialog"/> so the user can pick a field from the report's
/// available fields or type a name. The value is written back through the
/// property descriptor so undo and dirty tracking work.
/// </summary>
public sealed class ReportDataFieldEditor : IValueEditor
{
    /// <inheritdoc />
    public string ButtonText => "…";

    /// <inheritdoc />
    public bool Handles(PropertyInfo property)
        => property.Name == nameof(ReportTextBox.DataField)
           && typeof(ReportControl).IsAssignableFrom(property.DeclaringType);

    /// <inheritdoc />
    public void Open(object target)
    {
    }

    /// <inheritdoc />
    public void Open(object target, object? context)
    {
        if (context is not PropertyDescriptor descriptor)
        {
            Open(target);
            return;
        }

        var report = ReportDesignContext.Current?.Report;
        var owner = ReportDesignContext.Current?.FindForm();

        IReadOnlyList<string> fields;
        if (report != null && target is ReportControl control)
        {
            var band = ReportDesignContext.GetBandForControl(report, control);
            fields = ReportDesignContext.GetFieldsForBand(report, band);
        }
        else
        {
            fields = report != null
                ? ReportDesignContext.GetAvailableFields(report)
                : Array.Empty<string>();
        }

        var current = descriptor.GetValue() as string;
        var result = DataFieldDialog.ShowDialog(current, fields, owner);
        if (result != null && result != current)
            descriptor.SetValue(result);
    }
}

/// <summary>
/// Value editor for the <c>Expression</c> property of <see cref="ReportTextBox"/>.
/// Opens the <see cref="ExpressionDialog"/> with a multi-line editor and a field
/// list for inserting <c>{Field}</c> placeholders. The value is written back
/// through the property descriptor so undo and dirty tracking work.
/// </summary>
public sealed class ReportExpressionEditor : IValueEditor
{
    /// <inheritdoc />
    public string ButtonText => "…";

    /// <inheritdoc />
    public bool Handles(PropertyInfo property)
        => property.Name == nameof(ReportTextBox.Expression)
           && property.DeclaringType == typeof(ReportTextBox);

    /// <inheritdoc />
    public void Open(object target)
    {
    }

    /// <inheritdoc />
    public void Open(object target, object? context)
    {
        if (context is not PropertyDescriptor descriptor)
        {
            Open(target);
            return;
        }

        var report = ReportDesignContext.Current?.Report;
        var owner = ReportDesignContext.Current?.FindForm();

        IReadOnlyList<string> fields;
        if (report != null && target is ReportControl control)
        {
            var band = ReportDesignContext.GetBandForControl(report, control);
            var combined = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in ReportDesignContext.GetFieldsForBand(report, band))
                combined.Add(f);
            foreach (var q in ReportDesignContext.GetQualifiedFields(report))
                combined.Add(q);
            fields = combined.ToList();
        }
        else
        {
            fields = report != null
                ? ReportDesignContext.GetAvailableFields(report)
                : Array.Empty<string>();
        }

        var current = descriptor.GetValue() as string;
        var result = ExpressionDialog.ShowDialog(current, fields, owner);
        if (result != null && result != current)
            descriptor.SetValue(result);
    }
}

/// <summary>
/// Value editor for the <see cref="Report.DesignFields"/> list. Opens the
/// <see cref="DesignFieldsDialog"/> and mutates the list in place on OK. The
/// change is recorded on the surface's undo stack as an action so it can be
/// reverted with Ctrl+Z.
/// </summary>
public sealed class ReportDesignFieldsEditor : IValueEditor
{
    /// <inheritdoc />
    public string ButtonText => "…";

    /// <inheritdoc />
    public bool Handles(PropertyInfo property)
        => property.Name == nameof(Report.DesignFields)
           && property.DeclaringType == typeof(Report);

    /// <inheritdoc />
    public void Open(object target)
    {
    }

    /// <inheritdoc />
    public void Open(object target, object? context)
    {
        if (target is not Report report) return;

        var owner = ReportDesignContext.Current?.FindForm();
        var result = DesignFieldsDialog.ShowDialog(report.DesignFields, owner);
        if (result == null) return;

        var oldFields = new List<string>(report.DesignFields);
        var newFields = new List<string>(result);
        ApplyDesignFields(report, newFields);

        var surface = ReportDesignContext.Current?.Surface;
        surface?.Undo.PushAction(
            () => ApplyDesignFields(report, oldFields),
            () => ApplyDesignFields(report, newFields));
    }

    private static void ApplyDesignFields(Report report, List<string> fields)
    {
        report.DesignFields.Clear();
        foreach (var field in fields)
            report.DesignFields.Add(field);
        ReportDesignContext.Current?.RefreshGeometry();
        ReportDesignContext.Current?.Surface?.Invalidate();
    }
}

/// <summary>
/// Value editor for the <see cref="Report.PageSetup"/> property. Opens the
/// <see cref="PageSetupDialog"/> which mutates the page setup in place on OK.
/// The previous paper size, orientation, and margins are captured so the change
/// can be reverted via the surface's undo stack.
/// </summary>
public sealed class ReportPageSetupEditor : IValueEditor
{
    /// <inheritdoc />
    public string ButtonText => "…";

    /// <inheritdoc />
    public bool Handles(PropertyInfo property)
        => property.PropertyType == typeof(ReportPageSetup);

    /// <inheritdoc />
    public void Open(object target)
    {
    }

    /// <inheritdoc />
    public void Open(object target, object? context)
    {
        if (target is not Report report) return;

        var setup = report.PageSetup;
        var oldPortraitWidth = setup.PortraitWidth;
        var oldPortraitHeight = setup.PortraitHeight;
        var oldLandscape = setup.Landscape;
        var oldLeft = setup.LeftMargin;
        var oldRight = setup.RightMargin;
        var oldTop = setup.TopMargin;
        var oldBottom = setup.BottomMargin;

        var owner = ReportDesignContext.Current?.FindForm();
        if (!PageSetupDialog.ShowDialog(setup, owner)) return;

        var newPortraitWidth = setup.PortraitWidth;
        var newPortraitHeight = setup.PortraitHeight;
        var newLandscape = setup.Landscape;
        var newLeft = setup.LeftMargin;
        var newRight = setup.RightMargin;
        var newTop = setup.TopMargin;
        var newBottom = setup.BottomMargin;

        var surface = ReportDesignContext.Current?.Surface;
        surface?.Undo.PushAction(
            () => Restore(setup, oldPortraitWidth, oldPortraitHeight, oldLandscape, oldLeft, oldRight, oldTop, oldBottom),
            () => Restore(setup, newPortraitWidth, newPortraitHeight, newLandscape, newLeft, newRight, newTop, newBottom));
    }

    private static void Restore(ReportPageSetup setup, Cm portraitWidth, Cm portraitHeight, bool landscape,
        Cm left, Cm right, Cm top, Cm bottom)
    {
        setup.SetPaperSize(portraitWidth, portraitHeight);
        setup.Landscape = landscape;
        setup.LeftMargin = left;
        setup.RightMargin = right;
        setup.TopMargin = top;
        setup.BottomMargin = bottom;
        ReportDesignContext.Current?.RefreshGeometry();
        ReportDesignContext.Current?.Surface?.Invalidate();
    }
}

/// <summary>
/// Value editor for the <c>Series</c> property of <see cref="ReportChartControl"/>. Opens the
/// <see cref="ChartSeriesEditorDialog"/> and replaces the chart's series on OK. The previous
/// series are captured so the change can be reverted via the surface's undo stack.
/// </summary>
public sealed class ReportChartSeriesEditor : IValueEditor
{
    /// <inheritdoc />
    public string ButtonText => "…";

    /// <inheritdoc />
    public bool Handles(PropertyInfo property)
        => property.Name == nameof(ReportChartControl.Series)
           && property.DeclaringType == typeof(ReportChartControl);

    /// <inheritdoc />
    public void Open(object target)
    {
    }

    /// <inheritdoc />
    public void Open(object target, object? context)
    {
        if (target is not ReportChartControl chart) return;

        var owner = ReportDesignContext.Current?.FindForm();
        var oldSeries = SnapshotSeries(chart);
        if (!ChartSeriesEditorDialog.ShowDialog(chart, owner)) return;
        var newSeries = SnapshotSeries(chart);

        var surface = ReportDesignContext.Current?.Surface;
        surface?.Undo.PushAction(
            () => RestoreSeries(chart, oldSeries),
            () => RestoreSeries(chart, newSeries));
        ReportDesignContext.Current?.Surface?.Invalidate();
    }

    private static List<(string Name, List<(string Label, double Value)> Points)> SnapshotSeries(ReportChartControl chart)
    {
        var snapshot = new List<(string Name, List<(string Label, double Value)> Points)>();
        foreach (var series in chart.Series)
        {
            var points = new List<(string Label, double Value)>();
            foreach (var point in series.Points)
                points.Add((point.Label ?? string.Empty, point.Value));
            snapshot.Add((series.Name, points));
        }
        return snapshot;
    }

    private static void RestoreSeries(ReportChartControl chart, List<(string Name, List<(string Label, double Value)> Points)> series)
    {
        chart.Series.Clear();
        foreach (var (name, points) in series)
        {
            var target = new DiagramViewSeries { Name = name };
            foreach (var (label, value) in points)
                target.Add(label, value);
            chart.Series.Add(target);
        }
        ReportDesignContext.Current?.Surface?.Invalidate();
    }
}

/// <summary>
/// Value editor for the <c>Fields</c> property of <see cref="ReportCrossTab"/>. Opens the
/// <see cref="CrossTabFieldsEditorDialog"/> and replaces the cross-tab's fields on OK. The
/// previous fields are captured so the change can be reverted via the surface's undo stack.
/// </summary>
public sealed class ReportCrossTabFieldsEditor : IValueEditor
{
    /// <inheritdoc />
    public string ButtonText => "…";

    /// <inheritdoc />
    public bool Handles(PropertyInfo property)
        => property.Name == nameof(ReportCrossTab.Fields)
           && property.DeclaringType == typeof(ReportCrossTab);

    /// <inheritdoc />
    public void Open(object target)
    {
    }

    /// <inheritdoc />
    public void Open(object target, object? context)
    {
        if (target is not ReportCrossTab crossTab) return;

        var owner = ReportDesignContext.Current?.FindForm();
        var oldFields = SnapshotFields(crossTab);
        if (!CrossTabFieldsEditorDialog.ShowDialog(crossTab, owner)) return;
        var newFields = SnapshotFields(crossTab);

        var surface = ReportDesignContext.Current?.Surface;
        surface?.Undo.PushAction(
            () => RestoreFields(crossTab, oldFields),
            () => RestoreFields(crossTab, newFields));
        ReportDesignContext.Current?.Surface?.Invalidate();
    }

    private static List<CrossTabField> SnapshotFields(ReportCrossTab crossTab)
    {
        var snapshot = new List<CrossTabField>();
        foreach (var field in crossTab.Fields)
        {
            snapshot.Add(new CrossTabField
            {
                DataField = field.DataField,
                HeaderText = field.HeaderText,
                Usage = field.Usage,
                Aggregation = field.Aggregation,
                Format = field.Format
            });
        }
        return snapshot;
    }

    private static void RestoreFields(ReportCrossTab crossTab, List<CrossTabField> fields)
    {
        crossTab.Fields.Clear();
        foreach (var field in fields)
        {
            crossTab.Fields.Add(new CrossTabField
            {
                DataField = field.DataField,
                HeaderText = field.HeaderText,
                Usage = field.Usage,
                Aggregation = field.Aggregation,
                Format = field.Format
            });
        }
        ReportDesignContext.Current?.Surface?.Invalidate();
    }
}

/// <summary>
/// Value editor for the <see cref="ReportBand.DataSourceName"/> property. Opens a
/// dialog listing the report's declared data sources plus a <em>(default)</em>
/// entry that clears the binding. The value is written through the property
/// descriptor so undo and dirty tracking work.
/// </summary>
public sealed class ReportBandDataSourceEditor : IValueEditor
{
    /// <inheritdoc />
    public string ButtonText => "…";

    /// <inheritdoc />
    public bool Handles(PropertyInfo property)
        => property.Name == nameof(ReportBand.DataSourceName)
           && property.DeclaringType == typeof(ReportBand);

    /// <inheritdoc />
    public void Open(object target)
    {
    }

    /// <inheritdoc />
    public void Open(object target, object? context)
    {
        if (context is not PropertyDescriptor descriptor)
        {
            Open(target);
            return;
        }

        var report = ReportDesignContext.Current?.Report;
        var owner = ReportDesignContext.Current?.FindForm();

        var names = new List<string>();
        if (report != null)
        {
            foreach (var source in report.DataSources)
            {
                if (!string.IsNullOrWhiteSpace(source.Name))
                    names.Add(source.Name);
            }
        }

        var current = descriptor.GetValue() as string;
        var result = DataSourceNameDialog.ShowDialog(current, names, owner);
        if (result != current)
            descriptor.SetValue(result);
    }
}
