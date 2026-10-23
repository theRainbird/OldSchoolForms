using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Designer.PropertyGrid;
using OldSchoolForms.Ui.Designer.Services;
using OldSchoolForms.Ui.Reports;
using OldSchoolForms.Ui.Theming;
using Graphics = OldSchoolForms.Ui.Rendering.Graphics;

namespace OldSchoolForms.Ui.Designer;

/// <summary>
/// The main window of the OldSchoolForms Form Designer standalone application.
/// Hosts the toolbox, design surface, and menu bar.
/// </summary>
public class DesignerForm : Form
{
    private readonly ToolboxService _toolboxService;
    private readonly DesignSurface _designSurface;
    private readonly PropertyGrid.PropertyGrid _propertyGrid;
    private readonly IDesignDocumentService _documentService;
    private readonly IJsonLayoutService _jsonService;
    private string? _currentFilePath;
    private ToolboxControl _toolbox = null!;
    private Panel _toolboxPanel = null!;
    private SplitPanel _sidebarSplit = null!;
    private Reports.DataSourceTreeControl _dataSourceTree = null!;
    private StatusStripPanel _statusPanel = null!;
    private Label _statusLabel = null!;
    private Label _positionLabel = null!;

    private bool _showGrid = true;
    private bool _snapToGrid = true;

    private const int ToolboxWidth = 220;
    private const int StatusBarHeight = 24;
    private const int PropertyGridWidth = 276;

    private SplitPanel _mainContentSplit = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="DesignerForm"/> class.
    /// </summary>
    /// <param name="documentService">The service used to load and save designed forms as C# source.</param>
    /// <param name="jsonService">
    /// The optional service used to load and save designed layouts as JSON documents.
    /// When <see langword="null"/>, a default <see cref="Serialization.JsonDocumentService"/> is created.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="documentService"/> is null.</exception>
    public DesignerForm(IDesignDocumentService documentService, IJsonLayoutService? jsonService = null)
    {
        _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
        _jsonService = jsonService ?? new Serialization.JsonDocumentService();

        Text = LangRes.GetString("Designer_Title");
        Width = 1400;
        Height = 900;

        Reports.ReportDocumentKind.EnsureRegistered();

        _toolboxService = new ToolboxService();
        _designSurface = new DesignSurface();
        _propertyGrid = new PropertyGrid.PropertyGrid(_designSurface.Selection);

        BuildUI();
        WireEvents();
    }

    private void BuildUI()
    {
        SuspendLayout();

        var menuStrip = BuildMenuStrip();
        Controls.Add(menuStrip);

        _toolboxPanel = new Panel
        {
            Dock = DockStyle.Left,
            Width = ToolboxWidth,
            Padding = new Padding(0, 0, 4, 0),
            BackColor = ThemeManager.CurrentTheme.ControlBackground
        };
        _toolbox = new ToolboxControl(_toolboxService)
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle
        };
        _toolboxPanel.Controls.Add(_toolbox);
        Controls.Add(_toolboxPanel);

        // Sidebar split shown only for report documents: toolbox on top, the
        // data source tree below. Created here but attached on demand so form
        // documents keep the plain full-height toolbox.
        _sidebarSplit = new SplitPanel
        {
            Dock = DockStyle.Fill,
            Orientation = SplitOrientation.Vertical,
            SplitterWidth = 6,
            Panel1MinSize = 80,
            Panel2MinSize = 80
        };
        _dataSourceTree = new Reports.DataSourceTreeControl { Dock = DockStyle.Fill };
        _dataSourceTree.Modified += (_, _) =>
        {
            _statusLabel.Text = LangRes.GetString("Designer_StatusModified");
            _designSurface.Invalidate();
        };
        _sidebarSplit.Panel2.Controls.Add(_dataSourceTree);

        var mainContentSplit = new SplitPanel
        {
            Dock = DockStyle.Fill,
            Orientation = SplitOrientation.Horizontal,
            SplitterWidth = 6,
            Panel1MinSize = 150,
            Panel2MinSize = 180
        };
        _mainContentSplit = mainContentSplit;

        _designSurface.BackColor = Color.FromArgb(240, 240, 240);
        mainContentSplit.Panel1.Controls.Add(_designSurface);
        _designSurface.Dock = DockStyle.Fill;

        var propertyPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(4, 0, 0, 0),
            BackColor = ThemeManager.CurrentTheme.ControlBackground
        };
        _propertyGrid.Dock = DockStyle.Fill;
        propertyPanel.Controls.Add(_propertyGrid);
        mainContentSplit.Panel2.Controls.Add(propertyPanel);

        Controls.Add(mainContentSplit);

        BuildStatusStrip();
        Controls.Add(_statusPanel!);

        ResumeLayout();

        _mainContentSplit.SplitterDistance = Math.Max(
            _mainContentSplit.Panel1MinSize,
            _mainContentSplit.Width - _mainContentSplit.SplitterWidth - PropertyGridWidth);
    }

    private MenuStrip BuildMenuStrip()
    {
        var menu = new MenuStrip();
        menu.Dock = DockStyle.Top;

        var fileItem = new ToolStripMenuItem(LangRes.GetString("Designer_MenuFile"));
        var newItem = new ToolStripMenuItem(LangRes.GetString("Designer_MenuNew"));
        newItem.Click += (_, _) => NewForm();
        fileItem.DropDownItems.Add(newItem);
        var openItem = new ToolStripMenuItem(LangRes.GetString("Designer_MenuOpen"));
        openItem.Click += (_, _) => OpenForm();
        fileItem.DropDownItems.Add(openItem);
        var saveItem = new ToolStripMenuItem(LangRes.GetString("Designer_MenuSave"));
        saveItem.Click += (_, _) => SaveForm();
        fileItem.DropDownItems.Add(saveItem);
        var saveAsItem = new ToolStripMenuItem(LangRes.GetString("Designer_MenuSaveAs"));
        saveAsItem.Click += (_, _) => SaveFormAs();
        fileItem.DropDownItems.Add(saveAsItem);
        fileItem.DropDownItems.Add(new ToolStripMenuItem("-"));
        var exitItem = new ToolStripMenuItem(LangRes.GetString("Designer_MenuExit"));
        exitItem.Click += (_, _) => Close();
        fileItem.DropDownItems.Add(exitItem);

        var editItem = new ToolStripMenuItem(LangRes.GetString("Designer_MenuEdit"));
        var undoItem = new ToolStripMenuItem(LangRes.GetString("Designer_MenuUndo"));
        undoItem.Click += (_, _) => _designSurface.Undo.Undo();
        editItem.DropDownItems.Add(undoItem);
        var redoItem = new ToolStripMenuItem(LangRes.GetString("Designer_MenuRedo"));
        redoItem.Click += (_, _) => _designSurface.Undo.Redo();
        editItem.DropDownItems.Add(redoItem);
        editItem.DropDownItems.Add(new ToolStripMenuItem("-"));
        var deleteItem = new ToolStripMenuItem(LangRes.GetString("Designer_MenuDelete"));
        deleteItem.Click += (_, _) => _designSurface.DeleteSelected();
        editItem.DropDownItems.Add(deleteItem);
        var selectAllItem = new ToolStripMenuItem(LangRes.GetString("Designer_MenuSelectAll"));
        selectAllItem.Click += (_, _) => _designSurface.Selection.SelectAll(_designSurface.Items);
        editItem.DropDownItems.Add(selectAllItem);

        var viewItem = new ToolStripMenuItem(LangRes.GetString("Designer_MenuView"));
        var showGridItem = new ToolStripMenuItem(LangRes.GetString("Designer_MenuShowGrid"));
        showGridItem.Click += (_, _) =>
        {
            _showGrid = !_showGrid;
            _designSurface.ShowGrid = _showGrid;
        };
        viewItem.DropDownItems.Add(showGridItem);
        var snapGridItem = new ToolStripMenuItem(LangRes.GetString("Designer_MenuSnapGrid"));
        snapGridItem.Click += (_, _) =>
        {
            _snapToGrid = !_snapToGrid;
            _designSurface.SnapToGrid = _snapToGrid;
        };
        viewItem.DropDownItems.Add(snapGridItem);

        var reportItem = new ToolStripMenuItem(LangRes.GetString("Designer_MenuReport"));
        var previewItem = new ToolStripMenuItem(LangRes.GetString("Designer_MenuPreview"));
        previewItem.Click += (_, _) => ShowReportPreview();
        reportItem.DropDownItems.Add(previewItem);

        var helpItem = new ToolStripMenuItem(LangRes.GetString("Designer_MenuHelp"));
        var aboutItem = new ToolStripMenuItem(LangRes.GetString("Designer_MenuAbout"));
        aboutItem.Click += (_, _) =>
            MessageBox.Show(LangRes.GetString("Designer_AboutText"), LangRes.GetString("Designer_MenuAbout"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        helpItem.DropDownItems.Add(aboutItem);

        menu.Items.Add(fileItem);
        menu.Items.Add(editItem);
        menu.Items.Add(viewItem);
        menu.Items.Add(reportItem);
        menu.Items.Add(helpItem);

        return menu;
    }

    /// <summary>
    /// Opens a modal <see cref="ReportViewer"/> previewing the report currently open in the designer.
    /// Before rendering, sample data is generated for the report's data sources so the
    /// layout can be tested with real rows even when no live data is attached. The
    /// generated data is runtime-only and never persisted with the report.
    /// Does nothing when the current document is not a report.
    /// </summary>
    private void ShowReportPreview()
    {
        if (_designSurface.RootControl is not Reports.DesignReport designReport)
        {
            _statusLabel.Text = LangRes.GetString("Designer_PreviewNotReport");
            return;
        }

        var report = designReport.Report;

        // For legacy single-source reports (no named data sources) derive the field
        // names from the declared design fields and the fields referenced by the
        // report's controls and groups.
        IEnumerable<(string Name, ReportDataType Type)>? legacyFields = null;
        if (report.DataSources.Count == 0)
        {
            legacyFields = Reports.ReportDesignContext
                .GetAvailableFields(report)
                .Select(name => (name, ReportDataType.String))
                .ToList();
        }

        int populated = ReportSampleData.EnsurePreviewData(report, legacyFields);

        var viewer = new ReportViewer
        {
            Dock = DockStyle.Fill
        };
        var previewForm = new Form
        {
            Text = string.Format(LangRes.GetString("Designer_PreviewTitle"), report.Name),
            Width = 1000,
            Height = 750
        };
        previewForm.Controls.Add(viewer);
        viewer.Report = report;
        viewer.RefreshReport();

        if (populated > 0)
            _statusLabel.Text = string.Format(LangRes.GetString("Designer_PreviewSampleData"), populated);

        previewForm.ShowDialog(this);
    }

    private void BuildStatusStrip()
    {
        _statusPanel = new StatusStripPanel();
        _statusPanel.Dock = DockStyle.Bottom;
        _statusPanel.Height = StatusBarHeight;

        _statusLabel = new Label
        {
            Text = LangRes.GetString("Designer_StatusReady"),
            Dock = DockStyle.Left,
            BackColor = Color.Transparent,
            Width = 400
        };

        _positionLabel = new Label
        {
            Text = "",
            Dock = DockStyle.Right,
            BackColor = Color.Transparent,
            Width = 250
        };

        _statusPanel.Controls.Add(_statusLabel);
        _statusPanel.Controls.Add(_positionLabel);
    }

    private void WireEvents()
    {
        _toolbox.DragStarted += (_, e) =>
        {
            _designSurface.BeginExternalDrop(e.Item);
            _statusLabel.Text = string.Format(LangRes.GetString("Designer_StatusPlacing"), e.Item.DisplayName);
        };

        _designSurface.SelectionChanged += (_, _) =>
        {
            var primary = _designSurface.Selection.PrimarySelection;
            _statusLabel.Text = primary != null
                ? string.Format(LangRes.GetString("Designer_StatusSelected"), primary.Control.Name ?? primary.Control.GetType().Name)
                : LangRes.GetString("Designer_StatusNoSelection");
            _propertyGrid.BindObject(_designSurface.AdapterSelectedValue);
        };

        _designSurface.AdapterSelectionChanged += (_, _) =>
        {
            _propertyGrid.BindObject(_designSurface.AdapterSelectedValue);
        };

        _designSurface.ContentModified += (_, _) =>
        {
            _statusLabel.Text = LangRes.GetString("Designer_StatusModified");
        };

        _propertyGrid.PropertyCommitted += (_, _) =>
        {
            if (_designSurface.RootControl is Reports.DesignReport reportRoot)
                reportRoot.RefreshGeometry();
            _designSurface.Invalidate();
        };

        _propertyGrid.PropertyValueChanged += (_, e) =>
        {
            if (e.Control != null)
                _designSurface.Undo.PushPropertyChange(e.Control, e.PropertyName, e.OldValue);
            else if (e.Target != null)
                _designSurface.Undo.PushPropertyChangeOn(e.Target, e.PropertyName, e.OldValue);
            if (_designSurface.RootControl is Reports.DesignReport reportRoot)
                reportRoot.RefreshGeometry();
            _statusLabel.Text = LangRes.GetString("Designer_StatusModified");
            _designSurface.Invalidate();
        };

        Resize += (_, _) =>
        {
            _mainContentSplit.SplitterDistance = Math.Max(
                _mainContentSplit.Panel1MinSize,
                _mainContentSplit.Width - _mainContentSplit.SplitterWidth - PropertyGridWidth);
        };
    }

    private Point ScreenToDesignSurface(Point screenPoint)
    {
        var origin = _designSurface.PointToScreen(Point.Empty);
        return new Point(
            screenPoint.X - origin.X,
            screenPoint.Y - origin.Y);
    }

    private void NewForm()
    {
        var type = NewDocumentDialog.ShowDialog();
        if (type is null)
            return;

        _designSurface.ClearAll();
        if (type == NewDocumentType.UserControl)
        {
            _designSurface.BeginDesignUserControl();
            _statusLabel.Text = LangRes.GetString("Designer_StatusNewUserControl");
        }
        else if (type == NewDocumentType.Report)
        {
            Reports.ReportDocumentKind.EnsureRegistered();
            Reports.ReportDocumentKind.Instance.CreateNew(_designSurface);
            _statusLabel.Text = LangRes.GetString("Designer_StatusNewReport");
        }
        else
        {
            _designSurface.BeginDesignForm();
            _statusLabel.Text = LangRes.GetString("Designer_StatusNewForm");
        }

        _currentFilePath = null;
        Text = LangRes.GetString("Designer_TitleUntitled");
        ApplyToolboxForDocument(_designSurface.RootControl is Reports.DesignReport);
    }

    /// <summary>
    /// Swaps the toolbox palette between the standard form controls and the report
    /// control proxies depending on the active document kind.
    /// </summary>
    /// <param name="isReport">Whether the active document is a report.</param>
    private void ApplyToolboxForDocument(bool isReport)
    {
        _toolboxService.Clear();
        if (isReport)
            Reports.ReportToolbox.RegisterReportItems(_toolboxService);
        else
            _toolboxService.RegisterStandardControls();

        ApplySidebarForDocument(isReport);
    }

    /// <summary>
    /// Swaps the left sidebar between the plain full-height toolbox (form
    /// documents) and the toolbox-over-data-source-tree split (report documents).
    /// </summary>
    /// <param name="isReport">Whether the active document is a report.</param>
    private void ApplySidebarForDocument(bool isReport)
    {
        if (isReport)
        {
            if (_toolbox.Parent != _sidebarSplit.Panel1)
            {
                _toolbox.Parent?.Controls.Remove(_toolbox);
                _toolbox.Dock = DockStyle.Fill;
                _sidebarSplit.Panel1.Controls.Add(_toolbox);
            }
            if (!_toolboxPanel.Controls.Contains(_sidebarSplit))
                _toolboxPanel.Controls.Add(_sidebarSplit);

            var root = _designSurface.RootControl as Reports.DesignReport;
            _dataSourceTree.BindReport(root, _designSurface);
            // Default the split so the toolbox keeps roughly the upper half.
            _sidebarSplit.SplitterDistance = Math.Max(
                _sidebarSplit.Panel1MinSize,
                _toolboxPanel.Height / 2);
        }
        else
        {
            _dataSourceTree.BindReport(null, null);
            if (_toolbox.Parent != _toolboxPanel)
            {
                _toolbox.Parent?.Controls.Remove(_toolbox);
                _toolbox.Dock = DockStyle.Fill;
                _toolboxPanel.Controls.Add(_toolbox);
            }
            _toolboxPanel.Controls.Remove(_sidebarSplit);
        }
    }

    private void OpenForm()
    {
        var dialog = new OpenFileDialog
        {
            Title = LangRes.GetString("Designer_OpenTitle"),
            Filter = "Alle Dokumente (*.cs;*.json;*.report)|*.cs;*.json;*.report|C# Dateien (*.cs)|*.cs|JSON Layouts (*.json)|*.json|Reports (*.report)|*.report|Alle Dateien (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                _designSurface.ClearAll();
                if (Services.DocumentKindRegistry.TryGet(dialog.FileName, out var kind))
                    kind.Load(_designSurface, dialog.FileName);
                else if (IsJsonPath(dialog.FileName))
                    _jsonService.LoadFromFile(_designSurface, dialog.FileName);
                else
                    _documentService.Load(File.ReadAllText(dialog.FileName), _designSurface);

                _currentFilePath = dialog.FileName;
                _statusLabel.Text = string.Format(LangRes.GetString("Designer_StatusOpened"), dialog.FileName);
                Text = string.Format(LangRes.GetString("Designer_TitleWithFile"), dialog.FileName);
                ApplyToolboxForDocument(_designSurface.RootControl is Reports.DesignReport);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(LangRes.GetString("Designer_ErrorOpen"), ex.Message), LangRes.GetString("Designer_ErrorTitle"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void SaveForm()
    {
        if (_currentFilePath != null && Services.DocumentKindRegistry.TryGet(_currentFilePath, out var kind))
        {
            try
            {
                kind.Save(_designSurface, _currentFilePath);
                _statusLabel.Text = string.Format(LangRes.GetString("Designer_StatusSaved"), _currentFilePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(LangRes.GetString("Designer_ErrorSave"), ex.Message), LangRes.GetString("Designer_ErrorTitle"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return;
        }

        if (_currentFilePath != null && IsJsonPath(_currentFilePath))
        {
            try
            {
                _jsonService.SaveToFile(_designSurface, _currentFilePath);
                _statusLabel.Text = string.Format(LangRes.GetString("Designer_StatusSaved"), _currentFilePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(LangRes.GetString("Designer_ErrorSave"), ex.Message), LangRes.GetString("Designer_ErrorTitle"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return;
        }

        string code = _documentService.Save(_designSurface, _jsonService.Namespace, _jsonService.ClassName);
        _statusLabel.Text = LangRes.GetString("Designer_StatusSavedPreview");
    }

    private void SaveFormAs()
    {
        var dialog = new SaveFileDialog
        {
            Title = LangRes.GetString("Designer_SaveTitle"),
            Filter = "Alle Dokumente (*.cs;*.json;*.report)|*.cs;*.json;*.report|C# Dateien (*.cs)|*.cs|JSON Layouts (*.json)|*.json|Reports (*.report)|*.report|Alle Dateien (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                if (Services.DocumentKindRegistry.TryGet(dialog.FileName, out var kind))
                {
                    kind.Save(_designSurface, dialog.FileName);
                }
                else if (IsJsonPath(dialog.FileName))
                {
                    _jsonService.SaveToFile(_designSurface, dialog.FileName);
                }
                else
                {
                    string code = _documentService.Save(_designSurface, _jsonService.Namespace, _jsonService.ClassName);
                    File.WriteAllText(dialog.FileName, code);
                }

                _currentFilePath = dialog.FileName;
                _statusLabel.Text = string.Format(LangRes.GetString("Designer_StatusSaved"), dialog.FileName);
                Text = string.Format(LangRes.GetString("Designer_TitleWithFile"), dialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(LangRes.GetString("Designer_ErrorSave"), ex.Message), LangRes.GetString("Designer_ErrorTitle"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private static bool IsJsonPath(string path)
        => Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// A status strip panel with a top border line.
/// </summary>
internal class StatusStripPanel : Panel
{
    public StatusStripPanel()
    {
        BackColor = ThemeManager.CurrentTheme.ControlBackground;
        Height = 24;
    }

    public override void Render(Graphics g)
    {
        var theme = ThemeManager.CurrentTheme;
        g.DrawLine(theme.StatusStripTopLine, 0, 0, Width, 0);
        g.FillRectangle(theme.ControlBackground, 0, 1, Width, Height - 1);
        base.Render(g);
    }
}
