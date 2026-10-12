using Jint.Native.Object;
using OldSchoolForms.Ui.Reports;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// JS facade for a <see cref="ReportTextBox"/> bound to a data field or an
/// expression. Expressions use the <c>{FieldName}</c> / <c>{Field:C}</c>
/// template syntax; the expression wins over the data field.
/// </summary>
public class JsReportTextBox : JsReportControl
{
    /// <summary>The wrapped text box.</summary>
    public ReportTextBox TextBox => (ReportTextBox)Control;

    /// <summary>
    /// Creates a facade around a report text box.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="textBox">The wrapped text box.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="textBox"/> is <see langword="null"/>.</exception>
    public JsReportTextBox(JsEngineHost host, ReportTextBox textBox)
        : base(host, textBox)
    {
        ArgumentNullException.ThrowIfNull(textBox);
    }

    /// <summary>The data source field bound to this text box.</summary>
    public string DataField
    {
        get => TextBox.DataField ?? string.Empty;
        set => TextBox.DataField = string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>The value expression using <c>{Field}</c> template syntax.</summary>
    public string Expression
    {
        get => TextBox.Expression ?? string.Empty;
        set => TextBox.Expression = string.IsNullOrEmpty(value) ? null : value;
    }

    /// <inheritdoc />
    protected override void ApplyTypeConfig(ObjectInstance config)
    {
        if (JsMarshal.GetString(config, "dataField") is { } dataField) DataField = dataField;
        if (JsMarshal.GetString(config, "expression") is { } expression) Expression = expression;
    }
}
