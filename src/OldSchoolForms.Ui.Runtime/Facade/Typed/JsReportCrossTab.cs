using Jint.Native.Object;
using OldSchoolForms.Ui.Reports;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// JS facade for a <see cref="ReportCrossTab"/>.
/// </summary>
public class JsReportCrossTab : JsReportControl
{
    private readonly JsCrossTabFieldCollection _fields;

    /// <summary>The wrapped cross-tab control.</summary>
    public ReportCrossTab CrossTab => (ReportCrossTab)Control;

    /// <summary>
    /// Creates a facade around a report cross-tab control.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="crossTab">The wrapped cross-tab control.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="crossTab"/> is <see langword="null"/>.</exception>
    public JsReportCrossTab(JsEngineHost host, ReportCrossTab crossTab)
        : base(host, crossTab)
    {
        ArgumentNullException.ThrowIfNull(crossTab);
        _fields = new JsCrossTabFieldCollection(crossTab);
    }

    /// <summary>The cross-tab field collection facade.</summary>
    public JsCrossTabFieldCollection Fields => _fields;

    /// <summary>The group field that scopes aggregation to the current group.</summary>
    public string GroupField
    {
        get => CrossTab.GroupField ?? string.Empty;
        set => CrossTab.GroupField = string.IsNullOrEmpty(value) ? null : value;
    }

    /// <inheritdoc />
    protected override void ApplyTypeConfig(ObjectInstance config)
    {
        if (JsMarshal.GetString(config, "groupField") is { } groupField) GroupField = groupField;
    }
}
