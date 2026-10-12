using Jint.Native.Object;
using OldSchoolForms.Ui.Reports;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// JS facade for a <see cref="ReportCheckBox"/>.
/// </summary>
public class JsReportCheckBox : JsReportControl
{
    /// <summary>The wrapped check box.</summary>
    public ReportCheckBox CheckBox => (ReportCheckBox)Control;

    /// <summary>
    /// Creates a facade around a report check box.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="checkBox">The wrapped check box.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="checkBox"/> is <see langword="null"/>.</exception>
    public JsReportCheckBox(JsEngineHost host, ReportCheckBox checkBox)
        : base(host, checkBox)
    {
        ArgumentNullException.ThrowIfNull(checkBox);
    }

    /// <summary>The boolean data field bound to the check state.</summary>
    public string DataField
    {
        get => CheckBox.DataField ?? string.Empty;
        set => CheckBox.DataField = string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>The caption shown next to the box.</summary>
    public string Caption
    {
        get => CheckBox.Caption ?? string.Empty;
        set => CheckBox.Caption = string.IsNullOrEmpty(value) ? null : value;
    }

    /// <inheritdoc />
    protected override void ApplyTypeConfig(ObjectInstance config)
    {
        if (JsMarshal.GetString(config, "dataField") is { } dataField) DataField = dataField;
        if (JsMarshal.GetString(config, "caption") is { } caption) Caption = caption;
    }
}
