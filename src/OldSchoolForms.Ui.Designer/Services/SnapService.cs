using System;
using System.Collections.Generic;
using OldSchoolForms.Ui.Core;

namespace OldSchoolForms.Ui.Designer.Services;

/// <summary>
/// Describes the category of a smart guide line, used to render each category distinctly.
/// </summary>
public enum GuideKind
{
    /// <summary>
    /// A guide aligning a moving edge to a sibling control edge or container edge.
    /// </summary>
    Edge,

    /// <summary>
    /// A guide aligning a moving edge or center to a sibling control center.
    /// </summary>
    Center,

    /// <summary>
    /// A guide aligning a moving edge to the client edge of a container or the form.
    /// </summary>
    Container,
}

/// <summary>
/// Describes a single smart guide line to render on the design surface.
/// </summary>
public class GuideLine
{
    /// <summary>
    /// Gets the start point of the guide line (in surface coordinates).
    /// </summary>
    public Point Start { get; }

    /// <summary>
    /// Gets the end point of the guide line.
    /// </summary>
    public Point End { get; }

    /// <summary>
    /// Gets the category of the guide line, controlling how it is rendered.
    /// </summary>
    public GuideKind Kind { get; }

    /// <summary>
    /// Initializes a new guide line.
    /// </summary>
    /// <param name="start">The start point (in surface coordinates).</param>
    /// <param name="end">The end point (in surface coordinates).</param>
    /// <param name="kind">The guide category. Defaults to <see cref="GuideKind.Edge"/>.</param>
    public GuideLine(Point start, Point end, GuideKind kind = GuideKind.Edge)
    {
        Start = start;
        End = end;
        Kind = kind;
    }
}

/// <summary>
/// Smart guide alignment data returned by the snap engine.
/// Contains offset deltas to apply and guide lines to render.
/// </summary>
public class SnapResult
{
    /// <summary>
    /// Gets the X offset to apply for snapping.
    /// </summary>
    public int SnapX { get; set; }

    /// <summary>
    /// Gets the Y offset to apply for snapping.
    /// </summary>
    public int SnapY { get; set; }

    /// <summary>
    /// Gets the list of guide lines to render.
    /// </summary>
    public List<GuideLine> GuideLines { get; } = new();

    /// <summary>
    /// Gets whether any snap was applied.
    /// </summary>
    public bool HasSnap => SnapX != 0 || SnapY != 0;
}

/// <summary>
/// Detects alignment between a dragged control and its siblings.
/// Produces snap offsets and guide lines for the design surface.
/// </summary>
public class SnapService
{
    private const int SnapThreshold = 6;

    /// <summary>
    /// Computes snap alignment for the given control bounds against sibling bounds and optional
    /// container client edges. Edges of the moving control snap to sibling control edges and to
    /// the client edges of the provided container rectangles; centers snap to sibling centers.
    /// </summary>
    /// <param name="movingBounds">The current bounds of the control being moved/resized.</param>
    /// <param name="originalBounds">The bounds of the control before movement started.</param>
    /// <param name="siblingBounds">Bounds of all other controls on the surface.</param>
    /// <param name="containerEdges">Client-area rectangles of the containing control or form whose
    /// edges the moving control should also snap to. When null or empty, container snapping is skipped.</param>
    /// <returns>A SnapResult with offsets and guide lines.</returns>
    public SnapResult ComputeSnap(Rectangle movingBounds, Rectangle originalBounds, IReadOnlyList<Rectangle> siblingBounds, IReadOnlyList<Rectangle>? containerEdges = null)
    {
        var result = new SnapResult();

        if (siblingBounds.Count == 0 && (containerEdges == null || containerEdges.Count == 0))
            return result;

        int bestSnapX = 0, bestSnapY = 0;
        int bestDistX = SnapThreshold, bestDistY = SnapThreshold;

        foreach (var sibling in siblingBounds)
        {
            if (AreEqual(sibling, movingBounds)) continue;

            // Horizontal alignments (snap X)
            TrySnap(movingBounds.X, sibling.X, ref bestSnapX, ref bestDistX);
            TrySnap(movingBounds.X, sibling.Right, ref bestSnapX, ref bestDistX);
            TrySnap(movingBounds.Right, sibling.X, ref bestSnapX, ref bestDistX);
            TrySnap(movingBounds.Right, sibling.Right, ref bestSnapX, ref bestDistX);

            // Horizontal center
            int mc = movingBounds.X + movingBounds.Width / 2;
            int sc = sibling.X + sibling.Width / 2;
            TrySnap(mc, sc, ref bestSnapX, ref bestDistX);

            // Vertical alignments (snap Y)
            TrySnap(movingBounds.Y, sibling.Y, ref bestSnapY, ref bestDistY);
            TrySnap(movingBounds.Y, sibling.Bottom, ref bestSnapY, ref bestDistY);
            TrySnap(movingBounds.Bottom, sibling.Y, ref bestSnapY, ref bestDistY);
            TrySnap(movingBounds.Bottom, sibling.Bottom, ref bestSnapY, ref bestDistY);

            // Vertical center
            int mcy = movingBounds.Y + movingBounds.Height / 2;
            int scy = sibling.Y + sibling.Height / 2;
            TrySnap(mcy, scy, ref bestSnapY, ref bestDistY);
        }

        if (containerEdges != null)
        {
            foreach (var container in containerEdges)
            {
                // Horizontal alignments against container edges (snap X).
                TrySnap(movingBounds.X, container.X, ref bestSnapX, ref bestDistX);
                TrySnap(movingBounds.X, container.Right, ref bestSnapX, ref bestDistX);
                TrySnap(movingBounds.Right, container.X, ref bestSnapX, ref bestDistX);
                TrySnap(movingBounds.Right, container.Right, ref bestSnapX, ref bestDistX);

                // Vertical alignments against container edges (snap Y).
                TrySnap(movingBounds.Y, container.Y, ref bestSnapY, ref bestDistY);
                TrySnap(movingBounds.Y, container.Bottom, ref bestSnapY, ref bestDistY);
                TrySnap(movingBounds.Bottom, container.Y, ref bestSnapY, ref bestDistY);
                TrySnap(movingBounds.Bottom, container.Bottom, ref bestSnapY, ref bestDistY);
            }
        }

        if (bestSnapX != 0)
        {
            result.SnapX = bestSnapX;
            AddHorizontalGuides(result, movingBounds, bestSnapX, siblingBounds, containerEdges);
        }

        if (bestSnapY != 0)
        {
            result.SnapY = bestSnapY;
            AddVerticalGuides(result, movingBounds, bestSnapY, siblingBounds, containerEdges);
        }

        return result;
    }

    private static void TrySnap(int movingCoord, int siblingCoord, ref int bestSnap, ref int bestDist)
    {
        int diff = siblingCoord - movingCoord;
        int absDiff = Math.Abs(diff);

        if (absDiff < bestDist)
        {
            bestDist = absDiff;
            bestSnap = diff;
        }
    }

    private static void AddHorizontalGuides(SnapResult result, Rectangle bounds, int snapX, IReadOnlyList<Rectangle> siblings, IReadOnlyList<Rectangle>? containerEdges)
    {
        int snappedX = bounds.X + snapX;
        int snappedRight = snappedX + bounds.Width;

        foreach (var sibling in siblings)
        {
            if (AreEqual(sibling, bounds)) continue;

            // Left edge match
            if (Math.Abs(snappedX - sibling.X) <= 1)
            {
                int lineY = Math.Min(bounds.Y, sibling.Y);
                int lineH = Math.Max(bounds.Bottom, sibling.Bottom) - lineY;
                result.GuideLines.Add(new GuideLine(
                    new Point(snappedX, lineY),
                    new Point(snappedX, lineY + lineH), GuideKind.Edge));
            }

            // Right edge match
            if (Math.Abs(snappedRight - sibling.Right) <= 1)
            {
                int lineY = Math.Min(bounds.Y, sibling.Y);
                int lineH = Math.Max(bounds.Bottom, sibling.Bottom) - lineY;
                result.GuideLines.Add(new GuideLine(
                    new Point(snappedRight, lineY),
                    new Point(snappedRight, lineY + lineH), GuideKind.Edge));
            }

            // Left vs right
            if (Math.Abs(snappedX - sibling.Right) <= 1)
            {
                result.GuideLines.Add(new GuideLine(
                    new Point(snappedX, bounds.Y),
                    new Point(snappedX, bounds.Bottom), GuideKind.Edge));
            }

            if (Math.Abs(snappedRight - sibling.X) <= 1)
            {
                result.GuideLines.Add(new GuideLine(
                    new Point(snappedRight, bounds.Y),
                    new Point(snappedRight, bounds.Bottom), GuideKind.Edge));
            }

            // Center match
            int mc = snappedX + bounds.Width / 2;
            int sc = sibling.X + sibling.Width / 2;
            if (Math.Abs(mc - sc) <= 1)
            {
                int lineY = Math.Min(bounds.Y, sibling.Y);
                int lineH = Math.Max(bounds.Bottom, sibling.Bottom) - lineY;
                result.GuideLines.Add(new GuideLine(
                    new Point(mc, lineY),
                    new Point(mc, lineY + lineH), GuideKind.Center));
            }
        }

        if (containerEdges != null)
            AddContainerGuides(result, snappedX, snappedRight, containerEdges, vertical: true);
    }

    private static bool AreEqual(Rectangle a, Rectangle b)
    {
        return a.X == b.X && a.Y == b.Y && a.Width == b.Width && a.Height == b.Height;
    }

    private static void AddVerticalGuides(SnapResult result, Rectangle bounds, int snapY, IReadOnlyList<Rectangle> siblings, IReadOnlyList<Rectangle>? containerEdges)
    {
        int snappedY = bounds.Y + snapY;
        int snappedBottom = snappedY + bounds.Height;

        foreach (var sibling in siblings)
        {
            if (AreEqual(sibling, bounds)) continue;

            if (Math.Abs(snappedY - sibling.Y) <= 1)
            {
                int lineX = Math.Min(bounds.X, sibling.X);
                int lineW = Math.Max(bounds.Right, sibling.Right) - lineX;
                result.GuideLines.Add(new GuideLine(
                    new Point(lineX, snappedY),
                    new Point(lineX + lineW, snappedY), GuideKind.Edge));
            }

            if (Math.Abs(snappedBottom - sibling.Bottom) <= 1)
            {
                int lineX = Math.Min(bounds.X, sibling.X);
                int lineW = Math.Max(bounds.Right, sibling.Right) - lineX;
                result.GuideLines.Add(new GuideLine(
                    new Point(lineX, snappedBottom),
                    new Point(lineX + lineW, snappedBottom), GuideKind.Edge));
            }

            if (Math.Abs(snappedY - sibling.Bottom) <= 1)
            {
                result.GuideLines.Add(new GuideLine(
                    new Point(bounds.X, snappedY),
                    new Point(bounds.Right, snappedY), GuideKind.Edge));
            }

            if (Math.Abs(snappedBottom - sibling.Y) <= 1)
            {
                result.GuideLines.Add(new GuideLine(
                    new Point(bounds.X, snappedBottom),
                    new Point(bounds.Right, snappedBottom), GuideKind.Edge));
            }

            int mcy = snappedY + bounds.Height / 2;
            int scy = sibling.Y + sibling.Height / 2;
            if (Math.Abs(mcy - scy) <= 1)
            {
                int lineX = Math.Min(bounds.X, sibling.X);
                int lineW = Math.Max(bounds.Right, sibling.Right) - lineX;
                result.GuideLines.Add(new GuideLine(
                    new Point(lineX, mcy),
                    new Point(lineX + lineW, mcy), GuideKind.Center));
            }
        }

        if (containerEdges != null)
            AddContainerGuides(result, snappedY, snappedBottom, containerEdges, vertical: false);
    }

    /// <summary>
    /// Adds container-edge guides spanning the full length of every matched container edge.
    /// Both of the moving control's perpendicular edges (snappedCoordA and snappedCoordB) are
    /// tested against each container edge, so a guide is emitted when either the left/right
    /// edge (vertical) or the top/bottom edge (horizontal) aligns.
    /// </summary>
    /// <param name="result">The result to append guides to.</param>
    /// <param name="snappedCoordA">The first perpendicular edge coordinate (e.g. the control's left edge).</param>
    /// <param name="snappedCoordB">The second perpendicular edge coordinate (e.g. the control's right edge).</param>
    /// <param name="containerEdges">The container client-area rectangles.</param>
    /// <param name="vertical">True to draw vertical guides (aligned to container left/right edges),
    /// false to draw horizontal guides (aligned to container top/bottom edges).</param>
    private static void AddContainerGuides(SnapResult result, int snappedCoordA, int snappedCoordB, IReadOnlyList<Rectangle> containerEdges, bool vertical)
    {
        foreach (var container in containerEdges)
        {
            if (vertical)
            {
                AddVerticalContainerGuide(result, snappedCoordA, container);
                AddVerticalContainerGuide(result, snappedCoordB, container);
            }
            else
            {
                AddHorizontalContainerGuide(result, snappedCoordA, container);
                AddHorizontalContainerGuide(result, snappedCoordB, container);
            }
        }
    }

    private static void AddVerticalContainerGuide(SnapResult result, int x, Rectangle container)
    {
        if (Math.Abs(x - container.X) <= 1)
        {
            result.GuideLines.Add(new GuideLine(
                new Point(x, container.Y),
                new Point(x, container.Y + container.Height), GuideKind.Container));
        }
        if (Math.Abs(x - container.Right) <= 1)
        {
            result.GuideLines.Add(new GuideLine(
                new Point(x, container.Y),
                new Point(x, container.Y + container.Height), GuideKind.Container));
        }
    }

    private static void AddHorizontalContainerGuide(SnapResult result, int y, Rectangle container)
    {
        if (Math.Abs(y - container.Y) <= 1)
        {
            result.GuideLines.Add(new GuideLine(
                new Point(container.X, y),
                new Point(container.X + container.Width, y), GuideKind.Container));
        }
        if (Math.Abs(y - container.Bottom) <= 1)
        {
            result.GuideLines.Add(new GuideLine(
                new Point(container.X, y),
                new Point(container.X + container.Width, y), GuideKind.Container));
        }
    }
}
