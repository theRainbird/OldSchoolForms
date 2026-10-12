using Jint;
using Jint.Native;
using Jint.Native.Object;
using OldSchoolForms.Ui.Reports;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// JS facade for a <see cref="ReportGroup"/>. Groups define a grouping field
/// and carry a header and footer band plus optional nested groups.
/// </summary>
public class JsReportGroup
{
    private readonly JsEngineHost _host;
    private readonly JsReportBand _header;
    private readonly JsReportBand _footer;
    private readonly JsReportGroupCollection _groups;

    /// <summary>The wrapped group.</summary>
    public ReportGroup Group { get; }

    /// <summary>
    /// Creates a facade around a report group.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="group">The wrapped group.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="group"/> is <see langword="null"/>.</exception>
    public JsReportGroup(JsEngineHost host, ReportGroup group)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(group);
        _host = host;
        Group = group;
        _header = new JsReportBand(host, group.Header);
        _footer = new JsReportBand(host, group.Footer);
        _groups = new JsReportGroupCollection(host, group.Groups);
    }

    /// <summary>The group name.</summary>
    public string Name
    {
        get => Group.Name;
        set => Group.Name = value ?? string.Empty;
    }

    /// <summary>The data field to group by.</summary>
    public string GroupField
    {
        get => Group.GroupField;
        set => Group.GroupField = value ?? string.Empty;
    }

    /// <summary>Whether the group header, detail rows and footer are kept together.</summary>
    public bool KeepTogether
    {
        get => Group.KeepTogether;
        set => Group.KeepTogether = value;
    }

    /// <summary>The group header band facade.</summary>
    public JsReportBand Header => _header;

    /// <summary>The group footer band facade.</summary>
    public JsReportBand Footer => _footer;

    /// <summary>The nested group collection facade.</summary>
    public JsReportGroupCollection Groups => _groups;
}
