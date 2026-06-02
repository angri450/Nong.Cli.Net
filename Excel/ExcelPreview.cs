using ClosedXML.Excel;

namespace ExcelCore;

public static class ExcelPreview
{
    /// <summary>预览 xlsx 文件，返回文本表示</summary>
    public static PreviewResult Preview(string path)
    {
        using var wb = new XLWorkbook(path);
        return Preview(wb);
    }

    /// <summary>预览已打开的 Workbook</summary>
    public static PreviewResult Preview(XLWorkbook wb)
    {
        var warnings = new List<string>();
        var sb = new System.Text.StringBuilder();

        foreach (var ws in wb.Worksheets)
        {
            sb.AppendLine($"=== Sheet: {ws.Name} ===");
            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
            var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;

            if (lastRow == 0)
            {
                sb.AppendLine("(empty)");
                continue;
            }

            // 列宽提示
            var colWidths = new int[lastCol];
            for (int c = 1; c <= lastCol; c++)
            {
                var w = (int)ws.Column(c).Width;
                colWidths[c - 1] = w;
            }
            sb.Append("|");
            for (int c = 0; c < lastCol; c++)
                sb.Append($" {colWidths[c],-4} |");
            sb.AppendLine();

            for (int r = 1; r <= Math.Min(lastRow, 50); r++)
            {
                sb.Append("|");
                for (int c = 1; c <= lastCol; c++)
                {
                    var cell = ws.Cell(r, c);
                    var val = cell.HasFormula
                        ? $"[F:{cell.FormulaA1}]"
                        : cell.Value.ToString();
                    if (val.Length > 30) val = val[..27] + "...";
                    sb.Append($" {val,-20} |");
                }
                sb.AppendLine();
            }

            if (lastRow > 50)
            {
                sb.AppendLine($"... ({lastRow - 50} more rows)");
            }

            // 检查列宽溢出
            for (int c = 1; c <= lastCol; c++)
            {
                if (colWidths[c - 1] == 0)
                    warnings.Add($"{ws.Name}: 列 {c} 宽度为 0");
            }
        }

        return new PreviewResult { Text = sb.ToString(), Warnings = warnings };
    }

    /// <summary>打印预览到 stdout</summary>
    public static string Print(string path)
    {
        var result = Preview(path);
        Console.WriteLine(result.Text);
        foreach (var w in result.Warnings)
            Console.WriteLine($"WARNING: {w}");
        return result.Text;
    }

    public class PreviewResult
    {
        public string Text { get; set; } = "";
        public List<string> Warnings { get; set; } = new();

        public void Deconstruct(out string text, out List<string> warnings)
        { text = Text; warnings = Warnings; }

        public override string ToString() => Text;
    }
}
