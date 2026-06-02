using DocumentFormat.OpenXml.Wordprocessing;

namespace DocxCore;

/// <summary>
/// Word 内置表格样式预设。直接引用 Word 内置样式 ID。
/// 使用时：tablePr.Append(new TableStyle { Val = TableStyles.LightShadingAccent1 });
/// </summary>
public static class TableStyles
{
    // Light Shading series
    public const string LightShading = "LightShading";
    public const string LightShadingAccent1 = "LightShading-Accent1";
    public const string LightShadingAccent2 = "LightShading-Accent2";
    public const string LightShadingAccent3 = "LightShading-Accent3";
    public const string LightShadingAccent4 = "LightShading-Accent4";
    public const string LightShadingAccent5 = "LightShading-Accent5";
    public const string LightShadingAccent6 = "LightShading-Accent6";

    // Medium Shading series
    public const string MediumShading1 = "MediumShading1";
    public const string MediumShading1Accent1 = "MediumShading1-Accent1";
    public const string MediumShading1Accent2 = "MediumShading1-Accent2";
    public const string MediumShading1Accent3 = "MediumShading1-Accent3";
    public const string MediumShading1Accent4 = "MediumShading1-Accent4";
    public const string MediumShading1Accent5 = "MediumShading1-Accent5";
    public const string MediumShading1Accent6 = "MediumShading1-Accent6";
    public const string MediumShading2 = "MediumShading2";
    public const string MediumShading2Accent1 = "MediumShading2-Accent1";
    public const string MediumShading2Accent2 = "MediumShading2-Accent2";
    public const string MediumShading2Accent3 = "MediumShading2-Accent3";
    public const string MediumShading2Accent4 = "MediumShading2-Accent4";
    public const string MediumShading2Accent5 = "MediumShading2-Accent5";
    public const string MediumShading2Accent6 = "MediumShading2-Accent6";

    // Light List series
    public const string LightList = "LightList";
    public const string LightListAccent1 = "LightList-Accent1";
    public const string LightListAccent2 = "LightList-Accent2";
    public const string LightListAccent3 = "LightList-Accent3";
    public const string LightListAccent4 = "LightList-Accent4";
    public const string LightListAccent5 = "LightList-Accent5";
    public const string LightListAccent6 = "LightList-Accent6";

    // Light Grid series
    public const string LightGrid = "LightGrid";
    public const string LightGridAccent1 = "LightGrid-Accent1";
    public const string LightGridAccent2 = "LightGrid-Accent2";
    public const string LightGridAccent3 = "LightGrid-Accent3";
    public const string LightGridAccent4 = "LightGrid-Accent4";
    public const string LightGridAccent5 = "LightGrid-Accent5";
    public const string LightGridAccent6 = "LightGrid-Accent6";

    // Medium List series
    public const string MediumList1 = "MediumList1";
    public const string MediumList1Accent1 = "MediumList1-Accent1";
    public const string MediumList1Accent2 = "MediumList1-Accent2";
    public const string MediumList1Accent3 = "MediumList1-Accent3";
    public const string MediumList1Accent4 = "MediumList1-Accent4";
    public const string MediumList1Accent5 = "MediumList1-Accent5";
    public const string MediumList1Accent6 = "MediumList1-Accent6";
    public const string MediumList2 = "MediumList2";
    public const string MediumList2Accent1 = "MediumList2-Accent1";
    public const string MediumList2Accent2 = "MediumList2-Accent2";
    public const string MediumList2Accent3 = "MediumList2-Accent3";
    public const string MediumList2Accent4 = "MediumList2-Accent4";
    public const string MediumList2Accent5 = "MediumList2-Accent5";
    public const string MediumList2Accent6 = "MediumList2-Accent6";

    // Medium Grid series
    public const string MediumGrid1 = "MediumGrid1";
    public const string MediumGrid1Accent1 = "MediumGrid1-Accent1";
    public const string MediumGrid1Accent2 = "MediumGrid1-Accent2";
    public const string MediumGrid1Accent3 = "MediumGrid1-Accent3";
    public const string MediumGrid1Accent4 = "MediumGrid1-Accent4";
    public const string MediumGrid1Accent5 = "MediumGrid1-Accent5";
    public const string MediumGrid1Accent6 = "MediumGrid1-Accent6";
    public const string MediumGrid2 = "MediumGrid2";
    public const string MediumGrid2Accent1 = "MediumGrid2-Accent1";
    public const string MediumGrid2Accent2 = "MediumGrid2-Accent2";
    public const string MediumGrid2Accent3 = "MediumGrid2-Accent3";
    public const string MediumGrid2Accent4 = "MediumGrid2-Accent4";
    public const string MediumGrid2Accent5 = "MediumGrid2-Accent5";
    public const string MediumGrid2Accent6 = "MediumGrid2-Accent6";
    public const string MediumGrid3 = "MediumGrid3";
    public const string MediumGrid3Accent1 = "MediumGrid3-Accent1";
    public const string MediumGrid3Accent2 = "MediumGrid3-Accent2";
    public const string MediumGrid3Accent3 = "MediumGrid3-Accent3";
    public const string MediumGrid3Accent4 = "MediumGrid3-Accent4";
    public const string MediumGrid3Accent5 = "MediumGrid3-Accent5";
    public const string MediumGrid3Accent6 = "MediumGrid3-Accent6";

    // Dark List series
    public const string DarkList = "DarkList";
    public const string DarkListAccent1 = "DarkList-Accent1";
    public const string DarkListAccent2 = "DarkList-Accent2";
    public const string DarkListAccent3 = "DarkList-Accent3";
    public const string DarkListAccent4 = "DarkList-Accent4";
    public const string DarkListAccent5 = "DarkList-Accent5";
    public const string DarkListAccent6 = "DarkList-Accent6";

    // Colorful series
    public const string ColorfulGrid = "ColorfulGrid";
    public const string ColorfulGridAccent1 = "ColorfulGrid-Accent1";
    public const string ColorfulGridAccent2 = "ColorfulGrid-Accent2";
    public const string ColorfulGridAccent3 = "ColorfulGrid-Accent3";
    public const string ColorfulGridAccent4 = "ColorfulGrid-Accent4";
    public const string ColorfulGridAccent5 = "ColorfulGrid-Accent5";
    public const string ColorfulGridAccent6 = "ColorfulGrid-Accent6";
    public const string ColorfulList = "ColorfulList";
    public const string ColorfulListAccent1 = "ColorfulList-Accent1";
    public const string ColorfulListAccent2 = "ColorfulList-Accent2";
    public const string ColorfulListAccent3 = "ColorfulList-Accent3";
    public const string ColorfulListAccent4 = "ColorfulList-Accent4";
    public const string ColorfulListAccent5 = "ColorfulList-Accent5";
    public const string ColorfulListAccent6 = "ColorfulList-Accent6";
    public const string ColorfulShading = "ColorfulShading";
    public const string ColorfulShadingAccent1 = "ColorfulShading-Accent1";
    public const string ColorfulShadingAccent2 = "ColorfulShading-Accent2";
    public const string ColorfulShadingAccent3 = "ColorfulShading-Accent3";
    public const string ColorfulShadingAccent4 = "ColorfulShading-Accent4";
    public const string ColorfulShadingAccent5 = "ColorfulShading-Accent5";
    public const string ColorfulShadingAccent6 = "ColorfulShading-Accent6";

    // Basic
    public const string TableNormal = "TableNormal";
    public const string TableGrid = "TableGrid";

    /// <summary>全部内置表格样式名（按类别分组）。</summary>
    public static readonly Dictionary<string, string[]> Categories = new()
    {
        ["Light Shading"] = new[] { LightShading, LightShadingAccent1, LightShadingAccent2, LightShadingAccent3, LightShadingAccent4, LightShadingAccent5, LightShadingAccent6 },
        ["Medium Shading"] = new[] { MediumShading1, MediumShading1Accent1, MediumShading1Accent2, MediumShading1Accent3, MediumShading1Accent4, MediumShading1Accent5, MediumShading1Accent6, MediumShading2, MediumShading2Accent1, MediumShading2Accent2, MediumShading2Accent3, MediumShading2Accent4, MediumShading2Accent5, MediumShading2Accent6 },
        ["Light List"] = new[] { LightList, LightListAccent1, LightListAccent2, LightListAccent3, LightListAccent4, LightListAccent5, LightListAccent6 },
        ["Light Grid"] = new[] { LightGrid, LightGridAccent1, LightGridAccent2, LightGridAccent3, LightGridAccent4, LightGridAccent5, LightGridAccent6 },
        ["Medium List"] = new[] { MediumList1, MediumList1Accent1, MediumList1Accent2, MediumList1Accent3, MediumList1Accent4, MediumList1Accent5, MediumList1Accent6, MediumList2, MediumList2Accent1, MediumList2Accent2, MediumList2Accent3, MediumList2Accent4, MediumList2Accent5, MediumList2Accent6 },
        ["Medium Grid"] = new[] { MediumGrid1, MediumGrid1Accent1, MediumGrid1Accent2, MediumGrid1Accent3, MediumGrid1Accent4, MediumGrid1Accent5, MediumGrid1Accent6, MediumGrid2, MediumGrid2Accent1, MediumGrid2Accent2, MediumGrid2Accent3, MediumGrid2Accent4, MediumGrid2Accent5, MediumGrid2Accent6, MediumGrid3, MediumGrid3Accent1, MediumGrid3Accent2, MediumGrid3Accent3, MediumGrid3Accent4, MediumGrid3Accent5, MediumGrid3Accent6 },
        ["Dark List"] = new[] { DarkList, DarkListAccent1, DarkListAccent2, DarkListAccent3, DarkListAccent4, DarkListAccent5, DarkListAccent6 },
        ["Colorful"] = new[] { ColorfulGrid, ColorfulGridAccent1, ColorfulGridAccent2, ColorfulGridAccent3, ColorfulGridAccent4, ColorfulGridAccent5, ColorfulGridAccent6, ColorfulList, ColorfulListAccent1, ColorfulListAccent2, ColorfulListAccent3, ColorfulListAccent4, ColorfulListAccent5, ColorfulListAccent6, ColorfulShading, ColorfulShadingAccent1, ColorfulShadingAccent2, ColorfulShadingAccent3, ColorfulShadingAccent4, ColorfulShadingAccent5, ColorfulShadingAccent6 },
        ["Basic"] = new[] { TableNormal, TableGrid },
    };
}
