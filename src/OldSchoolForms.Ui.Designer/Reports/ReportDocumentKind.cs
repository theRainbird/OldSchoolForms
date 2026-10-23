using System;
using OldSchoolForms.Ui.Core;
using System.IO;
using OldSchoolForms.Ui.Design;
using OldSchoolForms.Ui.Reports;
using OldSchoolForms.Ui.Designer.Services;

namespace OldSchoolForms.Ui.Designer.Reports;

/// <summary>
/// Document kind for banded reports persisted in the <c>OldSchoolForms.Report</c>
/// JSON format with the <c>.report</c> file extension. Bridges the
/// <see cref="ReportJsonSerializer"/> to the designer surface via a
/// <see cref="DesignReport"/> root.
/// </summary>
public class ReportDocumentKind : IDesignDocumentKind
{
    private static ReportDocumentKind? _instance;
    private static bool _registered;

    /// <summary>
    /// Gets the shared instance of this document kind.
    /// </summary>
    public static ReportDocumentKind Instance => _instance ??= new ReportDocumentKind();

    /// <summary>
    /// Registers this document kind in the <see cref="DocumentKindRegistry"/> if it
    /// has not been registered yet. Safe to call multiple times.
    /// </summary>
    public static void EnsureRegistered()
    {
        if (_registered) return;
        DocumentKindRegistry.Register(Instance);
        ValueEditorRegistry.Register(new ReportDataFieldEditor());
        ValueEditorRegistry.Register(new ReportExpressionEditor());
        ValueEditorRegistry.Register(new ReportDesignFieldsEditor());
        ValueEditorRegistry.Register(new ReportPageSetupEditor());
        ValueEditorRegistry.Register(new ReportChartSeriesEditor());
        ValueEditorRegistry.Register(new ReportCrossTabFieldsEditor());
        ValueEditorRegistry.Register(new ReportBandDataSourceEditor());
        _registered = true;
    }

    /// <inheritdoc />
    public string Extension => ".report";

    /// <inheritdoc />
    public string DisplayName => LangRes.GetString("ReportDesigner_DocumentName");

    /// <inheritdoc />
    public void CreateNew(DesignSurface surface)
    {
        if (surface == null) throw new ArgumentNullException(nameof(surface));
        surface.ClearAll();
        var report = new Report(LangRes.GetString("ReportDesigner_DocumentName"));
        // Seed an initial default data source so the data source tree starts
        // usable and bands have a source to bind to out of the box.
        report.DataSources.Add(new ReportDataSource("Data"));
        surface.BeginDesignReport(report);
    }

    /// <inheritdoc />
    public void Load(DesignSurface surface, string path)
    {
        if (surface == null) throw new ArgumentNullException(nameof(surface));
        var report = ReportJsonSerializer.Load(File.ReadAllText(path));
        surface.ClearAll();
        surface.BeginDesignReport(report);
    }

    /// <inheritdoc />
    public void Save(DesignSurface surface, string path)
    {
        if (surface == null) throw new ArgumentNullException(nameof(surface));
        if (surface.RootControl is not DesignReport designReport)
            throw new InvalidOperationException(LangRes.GetString("ReportDesigner_NoReportOnSurface"));
        File.WriteAllText(path, ReportJsonSerializer.Save(designReport.Report));
    }
}
