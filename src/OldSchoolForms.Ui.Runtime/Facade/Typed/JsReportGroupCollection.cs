using Jint;
using Jint.Native;
using Jint.Native.Object;
using OldSchoolForms.Ui.Reports;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// JS facade for a <see cref="ReportGroupCollection"/>. Groups are added with a
/// grouping field and an optional configuration object, e.g.
/// <c>report.groups.add('Category', { name: 'By Category', keepTogether: true })</c>.
/// </summary>
public class JsReportGroupCollection
{
    private readonly JsEngineHost _host;
    private readonly ReportGroupCollection _groups;

    /// <summary>
    /// Creates the facade over a group collection.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="groups">The wrapped group collection.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="groups"/> is <see langword="null"/>.</exception>
    public JsReportGroupCollection(JsEngineHost host, ReportGroupCollection groups)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(groups);
        _host = host;
        _groups = groups;
    }

    /// <summary>The number of groups.</summary>
    public int Count => _groups.Count;

    /// <summary>
    /// Gets the group facade at the given index.
    /// </summary>
    /// <param name="index">The zero-based group index.</param>
    /// <returns>The group facade.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside the collection.</exception>
    public JsReportGroup GetAt(int index) => new(_host, _groups[index]);

    /// <summary>
    /// Adds a group for the given field with an optional configuration object.
    /// Recognized members: <c>name</c>, <c>keepTogether</c>.
    /// </summary>
    /// <param name="field">The data field to group by.</param>
    /// <param name="config">Optional JS configuration object.</param>
    /// <returns>The new group facade.</returns>
    public JsReportGroup Add(string field, JsValue? config = null)
    {
        var group = new ReportGroup(field ?? string.Empty);
        if (config is ObjectInstance obj)
        {
            if (JsMarshal.GetString(obj, "name") is { } name) group.Name = name;
            if (JsMarshal.GetBool(obj, "keepTogether") is { } keepTogether) group.KeepTogether = keepTogether;
        }
        _groups.Add(group);
        return new JsReportGroup(_host, group);
    }

    /// <summary>
    /// Removes the group at the given index.
    /// </summary>
    /// <param name="index">The zero-based group index.</param>
    /// <returns><see langword="true"/> when a group was removed.</returns>
    public bool RemoveAt(int index)
    {
        if (index < 0 || index >= _groups.Count)
            return false;
        _groups.RemoveAt(index);
        return true;
    }

    /// <summary>Removes all groups.</summary>
    public void Clear() => _groups.Clear();
}
