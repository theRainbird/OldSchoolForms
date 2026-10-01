using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Designer.Serialization;
using OldSchoolForms.Ui.Designer.Services;
using Xunit;

namespace OldSchoolForms.Ui.Designer.Tests;

/// <summary>
/// Verifies that a designed control tree survives a save/load round-trip through the
/// <c>OldSchoolForms.Layout</c> JSON format via <see cref="JsonDocumentService"/>:
/// the hierarchy, control properties, tab pages, data grid columns and tool/menu/status
/// strip items are reconstructed on a fresh surface. Also covers stream/file transport,
/// root-type selection, error handling and C# regeneration after a JSON load.
/// </summary>
public class JsonLayoutRoundTripTests
{
    private static DesignSurface BuildRichSurface()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        surface.BeginDesignForm();

        var panel = new Panel { Name = "panel1", Size = new Size(220, 200) };
        surface.AddControl(panel, new Point(30, 40));

        var button = new Button
        {
            Name = "okButton",
            Text = "OK",
            Size = new Size(120, 40),
            BackColor = Color.FromArgb(200, 50, 50),
            ForeColor = Color.White,
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
            Padding = new Padding(2, 3, 4, 5),
            TabIndex = 3,
            TabStop = true
        };
        surface.AddControl(button, panel, new Point(50, 60));

        var label = new Label { Name = "infoLabel", Text = "Hallo", Location = new Point(10, 10), Size = new Size(100, 24) };
        surface.AddControl(label, panel, new Point(10, 10));

        var tabs = new TabControl { Name = "tabs1", Size = new Size(300, 200) };
        surface.AddControl(tabs, new Point(280, 40));
        var pageA = new TabPage { Text = "Seite A" };
        var pageB = new TabPage { Text = "Seite B" };
        tabs.AddTabPage(pageA);
        tabs.AddTabPage(pageB);
        var innerButton = new Button { Name = "innerBtn", Text = "Inner" };
        surface.AddControl(innerButton, pageA, new Point(20, 20));

        var grid = new DataGridView { Name = "grid1", Size = new Size(400, 150) };
        surface.AddControl(grid, new Point(30, 260));
        grid.Columns.Add(new DataGridViewColumn { Name = "id", HeaderText = "ID", Width = 80, ValueType = typeof(int) });
        grid.Columns.Add(new DataGridViewColumn
        {
            Name = "name",
            HeaderText = "Name",
            Width = 150,
            CellEditType = DataGridViewColumnEditType.ComboBox,
            Items = new System.Collections.Generic.List<object> { "a", "b", "c" }
        });

        var strip = new ToolStrip { Name = "strip1", Size = new Size(300, 29) };
        surface.AddControl(strip, new Point(30, 430));
        var toolButton = new ToolStripButton { Text = "Öffnen" };
        var menuButton = new ToolStripButton { Text = "Extras" };
        menuButton.DropDownItems.Add(new ToolStripMenuItem { Text = "Punkt 1" });
        menuButton.DropDownItems.Add(new ToolStripMenuItem { Text = "Punkt 2" });
        strip.Items.Add(toolButton);
        strip.Items.Add(menuButton);

        var status = new StatusStrip { Name = "status1", Size = new Size(300, 25) };
        surface.AddControl(status, new Point(30, 470));
        status.Items.Add(new ToolStripStatusLabel { Text = "Bereit" });

        return surface;
    }

    [Fact]
    public void SaveToJson_ProducesFormatEnvelope()
    {
        var surface = BuildRichSurface();
        var service = new JsonDocumentService("My.App", "MainForm");

        string json = service.SaveToJson(surface);
        var doc = JsonNode.Parse(json)!.AsObject();

        Assert.Equal("OldSchoolForms.Layout", (string?)doc["format"]);
        Assert.Equal(1, (int?)doc["version"]);
        Assert.Equal("Form", (string?)doc["rootType"]);
        Assert.Equal("My.App", (string?)doc["namespace"]);
        Assert.Equal("MainForm", (string?)doc["className"]);
        Assert.NotNull(doc["controls"]);
    }

    [Fact]
    public void RoundTrip_PreservesHierarchyAndProperties()
    {
        var surface = BuildRichSurface();
        var service = new JsonDocumentService("My.App", "MainForm");
        string json = service.SaveToJson(surface);
        var originalPanel = surface.Items.Select(i => i.Control).OfType<Panel>().Single();
        Point expectedPanelLocation = originalPanel.Location;

        var loaded = new DesignSurface { SnapToGrid = false };
        var warnings = service.LoadFromJson(loaded, json);

        var panel = loaded.Items.Select(i => i.Control).OfType<Panel>().Single();
        Assert.Equal("panel1", panel.Name);
        Assert.Equal(expectedPanelLocation, panel.Location);

        var button = panel.Controls.OfType<Button>().Single();
        Assert.Equal("okButton", button.Name);
        Assert.Equal("OK", button.Text);
        Assert.Equal(Color.FromArgb(200, 50, 50), button.BackColor);
        Assert.Equal(Color.White, button.ForeColor);
        Assert.Equal(AnchorStyles.Top | AnchorStyles.Left, button.Anchor);
        Assert.Equal(new Padding(2, 3, 4, 5), button.Padding);
        Assert.Equal(3, button.TabIndex);
        Assert.True(button.TabStop);
        Assert.Same(panel, button.Parent);

        var label = panel.Controls.OfType<Label>().Single();
        Assert.Equal("Hallo", label.Text);
    }

    [Fact]
    public void RoundTrip_PreservesTabPages()
    {
        var surface = BuildRichSurface();
        var service = new JsonDocumentService();
        string json = service.SaveToJson(surface);

        var loaded = new DesignSurface { SnapToGrid = false };
        service.LoadFromJson(loaded, json);

        var tabs = loaded.Items.Select(i => i.Control).OfType<TabControl>().Single();
        Assert.Equal(2, tabs.TabPages.Count);
        Assert.Equal("Seite A", tabs.TabPages[0].Text);
        Assert.Equal("Seite B", tabs.TabPages[1].Text);

        var inner = tabs.TabPages[0].Controls.OfType<Button>().Single();
        Assert.Equal("innerBtn", inner.Name);
    }

    [Fact]
    public void RoundTrip_PreservesDataGridViewColumns()
    {
        var surface = BuildRichSurface();
        var service = new JsonDocumentService();
        string json = service.SaveToJson(surface);

        var loaded = new DesignSurface { SnapToGrid = false };
        service.LoadFromJson(loaded, json);

        var grid = loaded.Items.Select(i => i.Control).OfType<DataGridView>().Single();
        Assert.Equal(2, grid.Columns.Count);

        var idCol = grid.Columns[0];
        Assert.Equal("id", idCol.Name);
        Assert.Equal("ID", idCol.HeaderText);
        Assert.Equal(80, idCol.Width);
        Assert.Equal(typeof(int), idCol.ValueType);

        var nameCol = grid.Columns[1];
        Assert.Equal("name", nameCol.Name);
        Assert.Equal(DataGridViewColumnEditType.ComboBox, nameCol.CellEditType);
        Assert.NotNull(nameCol.Items);
        Assert.Equal(3, nameCol.Items!.Count);
        Assert.Contains("b", nameCol.Items.Select(o => (string)o));
    }

    [Fact]
    public void RoundTrip_PreservesToolStripItemsAndDropDowns()
    {
        var surface = BuildRichSurface();
        var service = new JsonDocumentService();
        string json = service.SaveToJson(surface);

        var loaded = new DesignSurface { SnapToGrid = false };
        service.LoadFromJson(loaded, json);

        var strip = loaded.Items.Select(i => i.Control).OfType<ToolStrip>().Single();
        Assert.Equal(2, strip.Items.Count);
        Assert.Equal("Öffnen", strip.Items[0].Text);

        var extras = strip.Items[1] as ToolStripButton;
        Assert.NotNull(extras);
        Assert.Equal("Extras", extras!.Text);
        Assert.Equal(2, extras.DropDownItems.Count);
        Assert.Equal("Punkt 1", extras.DropDownItems[0].Text);
        Assert.Equal("Punkt 2", extras.DropDownItems[1].Text);
    }

    [Fact]
    public void RoundTrip_PreservesStatusStripLabels()
    {
        var surface = BuildRichSurface();
        var service = new JsonDocumentService();
        string json = service.SaveToJson(surface);

        var loaded = new DesignSurface { SnapToGrid = false };
        service.LoadFromJson(loaded, json);

        var status = loaded.Items.Select(i => i.Control).OfType<StatusStrip>().Single();
        Assert.Single(status.Items);
        Assert.Equal("Bereit", status.Items[0].Text);
    }

    [Fact]
    public void RoundTrip_Stream_TransportsUtf8Json()
    {
        var surface = BuildRichSurface();
        var service = new JsonDocumentService("Stream.App", "StreamForm");

        using var stream = new MemoryStream();
        service.Save(surface, stream);
        Assert.True(stream.Length > 0);

        stream.Position = 0;
        var loaded = new DesignSurface { SnapToGrid = false };
        service.Load(loaded, stream);

        Assert.IsType<DesignForm>(loaded.RootControl);
        Assert.Equal("Stream.App", service.Namespace);
        Assert.Equal("StreamForm", service.ClassName);
        Assert.Contains(loaded.Items.Select(i => i.Control), c => c is Button b && b.Name == "okButton");
    }

    [Fact]
    public void RoundTrip_File_TransportsUtf8Json()
    {
        var surface = BuildRichSurface();
        var service = new JsonDocumentService("File.App", "FileForm");
        string path = Path.Combine(Path.GetTempPath(), $"osf_layout_{Guid.NewGuid():N}.json");

        try
        {
            service.SaveToFile(surface, path);
            Assert.True(File.Exists(path));

            var loaded = new DesignSurface { SnapToGrid = false };
            service.LoadFromFile(loaded, path);

            Assert.Contains(loaded.Items.Select(i => i.Control), c => c is TabControl);
            Assert.Equal("File.App", service.Namespace);
            Assert.Equal("FileForm", service.ClassName);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void RoundTrip_UserControlRoot_IsRestoredAsUserControl()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        surface.BeginDesignUserControl();
        surface.AddControl(new Button { Name = "b1", Text = "X" }, new Point(10, 10));

        var service = new JsonDocumentService();
        string json = service.SaveToJson(surface);
        Assert.Contains("UserControl", json);

        var loaded = new DesignSurface { SnapToGrid = false };
        service.LoadFromJson(loaded, json);
        Assert.IsType<DesignUserControl>(loaded.RootControl);
    }

    [Fact]
    public void LoadFromJson_MalformedJson_ThrowsInvalidOperation()
    {
        var service = new JsonDocumentService();
        var loaded = new DesignSurface { SnapToGrid = false };

        Assert.Throws<InvalidOperationException>(() => service.LoadFromJson(loaded, "{ not valid json "));
    }

    [Fact]
    public void LoadFromJson_UnknownType_AddsWarningAndSkips()
    {
        string json = """
            {
              "format": "OldSchoolForms.Layout",
              "version": 1,
              "rootType": "Form",
              "namespace": "X",
              "className": "Y",
              "controls": [
                { "type": "NoSuchControl", "name": "ghost" }
              ]
            }
            """;

        var service = new JsonDocumentService();
        var loaded = new DesignSurface { SnapToGrid = false };
        var warnings = service.LoadFromJson(loaded, json);

        Assert.Contains(warnings, w => w.Contains("NoSuchControl"));
        Assert.DoesNotContain(loaded.Items.Select(i => i.Control), c => c.Name == "ghost");
    }

    [Fact]
    public void LoadFromJson_UnknownProperty_AddsWarning()
    {
        string json = """
            {
              "format": "OldSchoolForms.Layout",
              "version": 1,
              "rootType": "Form",
              "controls": [
                { "type": "Button", "name": "b1", "props": { "totallyBogus": 1 } }
              ]
            }
            """;

        var service = new JsonDocumentService();
        var loaded = new DesignSurface { SnapToGrid = false };
        var warnings = service.LoadFromJson(loaded, json);

        Assert.Contains(warnings, w => w.Contains("totallyBogus"));
    }

    [Fact]
    public void LoadFromJson_RestoresNamespaceAndClassName()
    {
        string json = """
            {
              "format": "OldSchoolForms.Layout",
              "version": 1,
              "rootType": "Form",
              "namespace": "Restored.App",
              "className": "RestoredForm",
              "controls": []
            }
            """;

        var service = new JsonDocumentService();
        var loaded = new DesignSurface { SnapToGrid = false };
        service.LoadFromJson(loaded, json);

        Assert.Equal("Restored.App", service.Namespace);
        Assert.Equal("RestoredForm", service.ClassName);
    }

    [Fact]
    public void LoadJson_ThenGenerateCSharp_ProducesValidCode()
    {
        var surface = BuildRichSurface();
        var service = new JsonDocumentService("Gen.App", "GenForm");
        string json = service.SaveToJson(surface);

        var loaded = new DesignSurface { SnapToGrid = false };
        service.LoadFromJson(loaded, json);

        string code = new Roslyn.CodeGenerator(loaded).Generate(service.Namespace, service.ClassName);

        Assert.Contains("partial class GenForm", code);
        Assert.Contains("namespace Gen.App", code);
        Assert.Contains("new Button", code);
        Assert.Contains("new Panel", code);
        Assert.Contains("TabPages.Add", code);
        Assert.Contains("Columns.Add(new DataGridViewColumn", code);
    }

    [Fact]
    public void SaveToJson_EmptySurface_ProducesValidEmptyDocument()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        surface.BeginDesignForm();
        var service = new JsonDocumentService();

        string json = service.SaveToJson(surface);
        var doc = JsonNode.Parse(json)!.AsObject();
        Assert.Empty((JsonArray?)doc["controls"] ?? new JsonArray());

        var loaded = new DesignSurface { SnapToGrid = false };
        var warnings = service.LoadFromJson(loaded, json);
        Assert.IsType<DesignForm>(loaded.RootControl);
        Assert.Single(loaded.Items); // only the root item
    }
}
