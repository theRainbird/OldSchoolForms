using System.Text.Json;
using System.Text.Json.Nodes;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Serialization;

namespace OldSchoolForms.Ui.Reports;

/// <summary>
/// Serializes <see cref="Report"/> definitions to and from the
/// <c>OldSchoolForms.Report</c> JSON format. The format stores the page setup
/// (portrait paper size, orientation, margins in centimeters), the five report
/// bands with their controls and the (nested) group definitions. Colors and
/// fonts are marshalled through <see cref="OldSchoolForms.Ui.Serialization.JsonValueConverter"/> so the
/// format stays consistent with <c>OldSchoolForms.Layout</c>. Round-trips
/// losslessly for all supported report control types.
/// </summary>
public static class ReportJsonSerializer
{
    /// <summary>The format identifier written into the <c>format</c> member.</summary>
    public const string FormatId = "OldSchoolForms.Report";

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <summary>
    /// Serializes a report definition to a JSON string.
    /// </summary>
    /// <param name="report">The report to serialize.</param>
    /// <returns>The JSON representation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="report"/> is <see langword="null"/>.</exception>
    public static string Save(Report report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return ToNode(report).ToJsonString(WriteOptions);
    }

    /// <summary>
    /// Loads a report definition from a JSON string.
    /// </summary>
    /// <param name="json">The JSON representation.</param>
    /// <returns>The deserialized report.</returns>
    /// <exception cref="InvalidDataException">The JSON is invalid or uses a different format id.</exception>
    public static Report Load(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var root = JsonNode.Parse(json)
            ?? throw new InvalidDataException(LangRes.GetEnglish("Runtime_ReportJsonEmpty"));
        return FromNode(root);
    }

    private static JsonObject ToNode(Report report)
    {
        var setup = report.PageSetup;
        var root = new JsonObject
        {
            ["format"] = FormatId,
            ["name"] = report.Name,
            ["pageSetup"] = new JsonObject
            {
                ["portraitWidth"] = setup.PortraitWidth.Value,
                ["portraitHeight"] = setup.PortraitHeight.Value,
                ["landscape"] = setup.Landscape,
                ["leftMargin"] = setup.LeftMargin.Value,
                ["rightMargin"] = setup.RightMargin.Value,
                ["topMargin"] = setup.TopMargin.Value,
                ["bottomMargin"] = setup.BottomMargin.Value
            },
            ["reportHeader"] = BandToNode(report.ReportHeader),
            ["pageHeader"] = BandToNode(report.PageHeader),
            ["detail"] = BandToNode(report.Detail),
            ["pageFooter"] = BandToNode(report.PageFooter),
            ["reportFooter"] = BandToNode(report.ReportFooter),
            ["groups"] = GroupsToNode(report.Groups)
        };

        if (report.DesignFields.Count > 0)
        {
            var designFields = new JsonArray();
            foreach (var field in report.DesignFields)
                designFields.Add((JsonNode?)field);
            root["designFields"] = designFields;
        }

        return root;
    }

    private static Report FromNode(JsonNode root)
    {
        var format = root["format"]?.GetValue<string>();
        if (format != FormatId)
            throw new InvalidDataException(string.Format(LangRes.GetEnglish("Runtime_ReportFormatMismatch"), FormatId, format));

        var report = new Report(root["name"]?.GetValue<string>() ?? "Report");

        if (root["pageSetup"] is JsonObject setup)
        {
            var pw = setup["portraitWidth"]?.GetValue<double>() ?? 21.0;
            var ph = setup["portraitHeight"]?.GetValue<double>() ?? 29.7;
            var landscape = setup["landscape"]?.GetValue<bool>() ?? false;
            report.PageSetup.SetPaperSize(pw, ph);
            report.PageSetup.Landscape = landscape;
            if (setup["leftMargin"] is JsonNode lm) report.PageSetup.LeftMargin = lm.GetValue<double>();
            if (setup["rightMargin"] is JsonNode rm) report.PageSetup.RightMargin = rm.GetValue<double>();
            if (setup["topMargin"] is JsonNode tm) report.PageSetup.TopMargin = tm.GetValue<double>();
            if (setup["bottomMargin"] is JsonNode bm) report.PageSetup.BottomMargin = bm.GetValue<double>();
        }

        if (root["reportHeader"] is JsonObject rh) BandFromNode(report.ReportHeader, rh);
        if (root["pageHeader"] is JsonObject pageHeader) BandFromNode(report.PageHeader, pageHeader);
        if (root["detail"] is JsonObject det) BandFromNode(report.Detail, det);
        if (root["pageFooter"] is JsonObject pf) BandFromNode(report.PageFooter, pf);
        if (root["reportFooter"] is JsonObject rf) BandFromNode(report.ReportFooter, rf);
        if (root["groups"] is JsonArray groups) GroupsFromNode(report.Groups, groups);

        if (root["designFields"] is JsonArray designFields)
        {
            foreach (var fieldNode in designFields)
            {
                var fieldName = fieldNode?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(fieldName))
                    report.DesignFields.Add(fieldName);
            }
        }

        return report;
    }

    private static JsonObject BandToNode(ReportBand band)
    {
        var node = new JsonObject
        {
            ["height"] = band.Height.Value,
            ["visible"] = band.Visible,
            ["canGrow"] = band.CanGrow,
            ["repeatOnNewPage"] = band.RepeatOnNewPage,
            ["keepTogether"] = band.KeepTogether,
            ["backColor"] = JsonValueConverter.ToNode(band.BackColor)
        };
        if (band.GroupField != null)
            node["groupField"] = band.GroupField;

        var controls = new JsonArray();
        foreach (var control in band.Controls)
            controls.Add(ControlToNode(control));
        node["controls"] = controls;
        return node;
    }

    private static void BandFromNode(ReportBand band, JsonObject node)
    {
        if (node["height"] is JsonNode h) band.Height = h.GetValue<double>();
        if (node["visible"] is JsonNode v) band.Visible = v.GetValue<bool>();
        if (node["canGrow"] is JsonNode cg) band.CanGrow = cg.GetValue<bool>();
        if (node["repeatOnNewPage"] is JsonNode rp) band.RepeatOnNewPage = rp.GetValue<bool>();
        if (node["keepTogether"] is JsonNode kt) band.KeepTogether = kt.GetValue<bool>();
        if (node["backColor"] is JsonNode bc) band.BackColor = (Color)JsonValueConverter.FromNode(bc, typeof(Color))!;
        if (node["groupField"] is JsonNode gf) band.GroupField = gf.GetValue<string>();

        if (node["controls"] is JsonArray controls)
        {
            foreach (var controlNode in controls)
            {
                if (controlNode is JsonObject co)
                    band.Controls.Add(ControlFromNode(co));
            }
        }
    }

    private static JsonArray GroupsToNode(ReportGroupCollection groups)
    {
        var array = new JsonArray();
        foreach (var group in groups)
        {
            var node = new JsonObject
            {
                ["field"] = group.GroupField,
                ["name"] = group.Name,
                ["keepTogether"] = group.KeepTogether,
                ["header"] = BandToNode(group.Header),
                ["footer"] = BandToNode(group.Footer),
                ["groups"] = GroupsToNode(group.Groups)
            };
            array.Add(node);
        }
        return array;
    }

    private static void GroupsFromNode(ReportGroupCollection target, JsonArray array)
    {
        foreach (var node in array)
        {
            if (node is not JsonObject go) continue;
            var group = new ReportGroup(go["field"]?.GetValue<string>() ?? string.Empty, go["name"]?.GetValue<string>());
            if (go["keepTogether"] is JsonNode kt) group.KeepTogether = kt.GetValue<bool>();
            if (go["header"] is JsonObject header) BandFromNode(group.Header, header);
            if (go["footer"] is JsonObject footer) BandFromNode(group.Footer, footer);
            if (go["groups"] is JsonArray nested) GroupsFromNode(group.Groups, nested);
            target.Add(group);
        }
    }

    private static JsonObject ControlToNode(ReportControl control)
    {
        var node = new JsonObject
        {
            ["type"] = TypeKey(control),
            ["name"] = control.Name,
            ["left"] = control.Left.Value,
            ["top"] = control.Top.Value,
            ["width"] = control.Width.Value,
            ["height"] = control.Height.Value,
            ["visible"] = control.Visible,
            ["canGrow"] = control.CanGrow,
            ["canShrink"] = control.CanShrink,
            ["backColor"] = JsonValueConverter.ToNode(control.BackColor),
            ["foreColor"] = JsonValueConverter.ToNode(control.ForeColor),
            ["textAlign"] = control.TextAlign.ToString()
        };
        if (control.Font != null)
            node["font"] = JsonValueConverter.ToNode(control.Font);
        if (control.Format != null)
            node["format"] = control.Format;

        switch (control)
        {
            case ReportLabel label:
                node["text"] = label.Text;
                break;
            case ReportTextBox textBox:
                if (textBox.DataField != null) node["dataField"] = textBox.DataField;
                if (textBox.Expression != null) node["expression"] = textBox.Expression;
                break;
            case ReportImage image:
                node["sizing"] = image.Sizing.ToString();
                break;
            case ReportLine line:
                node["x2"] = line.X2.Value;
                node["y2"] = line.Y2.Value;
                node["lineWidth"] = line.LineWidth.Value;
                node["lineColor"] = JsonValueConverter.ToNode(line.LineColor);
                break;
            case ReportCheckBox checkBox:
                if (checkBox.DataField != null) node["dataField"] = checkBox.DataField;
                if (checkBox.Caption != null) node["caption"] = checkBox.Caption;
                break;
            case ReportChartControl chart:
                node["chartType"] = chart.ChartType.ToString();
                node["title"] = chart.Title;
                node["showLegend"] = chart.ShowLegend;
                node["legendPosition"] = chart.LegendPosition.ToString();
                node["dataLabelStyle"] = chart.DataLabelStyle.ToString();
                var seriesArray = new JsonArray();
                foreach (var series in chart.Series)
                {
                    var points = new JsonArray();
                    foreach (var point in series.Points)
                    {
                        var pointNode = new JsonObject { ["value"] = point.Value };
                        if (point.Label != null) pointNode["label"] = point.Label;
                        if (point.Color.HasValue) pointNode["color"] = JsonValueConverter.ToNode(point.Color.Value);
                        points.Add(pointNode);
                    }
                    var seriesNode = new JsonObject { ["name"] = series.Name, ["points"] = points };
                    if (series.Color.HasValue) seriesNode["color"] = JsonValueConverter.ToNode(series.Color.Value);
                    seriesArray.Add(seriesNode);
                }
                node["series"] = seriesArray;
                break;
            case ReportCrossTab crossTab:
                var fields = new JsonArray();
                foreach (var field in crossTab.Fields)
                {
                    var fieldNode = new JsonObject
                    {
                        ["dataField"] = field.DataField,
                        ["usage"] = field.Usage.ToString(),
                        ["aggregation"] = field.Aggregation.ToString()
                    };
                    if (field.HeaderText != null) fieldNode["headerText"] = field.HeaderText;
                    if (field.Format != null) fieldNode["format"] = field.Format;
                    fields.Add(fieldNode);
                }
                node["fields"] = fields;
                if (crossTab.GroupField != null) node["groupField"] = crossTab.GroupField;
                break;
        }

        return node;
    }

    private static ReportControl ControlFromNode(JsonObject node)
    {
        var type = node["type"]?.GetValue<string>()
            ?? throw new InvalidDataException(LangRes.GetEnglish("Runtime_ReportTypeMemberMissing"));

        ReportControl control = type switch
        {
            "label" => new ReportLabel { Text = node["text"]?.GetValue<string>() ?? string.Empty },
            "textBox" => new ReportTextBox
            {
                DataField = OptString(node, "dataField"),
                Expression = OptString(node, "expression")
            },
            "image" => new ReportImage { Sizing = ParseEnum(node, "sizing", ImageSizing.Zoom) },
            "line" => new ReportLine
            {
                X2 = OptDouble(node, "x2") ?? 0,
                Y2 = OptDouble(node, "y2") ?? 0,
                LineWidth = OptDouble(node, "lineWidth") ?? 0.02,
                LineColor = node["lineColor"] != null
                    ? (Color)JsonValueConverter.FromNode(node["lineColor"], typeof(Color))!
                    : Color.Black
            },
            "checkBox" => new ReportCheckBox
            {
                DataField = OptString(node, "dataField"),
                Caption = OptString(node, "caption")
            },
            "chart" => ChartFromNode(node),
            "crossTab" => CrossTabFromNode(node),
            _ => throw new InvalidDataException(string.Format(LangRes.GetEnglish("Runtime_ReportControlTypeUnknown"), type))
        };

        if (node["name"] is JsonNode name) control.Name = name.GetValue<string>();
        if (node["left"] is JsonNode left) control.Left = left.GetValue<double>();
        if (node["top"] is JsonNode top) control.Top = top.GetValue<double>();
        if (node["width"] is JsonNode width) control.Width = width.GetValue<double>();
        if (node["height"] is JsonNode height) control.Height = height.GetValue<double>();
        if (node["visible"] is JsonNode visible) control.Visible = visible.GetValue<bool>();
        if (node["canGrow"] is JsonNode canGrow) control.CanGrow = canGrow.GetValue<bool>();
        if (node["canShrink"] is JsonNode canShrink) control.CanShrink = canShrink.GetValue<bool>();
        if (node["backColor"] is JsonNode backColor) control.BackColor = (Color)JsonValueConverter.FromNode(backColor, typeof(Color))!;
        if (node["foreColor"] is JsonNode foreColor) control.ForeColor = (Color)JsonValueConverter.FromNode(foreColor, typeof(Color))!;
        if (node["font"] is JsonNode font) control.Font = (Font?)JsonValueConverter.FromNode(font, typeof(Font));
        if (node["format"] is JsonNode format) control.Format = format.GetValue<string>();
        if (node["textAlign"] is JsonNode align && Enum.TryParse<TextAlignment>(align.GetValue<string>(), true, out var textAlign))
            control.TextAlign = textAlign;

        return control;
    }

    private static ReportChartControl ChartFromNode(JsonObject node)
    {
        var chart = new ReportChartControl
        {
            Title = node["title"]?.GetValue<string>() ?? string.Empty,
            ShowLegend = node["showLegend"]?.GetValue<bool>() ?? true
        };
        if (node["chartType"] is JsonNode ct && Enum.TryParse<DiagramType>(ct.GetValue<string>(), true, out var chartType))
            chart.ChartType = chartType;
        if (node["legendPosition"] is JsonNode lp && Enum.TryParse<LegendPosition>(lp.GetValue<string>(), true, out var legend))
            chart.LegendPosition = legend;
        if (node["dataLabelStyle"] is JsonNode dls && Enum.TryParse<LabelStyle>(dls.GetValue<string>(), true, out var labelStyle))
            chart.DataLabelStyle = labelStyle;

        if (node["series"] is JsonArray seriesArray)
        {
            foreach (var seriesNode in seriesArray)
            {
                if (seriesNode is not JsonObject so) continue;
                var series = new DiagramViewSeries { Name = so["name"]?.GetValue<string>() ?? string.Empty };
                if (so["color"] is JsonNode sc)
                    series.Color = (Color)JsonValueConverter.FromNode(sc, typeof(Color))!;
                if (so["points"] is JsonArray points)
                {
                    foreach (var pointNode in points)
                    {
                        if (pointNode is not JsonObject po) continue;
                        var point = new DiagramViewDataPoint(po["label"]?.GetValue<string>(), po["value"]?.GetValue<double>() ?? 0);
                        if (po["color"] is JsonNode pc)
                            point.Color = (Color)JsonValueConverter.FromNode(pc, typeof(Color))!;
                        series.Add(point);
                    }
                }
                chart.Series.Add(series);
            }
        }
        return chart;
    }

    private static ReportCrossTab CrossTabFromNode(JsonObject node)
    {
        var crossTab = new ReportCrossTab { GroupField = OptString(node, "groupField") };
        if (node["fields"] is JsonArray fields)
        {
            foreach (var fieldNode in fields)
            {
                if (fieldNode is not JsonObject fo) continue;
                var field = new CrossTabField
                {
                    DataField = fo["dataField"]?.GetValue<string>() ?? string.Empty,
                    HeaderText = OptString(fo, "headerText"),
                    Format = OptString(fo, "format")
                };
                if (fo["usage"] is JsonNode u && Enum.TryParse<FieldUsage>(u.GetValue<string>(), true, out var usage))
                    field.Usage = usage;
                if (fo["aggregation"] is JsonNode a && Enum.TryParse<CrossTabAggregation>(a.GetValue<string>(), true, out var aggregation))
                    field.Aggregation = aggregation;
                crossTab.Fields.Add(field);
            }
        }
        return crossTab;
    }

    private static string TypeKey(ReportControl control) => control switch
    {
        ReportLabel => "label",
        ReportTextBox => "textBox",
        ReportImage => "image",
        ReportLine => "line",
        ReportCheckBox => "checkBox",
        ReportChartControl => "chart",
        ReportCrossTab => "crossTab",
        _ => throw new InvalidDataException(string.Format(LangRes.GetEnglish("Runtime_ReportControlTypeUnsupported"), control.GetType().Name))
    };

    private static string? OptString(JsonObject node, string key)
        => node[key] is { } v && v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() : null;

    private static double? OptDouble(JsonObject node, string key)
        => node[key] is { } v && v.GetValueKind() == JsonValueKind.Number ? v.GetValue<double>() : null;

    private static T ParseEnum<T>(JsonObject node, string key, T fallback)
        where T : struct, Enum
        => node[key] is { } v && v.GetValueKind() == JsonValueKind.String
            && Enum.TryParse<T>(v.GetValue<string>(), true, out var parsed)
            ? parsed
            : fallback;
}
