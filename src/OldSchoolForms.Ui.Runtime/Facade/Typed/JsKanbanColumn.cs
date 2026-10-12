using Jint;
using Jint.Native;
using Jint.Native.Object;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// JS facade for a <see cref="KanbanBoardColumn"/>.
/// </summary>
public class JsKanbanColumn
{
    private readonly JsEngineHost _host;

    /// <summary>The wrapped column.</summary>
    public KanbanBoardColumn Column { get; }

    /// <summary>
    /// Creates a facade around a kanban column.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="column">The wrapped column.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="column"/> is <see langword="null"/>.</exception>
    public JsKanbanColumn(JsEngineHost host, KanbanBoardColumn column)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(column);
        _host = host;
        Column = column;
    }

    /// <summary>The column caption.</summary>
    public string Name
    {
        get => Column.Name;
        set => Column.Name = value ?? string.Empty;
    }

    /// <summary>The state value that maps items to this column.</summary>
    public JsValue StateValue
    {
        get => JsValue.FromObject(_host.Engine, Column.StateValue);
        set => Column.StateValue = value?.ToObject();
    }

    /// <summary>The column background color as a hex string.</summary>
    public JsValue BackColor
    {
        get => JsValue.FromObject(_host.Engine, JsMarshal.FromColor(Column.BackColor));
        set => Column.BackColor = JsMarshal.ToColor(value, Column.BackColor);
    }

    /// <summary>The column header color as a hex string.</summary>
    public JsValue HeaderColor
    {
        get => JsValue.FromObject(_host.Engine, JsMarshal.FromColor(Column.HeaderColor));
        set => Column.HeaderColor = JsMarshal.ToColor(value, Column.HeaderColor);
    }

    /// <summary>The column width in pixels.</summary>
    public int Width
    {
        get => Column.Width;
        set => Column.Width = value;
    }

    /// <summary>The number of cards currently in this column.</summary>
    public int CardCount => Column.CardCount;

    /// <summary>
    /// Applies a JS configuration object to this column. Recognized members:
    /// <c>name</c>, <c>stateValue</c>, <c>backColor</c>, <c>headerColor</c>, <c>width</c>.
    /// </summary>
    /// <param name="config">The JS configuration object.</param>
    internal void ApplyConfig(ObjectInstance config)
    {
        if (JsMarshal.GetString(config, "name") is { } name) Name = name;
        var state = JsMarshal.GetMember(config, "stateValue");
        if (!state.IsUndefined()) StateValue = state;
        var back = JsMarshal.GetMember(config, "backColor");
        if (!back.IsUndefined()) BackColor = back;
        var header = JsMarshal.GetMember(config, "headerColor");
        if (!header.IsUndefined()) HeaderColor = header;
        if (JsMarshal.GetInt(config, "width") is { } width) Width = width;
    }
}
