using System.Globalization;
using System.Linq;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Controls.Containers;

namespace OldSchoolForms.Ui.Designer.Dialogs;

/// <summary>
/// A modal dialog for editing a <see cref="Font"/> value. The user narrows the installed font
/// families with a search box that matches any part of the family name, selects a family from the
/// filtered list, enters or picks a point size, toggles the bold, italic, underline and strikeout
/// style flags, and sees a live preview rendered in the chosen font. Confirming with OK returns the
/// new font; cancelling returns <see langword="null"/>.
/// </summary>
public sealed class FontEditorDialog : Form
{
    private readonly TextBox _searchBox = null!;
    private readonly ListBox _familyList = null!;
    private readonly ComboBox _sizeCombo = null!;
    private readonly CheckBox _boldCheck = null!;
    private readonly CheckBox _italicCheck = null!;
    private readonly CheckBox _underlineCheck = null!;
    private readonly CheckBox _strikeoutCheck = null!;
    private readonly Label _preview = null!;
    private string[] _allFamilies = System.Array.Empty<string>();
    private Font _result;

    /// <summary>
    /// The point sizes offered in the size drop-down. The field stays editable so any other
    /// size can still be typed in.
    /// </summary>
    private static readonly float[] CommonSizes =
        { 6f, 7f, 8f, 9f, 10f, 11f, 12f, 14f, 16f, 18f, 20f, 22f, 24f, 26f, 28f, 36f, 48f, 72f };

    /// <summary>
    /// Shows the font editor and blocks until the user confirms or cancels.
    /// </summary>
    /// <param name="initial">The font preselected when the dialog opens, or <see langword="null"/> for the default font.</param>
    /// <param name="owner">The owner form, or <see langword="null"/>.</param>
    /// <returns>The edited font when confirmed; <see langword="null"/> when cancelled.</returns>
    public static Font? ShowDialog(Font? initial, Form? owner = null)
    {
        var dialog = new FontEditorDialog(initial ?? Font.Default);
        var result = dialog.ShowDialog(owner);
        return result == DialogResult.OK ? dialog._result : null;
    }

    private FontEditorDialog(Font initial)
    {
        _result = initial;
        Text = LangRes.GetString("FontDialog_Title");
        Width = 440;
        Height = 510;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Zoom = 1f;

        // Font family search box - filters the list below by any part of the name
        Controls.Add(new Label { Text = LangRes.GetString("FontDialog_Family"), X = 16, Y = 16, Width = 200 });
        _searchBox = new TextBox { X = 80, Y = 40, Width = 344, Height = 24 };
        Controls.Add(new Label { Text = LangRes.GetString("FontDialog_Search"), X = 16, Y = 43, Width = 60 });
        _searchBox.TextChanged += (_, _) => ApplySearchFilter();
        Controls.Add(_searchBox);

        // Font family list (populated from the installed families, narrowed by the search box)
        _familyList = new ListBox { X = 16, Y = 70, Width = 408, Height = 180 };
        _allFamilies = FontManager.GetFontFamilies();
        foreach (var family in _allFamilies)
            _familyList.Items.Add(family);
        SelectFamily(initial.Name);
        _familyList.SelectedIndexChanged += (_, _) => UpdatePreview();
        Controls.Add(_familyList);

        // Size (editable so any size can be typed, with common sizes offered)
        Controls.Add(new Label { Text = LangRes.GetString("FontDialog_Size"), X = 16, Y = 262, Width = 90 });
        _sizeCombo = new ComboBox
        {
            X = 16,
            Y = 286,
            Width = 90,
            DropDownStyle = DropDownStyle.DropDown
        };
        foreach (var size in CommonSizes)
            _sizeCombo.Items.Add(size.ToString("0.#", CultureInfo.InvariantCulture));
        _sizeCombo.Text = initial.Size.ToString("0.#", CultureInfo.InvariantCulture);
        _sizeCombo.TextChanged += (_, _) => UpdatePreview();
        _sizeCombo.SelectedIndexChanged += (_, _) => UpdatePreview();
        Controls.Add(_sizeCombo);

        // Style flags
        Controls.Add(new Label { Text = LangRes.GetString("FontDialog_Style"), X = 130, Y = 262, Width = 160 });
        _boldCheck = new CheckBox
        {
            Text = LangRes.GetString("FontDialog_Bold"),
            X = 130,
            Y = 286,
            Width = 90,
            Checked = initial.Style.HasFlag(FontStyle.Bold)
        };
        _italicCheck = new CheckBox
        {
            Text = LangRes.GetString("FontDialog_Italic"),
            X = 224,
            Y = 286,
            Width = 90,
            Checked = initial.Style.HasFlag(FontStyle.Italic)
        };
        _underlineCheck = new CheckBox
        {
            Text = LangRes.GetString("FontDialog_Underline"),
            X = 130,
            Y = 312,
            Width = 110,
            Checked = initial.Style.HasFlag(FontStyle.Underline)
        };
        _strikeoutCheck = new CheckBox
        {
            Text = LangRes.GetString("FontDialog_Strikeout"),
            X = 244,
            Y = 312,
            Width = 120,
            Checked = initial.Style.HasFlag(FontStyle.Strikeout)
        };
        foreach (var check in new[] { _boldCheck, _italicCheck, _underlineCheck, _strikeoutCheck })
            check.CheckedChanged += (_, _) => UpdatePreview();
        Controls.Add(_boldCheck);
        Controls.Add(_italicCheck);
        Controls.Add(_underlineCheck);
        Controls.Add(_strikeoutCheck);

        // Live preview rendered in the selected font
        Controls.Add(new Label { Text = LangRes.GetString("FontDialog_Preview"), X = 16, Y = 346, Width = 200 });
        var previewBox = new Panel { X = 16, Y = 366, Width = 408, Height = 58, BackColor = Color.White };
        _preview = new Label
        {
            Text = LangRes.GetString("FontDialog_PreviewSample"),
            X = 8,
            Y = 6,
            Width = 392
        };
        previewBox.Controls.Add(_preview);
        Controls.Add(previewBox);

        UpdatePreview();

        // OK / Cancel
        var buttonPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            BackColor = Color.Transparent
        };
        var okButton = new Button
        {
            Text = LangRes.GetString("FontDialog_OK"),
            Width = 75,
            Height = 30,
            X = 152,
            Y = 10
        };
        okButton.Click += (_, _) => CloseDialog(true);

        var cancelButton = new Button
        {
            Text = LangRes.GetString("FontDialog_Cancel"),
            Width = 75,
            Height = 30,
            X = 237,
            Y = 10
        };
        cancelButton.Click += (_, _) => CloseDialog(false);

        buttonPanel.Controls.AddRange([okButton, cancelButton]);
        Controls.Add(buttonPanel);
    }

    /// <summary>
    /// Selects the list entry matching <paramref name="name"/> (case-insensitive), leaving the
    /// selection untouched when the family is not among the installed fonts.
    /// </summary>
    /// <param name="name">The font family name to select.</param>
    private void SelectFamily(string name)
    {
        for (int i = 0; i < _familyList.Items.Count; i++)
        {
            if (string.Equals(_familyList.Items[i] as string, name, StringComparison.OrdinalIgnoreCase))
            {
                _familyList.SelectedIndex = i;
                return;
            }
        }
    }

    /// <summary>
    /// Narrows the family list to the families whose name contains the current search text
    /// (case-insensitive). The current selection is kept when it still matches the query; otherwise
    /// the first match is selected so the preview follows what the user typed. An empty query
    /// restores the full list of installed families.
    /// </summary>
    private void ApplySearchFilter()
    {
        var query = _searchBox.Text?.Trim() ?? string.Empty;
        var filtered = FilterFamilies(_allFamilies, query);
        var current = _familyList.SelectedItem as string;

        _familyList.Items.Clear();
        foreach (var family in filtered)
            _familyList.Items.Add(family);

        if (current != null)
        {
            for (int i = 0; i < filtered.Length; i++)
            {
                if (string.Equals(filtered[i], current, StringComparison.OrdinalIgnoreCase))
                {
                    _familyList.SelectedIndex = i;
                    UpdatePreview();
                    return;
                }
            }
        }

        _familyList.SelectedIndex = filtered.Length > 0 ? 0 : -1;
        UpdatePreview();
    }

    /// <summary>
    /// Returns the families whose name contains <paramref name="query"/> as a case-insensitive
    /// substring, preserving their original order. A <see langword="null"/>, empty or whitespace
    /// query returns every family unfiltered.
    /// </summary>
    /// <param name="families">The full list of installed family names.</param>
    /// <param name="query">The search text to match against any part of the name.</param>
    /// <returns>The matching family names.</returns>
    internal static string[] FilterFamilies(string[] families, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return families;

        return families
            .Where(f => f.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            .ToArray();
    }

    /// <summary>
    /// Rebuilds the preview label's font from the current family, size and style selections so the
    /// user sees the result immediately.
    /// </summary>
    private void UpdatePreview()
    {
        _preview.Font = BuildFont(_result);
        Invalidate();
    }

    /// <summary>
    /// Combines the current dialog selections into a <see cref="Font"/>, falling back to
    /// <paramref name="fallback"/> for any field that cannot be read.
    /// </summary>
    /// <param name="fallback">The font to fall back to for unreadable fields.</param>
    /// <returns>The combined font.</returns>
    private Font BuildFont(Font fallback)
    {
        var family = _familyList.SelectedItem as string ?? fallback.Name;
        float size = ParseSize(_sizeCombo.Text, fallback.Size);

        var style = FontStyle.Regular;
        if (_boldCheck.Checked) style |= FontStyle.Bold;
        if (_italicCheck.Checked) style |= FontStyle.Italic;
        if (_underlineCheck.Checked) style |= FontStyle.Underline;
        if (_strikeoutCheck.Checked) style |= FontStyle.Strikeout;

        return new Font(family, size, style);
    }

    private void CloseDialog(bool ok)
    {
        if (ok)
            _result = BuildFont(_result);

        DialogResult = ok ? DialogResult.OK : DialogResult.Cancel;
        _modal = false;
    }

    /// <summary>
    /// Parses a positive point size from the size field, trying the invariant culture first and the
    /// current culture second, and returning <paramref name="fallback"/> when neither parses.
    /// </summary>
    /// <param name="text">The raw text from the size field.</param>
    /// <param name="fallback">The size to use when the text is not a positive number.</param>
    /// <returns>The parsed point size.</returns>
    private static float ParseSize(string? text, float fallback)
    {
        var trimmed = text?.Trim();
        if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0)
            return value;
        if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.CurrentCulture, out value) && value > 0)
            return value;
        return fallback;
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
