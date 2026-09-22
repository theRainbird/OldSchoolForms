using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Designer;
using OldSchoolForms.Ui.Designer.Roslyn;
using Xunit;

namespace OldSchoolForms.Ui.Tests;

public class LabelTextAlignTests
{
    [Fact]
    public void TextAlign_Default_IsMiddleLeft()
    {
        var label = new Label();

        Assert.Equal(ContentAlignment.MiddleLeft, label.TextAlign);
    }

    [Fact]
    public void TextAlign_Setter_UpdatesValue()
    {
        var label = new Label();

        label.TextAlign = ContentAlignment.MiddleRight;

        Assert.Equal(ContentAlignment.MiddleRight, label.TextAlign);
    }

    [Fact]
    public void CodeGenerator_EmitsTextAlign_WhenNotDefault()
    {
        var surface = new DesignSurface();
        var label = new Label { Text = "Hello", TextAlign = ContentAlignment.MiddleCenter };
        surface.AddControl(label, new Point(10, 10));

        string code = new CodeGenerator(surface).Generate("TestNs", "TestForm");

        Assert.Contains(".TextAlign = ContentAlignment.MiddleCenter;", code);
    }

    [Fact]
    public void CodeGenerator_OmitsTextAlign_WhenDefault()
    {
        var surface = new DesignSurface();
        var label = new Label { Text = "Hello" };
        surface.AddControl(label, new Point(10, 10));

        string code = new CodeGenerator(surface).Generate("TestNs", "TestForm");

        Assert.DoesNotContain("TextAlign", code);
    }

    [Fact]
    public void RoundTrip_PreservesTextAlign()
    {
        var surface = new DesignSurface();
        var label = new Label { Text = "Hello", TextAlign = ContentAlignment.BottomRight };
        surface.AddControl(label, new Point(10, 10));

        string code = new CodeGenerator(surface).Generate("TestNs", "TestForm");

        var reloaded = new DesignSurface();
        new FormParser().Parse(code, reloaded);

        var parsedLabel = reloaded.Items.Select(i => i.Control).OfType<Label>().Single();

        Assert.Equal(ContentAlignment.BottomRight, parsedLabel.TextAlign);
    }
}
