using System;
using System.Collections.Generic;
using System.Linq;
using OldSchoolForms.Ui.Reports;
using OldSchoolForms.Ui.Rendering;
using Xunit;

namespace OldSchoolForms.Ui.Tests;

/// <summary>
/// Tests for <see cref="ReportSampleData"/>: deterministic value generation per
/// data type, field-name heuristics, group-field value repetition, and the
/// <c>EnsurePreviewData</c> population rules for named and legacy data sources.
/// </summary>
public class ReportSampleDataTests
{
    private static readonly string[] KnownCities =
    {
        "Berlin", "Hamburg", "München", "Köln", "Frankfurt am Main",
        "Stuttgart", "Düsseldorf", "Leipzig", "Dresden", "Hannover"
    };

    private static readonly string[] KnownCompanies =
    {
        "Muster GmbH", "Beispiel AG", "Testwerk KG", "Nordhandel OHG",
        "Südstrom GmbH", "Bergbau AG", "Lichtblick GmbH", "Hansehandel KG"
    };

    private static string Canonical(List<Dictionary<string, object?>> records)
        => string.Join("|", records.Select(r =>
            string.Join(",", r.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kv => kv.Key + "=" + kv.Value))));

    // ---- Value generation by data type ------------------------------------

    [Fact]
    public void GenerateValue_ReturnsClrType_ForEveryDataType()
    {
        var random = new Random(1);
        var baseDate = new DateTime(2026, 1, 1);

        Assert.IsType<int>(ReportSampleData.GenerateValue(ReportDataType.Int, "Wert", 0, random, baseDate));
        Assert.IsType<long>(ReportSampleData.GenerateValue(ReportDataType.Long, "Wert", 0, random, baseDate));
        Assert.IsType<decimal>(ReportSampleData.GenerateValue(ReportDataType.Decimal, "Wert", 0, random, baseDate));
        Assert.IsType<double>(ReportSampleData.GenerateValue(ReportDataType.Double, "Wert", 0, random, baseDate));
        Assert.IsType<bool>(ReportSampleData.GenerateValue(ReportDataType.Boolean, "Wert", 0, random, baseDate));
        Assert.IsType<DateTime>(ReportSampleData.GenerateValue(ReportDataType.DateTime, "Wert", 0, random, baseDate));
        Assert.IsType<Guid>(ReportSampleData.GenerateValue(ReportDataType.Guid, "Wert", 0, random, baseDate));
        Assert.IsType<string>(ReportSampleData.GenerateValue(ReportDataType.String, "Wert", 0, random, baseDate));
    }

    [Fact]
    public void GenerateValue_DateTime_AnchorsToBaseDatePlusRowIndex()
    {
        var random = new Random(1);
        var baseDate = new DateTime(2026, 3, 10, 15, 45, 0);

        var date = (DateTime)ReportSampleData.GenerateValue(ReportDataType.DateTime, "Datum", 3, random, baseDate);

        Assert.Equal(new DateTime(2026, 3, 13, 8, 39, 0), date);
    }

    [Fact]
    public void GenerateValue_Boolean_AlternatesByRowIndex()
    {
        var random = new Random(1);
        var baseDate = new DateTime(2026, 1, 1);

        Assert.True((bool)ReportSampleData.GenerateValue(ReportDataType.Boolean, "Flag", 0, random, baseDate));
        Assert.False((bool)ReportSampleData.GenerateValue(ReportDataType.Boolean, "Flag", 1, random, baseDate));
        Assert.True((bool)ReportSampleData.GenerateValue(ReportDataType.Boolean, "Flag", 2, random, baseDate));
    }

    // ---- Field-name heuristics ------------------------------------------

    [Fact]
    public void GenerateValue_StringField_City_YieldsKnownCity()
    {
        var random = new Random(1);
        var baseDate = new DateTime(2026, 1, 1);

        var value = (string)ReportSampleData.GenerateValue(ReportDataType.String, "Stadt", 2, random, baseDate);

        Assert.Contains(value, KnownCities);
    }

    [Fact]
    public void GenerateValue_StringField_Customer_YieldsKnownCompany()
    {
        var random = new Random(1);
        var baseDate = new DateTime(2026, 1, 1);

        var value = (string)ReportSampleData.GenerateValue(ReportDataType.String, "Kunde", 1, random, baseDate);

        Assert.Contains(value, KnownCompanies);
    }

    [Fact]
    public void GenerateValue_StringField_Email_ContainsDomain()
    {
        var random = new Random(1);
        var baseDate = new DateTime(2026, 1, 1);

        var value = (string)ReportSampleData.GenerateValue(ReportDataType.String, "Email", 0, random, baseDate);

        Assert.Contains("@example.com", value);
    }

    [Fact]
    public void GenerateValue_DecimalField_Price_IsMoneyWithTwoDecimals()
    {
        var random = new Random(7);
        var baseDate = new DateTime(2026, 1, 1);

        for (int i = 0; i < 20; i++)
        {
            var value = (decimal)ReportSampleData.GenerateValue(ReportDataType.Decimal, "Preis", i, random, baseDate);
            Assert.InRange(value, 1.99m, 999.99m);
            Assert.Equal(value, Math.Round(value, 2));
        }
    }

    [Fact]
    public void GenerateValue_IntField_Quantity_IsSmallPositive()
    {
        var random = new Random(3);
        var baseDate = new DateTime(2026, 1, 1);

        var value = (int)ReportSampleData.GenerateValue(ReportDataType.Int, "Menge", 5, random, baseDate);

        Assert.InRange(value, 1, 20);
    }

    [Fact]
    public void GenerateValue_UnknownStringField_FallsBackToNameAndIndex()
    {
        var random = new Random(1);
        var baseDate = new DateTime(2026, 1, 1);

        var value = (string)ReportSampleData.GenerateValue(ReportDataType.String, "Xyz", 4, random, baseDate);

        Assert.Equal("Xyz 5", value);
    }

    [Fact]
    public void GenerateValue_Guid_IsDeterministicPerFieldAndRow()
    {
        var a = ReportSampleData.GenerateValue(ReportDataType.Guid, "Id", 2, new Random(1), DateTime.Now);
        var b = ReportSampleData.GenerateValue(ReportDataType.Guid, "Id", 2, new Random(999), DateTime.Now);
        var c = ReportSampleData.GenerateValue(ReportDataType.Guid, "Id", 3, new Random(1), DateTime.Now);

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    // ---- Record generation ----------------------------------------------

    [Fact]
    public void GenerateRecords_CoversAllFields_AndRowCount()
    {
        var fields = new[]
        {
            ("Name", ReportDataType.String),
            ("Betrag", ReportDataType.Decimal),
            ("Anzahl", ReportDataType.Int)
        };

        var records = ReportSampleData.GenerateRecords(fields, rowCount: 6, seed: 42);

        Assert.Equal(6, records.Count);
        foreach (var record in records)
        {
            Assert.Equal(3, record.Count);
            Assert.True(record.ContainsKey("Name"));
            Assert.True(record.ContainsKey("Betrag"));
            Assert.True(record.ContainsKey("Anzahl"));
        }
    }

    [Fact]
    public void GenerateRecords_IsDeterministic_ForSameSeed()
    {
        var fields = new[]
        {
            ("Name", ReportDataType.String),
            ("Betrag", ReportDataType.Decimal),
            ("Datum", ReportDataType.DateTime)
        };

        var first = ReportSampleData.GenerateRecords(fields, rowCount: 5, seed: 123, baseDate: new DateTime(2026, 1, 1));
        var second = ReportSampleData.GenerateRecords(fields, rowCount: 5, seed: 123, baseDate: new DateTime(2026, 1, 1));

        Assert.Equal(Canonical(first), Canonical(second));
    }

    [Fact]
    public void GenerateRecords_FromDataSource_UsesSchemaTypes()
    {
        var source = new ReportDataSource("Pos");
        source.AddField("Artikel", ReportDataType.String);
        source.AddField("Betrag", ReportDataType.Decimal);

        var records = ReportSampleData.GenerateRecords(source, rowCount: 3, seed: 5);

        Assert.Equal(3, records.Count);
        Assert.All(records, r => Assert.IsType<string>(r["Artikel"]));
        Assert.All(records, r => Assert.IsType<decimal>(r["Betrag"]));
    }

    // ---- EnsurePreviewData ----------------------------------------------

    [Fact]
    public void EnsurePreviewData_PopulatesEmptyNamedSources()
    {
        var report = new Report("R");
        var source = report.DataSources.Add("Pos");
        source.AddField("Artikel", ReportDataType.String);
        source.AddField("Betrag", ReportDataType.Decimal);

        int populated = ReportSampleData.EnsurePreviewData(report, rowCount: 7);

        Assert.Equal(1, populated);
        var data = Assert.IsAssignableFrom<System.Collections.IEnumerable>(source.Data);
        Assert.Equal(7, data.Cast<object>().Count());
    }

    [Fact]
    public void EnsurePreviewData_LeavesExistingDataUntouched()
    {
        var report = new Report("R");
        var source = report.DataSources.Add("Pos");
        source.AddField("Artikel", ReportDataType.String);
        var existing = new List<Dictionary<string, object?>>
        {
            new() { ["Artikel"] = "Real" }
        };
        source.Data = existing;

        int populated = ReportSampleData.EnsurePreviewData(report);

        Assert.Equal(0, populated);
        Assert.Same(existing, source.Data);
    }

    [Fact]
    public void EnsurePreviewData_SkipsSchemalessSource()
    {
        var report = new Report("R");
        report.DataSources.Add("Empty"); // no fields

        int populated = ReportSampleData.EnsurePreviewData(report);

        Assert.Equal(0, populated);
        Assert.Null(report.DataSources[0].Data);
    }

    [Fact]
    public void EnsurePreviewData_LegacyMode_UsesLegacyFields()
    {
        var report = new Report("R"); // no data sources
        var legacyFields = new[] { ("Kunde", ReportDataType.String), ("Summe", ReportDataType.Decimal) };

        int populated = ReportSampleData.EnsurePreviewData(report, legacyFields, rowCount: 4);

        Assert.Equal(1, populated);
        var data = Assert.IsAssignableFrom<System.Collections.IEnumerable>(report.DataSource);
        var records = data.Cast<object>().ToList();
        Assert.Equal(4, records.Count);
    }

    [Fact]
    public void EnsurePreviewData_LegacyMode_SkipsWhenDataSourceHasData()
    {
        var report = new Report("R")
        {
            DataSource = new List<Dictionary<string, object?>> { new() { ["Kunde"] = "Real" } }
        };
        var legacyFields = new[] { ("Kunde", ReportDataType.String) };

        int populated = ReportSampleData.EnsurePreviewData(report, legacyFields);

        Assert.Equal(0, populated);
    }

    [Fact]
    public void EnsurePreviewData_GroupField_RepeatsValues()
    {
        var report = new Report("R");
        var source = report.DataSources.Add("Pos");
        source.AddField("Kunde", ReportDataType.String);
        source.AddField("Betrag", ReportDataType.Decimal);
        report.Groups.Add(new ReportGroup("Kunde"));

        ReportSampleData.EnsurePreviewData(report, rowCount: 8);

        var data = ((System.Collections.IEnumerable)source.Data!)
            .Cast<Dictionary<string, object?>>()
            .ToList();
        int distinctCustomers = data.Select(r => (string)r["Kunde"]!).Distinct().Count();

        Assert.Equal(3, distinctCustomers);
    }

    [Fact]
    public void EnsurePreviewData_NullReport_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ReportSampleData.EnsurePreviewData(null!));
    }

    [Fact]
    public void EnsurePreviewData_NonPositiveRowCount_DoesNothing()
    {
        var report = new Report("R");
        var source = report.DataSources.Add("Pos");
        source.AddField("Artikel", ReportDataType.String);

        int populated = ReportSampleData.EnsurePreviewData(report, rowCount: 0);

        Assert.Equal(0, populated);
        Assert.Null(source.Data);
    }

    // ---- Integration: generated data drives the render engine ------------

    [Fact]
    public void EnsurePreviewData_SampleData_FlowsIntoRenderedDetail()
    {
        var report = new Report("Preview")
        {
            PageWidth = 21.0,
            PageHeight = 29.7
        };
        var pos = report.DataSources.Add("Pos");
        pos.AddField("Artikel", ReportDataType.String);
        pos.AddField("Betrag", ReportDataType.Decimal);
        report.Detail.Height = 0.6;
        report.Detail.DataSourceName = "Pos";
        report.Detail.Controls.Add(new ReportTextBox
        {
            DataField = "Artikel",
            Left = 0, Top = 0, Width = 8, Height = 0.5
        });

        ReportSampleData.EnsurePreviewData(report, rowCount: 8);
        var pages = new ReportRenderEngine().Render(report, 96f);

        var drawn = pages
            .SelectMany(p => p.Graphics.GetCommands())
            .Where(c => c.Type == DrawCommandType.DrawString && c.Text != null)
            .Select(c => c.Text!)
            .ToList();

        // Eight detail records -> eight drawn article values (all distinct from the product bank).
        Assert.Equal(8, drawn.Count);
        Assert.All(drawn, t => Assert.False(string.IsNullOrWhiteSpace(t)));
    }
}
