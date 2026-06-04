using System.Text.Json.Serialization;

namespace Nong.Inspect;

/// <summary>
/// 单个角色（标题/一级/二级/三级/正文/脚注）的完整排版规格。
/// 用户可经 JSON 配置文件覆盖任意字段；未给就用 FormatOptions 里预置的默认值。
/// </summary>
public sealed class StyleSpec
{
    /// <summary>字体名（中文名优先，匹配 Windows 上的字体注册名）。</summary>
    [JsonPropertyName("font")]
    public string Font { get; set; } = "仿宋_GB2312";

    /// <summary>字号（磅/point）。三号=16，二号=22，小四=12 等。</summary>
    [JsonPropertyName("size_pt")]
    public double SizePt { get; set; } = 16;

    [JsonPropertyName("bold")]
    public bool Bold { get; set; } = false;

    [JsonPropertyName("italic")]
    public bool Italic { get; set; } = false;

    /// <summary>对齐方式："left" / "center" / "right" / "justify" / "" (不设置=继承)。</summary>
    [JsonPropertyName("alignment")]
    public string Alignment { get; set; } = "";

    /// <summary>行距模式："multiple"（倍数）或 "exact"（固定磅值）。</summary>
    [JsonPropertyName("line_spacing_mode")]
    public string LineSpacingMode { get; set; } = "multiple";

    /// <summary>
    /// 行距值。multiple 模式下是倍数（1.0 / 1.5 / 2.0 等）；
    /// exact 模式下是磅值（如 28 = 固定 28 磅）。
    /// </summary>
    [JsonPropertyName("line_spacing_value")]
    public double LineSpacingValue { get; set; } = 1.5;

    [JsonPropertyName("spacing_before_pt")]
    public double SpacingBeforePt { get; set; } = 0;

    [JsonPropertyName("spacing_after_pt")]
    public double SpacingAfterPt { get; set; } = 0;

    [JsonPropertyName("first_line_indent_chars")]
    public int FirstLineIndentChars { get; set; } = 2;

    // ================ 派生属性（OpenXML 单位） ================

    /// <summary>OpenXML 字号 (half-points)：16pt → "32"。</summary>
    [JsonIgnore]
    public string SzHalfPoints => ((int)System.Math.Round(SizePt * 2)).ToString();

    /// <summary>OpenXML 段前距 (twips = 磅×20)：6磅 → "120"。</summary>
    [JsonIgnore]
    public string SpacingBeforeTwips => ((int)System.Math.Round(SpacingBeforePt * 20)).ToString();

    [JsonIgnore]
    public string SpacingAfterTwips => ((int)System.Math.Round(SpacingAfterPt * 20)).ToString();

    /// <summary>OpenXML w:ind 的 firstLineChars（单位 1/100 字符）：2 字符 → 200。</summary>
    [JsonIgnore]
    public int FirstLineCharsValue => System.Math.Max(0, FirstLineIndentChars * 100);

    /// <summary>
    /// 返回 (w:line 值, w:lineRule 值) 二元组。
    /// multiple 模式：单倍 line=240，1.5倍 line=360，2倍 line=480，lineRule=auto
    /// exact 模式：line 为磅×20 的 twips，lineRule=exact
    /// </summary>
    public (string line, string lineRule) GetLineSpec()
    {
        if (LineSpacingMode == "exact")
        {
            int tw = (int)System.Math.Round(LineSpacingValue * 20);
            return (tw.ToString(), "exact");
        }
        int mult = (int)System.Math.Round(LineSpacingValue * 240);
        return (mult.ToString(), "auto");
    }

    public StyleSpec Clone()
    {
        return new StyleSpec
        {
            Font = Font,
            SizePt = SizePt,
            Bold = Bold,
            Italic = Italic,
            Alignment = Alignment,
            LineSpacingMode = LineSpacingMode,
            LineSpacingValue = LineSpacingValue,
            SpacingBeforePt = SpacingBeforePt,
            SpacingAfterPt = SpacingAfterPt,
            FirstLineIndentChars = FirstLineIndentChars
        };
    }
}
