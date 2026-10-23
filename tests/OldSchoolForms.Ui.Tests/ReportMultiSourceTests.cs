using System.Collections.Generic;
using System.Linq;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Rendering;
using OldSchoolForms.Ui.Reports;
using Xunit;

namespace OldSchoolForms.Ui.Tests;

/// <summary>
/// Tests for the multi-data-source report model, JSON round-trip, field-value
/// resolution (including qualified <c>{Source.Field}</c> expressions) and the
/// render engine's per-band source binding (first-record headers/footers,
/// per-record detail repetition, group inheritance).
/// </summary>
public class ReportMultiSourceTests
{
    private sealed class KopfRecord
    {
        public string Kunde { get; set; } = "";
        public int AuftragsNr { get; set; }
    }

    private sealed class PositionRecord
    {
        public string Artikel { get; set; } = "";
        public decimal Betrag { get; set; }
    }

    // ---- Model -------------------------------------------------------------

    [Fact]
    public void ReportDataSource_AddField_And_FindField_CaseInsensitive()
    {
        var source = new ReportDataSource("Positions");
        source.AddField("Artikel", ReportDataType.String);
        source.AddField("Betrag", ReportDataType.Decimal);

        Assert.Equal(2, source.Fields.Count);
        Assert.NotNull(source.FindField("artikel"));
        Assert.Equal(ReportDataType.Decimal, source.FindField("BETRAG")!.DataType);
        Assert.Null(source.FindField("missing"));
        Assert.Null(source.FindField(null));
    }

    [Fact]
    public void ReportDataSourceField_ClrType_MapsFromDataType()
    {
        var field = new ReportDataSourceField("Betrag", ReportDataType.Decimal);
        Assert.Equal(typeof(decimal), field.ClrType);
    }

    [Fact]
    public void ReportDataSourceCollection_Indexer_CaseInsensitive_ReturnsNull_WhenAbsent()
    {
        var sources = new ReportDataSourceCollection();
        var a = sources.Add("Kopf");
        sources.Add(new ReportDataSource("Positionen"));

        Assert.Same(a, sources["kopf"]);
        Assert.True(sources.Contains("POSITIONEN"));
        Assert.Null(sources["nope"]);
        Assert.Null(sources[null]);
        Assert.Null(sources[string.Empty]);
        Assert.True(sources.Remove("KOPF"));
        Assert.False(sources.Contains("Kopf"));
    }

    [Fact]
    public void ReportDataTypeMapper_RoundTrips_AllTypes()
    {
        foreach (ReportDataType t in System.Enum.GetValues<ReportDataType>())
        {
            var json = t.ToJsonName();
            Assert.True(ReportDataTypeMapper.TryParse(json, out var parsed));
            Assert.Equal(t, parsed);
        }
    }

    [Fact]
    public void ReportDataTypeMapper_TryParse_Aliases()
    {
        Assert.True(ReportDataTypeMapper.TryParse("bool", out var b));
        Assert.Equal(ReportDataType.Boolean, b);
        Assert.True(ReportDataTypeMapper.TryParse("date", out var d));
        Assert.Equal(ReportDataType.DateTime, d);
        Assert.True(ReportDataTypeMapper.TryParse("integer", out var i));
        Assert.Equal(ReportDataType.Int, i);
        Assert.True(ReportDataTypeMapper.TryParse("float", out var f));
        Assert.Equal(ReportDataType.Double, f);
        Assert.False(ReportDataTypeMapper.TryParse("banana", out _));
        Assert.Equal(ReportDataType.String, ReportDataTypeMapper.ParseOrString("banana"));
    }

    [Fact]
    public void ReportDataTypeMapper_FromClrType_HandlesNullable()
    {
        Assert.Equal(ReportDataType.Int, ReportDataTypeMapper.FromClrType(typeof(int?)));
        Assert.Equal(ReportDataType.DateTime, ReportDataTypeMapper.FromClrType(typeof(System.DateTime?)));
        Assert.Equal(ReportDataType.String, ReportDataTypeMapper.FromClrType(null));
    }

    // ---- Serializer round-trip --------------------------------------------

    [Fact]
    public void Serializer_RoundTrips_DataSources_And_BandDataSourceName()
    {
        var report = new Report("Multi");
        var kopf = report.DataSources.Add("Auftragskopf");
        kopf.AddField("Kunde", ReportDataType.String);
        kopf.AddField("AuftragsNr", ReportDataType.Int);
        var pos = report.DataSources.Add("Auftragspositionen");
        pos.AddField("Artikel", ReportDataType.String);
        pos.AddField("Betrag", ReportDataType.Decimal);

        report.ReportHeader.DataSourceName = "Auftragskopf";
        report.Detail.DataSourceName = "Auftragspositionen";

        var json = ReportJsonSerializer.Save(report);
        var loaded = ReportJsonSerializer.Load(json);

        Assert.Equal(2, loaded.DataSources.Count);
        Assert.Equal("Auftragskopf", loaded.DataSources[0].Name);
        Assert.Equal(2, loaded.DataSources[0].Fields.Count);
        Assert.Equal(ReportDataType.Int, loaded.DataSources[0].FindField("AuftragsNr")!.DataType);
        Assert.Equal(ReportDataType.Decimal, loaded.DataSources[1].FindField("Betrag")!.DataType);
        Assert.Equal("Auftragskopf", loaded.ReportHeader.DataSourceName);
        Assert.Equal("Auftragspositionen", loaded.Detail.DataSourceName);
    }

    [Fact]
    public void Serializer_OldFormat_WithoutDataSources_LoadsEmpty()
    {
        // A report saved before multi-source support has no dataSources member.
        var report = new Report("Legacy");
        report.Detail.Height = 1.0;
        var json = ReportJsonSerializer.Save(report);
        Assert.DoesNotContain("dataSources", json);

        var loaded = ReportJsonSerializer.Load(json);
        Assert.Empty(loaded.DataSources);
        Assert.Null(loaded.Detail.DataSourceName);
    }

    [Fact]
    public void Serializer_BandDataSourceName_OmittedWhenNull()
    {
        var report = new Report("NoDsn");
        var json = ReportJsonSerializer.Save(report);
        Assert.DoesNotContain("dataSourceName", json);
    }

    // ---- Field value resolution -------------------------------------------

    [Fact]
    public void GetFieldValue_Unqualified_ResolvesCurrentRecord()
    {
        var report = new Report("R");
        report.DataSources.Add(new ReportDataSource("Kopf"));
        var ctx = new ReportRenderContext(report)
        {
            CurrentSourceName = "Kopf",
            CurrentRecord = new KopfRecord { Kunde = "Meier", AuftragsNr = 42 }
        };

        Assert.Equal("Meier", ctx.GetFieldValue("Kunde"));
        Assert.Equal(42, ctx.GetFieldValue("AuftragsNr"));
    }

    [Fact]
    public void GetFieldValue_Qualified_MatchingSource_UsesCurrentRecord()
    {
        var report = new Report("R");
        report.DataSources.Add(new ReportDataSource("Kopf"));
        var ctx = new ReportRenderContext(report)
        {
            CurrentSourceName = "Kopf",
            CurrentRecord = new KopfRecord { Kunde = "Schmidt", AuftragsNr = 7 }
        };

        Assert.Equal("Schmidt", ctx.GetFieldValue("Kopf.Kunde"));
        Assert.Equal(7, ctx.GetFieldValue("Kopf.AuftragsNr"));
    }

    [Fact]
    public void GetFieldValue_Qualified_OtherSource_UsesFirstRecord()
    {
        var report = new Report("R");
        var kopf = report.DataSources.Add("Auftragskopf");
        kopf.Data = new List<KopfRecord>
        {
            new() { Kunde = "Erster", AuftragsNr = 1 },
            new() { Kunde = "Zweiter", AuftragsNr = 2 }
        };
        var ctx = new ReportRenderContext(report)
        {
            CurrentSourceName = "AndereQuelle",
            CurrentRecord = new PositionRecord { Artikel = "Widget", Betrag = 9 }
        };

        // Prefix names a different source -> resolves against its FIRST record.
        Assert.Equal("Erster", ctx.GetFieldValue("Auftragskopf.Kunde"));
        Assert.Equal(1, ctx.GetFieldValue("Auftragskopf.AuftragsNr"));
    }

    [Fact]
    public void GetFieldValue_UnknownPrefix_FallsThroughToPlainField()
    {
        var report = new Report("R");
        // No data sources defined; a dotted token must be treated as a plain field name.
        var ctx = new ReportRenderContext(report)
        {
            CurrentRecord = new Dictionary<string, object> { ["a.b"] = "value" }
        };

        Assert.Equal("value", ctx.GetFieldValue("a.b"));
    }

    [Fact]
    public void GetFieldValue_SpecialVars_StillWork()
    {
        var report = new Report("R");
        var ctx = new ReportRenderContext(report)
        {
            PageNumber = 3,
            TotalPages = 9,
            CurrentRowIndex = 4
        };

        Assert.Equal(3, ctx.GetFieldValue("PageNumber"));
        Assert.Equal(9, ctx.GetFieldValue("TotalPages"));
        Assert.Equal(4, ctx.GetFieldValue("CurrentRowIndex"));
    }

    [Fact]
    public void SeedFirstRecord_OverridesLookup()
    {
        var report = new Report("R");
        var kopf = report.DataSources.Add("Kopf");
        kopf.Data = new List<KopfRecord> { new() { Kunde = "Real" } };
        var ctx = new ReportRenderContext(report);
        ctx.SeedFirstRecord("Kopf", new Dictionary<string, object> { ["Kunde"] = "Synthetic" });

        Assert.Equal("Synthetic", ctx.GetFieldValue("Kopf.Kunde"));
    }

    [Fact]
    public void ResolveSource_Named_ThenFirst_ThenNull()
    {
        var report = new Report("R");
        var a = report.DataSources.Add("A");
        var b = report.DataSources.Add("B");
        var ctx = new ReportRenderContext(report);

        Assert.Same(b, ctx.ResolveSource("b"));
        Assert.Same(a, ctx.ResolveSource("missing")); // falls back to first
        Assert.Same(a, ctx.ResolveSource(null));
        Assert.Same(a, ctx.ResolveSource(string.Empty));

        var empty = new Report("Empty");
        var ctx2 = new ReportRenderContext(empty);
        Assert.Null(ctx2.ResolveSource("anything")); // legacy mode
    }

    // ---- Render engine binding --------------------------------------------

    private static List<string> DrawnTexts(ReportPage page)
        => page.Graphics.GetCommands()
            .Where(c => c.Type == DrawCommandType.DrawString && c.Text != null)
            .Select(c => c.Text!)
            .ToList();

    private static void AssertDrawn(ReportPage page, string expected)
        => Assert.Contains(DrawnTexts(page), t => t == expected || t.Contains(expected));

    [Fact]
    public void Engine_DetailRepeats_PerRecordOfBoundSource()
    {
        var report = new Report("DetailRepeat")
        {
            PageWidth = 21.0,
            PageHeight = 29.7
        };
        var pos = report.DataSources.Add("Pos");
        pos.Data = new List<PositionRecord>
        {
            new() { Artikel = "A1", Betrag = 10m },
            new() { Artikel = "A2", Betrag = 20m },
            new() { Artikel = "A3", Betrag = 30m }
        };
        report.Detail.Height = 0.6;
        report.Detail.DataSourceName = "Pos";
        report.Detail.Controls.Add(new ReportTextBox
        {
            DataField = "Artikel",
            Left = 0, Top = 0, Width = 5, Height = 0.5
        });

        var pages = new ReportRenderEngine().Render(report, 96f);
        var all = pages.SelectMany(DrawnTexts).ToList();

        Assert.Contains("A1", all);
        Assert.Contains("A2", all);
        Assert.Contains("A3", all);
    }

    [Fact]
    public void Engine_ReportHeader_BindsFirstRecordOfItsSource()
    {
        var report = new Report("HeaderFirst")
        {
            PageWidth = 21.0,
            PageHeight = 29.7
        };
        var kopf = report.DataSources.Add("Kopf");
        kopf.Data = new List<KopfRecord>
        {
            new() { Kunde = "Alpha", AuftragsNr = 100 },
            new() { Kunde = "Beta", AuftragsNr = 200 }
        };
        report.ReportHeader.Height = 1.0;
        report.ReportHeader.DataSourceName = "Kopf";
        report.ReportHeader.Controls.Add(new ReportTextBox
        {
            DataField = "Kunde",
            Left = 0, Top = 0, Width = 5, Height = 0.5
        });
        report.Detail.Height = 0.5;
        report.Detail.Controls.Add(new ReportLabel { Text = "x", Left = 0, Top = 0, Width = 1, Height = 0.4 });

        var pages = new ReportRenderEngine().Render(report, 96f);
        var headerTexts = DrawnTexts(pages[0]);

        Assert.Contains("Alpha", headerTexts);
        Assert.DoesNotContain("Beta", headerTexts);
    }

    [Fact]
    public void Engine_PageFooter_ResolvesCrossSourceQualifiedField()
    {
        var report = new Report("CrossSource")
        {
            PageWidth = 21.0,
            PageHeight = 29.7
        };
        var kopf = report.DataSources.Add("Auftragskopf");
        kopf.Data = new List<KopfRecord> { new() { Kunde = "Mueller", AuftragsNr = 5 } };
        var pos = report.DataSources.Add("Pos");
        pos.Data = new List<PositionRecord>
        {
            new() { Artikel = "P1", Betrag = 1m },
            new() { Artikel = "P2", Betrag = 2m }
        };

        report.Detail.Height = 0.5;
        report.Detail.DataSourceName = "Pos";
        report.Detail.Controls.Add(new ReportTextBox { DataField = "Artikel", Left = 0, Top = 0, Width = 3, Height = 0.4 });

        report.PageFooter.Height = 0.6;
        report.PageFooter.DataSourceName = "Auftragskopf";
        report.PageFooter.Controls.Add(new ReportTextBox
        {
            Expression = "Kunde: {Auftragskopf.Kunde}",
            Left = 0, Top = 0, Width = 8, Height = 0.5
        });

        var pages = new ReportRenderEngine().Render(report, 96f);
        var footerTexts = DrawnTexts(pages[0]);
        Assert.Contains(footerTexts, t => t.Contains("Mueller"));
    }

    [Fact]
    public void Engine_Groups_InheritDetailSource()
    {
        var report = new Report("GroupInherit")
        {
            PageWidth = 21.0,
            PageHeight = 29.7
        };
        var pos = report.DataSources.Add("Pos");
        pos.Data = new List<PositionRecord>
        {
            new() { Artikel = "A", Betrag = 1m },
            new() { Artikel = "B", Betrag = 2m }
        };

        var group = new ReportGroup("Artikel", "ByArtikel");
        group.Header.Height = 0.5;
        group.Header.Controls.Add(new ReportTextBox { DataField = "Artikel", Left = 0, Top = 0, Width = 3, Height = 0.4 });
        report.Groups.Add(group);

        report.Detail.Height = 0.5;
        report.Detail.DataSourceName = "Pos";
        report.Detail.Controls.Add(new ReportTextBox { DataField = "Betrag", Left = 0, Top = 0, Width = 3, Height = 0.4 });

        // Group header has no explicit DataSourceName; it must resolve against the
        // detail's source (Pos) so {Artikel} renders per group.
        var pages = new ReportRenderEngine().Render(report, 96f);
        var all = pages.SelectMany(DrawnTexts).ToList();
        Assert.Contains("A", all);
        Assert.Contains("B", all);
    }

    [Fact]
    public void Engine_LegacyDataSource_StillWorks_WhenNoDataSources()
    {
        var report = new Report("Legacy")
        {
            PageWidth = 21.0,
            PageHeight = 29.7,
            DataSource = new List<KopfRecord> { new() { Kunde = "LegacyKunde", AuftragsNr = 1 } }
        };
        report.Detail.Height = 0.5;
        report.Detail.Controls.Add(new ReportTextBox { DataField = "Kunde", Left = 0, Top = 0, Width = 5, Height = 0.4 });

        var pages = new ReportRenderEngine().Render(report, 96f);
        Assert.Contains("LegacyKunde", pages.SelectMany(DrawnTexts));
    }
}
