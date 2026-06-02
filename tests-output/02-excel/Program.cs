using ClosedXML.Excel;
using ExcelCore;

var outputDir = AppContext.BaseDirectory;

// ============================================================
// Test 1: ExcelBuilder (SheetBuilder chainable API)
// ============================================================
Console.WriteLine("=== Test 1: ExcelBuilder (basic) ===");
var basicPath = Path.Combine(outputDir, "excel-basic.xlsx");

try
{
    using var wb1 = new XLWorkbook();

    ExcelBuilder.Sheet(wb1, "Products")
        .Headers("Product", "Price", "Quantity", "Total")
        .Data(new[] {
            new[] { "Widget A", "12.50", "100", "1250.00" },
            new[] { "Gadget B", "8.75", "250", "2187.50" },
            new[] { "Gizmo C", "24.00", "50", "1200.00" },
            new[] { "Doohickey D", "3.99", "1000", "3990.00" },
            new[] { "Thingamajig E", "45.50", "30", "1365.00" },
        })
        .ColumnWidths(new[] { 20.0, 12.0, 12.0, 12.0 })
        .HeaderStyle("#333333", "#FFFFFF", bold: true, fontSize: 12)
        .AlternatingRows(2, "#F5F5F5")
        .Table("ProductsTable");

    wb1.SaveAs(basicPath);
    Console.WriteLine($"  PASS: Saved {basicPath}");

    // Verify the file exists and is non-empty
    if (File.Exists(basicPath))
    {
        var info = new FileInfo(basicPath);
        Console.WriteLine($"  PASS: File size = {info.Length} bytes");
    }
    else
    {
        Console.WriteLine("  FAIL: File not found after save");
    }

    // Preview
    var preview = ExcelPreview.Preview(basicPath);
    Console.WriteLine("  --- Preview ---");
    Console.WriteLine(preview.Text);
    if (preview.Warnings.Count > 0)
    {
        foreach (var w in preview.Warnings)
            Console.WriteLine($"  WARNING: {w}");
    }

    // Formula audit
    var issues = FormulaValidator.Audit(wb1);
    if (issues.Count > 0)
    {
        Console.WriteLine("  --- Formula Issues ---");
        foreach (var i in issues)
            Console.WriteLine($"  {i}");
    }
    else
    {
        Console.WriteLine("  PASS: No formula issues found");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"  FAIL: {ex.GetType().Name}: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
}

// ============================================================
// Test 2: AdvancedBuilder (frozen panes + conditional formatting)
// ============================================================
Console.WriteLine();
Console.WriteLine("=== Test 2: AdvancedBuilder (advanced) ===");
var advancedPath = Path.Combine(outputDir, "excel-advanced.xlsx");

try
{
    using var wb2 = new XLWorkbook();

    // Sheet 1: Sales data with frozen header, conditional formatting
    var sb2 = ExcelBuilder.Sheet(wb2, "Sales")
        .At(0, 0)
        .Headers("Region", "Month", "Revenue", "Expenses", "Profit")
        .Data(new[] {
            new[] { "North",  "Jan", "50000", "30000", "20000" },
            new[] { "South",  "Jan", "42000", "28000", "14000" },
            new[] { "East",   "Jan", "65000", "40000", "25000" },
            new[] { "West",   "Jan", "38000", "25000", "13000" },
            new[] { "North",  "Feb", "52000", "31000", "21000" },
            new[] { "South",  "Feb", "44000", "27000", "17000" },
            new[] { "East",   "Feb", "68000", "42000", "26000" },
            new[] { "West",   "Feb", "40000", "26000", "14000" },
            new[] { "North",  "Mar", "54000", "32000", "22000" },
            new[] { "South",  "Mar", "46000", "29000", "17000" },
            new[] { "East",   "Mar", "70000", "43000", "27000" },
            new[] { "West",   "Mar", "41000", "26000", "15000" },
        })
        .ColumnWidths(new[] { 12.0, 10.0, 14.0, 14.0, 14.0 })
        .HeaderStyle("#1F4E79", "#FFFFFF", bold: true, fontSize: 11)
        .FreezeHeader()
        .Table("SalesTable");

    var ws = sb2.Ws;

    // Number format for currency columns
    ws.Range("C2:E13").Style.NumberFormat.Format = "#,##0";

    // Conditional formatting: Revenue > 60000 highlighted green
    var cfRevenue = AdvancedBuilder.ConditionalFormat(ws.Range("C2:C13"))
        .WhenGreaterThan(60000);
    cfRevenue.Fill.BackgroundColor = XLColor.FromHtml("#E2EFDA");
    cfRevenue.Font.FontColor = XLColor.FromHtml("#375623");

    var cfRevenue2 = AdvancedBuilder.ConditionalFormat(ws.Range("C2:C13"))
        .WhenLessThan(45000);
    cfRevenue2.Fill.BackgroundColor = XLColor.FromHtml("#FCE4D6");
    cfRevenue2.Font.FontColor = XLColor.FromHtml("#823A1E");

    // Conditional formatting: Profit < 15000 highlighted red
    var cfProfit = AdvancedBuilder.ConditionalFormat(ws.Range("E2:E13"))
        .WhenLessThan(15000);
    cfProfit.Fill.BackgroundColor = XLColor.FromHtml("#F4B4C2");
    cfProfit.Font.FontColor = XLColor.FromHtml("#9B1B30");
    cfProfit.Font.Bold = true;

    // Add comment on header
    ws.Cell("E1").AddComment("Profit = Revenue - Expenses", "System");

    // Sheet 2: Summary with data bars and color scale
    var sb3 = ExcelBuilder.Sheet(wb2, "Summary", 2)
        .Headers("Region", "Q1 Revenue", "Q1 Expenses", "Q1 Profit")
        .Data(new[] {
            new[] { "North", "156000", "93000", "63000" },
            new[] { "South", "132000", "84000", "48000" },
            new[] { "East",  "203000", "125000", "78000" },
            new[] { "West",  "119000", "77000", "42000" },
        })
        .ColumnWidths(new[] { 12.0, 16.0, 16.0, 16.0 })
        .HeaderStyle("#1F4E79", "#FFFFFF", bold: true, fontSize: 11);

    var ws2 = sb3.Ws;

    // Data bars on Revenue column
    ws2.Range("B2:B5").AddConditionalFormat().DataBar(XLColor.FromHtml("#4472C4"));

    // Color scale on Profit column
    var cfScale = AdvancedBuilder.ConditionalFormat(ws2.Range("D2:D5"));
    cfScale.ColorScale(XLColor.FromHtml("#F8696B"), XLColor.FromHtml("#FFEB84"), XLColor.FromHtml("#63BE7B"));

    // Number format
    ws2.Range("B2:D5").Style.NumberFormat.Format = "#,##0";

    // Thin border
    StylePresets.ThinBorder(ws2.Range("A1:D5"), "#D0D0D0");

    // AutoFilter on Summary sheet
    AdvancedBuilder.SetAutoFilter(ws2, "A1:D5");

    wb2.SaveAs(advancedPath);
    Console.WriteLine($"  PASS: Saved {advancedPath}");

    if (File.Exists(advancedPath))
    {
        var info = new FileInfo(advancedPath);
        Console.WriteLine($"  PASS: File size = {info.Length} bytes");
    }
    else
    {
        Console.WriteLine("  FAIL: File not found after save");
    }

    var preview2 = ExcelPreview.Preview(advancedPath);
    Console.WriteLine("  --- Preview ---");
    Console.WriteLine(preview2.Text);
    if (preview2.Warnings.Count > 0)
    {
        foreach (var w in preview2.Warnings)
            Console.WriteLine($"  WARNING: {w}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"  FAIL: {ex.GetType().Name}: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
}

// ============================================================
// Test 3: StylePresets
// ============================================================
Console.WriteLine();
Console.WriteLine("=== Test 3: StylePresets ===");
try
{
    var mono = StylePresets.Mono;
    Console.WriteLine($"  PASS: Mono preset loaded: bg={mono.HeaderBg}, fg={mono.HeaderFg}");

    var finance = StylePresets.Finance;
    Console.WriteLine($"  PASS: Finance preset loaded: bg={finance.HeaderBg}, fg={finance.HeaderFg}");

    Console.WriteLine("  PASS: StylePresets API available");
}
catch (Exception ex)
{
    Console.WriteLine($"  FAIL: {ex.GetType().Name}: {ex.Message}");
}

Console.WriteLine();
Console.WriteLine("=== All tests completed ===");
