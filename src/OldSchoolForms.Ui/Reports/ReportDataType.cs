namespace OldSchoolForms.Ui.Reports;

/// <summary>
/// Data types available for fields of a <see cref="ReportDataSource"/>.
/// The set mirrors the simple type names supported by the scripting
/// <c>JsDataTable.AddColumn</c> mapping so schema definitions round-trip
/// consistently between the designer, the JSON format, and the JS runtime.
/// </summary>
public enum ReportDataType
{
    /// <summary>A text field (<see cref="string"/>).</summary>
    String,

    /// <summary>A 32-bit integer field (<see cref="int"/>).</summary>
    Int,

    /// <summary>A 64-bit integer field (<see cref="long"/>).</summary>
    Long,

    /// <summary>An exact decimal field (<see cref="decimal"/>).</summary>
    Decimal,

    /// <summary>A double-precision floating point field (<see cref="double"/>).</summary>
    Double,

    /// <summary>A boolean field (<see cref="bool"/>).</summary>
    Boolean,

    /// <summary>A date/time field (<see cref="DateTime"/>).</summary>
    DateTime,

    /// <summary>A globally unique identifier field (<see cref="Guid"/>).</summary>
    Guid
}

/// <summary>
/// Maps <see cref="ReportDataType"/> values to CLR types and back, and parses
/// the lowercase type names used in the <c>OldSchoolForms.Report</c> JSON format.
/// </summary>
public static class ReportDataTypeMapper
{
    /// <summary>
    /// Gets the CLR type backing the given report data type.
    /// </summary>
    /// <param name="dataType">The report data type.</param>
    /// <returns>The corresponding CLR type.</returns>
    public static Type ToClrType(this ReportDataType dataType) => dataType switch
    {
        ReportDataType.String => typeof(string),
        ReportDataType.Int => typeof(int),
        ReportDataType.Long => typeof(long),
        ReportDataType.Decimal => typeof(decimal),
        ReportDataType.Double => typeof(double),
        ReportDataType.Boolean => typeof(bool),
        ReportDataType.DateTime => typeof(DateTime),
        ReportDataType.Guid => typeof(Guid),
        _ => typeof(string)
    };

    /// <summary>
    /// Maps a CLR type to the closest report data type.
    /// Unknown types map to <see cref="ReportDataType.String"/>.
    /// </summary>
    /// <param name="clrType">The CLR type to map.</param>
    /// <returns>The corresponding report data type.</returns>
    public static ReportDataType FromClrType(Type? clrType)
    {
        if (clrType == null) return ReportDataType.String;
        var t = Nullable.GetUnderlyingType(clrType) ?? clrType;
        if (t == typeof(int)) return ReportDataType.Int;
        if (t == typeof(long)) return ReportDataType.Long;
        if (t == typeof(decimal)) return ReportDataType.Decimal;
        if (t == typeof(double)) return ReportDataType.Double;
        if (t == typeof(float)) return ReportDataType.Double;
        if (t == typeof(bool)) return ReportDataType.Boolean;
        if (t == typeof(DateTime)) return ReportDataType.DateTime;
        if (t == typeof(Guid)) return ReportDataType.Guid;
        return ReportDataType.String;
    }

    /// <summary>
    /// Gets the lowercase JSON name of the given report data type
    /// (e.g. <c>"string"</c>, <c>"decimal"</c>, <c>"datetime"</c>).
    /// </summary>
    /// <param name="dataType">The report data type.</param>
    /// <returns>The lowercase type name.</returns>
    public static string ToJsonName(this ReportDataType dataType) => dataType switch
    {
        ReportDataType.String => "string",
        ReportDataType.Int => "int",
        ReportDataType.Long => "long",
        ReportDataType.Decimal => "decimal",
        ReportDataType.Double => "double",
        ReportDataType.Boolean => "boolean",
        ReportDataType.DateTime => "datetime",
        ReportDataType.Guid => "guid",
        _ => "string"
    };

    /// <summary>
    /// Parses a type name (case-insensitive) into a <see cref="ReportDataType"/>.
    /// Accepts the JSON names plus common aliases (<c>bool</c>, <c>date</c>,
    /// <c>integer</c>, <c>float</c>).
    /// </summary>
    /// <param name="name">The type name to parse.</param>
    /// <param name="result">The parsed data type when this method returns <c>true</c>.</param>
    /// <returns><c>true</c> when the name could be parsed; otherwise <c>false</c>.</returns>
    public static bool TryParse(string? name, out ReportDataType result)
    {
        result = ReportDataType.String;
        switch (name?.Trim().ToLowerInvariant())
        {
            case "string":
                result = ReportDataType.String;
                return true;
            case "int":
            case "integer":
                result = ReportDataType.Int;
                return true;
            case "long":
                result = ReportDataType.Long;
                return true;
            case "decimal":
                result = ReportDataType.Decimal;
                return true;
            case "double":
            case "float":
                result = ReportDataType.Double;
                return true;
            case "bool":
            case "boolean":
                result = ReportDataType.Boolean;
                return true;
            case "datetime":
            case "date":
                result = ReportDataType.DateTime;
                return true;
            case "guid":
                result = ReportDataType.Guid;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Parses a type name (case-insensitive) into a <see cref="ReportDataType"/>,
    /// falling back to <see cref="ReportDataType.String"/> for unknown names.
    /// </summary>
    /// <param name="name">The type name to parse.</param>
    /// <returns>The parsed data type, or <see cref="ReportDataType.String"/>.</returns>
    public static ReportDataType ParseOrString(string? name)
        => TryParse(name, out var result) ? result : ReportDataType.String;
}
