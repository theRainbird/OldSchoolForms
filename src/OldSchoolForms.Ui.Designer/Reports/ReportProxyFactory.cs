using System;
using System.Collections.Generic;
using OldSchoolForms.Ui.Reports;

namespace OldSchoolForms.Ui.Designer.Reports;

/// <summary>
/// Creates design-time proxy controls for report model controls. Factories are
/// registered per concrete <see cref="ReportControl"/> type with a base-type
/// fallback for future report control types.
/// </summary>
internal static class ReportProxyFactory
{
    private static readonly Dictionary<Type, Func<ReportControl, DesignReportControl>> _factories = new();

    static ReportProxyFactory()
    {
        Register<ReportLabel>(rc => new DesignReportLabel(rc));
        Register<ReportTextBox>(rc => new DesignReportTextBox(rc));
        Register<ReportImage>(rc => new DesignReportImage(rc));
        Register<ReportLine>(rc => new DesignReportLine(rc));
        Register<ReportCheckBox>(rc => new DesignReportCheckBox(rc));
        Register<ReportChartControl>(rc => new DesignReportChart(rc));
        Register<ReportCrossTab>(rc => new DesignReportCrossTab(rc));
    }

    /// <summary>
    /// Registers a proxy factory for a concrete report control type.
    /// </summary>
    /// <typeparam name="TReport">The report control type to proxy.</typeparam>
    /// <param name="factory">Factory creating the proxy for an instance of the type.</param>
    public static void Register<TReport>(Func<TReport, DesignReportControl> factory)
        where TReport : ReportControl
    {
        _factories[typeof(TReport)] = rc => factory((TReport)rc);
    }

    /// <summary>
    /// Creates the design proxy for the given report control.
    /// </summary>
    /// <param name="reportControl">The report control to wrap.</param>
    /// <returns>The proxy control, or <see langword="null"/> when no factory matches.</returns>
    public static DesignReportControl? Create(ReportControl reportControl)
    {
        if (reportControl == null) return null;

        var type = reportControl.GetType();
        if (_factories.TryGetValue(type, out var exact))
            return exact(reportControl);

        foreach (var pair in _factories)
        {
            if (pair.Key.IsAssignableFrom(type))
                return pair.Value(reportControl);
        }

        return null;
    }
}
