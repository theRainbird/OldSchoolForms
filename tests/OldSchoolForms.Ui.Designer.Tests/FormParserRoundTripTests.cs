using System.Linq;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Designer;
using OldSchoolForms.Ui.Designer.Roslyn;
using Xunit;

namespace OldSchoolForms.Ui.Designer.Tests;

/// <summary>
/// Verifies that code produced by <see cref="CodeGenerator"/> can be parsed back onto a
/// fresh design surface via <see cref="FormParser"/>, reconstructing every control and its
/// hierarchy. This guards the round-trip the designer relies on when opening a previously
/// generated form file (previously the surface stayed empty because the root was detached
/// by <see cref="DesignSurface.ClearAll"/> without being re-established).
/// </summary>
public class FormParserRoundTripTests
{
    private static string BuildGeneratedForm()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        var panel = new Panel { Size = new Size(200, 200) };
        surface.AddControl(panel, new Point(30, 30));

        var button = new Button { Text = "OK", Size = new Size(120, 40) };
        surface.AddControl(button, panel, new Point(40, 40));

        return new CodeGenerator(surface).Generate("Demo", "MainForm");
    }

    [Fact]
    public void Parse_GeneratedForm_ReconstructsControlsAndHierarchy()
    {
        string code = BuildGeneratedForm();

        var loaded = new DesignSurface { SnapToGrid = false };
        new FormParser().Parse(code, loaded);

        // The root must be a fresh form so the reconstructed tree is part of the render
        // tree; otherwise the surface stays empty after ClearAll.
        Assert.IsType<DesignForm>(loaded.RootControl);

        var panel = loaded.Items.Select(i => i.Control).FirstOrDefault(c => c is Panel) as Panel;
        Assert.NotNull(panel);

        // The button placed inside the panel is reconstructed as a child of that same
        // panel. Nested controls are not registered as top-level design items, so they are
        // found through their container's control collection.
        var button = panel!.Controls.OfType<Button>().FirstOrDefault();
        Assert.NotNull(button);
        Assert.Same(panel, button!.Parent);
        Assert.Same(loaded.RootControl, panel.Parent);
    }

    [Fact]
    public void Parse_GeneratedForm_NestedControlIsSelectable()
    {
        string code = BuildGeneratedForm();

        var loaded = new DesignSurface { SnapToGrid = false };
        new FormParser().Parse(code, loaded);

        var panel = loaded.Items.Select(i => i.Control).FirstOrDefault(c => c is Panel) as Panel;
        Assert.NotNull(panel);
        var button = panel!.Controls.OfType<Button>().Single();

        // The nested button must have a design item so the selection machinery (which
        // resolves the deepest child via FindItem) can select it. Before the fix nested
        // controls were never registered as design items and could not be selected.
        var item = loaded.FindItem(button);
        Assert.NotNull(item);
        Assert.Same(button, item!.Control);

        // The full hit-test path must return the button when clicking inside the panel.
        var surfacePoint = loaded.PointToClient(button.PointToScreen(Point.Empty));
        var hit = loaded.FindItemAt(surfacePoint);
        Assert.NotNull(hit);
        Assert.Same(button, hit!.Control);
    }

    [Fact]
    public void Parse_UserControlSource_UsesUserControlRoot()
    {
        const string source = @"
namespace Demo
{
    partial class MyControl : ContainerControl
    {
        private void InitializeComponent()
        {
            var button = new Button();
            button.Text = ""OK"";
            button.Location = new Point(10, 10);
            button.Size = new Size(100, 30);
            Controls.Add(button);
        }
    }
}";
        var loaded = new DesignSurface { SnapToGrid = false };
        new FormParser().Parse(source, loaded);

        Assert.IsType<DesignUserControl>(loaded.RootControl);

        var button = loaded.Items.Select(i => i.Control).FirstOrDefault(c => c is Button);
        Assert.NotNull(button);
        Assert.Same(loaded.RootControl, button!.Parent);
    }
}
