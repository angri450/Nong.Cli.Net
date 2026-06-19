#nullable disable

using System.Collections.Generic;

namespace ClosedXML.Excel
{
    internal class XLCharts: IXLCharts
    {
        private List<IXLChart> charts = new List<IXLChart>();
        private XLWorksheet worksheet;

        public XLCharts(XLWorksheet ws)
        {
            worksheet = ws;
        }

        public IEnumerator<IXLChart> GetEnumerator() => charts.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add(IXLChart chart) => charts.Add(chart);

        public IXLChart AddChart(XLChartType chartType)
        {
            var chart = new XLChart(worksheet) { ChartType = chartType };
            charts.Add(chart);
            return chart;
        }
    }
}
