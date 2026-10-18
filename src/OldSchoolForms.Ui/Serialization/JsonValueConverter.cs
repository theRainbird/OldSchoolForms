using System.Globalization;
using System.Text.Json.Nodes;
using OldSchoolForms.Ui.Core;

namespace OldSchoolForms.Ui.Serialization;

/// <summary>
/// Converts the value types used by designed controls to and from JSON nodes.
/// Supports primitives (string, bool, int, float, double, decimal), the custom
/// OldSchoolForms value types (<see cref="Color"/>, <see cref="Point"/>,
/// <see cref="Size"/>, <see cref="Padding"/>, <see cref="Font"/>), enums
/// (serialized by name, flags as a comma-separated list) and images
/// (<see cref="SvgImage"/> / <see cref="RasterImage"/>).
/// </summary>
public static class JsonValueConverter
{
    /// <summary>
    /// Determines whether values of the given type can be represented in the layout JSON format.
    /// </summary>
    /// <param name="type">The type to test.</param>
    /// <returns><see langword="true"/> when the type is serializable; otherwise <see langword="false"/>.</returns>
    public static bool IsSupportedType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type == typeof(string)
            || type == typeof(bool)
            || type == typeof(int)
            || type == typeof(float)
            || type == typeof(double)
            || type == typeof(decimal)
            || type == typeof(Color)
            || type == typeof(Point)
            || type == typeof(Size)
            || type == typeof(Padding)
            || type == typeof(Font)
            || type.IsEnum
            || type == typeof(SvgImage)
            || type == typeof(RasterImage)
            || type == typeof(IGraphicsImage);
    }

    /// <summary>
    /// Converts a supported CLR value into its JSON node representation.
    /// </summary>
    /// <param name="value">The value to convert; may be <see langword="null"/>.</param>
    /// <returns>The JSON node, or <see langword="null"/> when the value is null or of an unsupported type.</returns>
    public static JsonNode? ToNode(object? value)
    {
        switch (value)
        {
            case null:
                return null;
            case string s:
                return JsonValue.Create(s);
            case bool b:
                return JsonValue.Create(b);
            case int i:
                return JsonValue.Create(i);
            case float f:
                return JsonValue.Create(f);
            case double d:
                return JsonValue.Create(d);
            case decimal m:
                return JsonValue.Create(m);
            case Color c:
                return ColorToNode(c);
            case Point p:
                return new JsonObject { ["x"] = p.X, ["y"] = p.Y };
            case Size sz:
                return new JsonObject { ["width"] = sz.Width, ["height"] = sz.Height };
            case Padding pad:
                return new JsonObject { ["left"] = pad.Left, ["top"] = pad.Top, ["right"] = pad.Right, ["bottom"] = pad.Bottom };
            case Font font:
                return FontToNode(font);
            case Enum e:
                return JsonValue.Create(e.ToString());
            case SvgImage svg:
                return SvgImageToNode(svg);
            case RasterImage raster:
                return RasterImageToNode(raster);
            default:
                return null;
        }
    }

    /// <summary>
    /// Converts a JSON node back into a CLR value of the requested target type.
    /// </summary>
    /// <param name="node">The JSON node to convert; may be <see langword="null"/>.</param>
    /// <param name="target">The CLR type the value should be converted to.</param>
    /// <param name="warnings">An optional list that receives a warning when conversion fails.</param>
    /// <returns>The converted value, or <see langword="null"/> when conversion is not possible.</returns>
    public static object? FromNode(JsonNode? node, Type target, List<string>? warnings = null)
    {
        if (node == null) return null;
        target = Nullable.GetUnderlyingType(target) ?? target;

        // For a loosely-typed target (object), infer the CLR type from the JSON node kind.
        if (target == typeof(object))
        {
            return node.GetValueKind() switch
            {
                System.Text.Json.JsonValueKind.String => node.GetValue<string>(),
                System.Text.Json.JsonValueKind.True => true,
                System.Text.Json.JsonValueKind.False => false,
                System.Text.Json.JsonValueKind.Number => InferNumber(node),
                _ => null
            };
        }

        try
        {
            if (target == typeof(string)) return node.GetValue<string>();
            if (target == typeof(bool)) return node.GetValue<bool>();
            if (target == typeof(int)) return node.GetValue<int>();
            if (target == typeof(float)) return node.GetValue<float>();
            if (target == typeof(double)) return node.GetValue<double>();
            if (target == typeof(decimal)) return node.GetValue<decimal>();
            if (target == typeof(Color)) return ColorFromNode(node);
            if (target == typeof(Point)) return PointFromNode(node);
            if (target == typeof(Size)) return SizeFromNode(node);
            if (target == typeof(Padding)) return PaddingFromNode(node);
            if (target == typeof(Font)) return FontFromNode(node);
            if (target.IsEnum) return ParseEnum(target, node);
            if (target == typeof(SvgImage)) return SvgImageFromNode(node, warnings);
            if (target == typeof(RasterImage)) return RasterImageFromNode(node, warnings);
            if (target == typeof(IGraphicsImage))
            {
                var kind = (node as JsonObject)?["kind"]?.GetValue<string>();
                return kind == "raster"
                    ? (object?)RasterImageFromNode(node, warnings)
                    : (object?)SvgImageFromNode(node, warnings);
            }
        }
        catch (Exception ex)
        {
            warnings?.Add($"Konnte Wert für Typ '{target.Name}' nicht konvertieren: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Formats a <see cref="Type"/> as a short, stable string for the layout format.
    /// </summary>
    /// <param name="type">The type to format.</param>
    /// <returns>A string representation of the type.</returns>
    public static string TypeToString(Type type)
    {
        if (type == typeof(int)) return "int";
        if (type == typeof(long)) return "long";
        if (type == typeof(double)) return "double";
        if (type == typeof(float)) return "float";
        if (type == typeof(decimal)) return "decimal";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(string)) return "string";
        if (type == typeof(DateTime)) return "DateTime";
        return type.FullName ?? type.Name;
    }

    /// <summary>
    /// Resolves a type string produced by <see cref="TypeToString"/> back into a <see cref="Type"/>.
    /// </summary>
    /// <param name="value">The type string to resolve.</param>
    /// <returns>The resolved type, or <see langword="null"/> when it cannot be found.</returns>
    public static Type? StringToType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        switch (value)
        {
            case "int": return typeof(int);
            case "long": return typeof(long);
            case "double": return typeof(double);
            case "float": return typeof(float);
            case "decimal": return typeof(decimal);
            case "bool": return typeof(bool);
            case "string": return typeof(string);
            case "DateTime": return typeof(DateTime);
            default:
                try { return Type.GetType(value, throwOnError: false); }
                catch { return null; }
        }
    }

    /// <summary>
    /// Compares two values of a supported type for equality, handling the custom value
    /// types that do not override <see cref="object.Equals(object)"/>.
    /// </summary>
    /// <param name="a">The first value.</param>
    /// <param name="b">The second value.</param>
    /// <returns><see langword="true"/> when both values are equal.</returns>
    public static bool ValuesEqual(object? a, object? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        if (a.GetType() != b.GetType()) return false;

        if (a is Point pa && b is Point pb) return pa.X == pb.X && pa.Y == pb.Y;
        if (a is Size sa && b is Size sb) return sa.Width == sb.Width && sa.Height == sb.Height;
        if (a is Padding pada && b is Padding padb)
            return pada.Left == padb.Left && pada.Top == padb.Top && pada.Right == padb.Right && pada.Bottom == padb.Bottom;
        if (a is Font fa && b is Font fb)
            return fa.Name == fb.Name && fa.Size.Equals(fb.Size) && fa.Style == fb.Style;

        return a.Equals(b);
    }

    private static JsonNode ColorToNode(Color c)
        => new JsonObject { ["r"] = (int)c.R, ["g"] = (int)c.G, ["b"] = (int)c.B, ["a"] = (int)c.A };

    private static Point PointFromNode(JsonNode node)
    {
        if (node is JsonObject o)
            return new Point(GetInt(o, "x"), GetInt(o, "y"));
        return Point.Empty;
    }

    private static Size SizeFromNode(JsonNode node)
    {
        if (node is JsonObject o)
            return new Size(GetInt(o, "width"), GetInt(o, "height"));
        return Size.Empty;
    }

    private static Padding PaddingFromNode(JsonNode node)
    {
        if (node is JsonObject o)
            return new Padding(GetInt(o, "left"), GetInt(o, "top"), GetInt(o, "right"), GetInt(o, "bottom"));
        return new Padding(0, 0, 0, 0);
    }

    private static Color ColorFromNode(JsonNode node)
    {
        if (node is JsonObject o)
        {
            int r = GetInt(o, "r");
            int g = GetInt(o, "g");
            int b = GetInt(o, "b");
            int a = o.ContainsKey("a") ? GetInt(o, "a") : 255;
            return Color.FromArgb(r, g, b, a);
        }
        if (node.GetValueKind() == System.Text.Json.JsonValueKind.String)
        {
            var s = node.GetValue<string>();
            if (s != null && s.StartsWith("#") &&
                int.TryParse(s.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb))
                return Color.FromArgb(argb);
        }
        return Color.Empty;
    }

    private static JsonNode FontToNode(Font font)
        => new JsonObject { ["name"] = font.Name, ["size"] = font.Size, ["style"] = font.Style.ToString() };

    private static Font FontFromNode(JsonNode node)
    {
        if (node is not JsonObject o) return Font.Default;
        string name = o["name"]?.GetValue<string>() ?? "Arial";
        float size = o["size"]?.GetValue<float>() ?? 14f;
        FontStyle style = FontStyle.Regular;
        if (o["style"] != null)
        {
            var parsed = ParseEnum(typeof(FontStyle), o["style"]);
            if (parsed is FontStyle fs) style = fs;
        }
        return new Font(name, size, style);
    }

    private static JsonNode SvgImageToNode(SvgImage svg)
    {
        var o = new JsonObject { ["kind"] = "svg" };
        if (!string.IsNullOrEmpty(svg.ResourceName)) o["resourceName"] = svg.ResourceName;
        if (!string.IsNullOrEmpty(svg.ImportedBase64)) o["base64"] = svg.ImportedBase64;
        o["size"] = svg.DefaultSize;
        return o;
    }

    private static SvgImage? SvgImageFromNode(JsonNode node, List<string>? warnings)
    {
        if (node is not JsonObject o) return null;
        int size = o.ContainsKey("size") ? GetInt(o, "size") : 16;
        var resourceName = o["resourceName"]?.GetValue<string>();
        var base64 = o["base64"]?.GetValue<string>();

        if (!string.IsNullOrEmpty(resourceName))
        {
            try
            {
                var img = SvgImage.FromSvgResource(resourceName, size);
                if (img != null) return img;
            }
            catch (Exception ex)
            {
                warnings?.Add($"SVG-Ressource '{resourceName}' konnte nicht geladen werden: {ex.Message}");
            }
        }

        if (!string.IsNullOrEmpty(base64))
        {
            try
            {
                return SvgImage.FromSvgBase64(base64, size);
            }
            catch (Exception ex)
            {
                warnings?.Add($"SVG-Base64-Bild konnte nicht geladen werden: {ex.Message}");
            }
        }

        return null;
    }

    private static JsonNode RasterImageToNode(RasterImage raster)
    {
        var o = new JsonObject { ["kind"] = "raster" };
        if (!string.IsNullOrEmpty(raster.ImportedBase64)) o["base64"] = raster.ImportedBase64;
        return o;
    }

    private static RasterImage? RasterImageFromNode(JsonNode node, List<string>? warnings)
    {
        if (node is not JsonObject o) return null;
        var base64 = o["base64"]?.GetValue<string>();
        if (string.IsNullOrEmpty(base64)) return null;
        try
        {
            return RasterImage.FromBase64(base64);
        }
        catch (Exception ex)
        {
            warnings?.Add($"Raster-Bild konnte nicht geladen werden: {ex.Message}");
            return null;
        }
    }

    private static object InferNumber(JsonNode node)
    {
        var el = node.AsValue().GetValue<System.Text.Json.JsonElement>();
        if (el.TryGetInt32(out var i)) return i;
        if (el.TryGetInt64(out var l)) return l;
        return el.GetDouble();
    }

    private static object? ParseEnum(Type enumType, JsonNode node)
    {
        if (node.GetValueKind() == System.Text.Json.JsonValueKind.String)
        {
            var s = node.GetValue<string>();
            if (!string.IsNullOrEmpty(s))
                return Enum.Parse(enumType, s, ignoreCase: true);
            return null;
        }
        return Enum.ToObject(enumType, node.GetValue<int>());
    }

    private static int GetInt(JsonObject o, string key) => o[key]?.GetValue<int>() ?? 0;
}
