using System.Text.Json;

namespace ChartCore;

public static class DataLoader
{
    /// <summary>从 JSON 读取分组数据。格式: {"T1": [1.2, 1.5, ...], "T2": [2.1, 2.3, ...]}</summary>
    public static Dictionary<string, List<double>> FromJson(string jsonPath)
    {
        var json = File.ReadAllText(jsonPath);
        var raw = JsonSerializer.Deserialize<Dictionary<string, double[]>>(json)
                  ?? throw new InvalidOperationException($"Failed to parse JSON: {jsonPath}");
        return raw.ToDictionary(kv => kv.Key, kv => kv.Value.ToList());
    }

    /// <summary>从 xlsx 单列读取。groupCol=组名列, valueCol=观测值列 (1-based)</summary>
    public static Dictionary<string, List<double>> FromXlsx(string xlsxPath, string sheetName,
        int groupCol = 1, int valueCol = 2)
    {
        using var wb = new ClosedXML.Excel.XLWorkbook(xlsxPath);
        var ws = wb.Worksheet(sheetName);
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
        var groups = new Dictionary<string, List<double>>();

        for (int r = 2; r <= lastRow; r++)
        {
            var label = ws.Cell(r, groupCol).GetString().Trim();
            if (string.IsNullOrEmpty(label)) continue;
            var val = ws.Cell(r, valueCol).GetDouble();
            if (!groups.ContainsKey(label)) groups[label] = new List<double>();
            groups[label].Add(val);
        }
        return groups;
    }

    /// <summary>从 xlsx 多列读取（取行均值作为重复单元）。valueCols 为多列观测值 (1-based)</summary>
    public static Dictionary<string, List<double>> FromXlsxMultiColumn(string xlsxPath, string sheetName,
        int groupCol = 1, int[]? valueCols = null)
    {
        using var wb = new ClosedXML.Excel.XLWorkbook(xlsxPath);
        var ws = wb.Worksheet(sheetName);
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
        var cols = valueCols ?? new[] { 2 };
        var groups = new Dictionary<string, List<double>>();

        for (int r = 2; r <= lastRow; r++)
        {
            var label = ws.Cell(r, groupCol).GetString().Trim();
            if (string.IsNullOrEmpty(label)) continue;
            double sum = 0;
            int count = 0;
            foreach (var c in cols)
            {
                var cell = ws.Cell(r, c);
                if (!cell.IsEmpty())
                {
                    sum += cell.GetDouble();
                    count++;
                }
            }
            if (count > 0)
            {
                if (!groups.ContainsKey(label)) groups[label] = new List<double>();
                groups[label].Add(sum / count);
            }
        }
        return groups;
    }

    /// <summary>从 CSV 读取。格式: label,value (每行一个观测值)</summary>
    public static Dictionary<string, List<double>> FromCsv(string csvPath)
    {
        var groups = new Dictionary<string, List<double>>();
        foreach (var line in File.ReadLines(csvPath))
        {
            var parts = line.Split(',');
            if (parts.Length < 2) continue;
            var label = parts[0].Trim();
            if (string.IsNullOrEmpty(label)) continue;
            if (double.TryParse(parts[1].Trim(), out var val))
            {
                if (!groups.ContainsKey(label)) groups[label] = new List<double>();
                groups[label].Add(val);
            }
        }
        return groups;
    }
}
