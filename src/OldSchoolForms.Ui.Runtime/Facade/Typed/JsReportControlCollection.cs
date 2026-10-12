using Jint;
using Jint.Native;
using Jint.Native.Object;
using OldSchoolForms.Ui.Reports;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// JS facade for the controls of a <see cref="ReportBand"/>. Controls are added
/// via configuration objects with a <c>type</c> member selecting the control
/// kind: <c>label</c>, <c>textBox</c>, <c>image</c>, <c>line</c>,
/// <c>checkBox</c>, <c>chart</c>, <c>crossTab</c>.
/// </summary>
public class JsReportControlCollection
{
    private readonly JsEngineHost _host;
    private readonly ReportBand _band;

    /// <summary>
    /// Creates the facade over a band's control collection.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="band">The owning band.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="band"/> is <see langword="null"/>.</exception>
    public JsReportControlCollection(JsEngineHost host, ReportBand band)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(band);
        _host = host;
        _band = band;
    }

    /// <summary>The number of controls in the band.</summary>
    public int Count => _band.Controls.Count;

    /// <summary>
    /// Gets the control facade at the given index.
    /// </summary>
    /// <param name="index">The zero-based control index.</param>
    /// <returns>The control facade.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside the collection.</exception>
    public JsReportControl GetAt(int index) => Wrap(_host, _band.Controls[index]);

    /// <summary>
    /// Adds a control from a JS configuration object. The <c>type</c> member
    /// selects the control kind.
    /// </summary>
    /// <param name="config">The JS configuration object.</param>
    /// <returns>The new control facade.</returns>
    /// <exception cref="ArgumentException">The config is not an object or has no/unknown <c>type</c>.</exception>
    public JsReportControl Add(JsValue config)
    {
        if (config is not ObjectInstance obj)
            throw new ArgumentException("Report control configuration must be an object.", nameof(config));
        var type = JsMarshal.GetString(obj, "type")
            ?? throw new ArgumentException("Report control configuration requires a 'type' member.", nameof(config));

        ReportControl control = type switch
        {
            "label" => new ReportLabel(),
            "textBox" => new ReportTextBox(),
            "image" => new ReportImage(),
            "line" => new ReportLine(),
            "checkBox" => new ReportCheckBox(),
            "chart" => new ReportChartControl(),
            "crossTab" => new ReportCrossTab(),
            _ => throw new ArgumentException($"Unknown report control type '{type}'.", nameof(config))
        };

        var facade = Wrap(_host, control);
        facade.ApplyConfig(obj);
        _band.Controls.Add(control);
        return facade;
    }

    /// <summary>
    /// Removes the control at the given index.
    /// </summary>
    /// <param name="index">The zero-based control index.</param>
    /// <returns><see langword="true"/> when a control was removed.</returns>
    public bool RemoveAt(int index)
    {
        if (index < 0 || index >= _band.Controls.Count)
            return false;
        _band.Controls.RemoveAt(index);
        return true;
    }

    /// <summary>Removes all controls from the band.</summary>
    public void Clear() => _band.Controls.Clear();

    internal static JsReportControl Wrap(JsEngineHost host, ReportControl control) => control switch
    {
        ReportLabel label => new JsReportLabel(host, label),
        ReportTextBox textBox => new JsReportTextBox(host, textBox),
        ReportImage image => new JsReportImage(host, image),
        ReportLine line => new JsReportLine(host, line),
        ReportCheckBox checkBox => new JsReportCheckBox(host, checkBox),
        ReportChartControl chart => new JsReportChart(host, chart),
        ReportCrossTab crossTab => new JsReportCrossTab(host, crossTab),
        _ => new JsReportControl(host, control)
    };
}
