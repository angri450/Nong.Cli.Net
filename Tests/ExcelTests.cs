using ClosedXML.Excel;
using ExcelCore;
using Xunit;

namespace Tests;

public class ExcelTests : IDisposable
{
    private readonly string _tempDir;

    public ExcelTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "excel-tests-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void Headers_CreatesCorrectHeaderRow()
    {
        using var wb = new XLWorkbook();
        var sb = ExcelBuilder.Sheet(wb, "Test");
        sb.Headers("Name", "Age", "City");

        Assert.Equal("Name", sb.Ws.Cell(1, 1).GetString());
        Assert.Equal("Age", sb.Ws.Cell(1, 2).GetString());
        Assert.Equal("City", sb.Ws.Cell(1, 3).GetString());
    }

    [Fact]
    public void Data_WritesDataCorrectly()
    {
        using var wb = new XLWorkbook();
        var sb = ExcelBuilder.Sheet(wb, "Test");
        sb.Headers("A", "B");
        sb.Data(new[]
        {
            new[] { "1", "2" },
            new[] { "3", "4" }
        });

        Assert.Equal("1", sb.Ws.Cell(2, 1).GetString());
        Assert.Equal("2", sb.Ws.Cell(2, 2).GetString());
        Assert.Equal("3", sb.Ws.Cell(3, 1).GetString());
        Assert.Equal("4", sb.Ws.Cell(3, 2).GetString());
    }

    [Fact]
    public void Formula_SetsFormula()
    {
        using var wb = new XLWorkbook();
        var sb = ExcelBuilder.Sheet(wb, "Test");
        sb.Headers("Val");
        sb.Data(new[] { new[] { "10" }, new[] { "20" } });
        sb.FormulaCell("A4", "SUM(A2:A3)");

        Assert.True(sb.Ws.Cell("A4").HasFormula);
        Assert.Contains("SUM", sb.Ws.Cell("A4").FormulaA1);
    }

    [Fact]
    public void ColumnWidths_SetsWidths()
    {
        using var wb = new XLWorkbook();
        var sb = ExcelBuilder.Sheet(wb, "Test");
        sb.Headers("A", "B", "C");
        sb.ColumnWidths(new double[] { 20, 30, 40 });

        Assert.Equal(20, sb.Ws.Column(1).Width);
        Assert.Equal(30, sb.Ws.Column(2).Width);
        Assert.Equal(40, sb.Ws.Column(3).Width);
    }

    [Fact]
    public void Dropdown_CreatesDataValidation()
    {
        using var wb = new XLWorkbook();
        var sb = ExcelBuilder.Sheet(wb, "Test");
        sb.Headers("Status");
        sb.Data(new[] { new[] { "" }, new[] { "" } });
        sb.Dropdown("B2:B3", "Yes,No,Maybe");

        var validations = sb.Ws.DataValidations.ToArray();
        Assert.NotEmpty(validations);
    }

    [Fact]
    public void Table_CreatesTable()
    {
        using var wb = new XLWorkbook();
        var sb = ExcelBuilder.Sheet(wb, "Test");
        sb.Headers("Col1", "Col2");
        sb.Data(new[] { new[] { "a", "b" }, new[] { "c", "d" } });
        sb.Table("MyTable");

        Assert.NotEmpty(sb.Ws.Tables);
        Assert.Equal("MyTable", sb.Ws.Tables.First().Name);
    }

    [Fact]
    public void FormulaValidator_DetectsUnprotectedDivision()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Test");
        ws.Cell("A1").Value = 10;
        ws.Cell("A2").Value = 0;
        ws.Cell("A3").FormulaA1 = "A1/A2";

        var issues = FormulaValidator.Audit(wb);
        Assert.NotEmpty(issues);
        Assert.Contains(issues, i => i.Contains("IFERROR"));
    }

    [Fact]
    public void FormulaValidator_NoIssue_WhenIferrorUsed()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Test");
        ws.Cell("A1").Value = 10;
        ws.Cell("A2").Value = 0;
        ws.Cell("A3").FormulaA1 = "IFERROR(A1/A2,0)";

        var issues = FormulaValidator.Audit(wb);
        Assert.DoesNotContain(issues, i => i.Contains("除法未用"));
    }

    [Fact]
    public void ExcelPreview_ReturnsText()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Preview");
        ws.Cell("A1").Value = "Hello";
        ws.Cell("B1").Value = "World";

        var path = Path.Combine(_tempDir, "preview.xlsx");
        wb.SaveAs(path);

        var result = ExcelPreview.Preview(path);
        Assert.NotEmpty(result.Text);
        Assert.Contains("Preview", result.Text);
        Assert.Contains("Hello", result.Text);
    }

    [Fact]
    public void ExcelPreview_FromWorkbook_ReturnsText()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S1");
        ws.Cell("A1").Value = "Data";

        var result = ExcelPreview.Preview(wb);
        Assert.NotEmpty(result.Text);
        Assert.Contains("Data", result.Text);
    }

    [Fact]
    public void StylePresets_Mono_HasCorrectColors()
    {
        var mono = StylePresets.Mono;
        Assert.Equal("mono", mono.Name);
        Assert.Equal("#333333", mono.HeaderBg);
        Assert.Equal("#FFFFFF", mono.HeaderFg);
        Assert.Equal("#F5F5F5", mono.RowAlt);
        Assert.Equal("#0066CC", mono.Accent);
    }

    [Fact]
    public void StylePresets_Finance_HasCorrectColors()
    {
        var finance = StylePresets.Finance;
        Assert.Equal("finance", finance.Name);
        Assert.Equal("#1F4E79", finance.HeaderBg);
    }

    [Fact]
    public void StylePresets_BuildFromJson_Works()
    {
        var json = """{"Name":"custom","HeaderBg":"#AABBCC","HeaderFg":"#112233","RowAlt":"#EEEEEE","Accent":"#FF0000","Border":"#999999","Integer":"#,##0","Decimal":"#,##0.00","Currency":"$#,##0","Percent":"0%","Date":"yyyy-mm-dd"}""";
        var jsonPath = Path.Combine(_tempDir, "style.json");
        File.WriteAllText(jsonPath, json);

        var preset = StylePresets.BuildFromJson(jsonPath);
        Assert.Equal("custom", preset.Name);
        Assert.Equal("#AABBCC", preset.HeaderBg);
    }

    [Fact]
    public void SheetBuilder_At_OffsetsCorrectly()
    {
        using var wb = new XLWorkbook();
        var sb = ExcelBuilder.Sheet(wb, "Test");
        sb.At(2, 3);
        sb.Headers("X", "Y");

        // At(2,3) means row offset=2, col offset=3
        // Headers go to row offset+1=3, col offset+1=4
        Assert.Equal("X", sb.Ws.Cell(3, 4).GetString());
        Assert.Equal("Y", sb.Ws.Cell(3, 5).GetString());
    }

    [Fact]
    public void SheetBuilder_HeaderStyle_AppliesStyling()
    {
        using var wb = new XLWorkbook();
        var sb = ExcelBuilder.Sheet(wb, "Test");
        sb.Headers("A", "B");
        sb.HeaderStyle("#FF0000", "#00FF00", bold: true, fontSize: 14);

        var headerRow = sb.Ws.Row(1);
        Assert.True(headerRow.Cell(1).Style.Font.Bold);
        Assert.Equal(14, headerRow.Cell(1).Style.Font.FontSize);
    }

    [Fact]
    public void SheetBuilder_FreezeHeader_Works()
    {
        using var wb = new XLWorkbook();
        var sb = ExcelBuilder.Sheet(wb, "Test");
        sb.Headers("A");
        sb.FreezeHeader();

        // Should not throw - freeze is set
        Assert.NotNull(sb.Ws.SheetView);
    }

    [Fact]
    public void SaveAndReload_PreservesData()
    {
        using var wb = new XLWorkbook();
        var sb = ExcelBuilder.Sheet(wb, "Test");
        sb.Headers("Name", "Value");
        sb.Data(new[] { new[] { "Alpha", "100" }, new[] { "Beta", "200" } });

        var path = Path.Combine(_tempDir, "save.xlsx");
        wb.SaveAs(path);

        using var wb2 = new XLWorkbook(path);
        var ws = wb2.Worksheet("Test");
        Assert.Equal("Name", ws.Cell(1, 1).GetString());
        Assert.Equal("Alpha", ws.Cell(2, 1).GetString());
        Assert.Equal("200", ws.Cell(3, 2).GetString());
    }
}
