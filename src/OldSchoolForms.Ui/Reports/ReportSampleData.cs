using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace OldSchoolForms.Ui.Reports;

/// <summary>
/// Generates deterministic sample records for the data sources of a
/// <see cref="Report"/> so the report layout can be previewed and tested without
/// real data being attached.
/// <para>
/// Values are derived from each field's <see cref="ReportDataSourceField.DataType"/>
/// and refined by heuristics on the field name (e.g. a string field named
/// <c>Stadt</c> yields city names, a decimal field named <c>Preis</c> yields
/// money-like amounts). Generation is seeded so repeated previews of the same
/// schema produce identical data, which keeps layout testing stable.
/// </para>
/// <para>
/// The generated records are plain <see cref="Dictionary{TKey,TValue}"/> objects,
/// the record shape understood by <c>DataRecord.GetValue</c>, and are assigned to
/// the runtime-only <see cref="ReportDataSource.Data"/> /
/// <see cref="Report.DataSource"/> properties. They are never persisted by the
/// <c>OldSchoolForms.Report</c> JSON serializer.
/// </para>
/// </summary>
public static class ReportSampleData
{
    /// <summary>Default number of sample records generated per data source.</summary>
    public const int DefaultRowCount = 8;

    /// <summary>Default seed used for deterministic value generation.</summary>
    public const int DefaultSeed = 20261024;

    /// <summary>
    /// Ensures the report carries sample data for preview. Every declared data
    /// source that has at least one schema field but no data yet receives
    /// <paramref name="rowCount"/> generated records. Sources that already carry
    /// data are left untouched. When the report declares no data sources at all
    /// (legacy single-source mode) and its <see cref="Report.DataSource"/> is
    /// empty, the optional <paramref name="legacyFields"/> are used to populate it.
    /// </summary>
    /// <param name="report">The report to populate with sample data.</param>
    /// <param name="legacyFields">
    /// Field names (and types) to use for the legacy <see cref="Report.DataSource"/>
    /// when the report declares no <see cref="Report.DataSources"/>. Ignored when
    /// named data sources exist. May be <see langword="null"/>.
    /// </param>
    /// <param name="rowCount">Number of sample records to generate per source.</param>
    /// <param name="seed">Seed for deterministic generation.</param>
    /// <param name="baseDate">
    /// Anchor date for generated <see cref="DateTime"/> values. Defaults to
    /// <see cref="DateTime.Now"/> when <see langword="null"/>.
    /// </param>
    /// <returns>The number of data sources (or the legacy source) that were populated.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="report"/> is <see langword="null"/>.</exception>
    public static int EnsurePreviewData(
        Report report,
        IEnumerable<(string Name, ReportDataType Type)>? legacyFields = null,
        int rowCount = DefaultRowCount,
        int seed = DefaultSeed,
        DateTime? baseDate = null)
    {
        if (report == null) throw new ArgumentNullException(nameof(report));
        if (rowCount <= 0) return 0;

        var anchor = baseDate ?? DateTime.Now;
        var groupFields = CollectGroupFields(report);
        int populated = 0;

        if (report.DataSources.Count > 0)
        {
            foreach (var source in report.DataSources)
            {
                if (source.Fields.Count == 0) continue;
                if (HasData(source.Data)) continue;

                source.Data = GenerateRecordsCore(
                    source.Fields
                        .Where(f => !string.IsNullOrWhiteSpace(f.Name))
                        .Select(f => (f.Name, f.DataType)),
                    rowCount, seed, anchor, groupFields);
                populated++;
            }
            return populated;
        }

        // Legacy single-source mode: no named sources declared.
        if (!HasData(report.DataSource) && legacyFields != null)
        {
            var fields = legacyFields.ToList();
            if (fields.Count > 0)
            {
                report.DataSource = GenerateRecordsCore(fields, rowCount, seed, anchor, groupFields);
                populated = 1;
            }
        }

        return populated;
    }

    /// <summary>
    /// Generates sample records for the schema of a single data source.
    /// </summary>
    /// <param name="source">The data source whose fields define the record shape.</param>
    /// <param name="rowCount">Number of records to generate.</param>
    /// <param name="seed">Seed for deterministic generation.</param>
    /// <param name="baseDate">Anchor date for <see cref="DateTime"/> values.</param>
    /// <returns>A list of records mapping each field name to a generated value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    public static List<Dictionary<string, object?>> GenerateRecords(
        ReportDataSource source,
        int rowCount = DefaultRowCount,
        int seed = DefaultSeed,
        DateTime? baseDate = null)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        return GenerateRecordsCore(
            source.Fields
                .Where(f => !string.IsNullOrWhiteSpace(f.Name))
                .Select(f => (f.Name, f.DataType)),
            rowCount, seed, baseDate ?? DateTime.Now, null);
    }

    /// <summary>
    /// Generates sample records for an explicit list of field names and types.
    /// </summary>
    /// <param name="fields">The field names and their data types.</param>
    /// <param name="rowCount">Number of records to generate.</param>
    /// <param name="seed">Seed for deterministic generation.</param>
    /// <param name="baseDate">Anchor date for <see cref="DateTime"/> values.</param>
    /// <returns>A list of records mapping each field name to a generated value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fields"/> is <see langword="null"/>.</exception>
    public static List<Dictionary<string, object?>> GenerateRecords(
        IEnumerable<(string Name, ReportDataType Type)> fields,
        int rowCount = DefaultRowCount,
        int seed = DefaultSeed,
        DateTime? baseDate = null)
        => GenerateRecordsCore(fields, rowCount, seed, baseDate ?? DateTime.Now, null);

    private static List<Dictionary<string, object?>> GenerateRecordsCore(
        IEnumerable<(string Name, ReportDataType Type)> fields,
        int rowCount,
        int seed,
        DateTime baseDate,
        HashSet<string>? groupFields)
    {
        var fieldList = fields.ToList();
        var records = new List<Dictionary<string, object?>>(rowCount);
        var random = new Random(seed);

        for (int row = 0; row < rowCount; row++)
        {
            var record = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (name, type) in fieldList)
            {
                bool isGroup = groupFields != null && groupFields.Contains(name);
                record[name] = GenerateValue(type, name, row, random, baseDate, isGroup);
            }
            records.Add(record);
        }

        return records;
    }

    /// <summary>
    /// Generates a single sample value for a field of the given data type.
    /// </summary>
    /// <param name="type">The field's data type.</param>
    /// <param name="fieldName">The field name, used to refine the value via heuristics.</param>
    /// <param name="rowIndex">The zero-based record index, used for stable variation across rows.</param>
    /// <param name="random">The seeded random source.</param>
    /// <param name="baseDate">Anchor date for <see cref="DateTime"/> values.</param>
    /// <param name="isGroupField">
    /// When <see langword="true"/>, string values are drawn from a small set so the
    /// field forms a few repeated groups instead of all-distinct values.
    /// </param>
    /// <returns>The generated value.</returns>
    public static object? GenerateValue(
        ReportDataType type,
        string fieldName,
        int rowIndex,
        Random random,
        DateTime baseDate,
        bool isGroupField = false)
    {
        switch (type)
        {
            case ReportDataType.Int:
                return GenerateInt(fieldName, rowIndex, random);
            case ReportDataType.Long:
                return (long)GenerateInt(fieldName, rowIndex, random);
            case ReportDataType.Decimal:
                return GenerateDecimal(fieldName, rowIndex, random);
            case ReportDataType.Double:
                return (double)Math.Round(GenerateDecimal(fieldName, rowIndex, random), 4);
            case ReportDataType.Boolean:
                return rowIndex % 2 == 0;
            case ReportDataType.DateTime:
                return baseDate.Date
                    .AddDays(rowIndex)
                    .AddHours(8)
                    .AddMinutes((rowIndex * 13) % 60);
            case ReportDataType.Guid:
                return GenerateGuid(fieldName, rowIndex);
            default:
                return GenerateString(fieldName, rowIndex, isGroupField);
        }
    }

    private static int GenerateInt(string fieldName, int rowIndex, Random random)
    {
        if (Matches(fieldName, "menge", "qty", "quantity", "anzahl", "count", "stück", "stueck", "position"))
            return 1 + rowIndex % 20;
        if (IsIdentifier(fieldName))
            return rowIndex + 1;
        return random.Next(1, 1001);
    }

    private static decimal GenerateDecimal(string fieldName, int rowIndex, Random random)
    {
        if (Matches(fieldName, "preis", "price", "betrag", "amount", "summe", "sum", "total",
                "umsatz", "revenue", "kosten", "netto", "brutto", "mwst", "steuer", "tax"))
        {
            // Money-like value between 1.99 and 999.99 with two decimals.
            return Math.Round(random.Next(199, 100000) / 100m, 2);
        }
        return Math.Round((decimal)random.NextDouble() * 100m, 2);
    }

    private static Guid GenerateGuid(string fieldName, int rowIndex)
    {
        // Deterministic GUID derived from the field name and row index.
        var bytes = new byte[16];
        int h = StableHash(fieldName) ^ unchecked(rowIndex * unchecked((int)0x9E3779B9));
        for (int i = 0; i < 16; i++)
        {
            h = unchecked(h * 1103515245 + 12345);
            bytes[i] = (byte)((h >> 16) & 0xFF);
        }
        return new Guid(bytes);
    }

    private static string GenerateString(string fieldName, int rowIndex, bool isGroupField)
    {
        if (Matches(fieldName, "email", "e-mail", "mail"))
            return EmailFor(rowIndex);
        if (Matches(fieldName, "telefon", "phone", "tel"))
            return PhoneFor(rowIndex);
        if (Matches(fieldName, "straße", "strasse", "street", "adresse", "address"))
            return AddressFor(rowIndex);
        if (Matches(fieldName, "plz", "postleitzahl", "postcode", "zip"))
            return PostalCodeFor(rowIndex);
        if (Matches(fieldName, "stadt", "city", "wohnort", "ortsteil"))
            return Pick(Cities, rowIndex, isGroupField);
        if (Matches(fieldName, "land", "country", "bundesland"))
            return Pick(Countries, rowIndex, isGroupField);
        if (Matches(fieldName, "kund", "customer", "client", "firma", "company"))
            return Pick(Companies, rowIndex, isGroupField);
        if (Matches(fieldName, "produkt", "product", "artikel", "article", "ware", "item"))
            return Pick(Products, rowIndex, isGroupField);
        if (Matches(fieldName, "kategorie", "category", "katergorie"))
            return Pick(Categories, rowIndex, isGroupField);
        if (Matches(fieldName, "status", "zustand"))
            return Pick(StatusValues, rowIndex, isGroupField);
        if (Matches(fieldName, "bemerkung", "notiz", "comment", "note", "beschreibung", "description", "anmerkung"))
            return Pick(Notes, rowIndex, isGroupField);
        if (Matches(fieldName, "name") && !Matches(fieldName, "filename", "file name", "dateiname"))
            return Pick(PersonNames, rowIndex, isGroupField);

        return fieldName + " " + (rowIndex + 1);
    }

    private static string EmailFor(int rowIndex)
    {
        var name = NormalizeAscii(PersonNames[rowIndex % PersonNames.Length]);
        var local = name.ToLowerInvariant().Replace(' ', '.');
        return local + "@example.com";
    }

    private static string PhoneFor(int rowIndex)
    {
        int area = 30 + (rowIndex % 70);
        int number = 100000 + (rowIndex * 1337) % 900000;
        return "+49 " + area + " " + number;
    }

    private static string AddressFor(int rowIndex)
    {
        int houseNo = 2 + (rowIndex * 3) % 40;
        return Streets[rowIndex % Streets.Length] + " " + houseNo;
    }

    private static string PostalCodeFor(int rowIndex)
    {
        int code = 10115 + (rowIndex * 1111) % 90000;
        return code.ToString("D5");
    }

    private static string Pick(string[] bank, int rowIndex, bool isGroupField)
    {
        // Group fields cycle through a small prefix so a few repeated groups form;
        // ordinary fields cycle through the whole bank.
        int span = isGroupField ? Math.Min(3, bank.Length) : bank.Length;
        return bank[rowIndex % span];
    }

    private static bool IsIdentifier(string fieldName)
    {
        var n = NormalizeAscii(fieldName).ToLowerInvariant();
        return n == "id"
            || n.EndsWith("id", StringComparison.Ordinal) && n.Length <= 4
            || n.EndsWith("nummer", StringComparison.Ordinal)
            || n.EndsWith("number", StringComparison.Ordinal)
            || n.EndsWith("nr", StringComparison.Ordinal)
            || n.EndsWith("no", StringComparison.Ordinal);
    }

    private static bool Matches(string fieldName, params string[] keys)
    {
        var n = NormalizeAscii(fieldName).ToLowerInvariant();
        foreach (var key in keys)
        {
            if (n.Contains(key, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Normalizes German umlauts to their ASCII transliteration so heuristics and
    /// generated e-mail addresses stay ASCII-safe and match regardless of how the
    /// field name spells umlauts.
    /// </summary>
    private static string NormalizeAscii(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value
            .Replace('ä', 'a').Replace('Ä', 'A')
            .Replace('ö', 'o').Replace('Ö', 'O')
            .Replace('ü', 'u').Replace('Ü', 'U')
            .Replace('ß', 's');
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            int hash = 23;
            foreach (char c in value)
                hash = hash * 31 + c;
            return hash;
        }
    }

    private static HashSet<string> CollectGroupFields(Report report)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectGroupFields(report.Groups, set);
        return set;
    }

    private static void CollectGroupFields(ReportGroupCollection groups, HashSet<string> set)
    {
        foreach (var group in groups)
        {
            if (!string.IsNullOrWhiteSpace(group.GroupField))
                set.Add(group.GroupField);
            CollectGroupFields(group.Groups, set);
        }
    }

    private static bool HasData(object? data)
    {
        if (data == null) return false;
        if (data is string) return false;
        if (data is ICollection collection) return collection.Count > 0;
        if (data is IEnumerable enumerable)
        {
            foreach (var _ in enumerable) return true;
            return false;
        }
        return true;
    }

    private static readonly string[] PersonNames =
    {
        "Anna Müller", "Ben Schmidt", "Clara Weber", "David Fischer", "Elena Wagner",
        "Felix Becker", "Greta Hoffmann", "Hans Koch", "Ida Richter", "Jonas Klein"
    };

    private static readonly string[] Companies =
    {
        "Muster GmbH", "Beispiel AG", "Testwerk KG", "Nordhandel OHG",
        "Südstrom GmbH", "Bergbau AG", "Lichtblick GmbH", "Hansehandel KG"
    };

    private static readonly string[] Cities =
    {
        "Berlin", "Hamburg", "München", "Köln", "Frankfurt am Main",
        "Stuttgart", "Düsseldorf", "Leipzig", "Dresden", "Hannover"
    };

    private static readonly string[] Streets =
    {
        "Hauptstraße", "Bahnhofstraße", "Gartenweg", "Lindenallee", "Schulstraße",
        "Bergstraße", "Kirchgasse", "Marktplatz", "Ringstraße", "Waldweg"
    };

    private static readonly string[] Countries =
    {
        "Deutschland", "Österreich", "Schweiz", "Frankreich",
        "Italien", "Niederlande", "Belgien", "Polen"
    };

    private static readonly string[] Products =
    {
        "Widget Standard", "Gadget Pro", "Bauteil A", "Modul X2",
        "Komponente 5", "Einheit Nord", "Set Süd", "Paket XL"
    };

    private static readonly string[] Categories =
    {
        "Allgemein", "Technik", "Büro", "Service", "Privat", "Firmenkunde"
    };

    private static readonly string[] StatusValues =
    {
        "Offen", "In Bearbeitung", "Erledigt", "Storniert", "Bezahlt", "Geliefert"
    };

    private static readonly string[] Notes =
    {
        "Eilbedürftig", "Bitte rückmelden", "Kundensonderwunsch",
        "Standardlieferung", "Nachtermin vereinbart", "Keine Beanstandung"
    };
}
