using Jint;
using Jint.Native;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// JS facade for a <see cref="KanbanBoardCard"/>. Exposes the card's display
/// values and state; the underlying data item is reachable through
/// <c>dataItem</c>.
/// </summary>
public class JsKanbanCard
{
    private readonly JsEngineHost _host;

    /// <summary>The wrapped card.</summary>
    public KanbanBoardCard Card { get; }

    /// <summary>
    /// Creates a facade around a kanban card.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="card">The wrapped card.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="card"/> is <see langword="null"/>.</exception>
    public JsKanbanCard(JsEngineHost host, KanbanBoardCard card)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(card);
        _host = host;
        Card = card;
    }

    /// <summary>The card's display text.</summary>
    public string DisplayText => Card.DisplayText;

    /// <summary>The card's description text.</summary>
    public string DescriptionText => Card.DescriptionText;

    /// <summary>The card's category text.</summary>
    public string CategoryText => Card.CategoryText;

    /// <summary>The card's assigned-to text.</summary>
    public string AssignedToText => Card.AssignedToText;

    /// <summary>The card's tags as a JS array.</summary>
    public JsValue Tags
    {
        get
        {
            var values = new JsValue[Card.Tags.Count];
            for (var i = 0; i < Card.Tags.Count; i++)
                values[i] = JsValue.FromObject(_host.Engine, Card.Tags[i]);
            return JsValue.FromObject(_host.Engine, values);
        }
    }

    /// <summary>The state value that determines the card's column.</summary>
    public JsValue StateValue
    {
        get => JsValue.FromObject(_host.Engine, Card.StateValue);
        set => Card.StateValue = value?.ToObject();
    }

    /// <summary>The card's value from the value member mapping.</summary>
    public JsValue Value
    {
        get => JsValue.FromObject(_host.Engine, Card.Value);
        set => Card.Value = value?.ToObject();
    }

    /// <summary>The underlying data item from the data source.</summary>
    public JsValue DataItem => JsValue.FromObject(_host.Engine, Card.DataItem);
}
