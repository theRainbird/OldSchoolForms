using System.Linq;
using System.Reflection;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Designer;
using OldSchoolForms.Ui.Designer.PropertyGrid;
using OldSchoolForms.Ui.Designer.Roslyn;
using OldSchoolForms.Ui.Rendering;
using OldSchoolForms.Ui.Theming;
using Xunit;

namespace OldSchoolForms.Ui.Designer.Tests;

/// <summary>
/// Tests for the <see cref="PaddingEditor"/> (handles/registration/codegen round-trip) and for the
/// inline text editor's clipboard operations and selection rendering in the property grid.
/// </summary>
public class PaddingAndClipboardTests
{
    [Fact]
    public void PaddingEditor_Handles_MatchesPaddingTypedProperty()
    {
        var editor = new PaddingEditor();

        Assert.True(editor.Handles(typeof(Control).GetProperty("Padding")!));
        Assert.False(editor.Handles(typeof(Control).GetProperty("Location")!));
        Assert.Equal("…", editor.ButtonText);
    }

    [Fact]
    public void PaddingEditor_ResolvesViaPropertyService()
    {
        var service = new PropertyService();
        var descriptor = service.GetProperties(new Panel()).FirstOrDefault(p => p.Name == "Padding");

        Assert.NotNull(descriptor);
        Assert.IsType<PaddingEditor>(descriptor!.Editor);
    }

    [Fact]
    public void Padding_CodeGen_Parse_RoundTrip()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        var panel = new Panel { Size = new Size(200, 200), Padding = new Padding(1, 2, 3, 4) };
        surface.AddControl(panel, new Point(30, 30));

        string code = new CodeGenerator(surface).Generate("Demo", "MainForm");
        Assert.Contains("new Padding(1, 2, 3, 4)", code);

        var loaded = new DesignSurface { SnapToGrid = false };
        new FormParser().Parse(code, loaded);

        var parsed = loaded.Items.Select(i => i.Control).OfType<Panel>().First();
        Assert.Equal(new Padding(1, 2, 3, 4), parsed.Padding);
    }

    [Fact]
    public void CutSelectedToClipboard_RemovesSelectionFromBuffer()
    {
        var row = CreateEditRow("HelloWorld", 0, 5);

        row.CutSelectedToClipboard();

        Assert.Equal("World", GetEditBuffer(row));
        Assert.Equal(0, GetCaret(row));
    }

    [Fact]
    public void CopySelectedToClipboard_LeavesBufferUnchanged()
    {
        var row = CreateEditRow("HelloWorld", 0, 5);

        row.CopySelectedToClipboard();

        Assert.Equal("HelloWorld", GetEditBuffer(row));
    }

    [Fact]
    public void PasteFromClipboard_EmptyClipboard_IsNoOp()
    {
        var row = CreateEditRow("HelloWorld", 5, 5);

        if (string.IsNullOrEmpty(Clipboard.GetText()))
        {
            row.PasteFromClipboard();
            Assert.Equal("HelloWorld", GetEditBuffer(row));
        }
    }

    [Fact]
    public void SelectionRendering_DrawsSelectedTextWithHighlightText()
    {
        var row = CreateEditRow("HelloWorld", 0, 5);
        var theme = ThemeManager.CurrentTheme;

        var g = new Graphics();
        row.Render(g, 0, 300, 26, theme);
        var cmds = g.GetCommands();

        Assert.Contains(cmds, c => c.Type == DrawCommandType.DrawString && c.Text == "Hello" && c.Color == theme.HighlightText);
    }

    private static PropertyGridRow CreateEditRow(string buffer, int caret, int anchor)
    {
        var button = new Button { Text = buffer };
        var descriptor = new PropertyDescriptor(typeof(Button).GetProperty("Text")!, button);
        var row = new PropertyGridRow(descriptor, 140);
        row.StartEdit();
        SetEditBuffer(row, buffer);
        SetAnchor(row, anchor);
        SetCaret(row, caret);
        return row;
    }

    private static void SetEditBuffer(PropertyGridRow row, string value)
        => typeof(PropertyGridRow).GetField("_editBuffer", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(row, value);

    private static string GetEditBuffer(PropertyGridRow row)
        => (string)typeof(PropertyGridRow).GetField("_editBuffer", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(row)!;

    private static void SetCaret(PropertyGridRow row, int value)
        => typeof(PropertyGridRow).GetField("_caretPos", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(row, value);

    private static int GetCaret(PropertyGridRow row)
        => (int)typeof(PropertyGridRow).GetField("_caretPos", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(row)!;

    private static void SetAnchor(PropertyGridRow row, int value)
        => typeof(PropertyGridRow).GetField("_anchor", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(row, value);

    private static int GetAnchor(PropertyGridRow row)
        => (int)typeof(PropertyGridRow).GetField("_anchor", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(row)!;
}
