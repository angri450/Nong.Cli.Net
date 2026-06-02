using ClosedXML.Excel;

namespace ExcelCore;

public static class FormulaValidator
{
    /// <summary>审计工作簿中所有公式，返回问题列表</summary>
    public static List<string> Audit(XLWorkbook wb)
    {
        var issues = new List<string>();
        foreach (var ws in wb.Worksheets)
        {
            foreach (var cell in ws.CellsUsed(c => c.HasFormula))
            {
                if (cell.CachedValue.IsError)
                    issues.Add($"{ws.Name}!{cell.Address}: {cell.GetError()} (formula: {cell.FormulaA1})");

                var f = cell.FormulaA1?.ToUpperInvariant() ?? "";
                // 检测未受保护的除法
                if (f.Contains('/') && !f.Contains("IFERROR"))
                    issues.Add($"{ws.Name}!{cell.Address}: 除法未用 IFERROR 保护 (formula: {cell.FormulaA1})");
                // 检测未受保护的 VLOOKUP
                if (f.Contains("VLOOKUP") && !f.Contains("IFERROR"))
                    issues.Add($"{ws.Name}!{cell.Address}: VLOOKUP 未用 IFERROR 保护 (formula: {cell.FormulaA1})");
            }
        }
        return issues;
    }

    /// <summary>保存并求值公式</summary>
    public static void SaveWithEvaluation(XLWorkbook wb, string path)
    {
        wb.SaveAs(path, new SaveOptions
        {
            EvaluateFormulasBeforeSaving = true,
            ValidatePackage = true,
            GenerateCalculationChain = true
        });
    }
}
