using Jint;
using Jint.Native;
using OldSchoolForms.Ui.Reports;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// JS facade for a <see cref="ReportDataSourceCollection"/>. Data sources are
/// added with a name and an optional configuration, e.g.
/// <c>report.dataSources.add('Order', { fields: [{ name: 'Customer', type: 'string' }], data: table })</c>
/// or simply <c>report.dataSources.add('Positions', table)</c> which imports the
/// table schema and data.
/// </summary>
public class JsReportDataSourceCollection
{
    private readonly JsEngineHost _host;
    private readonly ReportDataSourceCollection _sources;

    /// <summary>
    /// Creates the facade over a data source collection.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="sources">The wrapped data source collection.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="sources"/> is <see langword="null"/>.</exception>
    public JsReportDataSourceCollection(JsEngineHost host, ReportDataSourceCollection sources)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(sources);
        _host = host;
        _sources = sources;
    }

    /// <summary>The number of data sources.</summary>
    public int Count => _sources.Count;

    /// <summary>
    /// Gets the data source facade at the given index.
    /// </summary>
    /// <param name="index">The zero-based data source index.</param>
    /// <returns>The data source facade.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside the collection.</exception>
    public JsReportDataSource GetAt(int index) => new(_host, _sources[index]);

    /// <summary>
    /// Gets the data source facade with the given name (case-insensitive).
    /// </summary>
    /// <param name="name">The data source name.</param>
    /// <returns>The data source facade, or <c>null</c> when not found.</returns>
    public JsReportDataSource? Get(string name)
    {
        var source = _sources[name];
        return source != null ? new JsReportDataSource(_host, source) : null;
    }

    /// <summary>
    /// Adds a new data source with the given name and optional configuration.
    /// Recognized config members: <c>fields</c> and <c>data</c>; a bare
    /// <c>JsDataTable</c>/<c>JsDataSet</c> imports schema and data directly.
    /// </summary>
    /// <param name="name">The data source name.</param>
    /// <param name="config">Optional JS configuration value.</param>
    /// <returns>The new data source facade.</returns>
    public JsReportDataSource Add(string name, JsValue? config = null)
    {
        var source = new ReportDataSource(name ?? string.Empty);
        JsReportDataSource.ApplyConfig(source, config);
        _sources.Add(source);
        return new JsReportDataSource(_host, source);
    }

    /// <summary>
    /// Removes the data source at the given index.
    /// </summary>
    /// <param name="index">The zero-based data source index.</param>
    /// <returns><see langword="true"/> when a data source was removed.</returns>
    public bool RemoveAt(int index)
    {
        if (index < 0 || index >= _sources.Count)
            return false;
        _sources.RemoveAt(index);
        return true;
    }

    /// <summary>
    /// Removes the data source with the given name (case-insensitive).
    /// </summary>
    /// <param name="name">The data source name to remove.</param>
    /// <returns><see langword="true"/> when the data source was found and removed.</returns>
    public bool Remove(string name) => _sources.Remove(name);

    /// <summary>Removes all data sources.</summary>
    public void Clear() => _sources.Clear();
}
