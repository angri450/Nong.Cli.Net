using ClosedXML.Excel;

namespace ExcelCore;

public static class StylePresets
{
    /// <summary>Default currency symbol used in built-in presets. Override before use:
    ///   StylePresets.DefaultCurrency = "\"$\"";
    ///   StylePresets.DefaultCurrency = "\"€\"";
    /// </summary>
    public static string DefaultCurrency = "\"¥\"";

    public static Preset Mono => new()
    {
        Name = "mono",
        HeaderBg = "#333333", HeaderFg = "#FFFFFF",
        RowAlt = "#F5F5F5", Accent = "#0066CC", Border = "#D0D0D0",
        Integer = "#,##0", Decimal = "#,##0.00", Currency = $"{DefaultCurrency}#,##0.00",
        Percent = "0.0%", Date = "yyyy-mm-dd"
    };

    public static Preset Finance => new()
    {
        Name = "finance",
        HeaderBg = "#1F4E79", HeaderFg = "#FFFFFF",
        RowAlt = "#FFF3E0", Accent = "#1F4E79", Border = "#B0B0B0",
        Integer = "#,##0", Decimal = "#,##0.00", Currency = $"{DefaultCurrency}#,##0.00",
        Percent = "0.00%", Date = "yyyy-mm-dd"
    };

    public static Preset BuildFromJson(string jsonPath)
    {
        var json = File.ReadAllText(jsonPath);
        var p = System.Text.Json.JsonSerializer.Deserialize<Preset>(json)
                ?? throw new InvalidOperationException($"Failed to parse style preset: {jsonPath}");
        return p;
    }

    public static void MonoHeader(IXLRow row, int startCol, int endCol)
    {
        for (int c = startCol; c <= endCol; c++)
        {
            var cell = row.Cell(c);
            cell.Style.Fill.BackgroundColor = Mono.HeaderBgColor;
            cell.Style.Font.FontColor = Mono.HeaderFgColor;
            cell.Style.Font.Bold = true;
        }
    }

    public static void FinanceHeader(IXLRow row, int startCol, int endCol)
    {
        for (int c = startCol; c <= endCol; c++)
        {
            var cell = row.Cell(c);
            cell.Style.Fill.BackgroundColor = Finance.HeaderBgColor;
            cell.Style.Font.FontColor = Finance.HeaderFgColor;
            cell.Style.Font.Bold = true;
        }
    }

    public static void AlternatingRows(IXLWorksheet ws, int startRow, int endRow,
        int startCol, int endCol, string evenColor)
    {
        var color = XLColor.FromHtml(evenColor);
        for (var r = startRow; r <= endRow; r++)
        {
            if ((r - startRow) % 2 == 1)
            {
                for (int c = startCol; c <= endCol; c++)
                    ws.Cell(r, c).Style.Fill.BackgroundColor = color;
            }
        }
    }

    public static void ThinBorder(IXLRange range, string color)
    {
        var c = XLColor.FromHtml(color);
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorderColor = c;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorderColor = c;
    }

    public static class Colors
    {
        public const string MonoHeaderBg = "#333333";
        public const string MonoHeaderFg = "#FFFFFF";
        public const string FinHeaderBg = "#1F4E79";
        public const string FinHeaderFg = "#FFFFFF";
        public const string UpRed = "#DB4453";
        public const string DownGreen = "#70AD47";
    }

    public static class Formats
    {
        public const string Integer = "#,##0";
        public const string Decimal = "#,##0.00";
        public const string Currency = "\"¥\"#,##0.00";
        public const string Percent = "0.0%";
        public const string Date = "yyyy-mm-dd";
    }

    public class Preset
    {
        public string Name { get; set; } = "";
        public string HeaderBg { get; set; } = "";
        public string HeaderFg { get; set; } = "";
        public string RowAlt { get; set; } = "";
        public string Accent { get; set; } = "";
        public string Border { get; set; } = "";
        public string Integer { get; set; } = "";
        public string Decimal { get; set; } = "";
        public string Currency { get; set; } = "";
        public string Percent { get; set; } = "";
        public string Date { get; set; } = "";

        public XLColor HeaderBgColor => XLColor.FromHtml(HeaderBg);
        public XLColor HeaderFgColor => XLColor.FromHtml(HeaderFg);
        public XLColor RowAltColor => XLColor.FromHtml(RowAlt);
        public XLColor AccentColor => XLColor.FromHtml(Accent);
        public XLColor BorderColor => XLColor.FromHtml(Border);

        public void Deconstruct(out string name, out string headerBg, out string headerFg,
            out string rowAlt, out string accent, out string border,
            out string integer, out string decimalFmt, out string currency,
            out string percent, out string date)
        {
            name = Name; headerBg = HeaderBg; headerFg = HeaderFg;
            rowAlt = RowAlt; accent = Accent; border = Border;
            integer = Integer; decimalFmt = Decimal; currency = Currency;
            percent = Percent; date = Date;
        }
    }
}
