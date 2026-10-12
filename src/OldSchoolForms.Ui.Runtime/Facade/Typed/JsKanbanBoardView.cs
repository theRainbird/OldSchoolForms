using Jint;
using Jint.Native;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// JS facade for a <see cref="KanbanBoardView"/>. Provides member mappings,
/// column management and card events. The <c>cardmoving</c> event is cancelable:
/// setting <c>e.handled = true</c> prevents the move.
/// </summary>
public class JsKanbanBoardView : JsControl
{
    private readonly JsKanbanColumnCollection _columns;

    /// <summary>The wrapped kanban board.</summary>
    public KanbanBoardView Board { get; }

    /// <summary>
    /// Creates a facade around a kanban board.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="board">The wrapped kanban board.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="board"/> is <see langword="null"/>.</exception>
    public JsKanbanBoardView(JsEngineHost host, KanbanBoardView board)
        : base(host, board)
    {
        ArgumentNullException.ThrowIfNull(board);
        Board = board;
        _columns = new JsKanbanColumnCollection(host, board);
    }

    /// <summary>The board column collection facade.</summary>
    public JsKanbanColumnCollection Columns => _columns;

    /// <summary>Data member whose value is shown as the card title.</summary>
    public string DisplayMember
    {
        get => Board.DisplayMember;
        set => Board.DisplayMember = value ?? string.Empty;
    }

    /// <summary>Data member shown as the card description.</summary>
    public string DescriptionMember
    {
        get => Board.DescriptionMember;
        set => Board.DescriptionMember = value ?? string.Empty;
    }

    /// <summary>Data member mapped to the card value.</summary>
    public string ValueMember
    {
        get => Board.ValueMember;
        set => Board.ValueMember = value ?? string.Empty;
    }

    /// <summary>Data member that determines the card's column.</summary>
    public string StateMember
    {
        get => Board.StateMember;
        set => Board.StateMember = value ?? string.Empty;
    }

    /// <summary>Data member mapped to the card tags.</summary>
    public string TagMember
    {
        get => Board.TagMember;
        set => Board.TagMember = value ?? string.Empty;
    }

    /// <summary>Data member mapped to the card category.</summary>
    public string CategoryMember
    {
        get => Board.CategoryMember;
        set => Board.CategoryMember = value ?? string.Empty;
    }

    /// <summary>Data member mapped to the assigned-to text.</summary>
    public string AssignedToMember
    {
        get => Board.AssignedToMember;
        set => Board.AssignedToMember = value ?? string.Empty;
    }

    /// <summary>Whether cards can be dragged between columns.</summary>
    public bool AllowDrop
    {
        get => Board.AllowDrop;
        set => Board.AllowDrop = value;
    }

    /// <summary>Whether cards can be reordered within a column.</summary>
    public bool AllowReorder
    {
        get => Board.AllowReorder;
        set => Board.AllowReorder = value;
    }

    /// <summary>
    /// The board's data source. Accepts a <c>JsBindingSource</c>, a
    /// <c>JsDataTable</c>, an array of plain JS objects, or any CLR
    /// <see cref="IEnumerable{T}"/>.
    /// </summary>
    public JsValue DataSource
    {
        get => JsValue.FromObject(Host.Engine, Board.DataSource);
        set => Board.DataSource = JsMarshal.ToDataSource(value);
    }

    /// <inheritdoc />
    protected override Delegate? WireCustom(string normalized, JsValue callback)
    {
        switch (normalized)
        {
            case "cardclick":
                EventHandler<KanbanBoardCardEventArgs> click = (s, e) =>
                {
                    var jsEvent = BuildEvent("cardclick", e);
                    jsEvent.Payload = new JsKanbanCard(Host, e.Card);
                    RaiseEvent("cardclick", callback, jsEvent, e);
                };
                Board.CardClick += click;
                return click;
            case "carddoubleclick":
                EventHandler<KanbanBoardCardEventArgs> doubleClick = (s, e) =>
                {
                    var jsEvent = BuildEvent("carddoubleclick", e);
                    jsEvent.Payload = new JsKanbanCard(Host, e.Card);
                    RaiseEvent("carddoubleclick", callback, jsEvent, e);
                };
                Board.CardDoubleClick += doubleClick;
                return doubleClick;
            case "cardgotfocus":
                EventHandler<KanbanBoardCardEventArgs> gotFocus = (s, e) =>
                {
                    var jsEvent = BuildEvent("cardgotfocus", e);
                    jsEvent.Payload = new JsKanbanCard(Host, e.Card);
                    RaiseEvent("cardgotfocus", callback, jsEvent, e);
                };
                Board.CardGotFocus += gotFocus;
                return gotFocus;
            case "cardmoving":
                EventHandler<KanbanBoardCardMovingEventArgs> moving = (s, e) =>
                {
                    var jsEvent = BuildEvent("cardmoving", e);
                    jsEvent.Payload = BuildMovePayload(e);
                    RaiseEvent("cardmoving", callback, jsEvent, e);
                };
                Board.CardMoving += moving;
                return moving;
            case "cardmoved":
                EventHandler<KanbanBoardCardMovingEventArgs> moved = (s, e) =>
                {
                    var jsEvent = BuildEvent("cardmoved", e);
                    jsEvent.Payload = BuildMovePayload(e);
                    RaiseEvent("cardmoved", callback, jsEvent, e);
                };
                Board.CardMoved += moved;
                return moved;
            default:
                return null;
        }
    }

    /// <inheritdoc />
    protected override bool UnwireCustom(string normalized, Delegate handler)
    {
        switch (normalized)
        {
            case "cardclick":
                Board.CardClick -= (EventHandler<KanbanBoardCardEventArgs>)handler;
                return true;
            case "carddoubleclick":
                Board.CardDoubleClick -= (EventHandler<KanbanBoardCardEventArgs>)handler;
                return true;
            case "cardgotfocus":
                Board.CardGotFocus -= (EventHandler<KanbanBoardCardEventArgs>)handler;
                return true;
            case "cardmoving":
                Board.CardMoving -= (EventHandler<KanbanBoardCardMovingEventArgs>)handler;
                return true;
            case "cardmoved":
                Board.CardMoved -= (EventHandler<KanbanBoardCardMovingEventArgs>)handler;
                return true;
            default:
                return false;
        }
    }

    private Dictionary<string, object?> BuildMovePayload(KanbanBoardCardMovingEventArgs e)
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["card"] = new JsKanbanCard(Host, e.Card),
            ["fromColumn"] = e.FromColumn?.Name,
            ["toColumn"] = e.ToColumn?.Name,
            ["newIndex"] = e.NewIndex
        };
}
