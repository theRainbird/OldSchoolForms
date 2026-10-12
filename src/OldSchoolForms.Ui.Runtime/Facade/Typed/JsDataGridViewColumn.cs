using Jint;
using Jint.Native;
using Jint.Native.Object;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// JS facade for a <see cref="DataGridViewColumn"/>. Exposes the column
/// configuration (header, binding, edit type, formatting) to JavaScript.
/// </summary>
public class JsDataGridViewColumn
{
    /// <summary>The JS engine used for callbacks.</summary>
    protected readonly JsEngineHost Host;

    /// <summary>The wrapped column.</summary>
    public DataGridViewColumn Column { get; }

    /// <summary>
    /// Creates a facade around a grid column.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="column">The wrapped column.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="column"/> is <see langword="null"/>.</exception>
    public JsDataGridViewColumn(JsEngineHost host, DataGridViewColumn column)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(column);
        Host = host;
        Column = column;
    }

    /// <summary>The column name.</summary>
    public string Name
    {
        get => Column.Name;
        set => Column.Name = value ?? string.Empty;
    }

    /// <summary>The column header text.</summary>
    public string HeaderText
    {
        get => Column.HeaderText;
        set => Column.HeaderText = value ?? string.Empty;
    }

    /// <summary>The data source property bound to this column.</summary>
    public string DataPropertyName
    {
        get => Column.DataPropertyName;
        set => Column.DataPropertyName = value ?? string.Empty;
    }

    /// <summary>The column width in pixels.</summary>
    public int Width
    {
        get => Column.Width;
        set => Column.Width = value;
    }

    /// <summary>The format string applied to cell values (e.g. <c>N2</c>).</summary>
    public string Format
    {
        get => Column.FormatString ?? string.Empty;
        set => Column.FormatString = string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>Whether the column is read-only.</summary>
    public bool ReadOnly
    {
        get => Column.ReadOnly;
        set => Column.ReadOnly = value;
    }

    /// <summary>
    /// The cell edit control type as a string
    /// (<c>None</c>, <c>TextBox</c>, <c>ComboBox</c>, <c>CheckBox</c>,
    /// <c>DateTimePicker</c>, <c>Button</c>). Invalid values are ignored.
    /// </summary>
    public string EditType
    {
        get => Column.CellEditType.ToString();
        set
        {
            if (Enum.TryParse<DataGridViewColumnEditType>(value, true, out var type))
                Column.CellEditType = type;
        }
    }

    /// <summary>
    /// The horizontal cell alignment as a string
    /// (<c>Left</c>, <c>Center</c>, <c>Right</c>). Invalid values are ignored.
    /// </summary>
    public string TextAlign
    {
        get => Column.TextAlign.ToString();
        set
        {
            if (Enum.TryParse<DataGridViewContentAlignment>(value, true, out var align))
                Column.TextAlign = align;
        }
    }

    /// <summary>Whether the column can be sorted by clicking its header.</summary>
    public bool Sortable
    {
        get => Column.Sortable;
        set => Column.Sortable = value;
    }

    /// <summary>Whether the column width can be resized by the user.</summary>
    public bool Resizable
    {
        get => Column.Resizable;
        set => Column.Resizable = value;
    }

    /// <summary>Whether the column can be used for grouping.</summary>
    public bool Groupable
    {
        get => Column.Groupable;
        set => Column.Groupable = value;
    }

    /// <summary>The caption of the button cell editor.</summary>
    public string ButtonText
    {
        get => Column.ButtonText ?? string.Empty;
        set => Column.ButtonText = string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>
    /// The drop-down items for <c>ComboBox</c> columns as a JS array.
    /// Setting replaces the current item list.
    /// </summary>
    public JsValue Items
    {
        get
        {
            if (Column.Items == null)
                return JsValue.Undefined;
            var values = new JsValue[Column.Items.Count];
            for (var i = 0; i < Column.Items.Count; i++)
                values[i] = JsValue.FromObject(Host.Engine, Column.Items[i]);
            return JsValue.FromObject(Host.Engine, values);
        }
        set
        {
            var items = JsMarshal.ToArray(value);
            if (value is null || value.IsUndefined() || value.IsNull())
            {
                Column.Items = null;
                return;
            }

            Column.Items = new List<object>(items.Length);
            foreach (var item in items)
                Column.Items.Add(item ?? string.Empty);
        }
    }

    /// <summary>
    /// Applies a JS configuration object to this column. Recognized members:
    /// <c>name</c>, <c>headerText</c>, <c>dataPropertyName</c>, <c>width</c>,
    /// <c>format</c>, <c>readOnly</c>, <c>editType</c>, <c>textAlign</c>,
    /// <c>sortable</c>, <c>resizable</c>, <c>groupable</c>, <c>buttonText</c>,
    /// <c>items</c>.
    /// </summary>
    /// <param name="config">The JS configuration object.</param>
    internal void ApplyConfig(ObjectInstance config)
    {
        if (JsMarshal.GetString(config, "name") is { } name) Name = name;
        if (JsMarshal.GetString(config, "headerText") is { } header) HeaderText = header;
        if (JsMarshal.GetString(config, "dataPropertyName") is { } dataProp) DataPropertyName = dataProp;
        if (JsMarshal.GetInt(config, "width") is { } width) Width = width;
        if (JsMarshal.GetString(config, "format") is { } format) Format = format;
        if (JsMarshal.GetBool(config, "readOnly") is { } readOnly) ReadOnly = readOnly;
        if (JsMarshal.GetString(config, "editType") is { } editType) EditType = editType;
        if (JsMarshal.GetString(config, "textAlign") is { } align) TextAlign = align;
        if (JsMarshal.GetBool(config, "sortable") is { } sortable) Sortable = sortable;
        if (JsMarshal.GetBool(config, "resizable") is { } resizable) Resizable = resizable;
        if (JsMarshal.GetBool(config, "groupable") is { } groupable) Groupable = groupable;
        if (JsMarshal.GetString(config, "buttonText") is { } buttonText) ButtonText = buttonText;
        var items = JsMarshal.GetMember(config, "items");
        if (!items.IsUndefined()) Items = items;
    }
}
