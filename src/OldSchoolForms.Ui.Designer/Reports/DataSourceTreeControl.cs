using System;
using System.Collections.Generic;
using System.Linq;
using OldSchoolForms.Ui.Controls;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Designer.Services;
using OldSchoolForms.Ui.Reports;
using OldSchoolForms.Ui.Theming;
using Graphics = OldSchoolForms.Ui.Rendering.Graphics;

namespace OldSchoolForms.Ui.Designer.Reports;

/// <summary>
/// Sidebar control that displays and edits the data source definitions of the
/// report currently open in the designer. Sources appear as root nodes and their
/// schema fields as child nodes. Add/remove operations are recorded on the
/// designer's undo stack and refresh the WYSIWYG preview.
/// </summary>
public sealed class DataSourceTreeControl : Panel
{
    private readonly Label _header = null!;
    private readonly TreeView _tree = null!;
    private readonly ToolStrip _toolbar = null!;
    private readonly ToolStripButton _addSourceButton = null!;
    private readonly ToolStripButton _renameSourceButton = null!;
    private readonly ToolStripButton _removeSourceButton = null!;
    private readonly ToolStripButton _addFieldButton = null!;
    private readonly ToolStripButton _renameFieldButton = null!;
    private readonly ToolStripButton _removeFieldButton = null!;

    private DesignReport? _root;
    private Action? _onModified;
    private int _sourceImageIndex = -1;
    private int _fieldImageIndex = -1;

    /// <summary>
    /// Associates a field node with its owning source and field.
    /// </summary>
    private sealed class FieldTag
    {
        public FieldTag(ReportDataSource source, ReportDataSourceField field)
        {
            Source = source;
            Field = field;
        }

        public ReportDataSource Source { get; }
        public ReportDataSourceField Field { get; }
    }

    /// <summary>
    /// Raised after a data source mutation is applied, so the host can mark the
    /// document dirty.
    /// </summary>
    public event EventHandler? Modified;

    /// <summary>
    /// Initializes the data source tree control.
    /// </summary>
    public DataSourceTreeControl()
    {
        BackColor = ThemeManager.CurrentTheme.ControlBackground;
        Padding = new Padding(0);

        _header = new Label
        {
            Text = LangRes.GetString("DataSourceTree_Title"),
            Dock = DockStyle.Top,
            Height = 22,
            BackColor = Color.Transparent
        };
        Controls.Add(_header);

        _toolbar = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden
        };
        _addSourceButton = CreateIconButton("addSourceButton", "datasource-add", LangRes.GetString("DataSourceTree_TipAddSource"), () => AddSource());
        _renameSourceButton = CreateIconButton("renameSourceButton", "datasource-rename", LangRes.GetString("DataSourceTree_TipRenameSource"), () => RenameSource());
        _removeSourceButton = CreateIconButton("removeSourceButton", "datasource-delete", LangRes.GetString("DataSourceTree_TipRemoveSource"), () => RemoveSource());
        _addFieldButton = CreateIconButton("addFieldButton", "field-add", LangRes.GetString("DataSourceTree_TipAddField"), () => AddField());
        _renameFieldButton = CreateIconButton("renameFieldButton", "field-rename", LangRes.GetString("DataSourceTree_TipRenameField"), () => RenameField());
        _removeFieldButton = CreateIconButton("removeFieldButton", "field-delete", LangRes.GetString("DataSourceTree_TipRemoveField"), () => RemoveField());
        _toolbar.Items.Add(_addSourceButton);
        _toolbar.Items.Add(_renameSourceButton);
        _toolbar.Items.Add(_removeSourceButton);
        _toolbar.Items.Add(new ToolStripSeparator());
        _toolbar.Items.Add(_addFieldButton);
        _toolbar.Items.Add(_renameFieldButton);
        _toolbar.Items.Add(_removeFieldButton);
        Controls.Add(_toolbar);

        _tree = new TreeView
        {
            Dock = DockStyle.Fill
        };
        var treeImages = new ImageList();
        var sourceIcon = SvgImage.FromSvgResource("OldSchoolForms.Ui.Resources.Icons.SvgRepo.datasource.svg", 16);
        var fieldIcon = SvgImage.FromSvgResource("OldSchoolForms.Ui.Resources.Icons.SvgRepo.field.svg", 16);
        if (sourceIcon != null) _sourceImageIndex = treeImages.Add(sourceIcon);
        if (fieldIcon != null) _fieldImageIndex = treeImages.Add(fieldIcon);
        _tree.ImageList = treeImages.Count > 0 ? treeImages : null;
        _tree.AfterSelect += (_, _) => UpdateButtonStates();
        Controls.Add(_tree);

        UpdateButtonStates();
    }

    /// <summary>
    /// Creates an icon-only toolstrip button that loads its glyph from an embedded
    /// SVG resource in the SvgRepo folder and shows the supplied tooltip on hover.
    /// </summary>
    /// <param name="name">The control name of the button.</param>
    /// <param name="iconFile">The SvgRepo file name without extension.</param>
    /// <param name="toolTip">The tooltip text shown when the mouse hovers the button.</param>
    /// <param name="onClick">The action invoked when the button is clicked.</param>
    /// <returns>A configured <see cref="ToolStripButton"/>.</returns>
    private static ToolStripButton CreateIconButton(string name, string iconFile, string toolTip, Action onClick)
    {
        var button = new ToolStripButton
        {
            Name = name,
            DisplayStyle = ToolStripItemDisplayStyle.Image,
            Image = SvgImage.FromSvgResource("OldSchoolForms.Ui.Resources.Icons.SvgRepo." + iconFile + ".svg", 16),
            ToolTipText = toolTip
        };
        button.Click += (_, _) => onClick();
        return button;
    }

    /// <summary>
    /// Binds the control to the report root currently edited on the designer
    /// surface and subscribes to undo state changes so the tree stays in sync.
    /// </summary>
    /// <param name="root">The design report root, or <see langword="null"/> to clear.</param>
    /// <param name="surface">The design surface providing the undo service.</param>
    /// <param name="onModified">Callback invoked after a mutation, to mark the document dirty.</param>
    public void BindReport(DesignReport? root, DesignSurface? surface, Action? onModified = null)
    {
        if (_root != null && _undoService != null)
            _undoService.StateChanged -= OnUndoStateChanged;

        _root = root;
        _undoService = surface?.Undo;
        _onModified = onModified;

        if (_root != null && _undoService != null)
            _undoService.StateChanged += OnUndoStateChanged;

        Rebuild();
    }

    private UndoService? _undoService;

    private void OnUndoStateChanged(object? sender, EventArgs e) => Rebuild();

    /// <summary>
    /// Rebuilds the tree from the bound report's data sources, preserving the
    /// expansion state of source nodes.
    /// </summary>
    public void Rebuild()
    {
        var expandedSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (TreeNode node in _tree.Nodes)
        {
            if (node.Tag is ReportDataSource s && node.IsExpanded)
                expandedSources.Add(s.Name);
        }

        _tree.Nodes.Clear();
        if (_root != null)
        {
            foreach (var source in _root.Report.DataSources)
            {
                var sourceNode = new TreeNode(FormatSourceLabel(source)) { Tag = source };
                if (_sourceImageIndex >= 0) sourceNode.ImageIndex = _sourceImageIndex;
                sourceNode.SetExpanded(expandedSources.Count == 0 || expandedSources.Contains(source.Name));
                foreach (var field in source.Fields)
                {
                    var fieldNode = new TreeNode(FormatFieldLabel(field)) { Tag = new FieldTag(source, field) };
                    if (_fieldImageIndex >= 0) fieldNode.ImageIndex = _fieldImageIndex;
                    sourceNode.Add(fieldNode);
                }
                _tree.Nodes.Add(sourceNode);
            }
        }

        UpdateButtonStates();
        _tree.Invalidate();
    }

    private static string FormatSourceLabel(ReportDataSource source)
    {
        var name = string.IsNullOrWhiteSpace(source.Name) ? LangRes.GetString("DataSourceTree_Unnamed") : source.Name;
        return name + " (" + source.Fields.Count + ")";
    }

    private static string FormatFieldLabel(ReportDataSourceField field)
    {
        var name = string.IsNullOrWhiteSpace(field.Name) ? LangRes.GetString("DataSourceTree_Unnamed") : field.Name;
        return name + " · " + field.DataType.ToJsonName();
    }

    private (ReportDataSource? Source, ReportDataSourceField? Field) GetSelection()
    {
        var node = _tree.SelectedNode;
        if (node?.Tag is ReportDataSource source)
            return (source, null);
        if (node?.Tag is FieldTag tag)
            return (tag.Source, tag.Field);
        return (null, null);
    }

    private void UpdateButtonStates()
    {
        var (source, field) = GetSelection();
        var hasReport = _root != null;
        _addSourceButton.Enabled = hasReport;
        _renameSourceButton.Enabled = hasReport && source != null;
        _removeSourceButton.Enabled = hasReport && source != null;
        _addFieldButton.Enabled = hasReport && source != null;
        _renameFieldButton.Enabled = hasReport && field != null;
        _removeFieldButton.Enabled = hasReport && field != null;
    }

    private void AddSource()
    {
        if (_root == null || _undoService == null) return;
        var report = _root.Report;

        var suggested = LangRes.GetString("DataSourceTree_SourcePrefix") + (report.DataSources.Count + 1);
        var name = TextInputDialog.ShowDialog(
            LangRes.GetString("DataSourceTree_AddSourceTitle"),
            LangRes.GetString("DataSourceTree_SourceNamePrompt"),
            suggested,
            FindForm());
        if (string.IsNullOrWhiteSpace(name)) return;
        if (report.DataSources.Contains(name)) return;

        var source = new ReportDataSource(name);
        report.DataSources.Add(source);
        _undoService.PushAction(
            () => { report.DataSources.Remove(source); _root.RefreshGeometry(); Rebuild(); },
            () => { report.DataSources.Add(source); _root.RefreshGeometry(); Rebuild(); });

        Rebuild();
        SelectSource(source);
        NotifyModified();
    }

    private void RemoveSource()
    {
        if (_root == null || _undoService == null) return;
        var report = _root.Report;
        var (source, _) = GetSelection();
        if (source == null) return;

        var index = IndexOfSource(report, source);
        // Capture bands bound to this source so their binding can be restored on undo.
        var affected = new List<(ReportBand Band, string? OldName)>();
        foreach (var band in _root.GetBands())
        {
            if (string.Equals(band.DataSourceName, source.Name, StringComparison.OrdinalIgnoreCase))
                affected.Add((band, band.DataSourceName));
        }

        report.DataSources.Remove(source);
        foreach (var (band, _) in affected)
            band.DataSourceName = null;

        _undoService.PushAction(
            () =>
            {
                if (index >= 0 && index <= report.DataSources.Count)
                    report.DataSources.Insert(index, source);
                else
                    report.DataSources.Add(source);
                foreach (var (band, oldName) in affected)
                    band.DataSourceName = oldName;
                _root.RefreshGeometry();
                Rebuild();
            },
            () =>
            {
                report.DataSources.Remove(source);
                foreach (var (band, _) in affected)
                    band.DataSourceName = null;
                _root.RefreshGeometry();
                Rebuild();
            });

        Rebuild();
        NotifyModified();
    }

    private void RenameSource()
    {
        if (_root == null || _undoService == null) return;
        var report = _root.Report;
        var (source, _) = GetSelection();
        if (source == null) return;

        var oldName = source.Name;
        var newName = TextInputDialog.ShowDialog(
            LangRes.GetString("DataSourceTree_RenameSourceTitle"),
            LangRes.GetString("DataSourceTree_SourceNamePrompt"),
            oldName,
            FindForm());
        if (string.IsNullOrWhiteSpace(newName)) return;
        if (string.Equals(newName, oldName, StringComparison.Ordinal)) return;
        if (report.DataSources.Contains(newName)) return;

        // Capture bands bound to the old name so their binding follows the rename and can be restored on undo.
        var affected = new List<(ReportBand Band, string? OldName)>();
        foreach (var band in _root.GetBands())
        {
            if (string.Equals(band.DataSourceName, oldName, StringComparison.OrdinalIgnoreCase))
                affected.Add((band, band.DataSourceName));
        }

        source.Name = newName;
        foreach (var (band, _) in affected)
            band.DataSourceName = newName;

        _undoService.PushAction(
            () =>
            {
                source.Name = oldName;
                foreach (var (band, oldBinding) in affected)
                    band.DataSourceName = oldBinding;
                _root.RefreshGeometry();
                Rebuild();
            },
            () =>
            {
                source.Name = newName;
                foreach (var (band, _) in affected)
                    band.DataSourceName = newName;
                _root.RefreshGeometry();
                Rebuild();
            });

        Rebuild();
        NotifyModified();
    }

    private void RenameField()
    {
        if (_root == null || _undoService == null) return;
        var (source, field) = GetSelection();
        if (source == null || field == null) return;

        var def = FieldDefinitionDialog.ShowDialog(
            LangRes.GetString("DataSourceTree_RenameFieldTitle"),
            field.Name,
            field.DataType,
            FindForm());
        if (def == null) return;
        if (string.Equals(def.Value.Name, field.Name, StringComparison.Ordinal) && def.Value.Type == field.DataType) return;
        if (!string.Equals(def.Value.Name, field.Name, StringComparison.OrdinalIgnoreCase) && source.FindField(def.Value.Name) != null) return;

        var oldName = field.Name;
        var oldType = field.DataType;
        field.Name = def.Value.Name;
        field.DataType = def.Value.Type;

        _undoService.PushAction(
            () => { field.Name = oldName; field.DataType = oldType; _root.RefreshGeometry(); Rebuild(); },
            () => { field.Name = def.Value.Name; field.DataType = def.Value.Type; _root.RefreshGeometry(); Rebuild(); });

        Rebuild();
        NotifyModified();
    }

    private void AddField()
    {
        if (_root == null || _undoService == null) return;
        var (source, _) = GetSelection();
        if (source == null) return;

        var def = FieldDefinitionDialog.ShowDialog(
            LangRes.GetString("DataSourceTree_AddFieldTitle"),
            string.Empty,
            ReportDataType.String,
            FindForm());
        if (def == null) return;
        if (source.FindField(def.Value.Name) != null) return;

        var field = source.AddField(def.Value.Name, def.Value.Type);
        _undoService.PushAction(
            () => { source.Fields.Remove(field); _root.RefreshGeometry(); Rebuild(); },
            () => { source.Fields.Add(field); _root.RefreshGeometry(); Rebuild(); });

        Rebuild();
        NotifyModified();
    }

    private void RemoveField()
    {
        if (_root == null || _undoService == null) return;
        var (_, field) = GetSelection();
        if (field == null) return;
        var source = GetSelection().Source;
        if (source == null) return;

        var index = source.Fields.IndexOf(field);
        source.Fields.Remove(field);

        _undoService.PushAction(
            () =>
            {
                if (index >= 0 && index <= source.Fields.Count)
                    source.Fields.Insert(index, field);
                else
                    source.Fields.Add(field);
                _root.RefreshGeometry();
                Rebuild();
            },
            () =>
            {
                source.Fields.Remove(field);
                _root.RefreshGeometry();
                Rebuild();
            });

        Rebuild();
        NotifyModified();
    }

    private static int IndexOfSource(Report report, ReportDataSource source)
    {
        for (int i = 0; i < report.DataSources.Count; i++)
        {
            if (ReferenceEquals(report.DataSources[i], source))
                return i;
        }
        return -1;
    }

    private void SelectSource(ReportDataSource source)
    {
        foreach (TreeNode node in _tree.Nodes)
        {
            if (ReferenceEquals(node.Tag, source))
            {
                _tree.SelectNode(node);
                _tree.EnsureVisible(node);
                break;
            }
        }
        UpdateButtonStates();
    }

    private void NotifyModified()
    {
        _root?.Invalidate();
        Modified?.Invoke(this, EventArgs.Empty);
    }
}
