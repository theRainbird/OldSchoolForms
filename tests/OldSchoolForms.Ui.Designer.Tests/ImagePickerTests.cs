using System;
using System.Linq;
using System.Reflection;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Designer.Dialogs;
using OldSchoolForms.Ui.Designer.PropertyGrid;
using OldSchoolForms.Ui.Designer.Roslyn;
using Xunit;

namespace OldSchoolForms.Ui.Designer.Tests;

/// <summary>
/// Verifies the image-picker feature: the <see cref="ToolStripItemImageEditor"/> targets the
/// <see cref="ToolStripItem.Image"/> property, imported images round-trip through Base64
/// factories, and the code generator/parser emit and read the imported-image forms.
/// </summary>
public class ImagePickerTests
{
    private const string SampleSvg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"24\" height=\"24\"><rect width=\"24\" height=\"24\" fill=\"red\"/></svg>";

    [Fact]
    public void Editor_Handles_ToolStripItemImage()
    {
        var editor = new ToolStripItemImageEditor();
        var prop = typeof(ToolStripButton).GetProperty(nameof(ToolStripItem.Image))!;

        Assert.True(editor.Handles(prop));
    }

    [Fact]
    public void Editor_Handles_NotForOtherProperties()
    {
        var editor = new ToolStripItemImageEditor();
        var textProp = typeof(ToolStripButton).GetProperty(nameof(ToolStripItem.Text))!;
        var otherProp = typeof(string).GetProperty(nameof(string.Length))!;

        Assert.False(editor.Handles(textProp));
        Assert.False(editor.Handles(otherProp));
    }

    [Fact]
    public void SvgImage_FromSvgBase64_PreservesImportedBase64()
    {
        var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(SampleSvg));

        var image = SvgImage.FromSvgBase64(base64, 24);

        Assert.NotNull(image);
        Assert.Equal(base64, image!.ImportedBase64);
        Assert.Null(image.ResourceName);
        Assert.Equal(24, image.DefaultSize);
    }

    [Fact]
    public void SvgImage_FromSvgResource_LeavesImportedBase64Null()
    {
        var image = SvgImage.FromSvgResource("OldSchoolForms.Ui.Resources.Icons.SvgRepo.picture.svg", 24);

        Assert.NotNull(image);
        Assert.Null(image!.ImportedBase64);
        Assert.NotNull(image.ResourceName);
    }

    [Fact]
    public void RasterImage_FromBase64_PreservesImportedBase64()
    {
        // A minimal valid 1x1 PNG.
        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        var base64 = Convert.ToBase64String(png);

        var image = RasterImage.FromBase64(base64);

        Assert.NotNull(image);
        Assert.Equal(base64, image!.ImportedBase64);
        Assert.Equal(1, image.Width);
        Assert.Equal(1, image.Height);
    }

    [Fact]
    public void RasterImage_FromBase64_InvalidInput_ReturnsNull()
    {
        Assert.Null(RasterImage.FromBase64("not-valid-base64!!!"));
        Assert.Null(RasterImage.FromBase64(""));
    }

    [Fact]
    public void CodeGenerator_ImportedSvgImage_EmitsFromSvgBase64()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        var strip = new ToolStrip { Name = "toolStrip1", Size = new Size(300, 29) };
        var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(SampleSvg));
        strip.Items.Add(new ToolStripButton { Text = "Btn", Image = SvgImage.FromSvgBase64(base64, 24) });
        surface.AddControl(strip, new Point(0, 0));

        string code = new CodeGenerator(surface).Generate("Demo", "MainForm");

        Assert.Contains($"Image = SvgImage.FromSvgBase64(\"{base64}\", 24)", code);
    }

    [Fact]
    public void CodeGenerator_ImportedRasterImage_EmitsFromBase64()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        var strip = new ToolStrip { Name = "toolStrip1", Size = new Size(300, 29) };
        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        var base64 = Convert.ToBase64String(png);
        strip.Items.Add(new ToolStripButton { Text = "Btn", Image = RasterImage.FromBase64(base64) });
        surface.AddControl(strip, new Point(0, 0));

        string code = new CodeGenerator(surface).Generate("Demo", "MainForm");

        Assert.Contains($"Image = RasterImage.FromBase64(\"{base64}\")", code);
    }

    [Fact]
    public void RoundTrip_ImportedSvgImage_StableAcrossRegenerate()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        var strip = new ToolStrip { Name = "toolStrip1", Size = new Size(300, 29) };
        var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(SampleSvg));
        strip.Items.Add(new ToolStripButton { Text = "Btn", Image = SvgImage.FromSvgBase64(base64, 24) });
        surface.AddControl(strip, new Point(0, 0));
        string first = new CodeGenerator(surface).Generate("Demo", "MainForm");

        var loaded = new DesignSurface { SnapToGrid = false };
        new FormParser().Parse(first, loaded);
        string second = new CodeGenerator(loaded).Generate("Demo", "MainForm");

        Assert.Equal(first.Replace("\r", "").Trim(), second.Replace("\r", "").Trim());
    }

    [Fact]
    public void Parse_ImportedRasterImage_ReconstructsImage()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        var strip = new ToolStrip { Name = "toolStrip1", Size = new Size(300, 29) };
        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        var base64 = Convert.ToBase64String(png);
        strip.Items.Add(new ToolStripButton { Text = "Btn", Image = RasterImage.FromBase64(base64) });
        surface.AddControl(strip, new Point(0, 0));
        string code = new CodeGenerator(surface).Generate("Demo", "MainForm");

        var loaded = new DesignSurface { SnapToGrid = false };
        new FormParser().Parse(code, loaded);

        var parsedStrip = loaded.Items.Select(i => i.Control).OfType<ToolStrip>().Single();
        var parsedImage = parsedStrip.Items[0].Image as RasterImage;
        Assert.NotNull(parsedImage);
        Assert.Equal(base64, parsedImage!.ImportedBase64);
    }

    [Fact]
    public void ImageResourceListView_ShortName_StripsNamespaceAndExtension()
    {
        Assert.Equal("picture", ImageResourceListView.ShortName("OldSchoolForms.Ui.Resources.Icons.SvgRepo.picture.svg"));
        Assert.Equal("ic_fluent_image_24_color", ImageResourceListView.ShortName("OldSchoolForms.Ui.Resources.Icons.FluentColor.ic_fluent_image_24_color.svg"));
    }
}
