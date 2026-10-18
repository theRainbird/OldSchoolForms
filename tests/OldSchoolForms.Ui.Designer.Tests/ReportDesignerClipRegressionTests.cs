using System.Reflection;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Designer;
using OldSchoolForms.Ui.Designer.Reports;
using OldSchoolForms.Ui.Designer.Roslyn;
using SkiaSharp;
using Xunit;
using GC = OldSchoolForms.Ui.Rendering.Graphics;
using GCCmd = OldSchoolForms.Ui.Rendering.DrawCommand;
using SkiaFontRenderer = OldSchoolForms.Ui.Rendering.SkiaFontRenderer;

namespace OldSchoolForms.Ui.Designer.Tests;

/// <summary>
/// Regression tests for the report designer rendering pipeline. Each test replays the
/// full designer window (toolbox, design surface, band containers, property grid) the
/// same way the platform renderer does and verifies that the produced command lists
/// remain paintable: text commands must keep non-empty clip regions and rasterize to
/// visible pixels. Guards against clip-region leaks that silently swallow text drawn
/// after a proxy render (the "empty property grid / missing label caption" defect).
/// </summary>
public class ReportDesignerClipRegressionTests
{
    /// <summary>
    /// Places the designer window into the reported bug scene: a report document with a
    /// resized detail band and two placed labels, the first label selected.
    /// </summary>
    /// <returns>The replayed designer form.</returns>
    private static DesignerForm BuildBugScene()
    {
        var form = new DesignerForm(new RoslynDocumentService());
        var surface = (DesignSurface)typeof(DesignerForm)
            .GetField("_designSurface", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(form)!;

        surface.ClearAll();
        ReportDocumentKind.EnsureRegistered();
        ReportDocumentKind.Instance.CreateNew(surface);
        typeof(DesignerForm).GetMethod("ApplyToolboxForDocument", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(form, new object[] { true });

        var root = (DesignReport)surface.RootControl;
        var detail = root.GetBandContainer(root.Report.Detail)!;
        detail.Bounds = new Rectangle(detail.X, detail.Y, detail.Width, 200);

        var label1 = new DesignReportLabel();
        surface.AddControl(label1, detail, new Point(16, 5));
        var label2 = new DesignReportLabel();
        surface.AddControl(label2, detail, new Point(16, 64));
        surface.Selection.Select(surface.FindItem(label1)!);

        return form;
    }

    /// <summary>
    /// After placing two report labels and selecting the first one, every property-grid
    /// text command produced by a full window render must keep a non-empty clip region
    /// and the rasterized grid zone must contain visible text pixels.
    /// </summary>
    [Fact]
    public void FullFormRender_PlaceTwoLabels_GridTextsRemainVisible()
    {
        var form = BuildBugScene();
        var width = Math.Max(1, form.Width);
        var height = Math.Max(1, form.Height);

        var g = new GC();
        g.Zoom = form.Zoom;
        form.Render(g);
        var cmds = g.GetCommands();

        var zoneX = (int)(width * 0.7f);
        var gridTexts = cmds
            .Where(c => c.Type == OldSchoolForms.Ui.Rendering.DrawCommandType.DrawString
                        && c.X >= zoneX && c.Font != null && !string.IsNullOrEmpty(c.Text))
            .ToList();

        Assert.True(gridTexts.Count >= 20, $"expected at least 20 property-grid text commands, got {gridTexts.Count}");
        Assert.DoesNotContain(gridTexts, t => t.ClipBounds.HasValue &&
            (t.ClipBounds.Value.Width <= 0 || t.ClipBounds.Value.Height <= 0));

        var (rightTextPixels, _) = Rasterize(width, height, cmds, zoneX);
        Assert.True(rightTextPixels > 1000, $"expected visible text pixels in the property-grid zone, got {rightTextPixels}");
    }

    /// <summary>
    /// After the same scene, the design surface must still rasterize the caption of both
    /// placed labels in the left zone (outside the property-grid zone).
    /// </summary>
    [Fact]
    public void FullFormRender_PlaceTwoLabels_SurfaceCaptionsRemainVisible()
    {
        var form = BuildBugScene();

        var g = new GC();
        g.Zoom = form.Zoom;
        form.Render(g);
        var cmds = g.GetCommands();

        int zoneX = (int)(form.Width * 0.7f);
        var captions = cmds
            .Where(c => c.Type == OldSchoolForms.Ui.Rendering.DrawCommandType.DrawString
                        && c.Text == "Beschriftung" && c.X < zoneX && c.Font != null)
            .ToList();

        Assert.Equal(2, captions.Count);
        Assert.DoesNotContain(captions, t => t.ClipBounds.HasValue &&
            (t.ClipBounds.Value.Width <= 0 || t.ClipBounds.Value.Height <= 0));

        var (rightPixels, leftPixels) = Rasterize(Math.Max(1, form.Width), Math.Max(1, form.Height), cmds, zoneX);
        Assert.True(leftPixels > 500, $"expected visible text pixels on the design surface, got {leftPixels}");
        Assert.True(rightPixels > 1000, $"expected visible text pixels in the property-grid zone, got {rightPixels}");
    }

    /// <summary>
    /// Executes the command list the same way the platform renderer does (per-command
    /// clip restore+save, draw strings skipped on null font or empty text) and counts
    /// text-like pixels in the right (grid) and left (surface) window zones.
    /// </summary>
    /// <param name="width">Raster width in pixels.</param>
    /// <param name="height">Raster height in pixels.</param>
    /// <param name="cmds">The command list to execute.</param>
    /// <param name="zoneX">X coordinate separating surface zone from grid zone.</param>
    /// <returns>Text-like pixel counts per zone (right/grid first).</returns>
    private static (int rightText, int leftText) Rasterize(int width, int height, IReadOnlyList<GCCmd> cmds, int zoneX)
    {
        using var fontRenderer = new SkiaFontRenderer();
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(new SKColor(255, 255, 255));
        canvas.Save();

        foreach (var cmd in cmds)
        {
            canvas.Restore();
            canvas.Save();
            if (cmd.ClipBounds.HasValue)
            {
                var r = cmd.ClipBounds.Value;
                canvas.ClipRect(new SKRect(r.X, r.Y, r.X + r.Width, r.Y + r.Height));
            }
            if (cmd.Type == OldSchoolForms.Ui.Rendering.DrawCommandType.DrawString)
            {
                if (cmd.Font == null || string.IsNullOrEmpty(cmd.Text)) continue;
                fontRenderer.DrawText(cmd.Text, cmd.Font, cmd.Color, cmd.X, cmd.Y, canvas, cmd.Zoom);
                continue;
            }
            switch (cmd.Type)
            {
                case OldSchoolForms.Ui.Rendering.DrawCommandType.FillRectangle:
                    canvas.DrawRect(cmd.X, cmd.Y, cmd.Width, cmd.Height,
                        new SKPaint { Color = new SKColor(cmd.Color.R, cmd.Color.G, cmd.Color.B, cmd.Color.A) });
                    break;
                case OldSchoolForms.Ui.Rendering.DrawCommandType.DrawRectangle:
                    canvas.DrawRect(cmd.X, cmd.Y, cmd.Width, cmd.Height, new SKPaint
                    {
                        Color = new SKColor(cmd.Color.R, cmd.Color.G, cmd.Color.B, cmd.Color.A),
                        Style = SKPaintStyle.Stroke
                    });
                    break;
                case OldSchoolForms.Ui.Rendering.DrawCommandType.DrawLine:
                    canvas.DrawLine(cmd.X, cmd.Y, cmd.X2, cmd.Y2,
                        new SKPaint { Color = new SKColor(cmd.Color.R, cmd.Color.G, cmd.Color.B, cmd.Color.A) });
                    break;
            }
        }
        canvas.Restore();

        int rightText = 0, leftText = 0;
        using var snap = surface.Snapshot();
        var pixels = snap.PeekPixels();
        if (pixels != null)
        {
            var span = pixels.GetPixelSpan();
            for (int py = 0; py < height; py++)
            {
                for (int px = 0; px < width; px++)
                {
                    var idx = (py * width + px) * 4;
                    byte b = span[idx], gg = span[idx + 1], r2 = span[idx + 2], a = span[idx + 3];
                    if (a <= 40 || r2 >= 120 || gg >= 120 || b >= 120) continue;
                    if (px >= zoneX) rightText++;
                    else leftText++;
                }
            }
        }
        surface.Dispose();
        return (rightText, leftText);
    }
}
