using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocxCore;

/// <summary>
/// 目录生成器 + 图表构建器。
/// </summary>
public static class TocAndChartBuilder
{
    // === TOC ===

    /// <summary>插入目录域。需要在之前有 Heading 段落。</summary>
    public static Paragraph InsertTableOfContents(string title = "目录", uint depth = 3)
    {
        // TOC heading
        var heading = new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "Heading1" }),
            new Run(new Text(title)));

        // TOC field: { TOC \o "1-3" \h \z }
        var toc = new Paragraph(
            new Run(new FieldChar { FieldCharType = FieldCharValues.Begin }),
            new Run(new FieldCode(" TOC \\o \"1-3\" \\h \\z ") { Space = SpaceProcessingModeValues.Preserve }),
            new Run(new FieldChar { FieldCharType = FieldCharValues.Separate }),
            new Run(new Text("[ 右键点击此处 → 更新域 以生成目录 ]")),
            new Run(new FieldChar { FieldCharType = FieldCharValues.End }));

        // Return the TOC paragraph — caller must insert heading before it
        return toc;
    }

    /// <summary>生成完整目录页（标题 + TOC 域 + 分页符）。</summary>
    public static void AppendTableOfContents(Body body, string title = "目录", uint depth = 3)
    {
        body.Append(new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "Heading1" }),
            new Run(new Text(title))));

        body.Append(new Paragraph(
            new Run(new FieldChar { FieldCharType = FieldCharValues.Begin }),
            new Run(new FieldCode($" TOC \\o \"1-{depth}\" \\h \\z ") { Space = SpaceProcessingModeValues.Preserve }),
            new Run(new FieldChar { FieldCharType = FieldCharValues.Separate }),
            new Run(new Text("[ 右键点击此处 → 更新域 以生成目录 ]")),
            new Run(new FieldChar { FieldCharType = FieldCharValues.End })));

        // Page break after TOC
        body.Append(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
    }

    // === Charts ===

    /// <summary>创建柱状图。</summary>
    public static void AppendBarChart(Body body, MainDocumentPart main, string title, string[] categories, double[] values, string seriesName = "系列 1")
    {
        var chartPart = main.AddNewPart<ChartPart>();
        var chartXml = BuildBarChartXml(title, categories, values, seriesName);
        chartPart.ChartSpace = new DocumentFormat.OpenXml.Drawing.Charts.ChartSpace(
            new DocumentFormat.OpenXml.Drawing.Charts.EditingLanguage { Val = "zh-CN" },
            chartXml);

        var chartRefId = main.GetIdOfPart(chartPart);
        var drawing = BuildChartDrawing(chartRefId, title, 6000000, 4000000);
        body.Append(new Paragraph(new Run(drawing)));
        body.Append(new Paragraph());
    }

    static DocumentFormat.OpenXml.Drawing.Charts.Chart BuildBarChartXml(string title, string[] cats, double[] vals, string series)
    {
        var chart = new DocumentFormat.OpenXml.Drawing.Charts.Chart(
            new DocumentFormat.OpenXml.Drawing.Charts.PlotArea(
                new DocumentFormat.OpenXml.Drawing.Charts.BarChart(
                    new DocumentFormat.OpenXml.Drawing.Charts.BarDirection { Val = DocumentFormat.OpenXml.Drawing.Charts.BarDirectionValues.Column },
                    new DocumentFormat.OpenXml.Drawing.Charts.BarGrouping { Val = DocumentFormat.OpenXml.Drawing.Charts.BarGroupingValues.Clustered },
                    new DocumentFormat.OpenXml.Drawing.Charts.BarChartSeries(
                        new DocumentFormat.OpenXml.Drawing.Charts.SeriesText(new DocumentFormat.OpenXml.Drawing.Charts.StringLiteral(new DocumentFormat.OpenXml.Drawing.Charts.StringPoint[0])),
                        new DocumentFormat.OpenXml.Drawing.Charts.CategoryAxisData { StringReference = MakeStringRef(cats) },
                        new DocumentFormat.OpenXml.Drawing.Charts.Values { NumberReference = MakeNumberRef(vals) })
                    { Index = new DocumentFormat.OpenXml.Drawing.Charts.Index { Val = 0u }, Order = new DocumentFormat.OpenXml.Drawing.Charts.Order { Val = 0u } }),
                new DocumentFormat.OpenXml.Drawing.Charts.CategoryAxis(
                    new DocumentFormat.OpenXml.Drawing.Charts.AxisId { Val = 1u },
                    new DocumentFormat.OpenXml.Drawing.Charts.Scaling(new DocumentFormat.OpenXml.Drawing.Charts.Orientation { Val = DocumentFormat.OpenXml.Drawing.Charts.OrientationValues.MinMax }))),
            new DocumentFormat.OpenXml.Drawing.Charts.ValueAxis(
                new DocumentFormat.OpenXml.Drawing.Charts.AxisId { Val = 2u },
                new DocumentFormat.OpenXml.Drawing.Charts.Scaling(new DocumentFormat.OpenXml.Drawing.Charts.Orientation { Val = DocumentFormat.OpenXml.Drawing.Charts.OrientationValues.MinMax })));
        return chart;
    }

    static DocumentFormat.OpenXml.Drawing.Charts.StringReference MakeStringRef(string[] cats)
    {
        var ref_ = new DocumentFormat.OpenXml.Drawing.Charts.StringReference();
        ref_.Append(new DocumentFormat.OpenXml.Drawing.Charts.StringCache(
            new DocumentFormat.OpenXml.Drawing.Charts.PointCount { Val = (uint)cats.Length }));
        return ref_;
    }

    static DocumentFormat.OpenXml.Drawing.Charts.NumberReference MakeNumberRef(double[] vals)
    {
        var ref_ = new DocumentFormat.OpenXml.Drawing.Charts.NumberReference();
        ref_.Append(new DocumentFormat.OpenXml.Drawing.Charts.NumberingCache(
            new DocumentFormat.OpenXml.Drawing.Charts.PointCount { Val = (uint)vals.Length }));
        return ref_;
    }

    static Drawing BuildChartDrawing(string chartRefId, string name, long cx, long cy)
    {
        return new Drawing(
            new DocumentFormat.OpenXml.Drawing.Wordprocessing.Inline(
                new DocumentFormat.OpenXml.Drawing.Wordprocessing.Extent { Cx = cx, Cy = cy },
                new DocumentFormat.OpenXml.Drawing.Wordprocessing.EffectExtent(),
                new DocumentFormat.OpenXml.Drawing.Wordprocessing.DocProperties { Id = 1u, Name = name },
                new DocumentFormat.OpenXml.Drawing.Wordprocessing.NonVisualGraphicFrameDrawingProperties(),
                new DocumentFormat.OpenXml.Drawing.Graphic(
                    new DocumentFormat.OpenXml.Drawing.GraphicData(
                        new DocumentFormat.OpenXml.Drawing.Charts.ChartReference { Id = chartRefId })
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/chart" }))
        );
    }
}
