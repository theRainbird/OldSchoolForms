using Jint;
using Jint.Native;
using Jint.Native.Object;
using OldSchoolForms.Ui.Reports;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// JS facade for the fields of a <see cref="ReportCrossTab"/>.
/// </summary>
public class JsCrossTabFieldCollection
{
    private readonly ReportCrossTab _crossTab;

    /// <summary>
    /// Creates the facade over a cross-tab control's field collection.
    /// </summary>
    /// <param name="crossTab">The owning cross-tab control.</param>
    /// <exception cref="ArgumentNullException"><paramref name="crossTab"/> is <see langword="null"/>.</exception>
    public JsCrossTabFieldCollection(ReportCrossTab crossTab)
    {
        ArgumentNullException.ThrowIfNull(crossTab);
        _crossTab = crossTab;
    }

    /// <summary>The number of fields.</summary>
    public int Count => _crossTab.Fields.Count;

    /// <summary>
    /// Gets the field facade at the given index.
    /// </summary>
    /// <param name="index">The zero-based field index.</param>
    /// <returns>The field facade.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside the collection.</exception>
    public JsCrossTabField GetAt(int index) => new(_crossTab.Fields[index]);

    /// <summary>
    /// Adds a field from a JS configuration object. Recognized members:
    /// <c>dataField</c>, <c>headerText</c>, <c>usage</c>, <c>aggregation</c>, <c>format</c>.
    /// </summary>
    /// <param name="config">The JS configuration object.</param>
    /// <returns>The new field facade.</returns>
    /// <exception cref="ArgumentException">The config is not an object.</exception>
    public JsCrossTabField Add(JsValue config)
    {
        if (config is not ObjectInstance obj)
            throw new ArgumentException("Cross-tab field configuration must be an object.", nameof(config));
        var field = new CrossTabField();
        var facade = new JsCrossTabField(field);
        facade.ApplyConfig(obj);
        _crossTab.Fields.Add(field);
        return facade;
    }

    /// <summary>
    /// Removes the field at the given index.
    /// </summary>
    /// <param name="index">The zero-based field index.</param>
    /// <returns><see langword="true"/> when a field was removed.</returns>
    public bool RemoveAt(int index)
    {
        if (index < 0 || index >= _crossTab.Fields.Count)
            return false;
        _crossTab.Fields.RemoveAt(index);
        return true;
    }

    /// <summary>Removes all fields.</summary>
    public void Clear() => _crossTab.Fields.Clear();
}
