using Jint;
using Jint.Native;
using Jint.Native.Object;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// JS facade for the fields of a <see cref="PivotTable"/>. Fields are added via
/// configuration objects, e.g.
/// <c>pivot.fields.add({ name: 'Region', dataPropertyName: 'region', usage: 'RowField' })</c>.
/// </summary>
public class JsPivotFieldCollection
{
    private readonly JsEngineHost _host;
    private readonly PivotTable _pivot;

    /// <summary>
    /// Creates the facade over the pivot table's field collection.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="pivot">The owning pivot table.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="pivot"/> is <see langword="null"/>.</exception>
    public JsPivotFieldCollection(JsEngineHost host, PivotTable pivot)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(pivot);
        _host = host;
        _pivot = pivot;
    }

    /// <summary>The number of fields.</summary>
    public int Count => _pivot.Fields.Count;

    /// <summary>
    /// Gets the field facade at the given index.
    /// </summary>
    /// <param name="index">The zero-based field index.</param>
    /// <returns>The field facade.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside the collection.</exception>
    public JsPivotField GetAt(int index) => new(_host, _pivot.Fields[index]);

    /// <summary>
    /// Adds a field from a JS configuration object.
    /// </summary>
    /// <param name="config">The configuration object (see <see cref="JsPivotField.ApplyConfig"/>).</param>
    /// <returns>The new field facade.</returns>
    /// <exception cref="ArgumentException">The config is not an object.</exception>
    public JsPivotField Add(JsValue config)
    {
        if (config is not ObjectInstance obj)
            throw new ArgumentException("Field configuration must be an object.", nameof(config));
        var field = new PivotTableField();
        var facade = new JsPivotField(_host, field);
        facade.ApplyConfig(obj);
        _pivot.Fields.Add(field);
        return facade;
    }

    /// <summary>
    /// Removes the field at the given index.
    /// </summary>
    /// <param name="index">The zero-based field index.</param>
    /// <returns><see langword="true"/> when a field was removed.</returns>
    public bool RemoveAt(int index)
    {
        if (index < 0 || index >= _pivot.Fields.Count)
            return false;
        _pivot.Fields.RemoveAt(index);
        return true;
    }

    /// <summary>Removes all fields.</summary>
    public void Clear() => _pivot.Fields.Clear();
}
