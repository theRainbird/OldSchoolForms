namespace OldSchoolForms.Ui.Layout;

/// <summary>
/// Computes the pixel sizes of the rows or columns of a table layout from a list of
/// <see cref="TableLayoutStyle"/> entries. The same calculation is used by
/// <see cref="TableLayoutPanel"/> for real layout and by designer previews so both
/// always agree.
/// </summary>
/// <remarks>
/// The sizing model is:
/// <list type="bullet">
/// <item><description><see cref="SizeType.Absolute"/>: a fixed pixel size.</description></item>
/// <item><description><see cref="SizeType.AutoSize"/>: the largest preferred size of the children in that row/column.</description></item>
/// <item><description><see cref="SizeType.Percent"/>: that percentage of the space left after absolute and
/// auto-sized entries. If the percentages add up to more than 100, they are scaled down to fit.</description></item>
/// <item><description><see cref="SizeType.Fill"/>: takes the space left over after the percent entries have
/// claimed their share; several fill entries split the leftover equally.</description></item>
/// <item><description>Indices without a style entry share the leftover space like fill entries.</description></item>
/// </list>
/// When no styles are defined at all, every row/column receives an equal share of the
/// available space (the legacy behavior of <see cref="TableLayoutPanel"/>).
/// </remarks>
public static class TableLayoutCalculator
{
    /// <summary>
    /// Computes the pixel size for each of the <paramref name="count"/> rows or columns.
    /// </summary>
    /// <param name="styles">The styles for this direction; may be empty or <c>null</c>.</param>
    /// <param name="count">The effective number of rows or columns to compute.</param>
    /// <param name="totalSize">The total available space in pixels for this direction.</param>
    /// <param name="autoSizes">
    /// Per-index content-driven sizes (the largest child preferred size in each row/column)
    /// used for <see cref="SizeType.AutoSize"/> entries; may be <c>null</c>.
    /// </param>
    /// <returns>An array of <paramref name="count"/> non-negative pixel sizes.</returns>
    public static float[] ComputeSizes(
        IReadOnlyList<TableLayoutStyle>? styles,
        int count,
        float totalSize,
        IReadOnlyList<float>? autoSizes)
    {
        var sizes = new float[Math.Max(0, count)];
        if (count <= 0)
            return sizes;

        if (totalSize < 0f)
            totalSize = 0f;

        // No styles at all: equal distribution (legacy behavior).
        if (styles == null || styles.Count == 0)
        {
            var equal = totalSize / count;
            for (var i = 0; i < count; i++)
                sizes[i] = equal;
            return sizes;
        }

        var fixedTotal = 0f;
        var autoTotal = 0f;
        var percentTotal = 0f;
        var fillCount = 0;
        var unstyledCount = 0;

        for (var i = 0; i < count; i++)
        {
            var style = i < styles.Count ? styles[i] : null;
            switch (style?.SizeType)
            {
                case SizeType.Absolute:
                    fixedTotal += Math.Max(0f, style!.Value);
                    break;
                case SizeType.AutoSize:
                    autoTotal += AutoSizeAt(autoSizes, i);
                    break;
                case SizeType.Percent:
                    percentTotal += Math.Max(0f, style!.Value);
                    break;
                case SizeType.Fill:
                    fillCount++;
                    break;
                default:
                    unstyledCount++;
                    break;
            }
        }

        // Percent entries claim their percentage of the space left after absolute and
        // auto-sized entries. Fill entries and unstyled indices then share whatever
        // the percent entries left over. If the percentages exceed 100 they are
        // scaled down so they still fit, leaving nothing for fill.
        var remaining = totalSize - fixedTotal - autoTotal;
        if (remaining < 0f)
            remaining = 0f;

        float percentSum;
        if (percentTotal <= 0f)
            percentSum = 0f;
        else if (percentTotal >= 100f)
            percentSum = remaining;
        else
            percentSum = remaining * percentTotal / 100f;

        var leftover = remaining - percentSum;
        if (leftover < 0f)
            leftover = 0f;

        var fillAndUnstyled = fillCount + unstyledCount;
        var fillShare = fillAndUnstyled > 0 ? leftover / fillAndUnstyled : 0f;

        for (var i = 0; i < count; i++)
        {
            var style = i < styles.Count ? styles[i] : null;
            switch (style?.SizeType)
            {
                case SizeType.Absolute:
                    sizes[i] = Math.Max(0f, style!.Value);
                    break;
                case SizeType.AutoSize:
                    sizes[i] = AutoSizeAt(autoSizes, i);
                    break;
                case SizeType.Percent:
                    sizes[i] = percentTotal >= 100f
                        ? remaining * Math.Max(0f, style!.Value) / percentTotal
                        : remaining * Math.Max(0f, style!.Value) / 100f;
                    break;
                case SizeType.Fill:
                    sizes[i] = fillShare;
                    break;
                default:
                    sizes[i] = fillShare;
                    break;
            }
        }

        return sizes;
    }

    private static float AutoSizeAt(IReadOnlyList<float>? autoSizes, int index)
    {
        if (autoSizes == null || index < 0 || index >= autoSizes.Count)
            return 0f;
        return Math.Max(0f, autoSizes[index]);
    }
}
