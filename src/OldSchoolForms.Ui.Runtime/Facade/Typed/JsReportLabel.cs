using Jint.Native.Object;
using OldSchoolForms.Ui.Reports;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// JS facade for a <see cref="ReportLabel"/> (static text).
/// </summary>
public class JsReportLabel : JsReportControl
{
    /// <summary>The wrapped label.</summary>
    public ReportLabel Label => (ReportLabel)Control;

    /// <summary>
    /// Creates a facade around a report label.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="label">The wrapped label.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="label"/> is <see langword="null"/>.</exception>
    public JsReportLabel(JsEngineHost host, ReportLabel label)
        : base(host, label)
    {
        ArgumentNullException.ThrowIfNull(label);
    }

    /// <summary>The label text.</summary>
    public string Text
    {
        get => Label.Text;
        set => Label.Text = value ?? string.Empty;
    }

    /// <inheritdoc />
    protected override void ApplyTypeConfig(ObjectInstance config)
    {
        if (JsMarshal.GetString(config, "text") is { } text) Text = text;
    }
}
