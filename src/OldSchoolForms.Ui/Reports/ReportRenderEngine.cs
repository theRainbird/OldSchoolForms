using OldSchoolForms.Ui.Data;
using OldSchoolForms.Ui.Rendering;

namespace OldSchoolForms.Ui.Reports;

/// <summary>
/// Internal state for tracking the current page being rendered.
/// </summary>
internal class PageRenderState
{
    public Graphics Graphics { get; set; } = null!;
    public ReportPage Page { get; set; } = null!;
    public Cm CurrentY { get; set; }
}

/// <summary>
/// Tracks group value changes across records for hierarchical grouping.
/// </summary>
internal class GroupTracker
{
    public ReportGroup Group { get; }
    public object? CurrentValue { get; set; }
    public bool IsOpen { get; set; }

    public GroupTracker(ReportGroup group) => Group = group;
}

/// <summary>
/// The rendering engine that processes a <see cref="Report"/> definition
/// together with its data source, handles pagination, grouping, and
/// band layout, and produces a list of <see cref="ReportPage"/> objects.
/// </summary>
public class ReportRenderEngine
{
    private Report _report = null!;
    private ReportRenderContext _context = null!;
    private PageRenderState _state = null!;
    private List<ReportPage> _pages = null!;

    /// <summary>
    /// Renders the specified report with data from its <see cref="Report.DataSource"/>.
    /// Uses a two-pass approach: first counts pages for <c>TotalPages</c> variable resolution,
    /// then renders all pages with the correct total.
    /// </summary>
    /// <param name="report">The report definition to render.</param>
    /// <param name="renderDpi">
    /// The resolution in DPI. Use 96 for screen preview, 72 for PDF export.
    /// </param>
    /// <returns>A list of rendered pages.</returns>
    public List<ReportPage> Render(Report report, float renderDpi = 96f)
    {
        _report = report;

        int totalPages = CountPages(report, renderDpi);

        _context = new ReportRenderContext(report) { RenderDpi = renderDpi, TotalPages = totalPages };
        _pages = new List<ReportPage>();

        var records = GetRecordsForBand(_context, report.Detail);

        if (report.Groups.Count > 0)
            records = SortByGroups(records, report.Groups);

        _state = StartNewPage();

        RenderReportHeader();

        if (report.Groups.Count > 0)
            RenderWithGroups(records);
        else
            RenderUngrouped(records);

        RenderReportFooter();
        RenderPageFooter();

        return _pages;
    }

    private int CountPages(Report report, float renderDpi)
    {
        var ctx = new ReportRenderContext(report) { RenderDpi = renderDpi };
        var records = GetRecordsForBand(ctx, report.Detail);
        if (report.Groups.Count > 0)
            records = SortByGroups(records, report.Groups);

        int pageCount = 0;
        Cm currentY = report.TopMargin;
        Cm pageBottom = report.PageHeight - report.BottomMargin;

        Cm pageHeaderH = MeasureHeaderFooterWithFirstRecord(ctx, report.PageHeader);
        Cm footerH = MeasureHeaderFooterWithFirstRecord(ctx, report.PageFooter);
        Cm reportFooterH = MeasureHeaderFooterWithFirstRecord(ctx, report.ReportFooter);

        if (report.ReportHeader.Visible)
            currentY += MeasureHeaderFooterWithFirstRecord(ctx, report.ReportHeader);
        currentY += pageHeaderH;

        Cm groupHeaderH = Cm.Zero;
        Cm groupFooterH = Cm.Zero;
        foreach (var g in report.Groups)
        {
            if (g.Header.Visible) groupHeaderH += g.Header.MeasureHeight(ctx);
            if (g.Footer.Visible) groupFooterH += g.Footer.MeasureHeight(ctx);
        }

        for (int i = 0; i < records.Count; i++)
        {
            ctx.CurrentRecord = records[i];
            Cm detailH = report.Detail.MeasureHeight(ctx);
            Cm extraH = (i == 0 || IsGroupBoundary(records, i, report.Groups))
                ? groupHeaderH + groupFooterH : Cm.Zero;

            if (currentY + detailH + extraH + footerH + reportFooterH > pageBottom)
            {
                pageCount++;
                currentY = report.TopMargin + pageHeaderH + extraH;
            }
            else
            {
                currentY += extraH;
            }
            currentY += detailH;
        }

        if (pageCount == 0 || currentY > report.TopMargin)
            pageCount++;

        return Math.Max(1, pageCount);
    }

    private static bool IsGroupBoundary(List<object> records, int index, ReportGroupCollection groups)
    {
        if (index <= 0 || groups.Count == 0) return index == 0;
        var current = records[index];
        var previous = records[index - 1];
        foreach (var g in groups)
        {
            var curVal = DataRecord.GetValue(current, g.GroupField);
            var prevVal = DataRecord.GetValue(previous, g.GroupField);
            if (!Equals(curVal, prevVal)) return true;
        }
        return false;
    }

    /// <summary>
    /// Resolves the record list a band renders against: the named data source's data,
    /// falling back to the first defined source, then to the legacy
    /// <see cref="Report.DataSource"/> when the resolved source carries no data.
    /// </summary>
    private static List<object> GetRecordsForBand(ReportRenderContext context, ReportBand band)
    {
        var source = context.ResolveSource(band.DataSourceName);
        var data = source?.Data ?? context.Report.DataSource;
        return GetRecords(data);
    }

    /// <summary>
    /// Binds the context to the first record of the band's resolved data source,
    /// measures the band, then restores the previous record binding so the
    /// first-record binding never leaks into other bands.
    /// </summary>
    private static Cm MeasureHeaderFooterWithFirstRecord(ReportRenderContext context, ReportBand band)
    {
        if (!band.Visible) return Cm.Zero;
        var savedRecord = context.CurrentRecord;
        var savedSource = context.CurrentSourceName;
        var source = context.ResolveSource(band.DataSourceName);
        context.CurrentSourceName = source?.Name;
        context.CurrentRecord = context.FirstRecordOf(band.DataSourceName);
        Cm h = band.MeasureHeight(context);
        context.CurrentRecord = savedRecord;
        context.CurrentSourceName = savedSource;
        return h;
    }

    /// <summary>
    /// Binds the context to the first record of the band's resolved data source.
    /// </summary>
    private void BindFirstRecordOf(ReportBand band)
    {
        var source = _context.ResolveSource(band.DataSourceName);
        _context.CurrentSourceName = source?.Name;
        _context.CurrentRecord = _context.FirstRecordOf(band.DataSourceName);
    }

    private void RenderReportHeader()
    {
        var band = _report.ReportHeader;
        if (!band.Visible) return;
        var savedRecord = _context.CurrentRecord;
        var savedSource = _context.CurrentSourceName;
        BindFirstRecordOf(band);
        Cm h = band.MeasureHeight(_context);
        if (h > Cm.Zero)
        {
            band.Render(_state.Graphics, _context, _state.CurrentY, h);
            _state.Page.Bands.Add(band);
            _state.CurrentY += h;
        }
        _context.CurrentRecord = savedRecord;
        _context.CurrentSourceName = savedSource;
    }

    private void RenderReportFooter()
    {
        var band = _report.ReportFooter;
        if (!band.Visible) return;
        var savedRecord = _context.CurrentRecord;
        var savedSource = _context.CurrentSourceName;
        BindFirstRecordOf(band);
        Cm h = band.MeasureHeight(_context);
        if (h > Cm.Zero)
        {
            EnsureSpace(h);
            band.Render(_state.Graphics, _context, _state.CurrentY, h);
            _state.Page.Bands.Add(band);
            _state.CurrentY += h;
        }
        _context.CurrentRecord = savedRecord;
        _context.CurrentSourceName = savedSource;
    }

    private void RenderPageHeader()
    {
        var band = _report.PageHeader;
        if (!band.Visible) return;
        var savedRecord = _context.CurrentRecord;
        var savedSource = _context.CurrentSourceName;
        BindFirstRecordOf(band);
        Cm h = band.MeasureHeight(_context);
        if (h > Cm.Zero)
        {
            band.Render(_state.Graphics, _context, _state.CurrentY, h);
            _state.Page.Bands.Add(band);
            _state.CurrentY += h;
        }
        _context.CurrentRecord = savedRecord;
        _context.CurrentSourceName = savedSource;
    }

    private void RenderPageFooter()
    {
        var band = _report.PageFooter;
        if (!band.Visible) return;
        var savedRecord = _context.CurrentRecord;
        var savedSource = _context.CurrentSourceName;
        BindFirstRecordOf(band);
        Cm h = band.MeasureHeight(_context);
        if (h > Cm.Zero)
        {
            Cm pageBottom = _report.PageHeight - _report.BottomMargin;
            Cm footerY = pageBottom - h;

            if (footerY < _state.CurrentY)
                footerY = _state.CurrentY;

            band.Render(_state.Graphics, _context, footerY, h);
            _state.Page.Bands.Add(band);
        }
        _context.CurrentRecord = savedRecord;
        _context.CurrentSourceName = savedSource;
    }

    private void RenderUngrouped(List<object> records)
    {
        var detail = _report.Detail;
        if (!detail.Visible) return;

        _context.CurrentSourceName = _context.ResolveSource(detail.DataSourceName)?.Name;

        for (int i = 0; i < records.Count; i++)
        {
            _context.CurrentRecord = records[i];
            _context.CurrentRowIndex = i;
            RenderBand(detail);
        }
    }

    private void RenderWithGroups(List<object> records)
    {
        var trackers = _report.Groups.Select(g => new GroupTracker(g)).ToArray();
        var detail = _report.Detail;

        // Group headers/footers automatically use the detail band's data source.
        _context.CurrentSourceName = _context.ResolveSource(detail.DataSourceName)?.Name;

        for (int i = 0; i < records.Count; i++)
        {
            _context.CurrentRecord = records[i];
            _context.CurrentRowIndex = i;

            int firstChangedLevel = DetectGroupChanges(records[i], trackers);

            if (firstChangedLevel >= 0)
            {
                for (int g = trackers.Length - 1; g >= firstChangedLevel; g--)
                {
                    if (trackers[g].IsOpen)
                    {
                        _context.CurrentGroupValue = trackers[g].CurrentValue;
                        RenderBand(trackers[g].Group.Footer);
                        trackers[g].IsOpen = false;
                    }
                }

                for (int g = firstChangedLevel; g < trackers.Length; g++)
                {
                    if (trackers[g].CurrentValue != null)
                    {
                        _context.CurrentGroupValue = trackers[g].CurrentValue;
                        RenderBand(trackers[g].Group.Header);
                        trackers[g].IsOpen = true;
                    }
                }
            }

            if (detail.Visible)
                RenderBand(detail);
        }

        for (int g = trackers.Length - 1; g >= 0; g--)
        {
            if (trackers[g].IsOpen)
            {
                _context.CurrentGroupValue = trackers[g].CurrentValue;
                RenderBand(trackers[g].Group.Footer);
                trackers[g].IsOpen = false;
            }
        }
    }

    private int DetectGroupChanges(object record, GroupTracker[] trackers)
    {
        int firstChanged = -1;

        for (int g = 0; g < trackers.Length; g++)
        {
            var newVal = _context.GetFieldValue(trackers[g].Group.GroupField);

            if (firstChanged >= 0)
            {
                trackers[g].CurrentValue = newVal;
            }
            else if (!trackers[g].IsOpen || !Equals(newVal, trackers[g].CurrentValue))
            {
                firstChanged = g;
                trackers[g].CurrentValue = newVal;
            }
        }

        return firstChanged;
    }

    private void RenderBand(ReportBand band)
    {
        Cm h = band.MeasureHeight(_context);
        if (h <= Cm.Zero) return;

        if (band.KeepTogether)
        {
            Cm available = GetAvailableHeight();
            if (h > available)
            {
                StartNewPage();
                if (band.BandType != ReportBandType.PageHeader && _report.PageHeader.RepeatOnNewPage)
                    RenderPageHeader();
            }
        }
        else
        {
            EnsureSpace(h);
        }

        band.Render(_state.Graphics, _context, _state.CurrentY, h);
        _state.Page.Bands.Add(band);
        _state.CurrentY += h;
    }

    private void EnsureSpace(Cm needed)
    {
        Cm available = GetAvailableHeight();
        if (needed <= available) return;

        RenderPageFooter();
        StartNewPage();

        if (_report.PageHeader.RepeatOnNewPage)
            RenderPageHeader();
    }

    private Cm GetAvailableHeight()
    {
        Cm pageBottom = _report.PageHeight - _report.BottomMargin;
        return pageBottom - _state.CurrentY;
    }

    private PageRenderState StartNewPage()
    {
        var g = new Graphics();
        g.MeasureText = _context.MeasureTextCallback;

        int pageNum = _pages.Count + 1;
        _context.PageNumber = pageNum;

        var page = new ReportPage(pageNum, _report.PageWidth, _report.PageHeight, g);

        var state = new PageRenderState
        {
            Graphics = g,
            Page = page,
            CurrentY = _report.TopMargin
        };

        _state = state;
        _pages.Add(page);

        if (_pages.Count > 1 || _state.CurrentY <= _report.TopMargin)
        {
            RenderPageHeader();
        }

        return state;
    }

    private static List<object> GetRecords(object? dataSource)
    {
        if (dataSource == null) return new List<object>();

        var records = new List<object>();

        if (dataSource is System.Collections.IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (item != null)
                    records.Add(item);
            }
        }

        return records;
    }

    private static List<object> SortByGroups(List<object> records, ReportGroupCollection groups)
    {
        if (groups.Count == 0) return records;

        var fields = new List<string>();
        CollectGroupFields(groups, fields);

        var sorted = records.OrderBy(r =>
        {
            var sb = new System.Text.StringBuilder();
            foreach (var f in fields)
            {
                var val = DataRecord.GetValue(r, f)?.ToString() ?? "";
                sb.Append(val.PadLeft(20));
            }
            return sb.ToString();
        }).ToList();

        return sorted;
    }

    private static void CollectGroupFields(ReportGroupCollection groups, List<string> fields)
    {
        foreach (var g in groups)
        {
            if (!string.IsNullOrEmpty(g.GroupField))
                fields.Add(g.GroupField);
            CollectGroupFields(g.Groups, fields);
        }
    }
}
