using System;
using System.IO;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Designer.PropertyGrid;
using OldSchoolForms.Ui.Designer.Services;
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

        Text = "OldSchoolForms Form Designer";
        Width = 1400;
        Height = 900;

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

        var fileItem = new ToolStripMenuItem("&Datei");
        var newItem = new ToolStripMenuItem("&Neu");
        newItem.Click += (_, _) => NewForm();
        fileItem.DropDownItems.Add(newItem);
        var openItem = new ToolStripMenuItem("&Öffnen...");
        openItem.Click += (_, _) => OpenForm();
        fileItem.DropDownItems.Add(openItem);
        var saveItem = new ToolStripMenuItem("&Speichern");
        saveItem.Click += (_, _) => SaveForm();
        fileItem.DropDownItems.Add(saveItem);
        var saveAsItem = new ToolStripMenuItem("Speichern &unter...");
        saveAsItem.Click += (_, _) => SaveFormAs();
        fileItem.DropDownItems.Add(saveAsItem);
        fileItem.DropDownItems.Add(new ToolStripMenuItem("-"));
        var exitItem = new ToolStripMenuItem("Be&enden");
        exitItem.Click += (_, _) => Close();
        fileItem.DropDownItems.Add(exitItem);

        var editItem = new ToolStripMenuItem("&Bearbeiten");
        var undoItem = new ToolStripMenuItem("&Rückgängig");
        undoItem.Click += (_, _) => _designSurface.Undo.Undo();
        editItem.DropDownItems.Add(undoItem);
        var redoItem = new ToolStripMenuItem("&Wiederholen");
        redoItem.Click += (_, _) => _designSurface.Undo.Redo();
        editItem.DropDownItems.Add(redoItem);
        editItem.DropDownItems.Add(new ToolStripMenuItem("-"));
        var deleteItem = new ToolStripMenuItem("&Löschen");
        deleteItem.Click += (_, _) => _designSurface.DeleteSelected();
        editItem.DropDownItems.Add(deleteItem);
        var selectAllItem = new ToolStripMenuItem("&Alles auswählen");
        selectAllItem.Click += (_, _) => _designSurface.Selection.SelectAll(_designSurface.Items);
        editItem.DropDownItems.Add(selectAllItem);

        var viewItem = new ToolStripMenuItem("&Ansicht");
        var showGridItem = new ToolStripMenuItem("&Raster anzeigen");
        showGridItem.Click += (_, _) =>
        {
            _showGrid = !_showGrid;
            _designSurface.ShowGrid = _showGrid;
        };
        viewItem.DropDownItems.Add(showGridItem);
        var snapGridItem = new ToolStripMenuItem("Am &Raster ausrichten");
        snapGridItem.Click += (_, _) =>
        {
            _snapToGrid = !_snapToGrid;
            _designSurface.SnapToGrid = _snapToGrid;
        };
        viewItem.DropDownItems.Add(snapGridItem);

        var helpItem = new ToolStripMenuItem("&Hilfe");
        var aboutItem = new ToolStripMenuItem("&Info");
        aboutItem.Click += (_, _) =>
            MessageBox.Show("OldSchoolForms Form Designer\nVersion 1.0\n\nEin grafischer Formular-Designer für OldSchoolForms.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
        helpItem.DropDownItems.Add(aboutItem);

        menu.Items.Add(fileItem);
        menu.Items.Add(editItem);
        menu.Items.Add(viewItem);
        menu.Items.Add(helpItem);

        return menu;
    }

    private void BuildStatusStrip()
    {
        _statusPanel = new StatusStripPanel();
        _statusPanel.Dock = DockStyle.Bottom;
        _statusPanel.Height = StatusBarHeight;

        _statusLabel = new Label
        {
            Text = "Bereit",
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
            _statusLabel.Text = $"Platziere {e.Item.DisplayName} – Klicken Sie auf die Designfläche";
        };

        _designSurface.SelectionChanged += (_, _) =>
        {
            var primary = _designSurface.Selection.PrimarySelection;
            _statusLabel.Text = primary != null
                ? $"Selektiert: {primary.Control.Name ?? primary.Control.GetType().Name}"
                : "Keine Auswahl";
            _propertyGrid.BindObject(_designSurface.AdapterSelectedValue);
        };

        _designSurface.AdapterSelectionChanged += (_, _) =>
        {
            _propertyGrid.BindObject(_designSurface.AdapterSelectedValue);
        };

        _designSurface.ContentModified += (_, _) =>
        {
            _statusLabel.Text = "Geändert";
        };

        _propertyGrid.PropertyCommitted += (_, _) =>
        {
            _designSurface.Invalidate();
        };

        _propertyGrid.PropertyValueChanged += (_, e) =>
        {
            _designSurface.Undo.PushPropertyChange(e.Control, e.PropertyName, e.OldValue);
            _statusLabel.Text = "Geändert";
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
            _designSurface.BeginDesignUserControl();
        else
            _designSurface.BeginDesignForm();

        _statusLabel.Text = type == NewDocumentType.UserControl
            ? "Neues Benutzersteuerelement"
            : "Neues Formular";
        Text = "OldSchoolForms Form Designer - [Unbenannt]";
    }

    private void OpenForm()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Formular öffnen",
            Filter = "Formulare (*.cs;*.json)|*.cs;*.json|C# Dateien (*.cs)|*.cs|JSON Layouts (*.json)|*.json|Alle Dateien (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                _designSurface.ClearAll();
                if (IsJsonPath(dialog.FileName))
                    _jsonService.LoadFromFile(_designSurface, dialog.FileName);
                else
                    _documentService.Load(File.ReadAllText(dialog.FileName), _designSurface);

                _currentFilePath = dialog.FileName;
                _statusLabel.Text = $"Geöffnet: {dialog.FileName}";
                Text = $"OldSchoolForms Form Designer - [{dialog.FileName}]";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Öffnen: {ex.Message}", "Fehler",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void SaveForm()
    {
        if (_currentFilePath != null && IsJsonPath(_currentFilePath))
        {
            try
            {
                _jsonService.SaveToFile(_designSurface, _currentFilePath);
                _statusLabel.Text = $"Gespeichert: {_currentFilePath}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern: {ex.Message}", "Fehler",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return;
        }

        string code = _documentService.Save(_designSurface, _jsonService.Namespace, _jsonService.ClassName);
        _statusLabel.Text = "Gespeichert (Vorschau generiert)";
    }

    private void SaveFormAs()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Formular speichern",
            Filter = "Formulare (*.cs;*.json)|*.cs;*.json|C# Dateien (*.cs)|*.cs|JSON Layouts (*.json)|*.json|Alle Dateien (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                if (IsJsonPath(dialog.FileName))
                {
                    _jsonService.SaveToFile(_designSurface, dialog.FileName);
                }
                else
                {
                    string code = _documentService.Save(_designSurface, _jsonService.Namespace, _jsonService.ClassName);
                    File.WriteAllText(dialog.FileName, code);
                }

                _currentFilePath = dialog.FileName;
                _statusLabel.Text = $"Gespeichert: {dialog.FileName}";
                Text = $"OldSchoolForms Form Designer - [{dialog.FileName}]";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern: {ex.Message}", "Fehler",
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
