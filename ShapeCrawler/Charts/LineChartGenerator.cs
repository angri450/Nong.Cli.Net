using System.Collections.Generic;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.Packaging;

namespace ShapeCrawler.Charts;

/// <summary>
///     Represents the content of a line chart.
/// </summary>
internal sealed class LineChartGenerator(
    ChartPart chartPart,
    Dictionary<string, double> categoryValues,
    string seriesName)
{
    public void Generate()
    {
        var chartSpace = new ChartSpace(new EditingLanguage { Val = "en-US" }, new RoundedCorners { Val = false });
        chartSpace.AddNamespaceDeclaration("c", "http://schemas.openxmlformats.org/drawingml/2006/chart");
        chartSpace.AddNamespaceDeclaration("a", "http://schemas.openxmlformats.org/drawingml/2006/main");
        chartSpace.AddNamespaceDeclaration("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");

        var chart = new DocumentFormat.OpenXml.Drawing.Charts.Chart();
        chart.AppendChild(new AutoTitleDeleted { Val = false });

        var series = new LineChartSeries(new DocumentFormat.OpenXml.Drawing.Charts.Index { Val = 0 }, new Order { Val = 0 });

        var seriesText = new SeriesText();
        seriesText.AppendChild(new NumericValue { Text = seriesName });
        series.AppendChild(seriesText);

        var categoriesCount = UInt32Value.FromUInt32((uint)categoryValues.Count);
        var categoryAxisData = new CategoryAxisData();
        var stringLiteral = new StringLiteral(new PointCount { Val = categoriesCount });

        uint index = 0;
        foreach (var item in categoryValues)
        {
            var point = new StringPoint { Index = index };
            point.AppendChild(new NumericValue(item.Key));
            stringLiteral.AppendChild(point);
            index++;
        }
        categoryAxisData.AppendChild(stringLiteral);
        series.AppendChild(categoryAxisData);

        var values = new Values();
        var numberLiteral = new NumberLiteral(new FormatCode("General"), new PointCount { Val = categoriesCount });

        index = 0;
        foreach (var item in categoryValues)
        {
            var point = new NumericPoint { Index = index };
            point.AppendChild(new NumericValue(item.Value.ToString()));
            numberLiteral.AppendChild(point);
            index++;
        }
        values.AppendChild(numberLiteral);
        series.AppendChild(values);

        const uint axisId1 = 1U;
        const uint axisId2 = 2U;

        var plotArea = new PlotArea(
            new Layout(), new DocumentFormat.OpenXml.Drawing.Charts.LineChart(
                new Grouping { Val = GroupingValues.Standard },
                new VaryColors { Val = false },
                series,
                new AxisId { Val = axisId1 },
                new AxisId { Val = axisId2 }));

        var categoryAxis = new CategoryAxis();
        categoryAxis.AppendChild(new AxisId { Val = axisId1 });
        var scalingCat = new Scaling();
        scalingCat.AppendChild(new Orientation { Val = OrientationValues.MinMax });
        categoryAxis.AppendChild(scalingCat);
        categoryAxis.AppendChild(new Delete { Val = false });
        categoryAxis.AppendChild(new AxisPosition { Val = AxisPositionValues.Bottom });
        categoryAxis.AppendChild(new CrossingAxis { Val = axisId2 });
        plotArea.AppendChild(categoryAxis);

        var valueAxis = new ValueAxis();
        valueAxis.AppendChild(new AxisId { Val = axisId2 });
        var scalingVal = new Scaling();
        scalingVal.AppendChild(new Orientation { Val = OrientationValues.MinMax });
        valueAxis.AppendChild(scalingVal);
        valueAxis.AppendChild(new Delete { Val = false });
        valueAxis.AppendChild(new AxisPosition { Val = AxisPositionValues.Left });
        valueAxis.AppendChild(new CrossingAxis { Val = axisId1 });
        plotArea.AppendChild(valueAxis);

        chart.AppendChild(plotArea);

        var legend = new Legend();
        legend.AppendChild(new LegendPosition { Val = LegendPositionValues.Right });
        chart.AppendChild(legend);

        chartSpace.AppendChild(chart);
        chartPart.ChartSpace = chartSpace;
    }
}
