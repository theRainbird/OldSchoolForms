using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Designer.Reports;
using OldSchoolForms.Ui.Reports;
using OldSchoolForms.Ui.Rendering;
using Xunit;

namespace OldSchoolForms.Ui.Designer.Tests;

/// <summary>
/// Regression coverage for the designer-only dotted light-gray bounds border that
/// makes control dimensions visible on the report designer surface even when the
/// control is not selected, and for the shortened German group band captions.
/// </summary>
public class ReportDesignerDottedBorderTests
{
    private static (DesignSurface Surface, DesignReport Root, Report Report) CreateReportSurface()
    {
        var report = new Report("TestReport");
        var surface = new DesignSurface { SnapToGrid = false };
        surface.BeginDesignReport(report);
        var root = Assert.IsType<DesignReport>(surface.RootControl);
        return (surface, root, report);
    }

    private static List<DrawCommand> DottedLightGrayLines(Graphics g) =>
        g.GetCommands()
            .Where(c => c.Type == DrawCommandType.DrawLine
                       && c.DashPattern != null
                       && c.Color == Color.LightGray)
            .ToList();

    [Fact]
    public void UnselectedProxy_Render_DrawsDottedLightGrayBorderAtBounds()
    {
        var (surface, root, report) = CreateReportSurface();
        report.Detail.Height = 3.0;
        root.RefreshGeometry();
        var detail = root.GetBandContainer(report.Detail)!;

        var label = new DesignReportLabel();
        surface.AddControl(label, detail, new Point(10, 5));
        label.Bounds = new Rectangle(10, 5, 120, 24);
        surface.Selection.DeselectAll();

        var g = new Graphics();
        label.Render(g);

        var border = DottedLightGrayLines(g);
        Assert.Equal(4, border.Count);

        var sides = new HashSet<string>(border.Select(c =>
            string.Format(CultureInfo.InvariantCulture, "{0}|{1}|{2}|{3}", c.X, c.Y, c.X2, c.Y2)));
        Assert.Contains("0.5|0.5|119.5|0.5", sides);
        Assert.Contains("119.5|0.5|119.5|23.5", sides);
        Assert.Contains("119.5|23.5|0.5|23.5", sides);
        Assert.Contains("0.5|23.5|0.5|0.5", sides);

        foreach (var line in border)
            Assert.Equal(new[] { 1f, 2f }, line.DashPattern);
    }

    [Fact]
    public void SelectedProxy_Render_StillDrawsDottedBorder()
    {
        var (surface, root, report) = CreateReportSurface();
        report.Detail.Height = 3.0;
        root.RefreshGeometry();
        var detail = root.GetBandContainer(report.Detail)!;

        var label = new DesignReportLabel();
        surface.AddControl(label, detail, new Point(10, 5));

        // The proxy is auto-selected by AddControl; the border must still be drawn.
        Assert.True(surface.Selection.IsSelected(surface.FindItem(label)!));

        var g = new Graphics();
        label.Render(g);

        Assert.Equal(4, DottedLightGrayLines(g).Count);
    }

    [Fact]
    public void ImageProxy_WithAndWithoutImage_AlwaysDrawsDottedBorder()
    {
        var (surface, root, report) = CreateReportSurface();
        report.Detail.Height = 3.0;
        root.RefreshGeometry();
        var detail = root.GetBandContainer(report.Detail)!;

        var image = new DesignReportImage();
        surface.AddControl(image, detail, new Point(10, 5));

        var g = new Graphics();
        image.Render(g);

        // Placeholder dashes use a different color/dash; the border is the
        // LightGray {1,2} set and must be present regardless of image content.
        Assert.Equal(4, DottedLightGrayLines(g).Count);
    }

    [Fact]
    public void ReportRenderEngine_Output_ContainsNoDesignerBorder()
    {
        var report = new Report("EngineReport");
        report.Detail.Controls.Add(new ReportLabel { Text = "Hello" });
        report.Detail.Controls.Add(new ReportTextBox { DataField = "Name" });

        var pages = new ReportRenderEngine().Render(report, 96f);

        Assert.NotEmpty(pages);
        foreach (var page in pages)
        {
            Assert.DoesNotContain(page.Graphics.GetCommands(),
                c => c.Type == DrawCommandType.DrawLine
                   && c.DashPattern != null
                   && c.Color == Color.LightGray);
        }
    }

    [Fact]
    public void GetBandDisplayName_GermanCulture_ReturnsShortenedGroupCaptions()
    {
        var previous = Thread.CurrentThread.CurrentUICulture;
        try
        {
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("de");

            var header = new ReportGroup("Kunde", "KundenGruppe").Header;
            var footer = new ReportGroup("Kunde", "KundenGruppe").Footer;

            Assert.Equal("Gr. Kopf", DesignReport.GetBandDisplayName(header));
            Assert.Equal("Gr. Fuß", DesignReport.GetBandDisplayName(footer));
        }
        finally
        {
            Thread.CurrentThread.CurrentUICulture = previous;
        }
    }
}
