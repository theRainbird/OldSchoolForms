using Jint;
using Jint.Native;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// JS facade for a <see cref="HtmlBox"/>. The <c>linkclick</c> event is
/// cancelable: setting <c>e.handled = true</c> prevents the default link
/// behavior; <c>e.value</c> carries the clicked URL.
/// </summary>
public class JsHtmlBox : JsControl
{
    /// <summary>The wrapped HTML box.</summary>
    public HtmlBox Box { get; }

    /// <summary>
    /// Creates a facade around an HTML box.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="box">The wrapped HTML box.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="box"/> is <see langword="null"/>.</exception>
    public JsHtmlBox(JsEngineHost host, HtmlBox box)
        : base(host, box)
    {
        ArgumentNullException.ThrowIfNull(box);
        Box = box;
    }

    /// <summary>The HTML content.</summary>
    public string Html
    {
        get => Box.Html;
        set => Box.Html = value ?? string.Empty;
    }

    /// <summary>Whether editing is disabled.</summary>
    public bool ReadOnly
    {
        get => Box.ReadOnly;
        set => Box.ReadOnly = value;
    }

    /// <summary>Whether the formatting toolbar is shown.</summary>
    public bool ShowToolbar
    {
        get => Box.ShowToolbar;
        set => Box.ShowToolbar = value;
    }

    /// <summary>
    /// The link behavior as a string (<c>None</c>, <c>RaiseEvent</c>,
    /// <c>OpenInBrowser</c>). Invalid values are ignored.
    /// </summary>
    public string LinkBehavior
    {
        get => Box.LinkBehavior.ToString();
        set
        {
            if (Enum.TryParse<LinkBehavior>(value, true, out var behavior))
                Box.LinkBehavior = behavior;
        }
    }

    /// <summary>
    /// Applies a named formatting command (e.g. <c>bold</c>, <c>italic</c>).
    /// </summary>
    /// <param name="formatType">The formatting command name.</param>
    public void ApplyFormat(string formatType) => Box.ApplyFormat(formatType);

    /// <summary>
    /// Applies a formatting command with a value (e.g. <c>fontName</c> + <c>Arial</c>).
    /// </summary>
    /// <param name="command">The formatting command.</param>
    /// <param name="value">The command value.</param>
    public void ApplyFormat(string command, string value) => Box.ApplyFormat(command, value);

    /// <summary>
    /// Inserts a table at the current cursor position.
    /// </summary>
    /// <param name="rows">The number of rows.</param>
    /// <param name="columns">The number of columns.</param>
    public void InsertTable(int rows, int columns) => Box.InsertTable(rows, columns);

    /// <summary>Adds a row to the current table.</summary>
    public void AddTableRow() => Box.AddTableRow();

    /// <summary>Removes the current table row.</summary>
    public void RemoveTableRow() => Box.RemoveTableRow();

    /// <summary>Adds a column to the current table.</summary>
    public void AddTableColumn() => Box.AddTableColumn();

    /// <summary>Removes the current table column.</summary>
    public void RemoveTableColumn() => Box.RemoveTableColumn();

    /// <summary>Whether the selection is bold.</summary>
    public bool IsBold => Box.IsBold;

    /// <summary>Whether the selection is italic.</summary>
    public bool IsItalic => Box.IsItalic;

    /// <summary>Whether the selection is underlined.</summary>
    public bool IsUnderline => Box.IsUnderline;

    /// <summary>Whether the selection is struck out.</summary>
    public bool IsStrikeout => Box.IsStrikeout;

    /// <summary>The font name of the current selection.</summary>
    public string FontName => Box.FontName;

    /// <summary>The font size of the current selection.</summary>
    public float FontSizeValue => Box.FontSizeValue;

    /// <inheritdoc />
    protected override Delegate? WireCustom(string normalized, JsValue callback)
    {
        switch (normalized)
        {
            case "contentchanged":
                EventHandler contentChanged = (s, e) => Raise("contentchanged", callback, e);
                Box.ContentChanged += contentChanged;
                return contentChanged;
            case "linkclick":
                EventHandler<HtmlLinkEventArgs> linkClick = (s, e) =>
                {
                    var jsEvent = BuildEvent("linkclick", e);
                    jsEvent.Value = e.Url;
                    RaiseEvent("linkclick", callback, jsEvent, e);
                };
                Box.LinkClick += linkClick;
                return linkClick;
            case "parseerror":
                EventHandler<HtmlErrorEventArgs> parseError = (s, e) =>
                {
                    var jsEvent = BuildEvent("parseerror", e);
                    var values = new JsValue[e.Errors.Count];
                    for (var i = 0; i < e.Errors.Count; i++)
                        values[i] = JsValue.FromObject(Host.Engine, e.Errors[i]);
                    jsEvent.Payload = JsValue.FromObject(Host.Engine, values);
                    RaiseEvent("parseerror", callback, jsEvent, e);
                };
                Box.ParseError += parseError;
                return parseError;
            default:
                return null;
        }
    }

    /// <inheritdoc />
    protected override bool UnwireCustom(string normalized, Delegate handler)
    {
        switch (normalized)
        {
            case "contentchanged":
                Box.ContentChanged -= (EventHandler)handler;
                return true;
            case "linkclick":
                Box.LinkClick -= (EventHandler<HtmlLinkEventArgs>)handler;
                return true;
            case "parseerror":
                Box.ParseError -= (EventHandler<HtmlErrorEventArgs>)handler;
                return true;
            default:
                return false;
        }
    }
}
