using System;
using System.Linq;
using System.Reflection;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Design;
using OldSchoolForms.Ui.Designer.Dialogs;
using OldSchoolForms.Ui.Designer.PropertyGrid;
using OldSchoolForms.Ui.Reports;
using Xunit;

namespace OldSchoolForms.Ui.Designer.Tests;

/// <summary>
/// Tests for the <see cref="FontEditor"/>: that it handles <see cref="Font"/>-typed properties for
/// both designer controls and report controls, resolves through <see cref="PropertyService"/>, and
/// that clicking a font value in the grid opens the editor instead of the inline text editor.
/// </summary>
public class FontEditorTests
{
    private sealed class RecordingFontEditor : IValueEditor
    {
        public int OpenCount { get; private set; }
        public object? OpenedTarget { get; private set; }
        public object? OpenedContext { get; private set; }

        public string ButtonText => "…";
        public bool Handles(PropertyInfo property) => property.PropertyType == typeof(Font);
        public void Open(object target)
        {
            OpenCount++;
            OpenedTarget = target;
        }
        public void Open(object target, object? context)
        {
            OpenCount++;
            OpenedTarget = target;
            OpenedContext = context;
        }
    }

    [Fact]
    public void FontEditor_Handles_MatchesFontTypedProperty()
    {
        var editor = new FontEditor();

        Assert.True(editor.Handles(typeof(Control).GetProperty("Font")!));
        Assert.False(editor.Handles(typeof(Control).GetProperty("Location")!));
        Assert.Equal("…", editor.ButtonText);
    }

    [Fact]
    public void FontEditor_ResolvesViaPropertyService_ForControl()
    {
        var service = new PropertyService();
        var descriptor = service.GetProperties(new Panel()).FirstOrDefault(p => p.Name == "Font");

        Assert.NotNull(descriptor);
        Assert.IsType<FontEditor>(descriptor!.Editor);
    }

    [Fact]
    public void FontEditor_ResolvesViaPropertyService_ForReportControl()
    {
        var service = new PropertyService();
        var descriptor = service.GetPropertiesFor(new ReportLabel()).FirstOrDefault(p => p.Name == "Font");

        Assert.NotNull(descriptor);
        Assert.IsType<FontEditor>(descriptor!.Editor);
        Assert.False(descriptor.IsReadOnly);
    }

    [Fact]
    public void PropertyGridRow_ValueClick_OpensFontEditor_WithoutInlineEdit()
    {
        var stub = new RecordingFontEditor();
        var button = new Button();
        var property = typeof(Control).GetProperty("Font")!;
        var descriptor = new PropertyDescriptor(property, button, stub);
        var row = new PropertyGridRow(descriptor, 140);

        // Click inside the value column but to the left of the ellipsis button.
        int valueColumnX = 140;
        int valueColumnWidth = 160;
        int clickX = valueColumnX + 40;

        bool dropdownOpened = row.HandleClick(clickX, 10, valueColumnX, valueColumnWidth);

        Assert.False(dropdownOpened);
        Assert.Equal(1, stub.OpenCount);
        Assert.Same(button, stub.OpenedTarget);
        Assert.Same(descriptor, stub.OpenedContext);
        Assert.False(row.IsEditing);
    }

    [Fact]
    public void PropertyGridRow_EllipsisClick_OpensFontEditor()
    {
        var stub = new RecordingFontEditor();
        var button = new Button();
        var property = typeof(Control).GetProperty("Font")!;
        var descriptor = new PropertyDescriptor(property, button, stub);
        var row = new PropertyGridRow(descriptor, 140);

        int valueColumnX = 140;
        int valueColumnWidth = 160;
        int clickX = valueColumnX + valueColumnWidth - PropertyGridRow.EllipsisButtonWidth + 5;

        bool opened = row.TryHandleEditorClick(clickX, 10, valueColumnX, valueColumnWidth);

        Assert.True(opened);
        Assert.Equal(1, stub.OpenCount);
        Assert.Same(descriptor, stub.OpenedContext);
    }

    [Fact]
    public void PropertyGridRow_ReadOnlyFontClick_DoesNothing()
    {
        var stub = new RecordingFontEditor();
        var button = new Button();
        var property = typeof(Control).GetProperty("Font")!;
        var descriptor = new PropertyDescriptor(property, button, stub, readOnlyOverride: true);
        var row = new PropertyGridRow(descriptor, 140);

        bool dropdownOpened = row.HandleClick(180, 10, 140, 160);

        Assert.False(dropdownOpened);
        Assert.Equal(0, stub.OpenCount);
        Assert.False(row.IsEditing);
    }
}

/// <summary>
/// Tests for the font family search filter used by <see cref="FontEditorDialog"/>: a query matches
/// any part of the family name (case-insensitive), an empty or whitespace query returns every
/// family, non-matching queries yield nothing, and the original ordering is preserved.
/// </summary>
public class FontEditorDialogSearchTests
{
    private static readonly string[] Sample =
        { "Arial", "Arial Black", "Calibri", "Cambria", "Consolas", "Roboto", "Times New Roman" };

    [Fact]
    public void FilterFamilies_NullOrEmptyOrWhitespace_ReturnsAll()
    {
        Assert.Equal(Sample, FontEditorDialog.FilterFamilies(Sample, null));
        Assert.Equal(Sample, FontEditorDialog.FilterFamilies(Sample, string.Empty));
        Assert.Equal(Sample, FontEditorDialog.FilterFamilies(Sample, "   "));
    }

    [Fact]
    public void FilterFamilies_MatchesAnyPartOfName_NotOnlyPrefix()
    {
        // "bri" occurs mid-word in "Cambria" and would be missed by a start-with match.
        var result = FontEditorDialog.FilterFamilies(Sample, "bri");

        Assert.Contains("Cambria", result);
        Assert.DoesNotContain("Arial", result);
    }

    [Fact]
    public void FilterFamilies_IsCaseInsensitive()
    {
        var result = FontEditorDialog.FilterFamilies(Sample, "aRiAl");

        Assert.Contains("Arial", result);
        Assert.Contains("Arial Black", result);
    }

    [Fact]
    public void FilterFamilies_NoMatch_ReturnsEmpty()
    {
        Assert.Empty(FontEditorDialog.FilterFamilies(Sample, "zzzz"));
    }

    [Fact]
    public void FilterFamilies_PreservesOriginalOrder()
    {
        var expected = Sample
            .Where(f => f.IndexOf("a", StringComparison.OrdinalIgnoreCase) >= 0)
            .ToArray();

        Assert.Equal(expected, FontEditorDialog.FilterFamilies(Sample, "a"));
    }
}
