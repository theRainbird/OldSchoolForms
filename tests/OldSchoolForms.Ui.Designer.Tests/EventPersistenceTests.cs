using System.Linq;
using System.Text.Json.Nodes;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Designer.Roslyn;
using OldSchoolForms.Ui.Designer.Serialization;
using Xunit;

namespace OldSchoolForms.Ui.Designer.Tests;

/// <summary>
/// Verifies that event bindings survive both persistence channels: the JSON layout format
/// (save/load round-trip) and the generated C# source (wiring lines, handler stubs, the
/// merge that preserves user-edited bodies, and the parser that reads wiring back).
/// </summary>
public class EventPersistenceTests
{
    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0;
        int index = 0;
        while ((index = haystack.IndexOf(needle, index, System.StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }

    // ---- JSON ----

    [Fact]
    public void SaveToJson_WritesEventsNode()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        surface.BeginDesignForm();
        var button = new Button { Name = "button1", Size = new Size(120, 40) };
        surface.AddControl(button, new Point(20, 20));
        surface.EventBindings.SetHandler(button, "Click", "Button1_Click");

        string json = new JsonDocumentService("App", "MainForm").SaveToJson(surface);
        var doc = JsonNode.Parse(json)!.AsObject();

        var controlNode = doc["controls"]!.AsArray()
            .Select(n => n!.AsObject())
            .Single(n => (string?)n["name"] == "button1");

        var events = controlNode["events"]!.AsObject();
        Assert.Equal("Button1_Click", (string?)events["Click"]);
    }

    [Fact]
    public void JsonRoundTrip_PreservesEventBindings()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        surface.BeginDesignForm();
        var button = new Button { Name = "button1", Size = new Size(120, 40) };
        surface.AddControl(button, new Point(20, 20));
        surface.EventBindings.SetHandler(button, "Click", "Button1_Click");
        surface.EventBindings.SetHandler(button, "DoubleClick", "Button1_DoubleClick");
        surface.EventBindings.SetHandler(surface.RootControl!, "Shown", "Form_Shown");

        string json = new JsonDocumentService("App", "MainForm").SaveToJson(surface);

        var loaded = new DesignSurface { SnapToGrid = false };
        new JsonDocumentService().LoadFromJson(loaded, json);

        var loadedButton = loaded.Items.Select(i => i.Control).OfType<Button>().Single();
        Assert.Equal("Button1_Click", loaded.EventBindings.GetHandler(loadedButton, "Click"));
        Assert.Equal("Button1_DoubleClick", loaded.EventBindings.GetHandler(loadedButton, "DoubleClick"));
        Assert.Equal("Form_Shown", loaded.EventBindings.GetHandler(loaded.RootControl, "Shown"));
    }

    [Fact]
    public void JsonRoundTrip_NoBindings_ProducesNoEventsNode_AndStaysClean()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        surface.BeginDesignForm();
        var button = new Button { Name = "button1", Size = new Size(120, 40) };
        surface.AddControl(button, new Point(20, 20));

        string json = new JsonDocumentService("App", "MainForm").SaveToJson(surface);
        var doc = JsonNode.Parse(json)!.AsObject();
        var controlNode = doc["controls"]!.AsArray()
            .Select(n => n!.AsObject())
            .Single(n => (string?)n["name"] == "button1");

        Assert.Null(controlNode["events"]);

        var loaded = new DesignSurface { SnapToGrid = false };
        new JsonDocumentService().LoadFromJson(loaded, json);
        var loadedButton = loaded.Items.Select(i => i.Control).OfType<Button>().Single();
        Assert.False(loaded.EventBindings.HasBindings(loadedButton));
    }

    // ---- C# code generation ----

    [Fact]
    public void Generate_EmitsWiringAndStub_ForControlBinding()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        surface.BeginDesignForm();
        var button = new Button { Name = "button1", Size = new Size(120, 40) };
        surface.AddControl(button, new Point(20, 20));
        surface.EventBindings.SetHandler(button, "Click", "Button1_Click");

        string code = new CodeGenerator(surface).Generate("Demo", "MainForm");

        Assert.Contains("button1.Click += Button1_Click;", code);
        Assert.Contains("private void Button1_Click(object sender, System.EventArgs e)", code);
    }

    [Fact]
    public void Generate_EmitsRootWiring_AndStub()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        surface.BeginDesignForm();
        surface.EventBindings.SetHandler(surface.RootControl!, "Shown", "Form_Shown");
        surface.EventBindings.SetHandler(surface.RootControl!, "FormClosing", "Form_FormClosing");

        string code = new CodeGenerator(surface).Generate("Demo", "MainForm");

        Assert.Contains("this.Shown += Form_Shown;", code);
        Assert.Contains("this.FormClosing += Form_FormClosing;", code);
        // Both are plain EventHandler on Form, so the stub args are System.EventArgs.
        Assert.Contains("private void Form_Shown(object sender, System.EventArgs e)", code);
        Assert.Contains("private void Form_FormClosing(object sender, System.EventArgs e)", code);
    }

    [Fact]
    public void Generate_ResolvesTypedEventHandlerArgs()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        surface.BeginDesignForm();
        var split = new SplitPanel { Name = "split1", Size = new Size(200, 200) };
        surface.AddControl(split, new Point(10, 10));
        // SplitterMoving is EventHandler<SplitterDragEventArgs>; bind it directly to
        // exercise the generic delegate args resolution in the generator.
        surface.EventBindings.SetHandler(split, "SplitterMoving", "Split_Moving");

        string code = new CodeGenerator(surface).Generate("Demo", "MainForm");

        Assert.Contains("split1.SplitterMoving += Split_Moving;", code);
        Assert.Contains(
            "private void Split_Moving(object sender, OldSchoolForms.Ui.Controls.Containers.SplitterDragEventArgs e)",
            code);
    }

    [Fact]
    public void Generate_NoBindings_EmitsEmptyHandlerRegion()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        surface.BeginDesignForm();
        var button = new Button { Name = "button1", Size = new Size(120, 40) };
        surface.AddControl(button, new Point(20, 20));

        string code = new CodeGenerator(surface).Generate("Demo", "MainForm");

        Assert.Contains("#region Event handlers", code);
        Assert.Contains("#endregion", code);
        Assert.DoesNotContain("private void Button1_Click", code);
    }

    [Fact]
    public void Generate_Merge_PreservesUserBody_NoDuplicateStub()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        surface.BeginDesignForm();
        var button = new Button { Name = "button1", Size = new Size(120, 40) };
        surface.AddControl(button, new Point(20, 20));
        surface.EventBindings.SetHandler(button, "Click", "Button1_Click");

        string existing =
            "class MainForm\n" +
            "{\n" +
            "    #region Event handlers\n" +
            "    private void Button1_Click(object sender, System.EventArgs e)\n" +
            "    {\n" +
            "        // USER BODY MARKER\n" +
            "    }\n" +
            "    #endregion\n" +
            "}\n";

        string code = new CodeGenerator(surface).Generate("Demo", "MainForm", existing);

        Assert.Contains("// USER BODY MARKER", code);
        Assert.Equal(1, CountOccurrences(code, "private void Button1_Click"));
    }

    [Fact]
    public void Generate_Merge_AddsFreshStub_ForNewHandlerNotInRegion()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        surface.BeginDesignForm();
        var button = new Button { Name = "button1", Size = new Size(120, 40) };
        surface.AddControl(button, new Point(20, 20));
        surface.EventBindings.SetHandler(button, "Click", "Button1_Click");
        surface.EventBindings.SetHandler(button, "DoubleClick", "Button1_DoubleClick");

        string existing =
            "class MainForm\n" +
            "{\n" +
            "    #region Event handlers\n" +
            "    private void Button1_Click(object sender, System.EventArgs e)\n" +
            "    {\n" +
            "        // USER BODY MARKER\n" +
            "    }\n" +
            "    #endregion\n" +
            "}\n";

        string code = new CodeGenerator(surface).Generate("Demo", "MainForm", existing);

        // Preserved handler stays single; the new handler gets exactly one fresh stub.
        Assert.Equal(1, CountOccurrences(code, "private void Button1_Click"));
        Assert.Equal(1, CountOccurrences(code, "private void Button1_DoubleClick"));
    }

    // ---- C# parser round-trip ----

    [Fact]
    public void Parse_ReadsControlAndRootWiring()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        surface.BeginDesignForm();
        var button = new Button { Name = "button1", Size = new Size(120, 40) };
        surface.AddControl(button, new Point(20, 20));
        surface.EventBindings.SetHandler(button, "Click", "Button1_Click");
        surface.EventBindings.SetHandler(surface.RootControl!, "Shown", "Form_Shown");

        string code = new CodeGenerator(surface).Generate("Demo", "MainForm");

        var parsed = new DesignSurface { SnapToGrid = false };
        new FormParser().Parse(code, parsed);

        var parsedButton = parsed.Items.Select(i => i.Control).OfType<Button>().Single();
        Assert.Equal("Button1_Click", parsed.EventBindings.GetHandler(parsedButton, "Click"));
        Assert.Equal("Form_Shown", parsed.EventBindings.GetHandler(parsed.RootControl, "Shown"));
    }

    [Fact]
    public void Parse_ClearsStaleBindingsBeforeLoading()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        surface.BeginDesignForm();
        var button = new Button { Name = "button1", Size = new Size(120, 40) };
        surface.AddControl(button, new Point(20, 20));
        surface.EventBindings.SetHandler(button, "Click", "Button1_Click");

        string code = new CodeGenerator(surface).Generate("Demo", "MainForm");

        var parsed = new DesignSurface { SnapToGrid = false };
        parsed.BeginDesignForm();
        var other = new Button { Name = "ghost", Size = new Size(120, 40) };
        parsed.AddControl(other, new Point(5, 5));
        parsed.EventBindings.SetHandler(other, "Click", "Ghost_Handler");

        new FormParser().Parse(code, parsed);

        // The pre-existing ghost binding must be gone after the parse.
        Assert.False(parsed.EventBindings.HasBindings(other));
    }
}
