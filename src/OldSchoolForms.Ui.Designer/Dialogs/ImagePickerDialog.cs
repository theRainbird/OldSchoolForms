using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Resources;

namespace OldSchoolForms.Ui.Designer.Dialogs;

/// <summary>
/// A modal dialog that lets the user choose an image for a <see cref="ToolStripItem"/>,
/// either by picking one of the framework's embedded icon resources or by importing an
/// image file from disk. Mirrors the image picker of the classic Windows Forms designer.
/// The chosen image is applied to the item only when the dialog is confirmed with OK.
/// </summary>
public sealed class ImagePickerDialog : Form
{
    private readonly ToolStripItem _item;
    private readonly ImageResourceListView _resourceList;
    private readonly TextBox _filterBox;
    private readonly PictureBox _preview;
    private readonly List<string> _allResources = new();
    private IGraphicsImage? _selectedImage;

    /// <summary>
    /// Opens the image picker as a modal dialog and blocks until the user confirms or cancels.
    /// </summary>
    /// <param name="item">The tool strip item whose image is being edited.</param>
    /// <param name="owner">The owner form, or <see langword="null"/>.</param>
    /// <returns><see cref="DialogResult.OK"/> if confirmed, otherwise <see cref="DialogResult.Cancel"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <see langword="null"/>.</exception>
    public static DialogResult ShowDialog(ToolStripItem item, Form? owner = null)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));

        var dialog = new ImagePickerDialog(item)
        {
            Text = LangRes.GetString("ImagePickerDialog_Title")
        };

        return dialog.ShowDialog(owner);
    }

    internal ImagePickerDialog(ToolStripItem item)
    {
        _item = item;

        Size = new Size(720, 520);
        FormBorderStyle = FormBorderStyle.Sizable;

        var toolbar = new ToolStrip
        {
            Dock = DockStyle.Top,
            Height = 30
        };

        var btnImport = MakeToolButton("ImagePickerDialog_Import", Icons.ImageFile, ImportFromDisk);
        var btnRemove = MakeToolButton("ImagePickerDialog_Remove", Icons.RemoveImage, () => SetSelectedImage(null));
        var btnOk = MakeToolButton("ImagePickerDialog_Ok", Icons.CheckmarkCircle16, () => CloseDialog(true));
        var btnCancel = MakeToolButton("ImagePickerDialog_Cancel", Icons.DismissCircle16, () => CloseDialog(false));

        toolbar.Items.Add(btnImport);
        toolbar.Items.Add(btnRemove);
        toolbar.Items.Add(new ToolStripSeparator());
        toolbar.Items.Add(btnOk);
        toolbar.Items.Add(btnCancel);

        var split = new SplitPanel
        {
            Dock = DockStyle.Fill,
            Orientation = SplitOrientation.Horizontal,
            SplitterDistance = 420
        };

        var leftPanel = new Panel { Dock = DockStyle.Fill };
        _filterBox = new TextBox
        {
            Dock = DockStyle.Top,
            Height = 26
        };
        _filterBox.TextChanged += (_, _) => ApplyFilter();

        _resourceList = new ImageResourceListView { Dock = DockStyle.Fill };
        _resourceList.SelectedResourceChanged += (_, _) => OnResourceSelected();

        leftPanel.Controls.Add(_resourceList);
        leftPanel.Controls.Add(_filterBox);

        var rightPanel = new Panel { Dock = DockStyle.Fill };
        var previewLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 22,
            Text = LangRes.GetString("ImagePickerDialog_PreviewLabel"),
            TextAlign = ContentAlignment.MiddleLeft
        };
        _preview = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom
        };
        rightPanel.Controls.Add(_preview);
        rightPanel.Controls.Add(previewLabel);

        split.Panel1.Controls.Add(leftPanel);
        split.Panel2.Controls.Add(rightPanel);

        Controls.Add(split);
        Controls.Add(toolbar);

        LoadResources();
        PreselectCurrent();
    }

    private void LoadResources()
    {
        _allResources.Clear();
        try
        {
            _allResources.AddRange(
                typeof(SvgImage).Assembly.GetManifestResourceNames()
                    .Where(n => n.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
        }
        catch
        {
            // Enumeration failures leave the list empty; import still works.
        }
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var filter = _filterBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(filter))
        {
            _resourceList.SetResources(_allResources);
        }
        else
        {
            var filtered = _allResources
                .Where(n => ImageResourceListView.ShortName(n)
                    .Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();
            _resourceList.SetResources(filtered);
        }
    }

    private void PreselectCurrent()
    {
        if (_item.Image is SvgImage svg && !string.IsNullOrEmpty(svg.ResourceName))
        {
            _resourceList.SelectResource(svg.ResourceName);
            SetSelectedImage(_item.Image);
        }
        else if (_item.Image != null)
        {
            SetSelectedImage(_item.Image);
        }
    }

    private void OnResourceSelected()
    {
        var name = _resourceList.SelectedResource;
        if (name == null) return;
        var image = SvgImage.FromSvgResource(name, 24);
        SetSelectedImage(image);
    }

    private void ImportFromDisk()
    {
        var dialog = new OpenFileDialog
        {
            Title = LangRes.GetString("ImagePickerDialog_ImportTitle"),
            Filter = LangRes.GetString("ImagePickerDialog_FilterImages")
        };

        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var path = dialog.FileName;
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

        IGraphicsImage? image = null;
        try
        {
            if (path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            {
                using var stream = File.OpenRead(path);
                image = SvgImage.FromSvgStream(stream, 24);
            }
            else
            {
                image = RasterImage.FromFile(path);
            }
        }
        catch
        {
            image = null;
        }

        if (image != null)
            SetSelectedImage(image);
    }

    private void SetSelectedImage(IGraphicsImage? image)
    {
        _selectedImage = image;
        _preview.Image = image;
        _preview.Invalidate();
    }

    private static ToolStripButton MakeToolButton(string resourceKey, SvgImage? icon, Action onClick)
    {
        var button = new ToolStripButton(LangRes.GetString(resourceKey))
        {
            DisplayStyle = icon != null ? ToolStripItemDisplayStyle.ImageAndText : ToolStripItemDisplayStyle.Text
        };
        if (icon != null) button.Image = icon;
        button.Click += (_, _) => onClick();
        return button;
    }

    private void CloseDialog(bool ok)
    {
        if (ok)
            _item.Image = _selectedImage;

        DialogResult = ok ? DialogResult.OK : DialogResult.Cancel;
        _modal = false;
    }

    /// <summary>
    /// Handles keyboard shortcuts. Enter confirms (OK) and Escape cancels the dialog.
    /// </summary>
    /// <param name="e">The key event data.</param>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            CloseDialog(true);
            e.Handled = true;
            return;
        }

        if (e.KeyCode == Keys.Escape)
        {
            CloseDialog(false);
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }
}
