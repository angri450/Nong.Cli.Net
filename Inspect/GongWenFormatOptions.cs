using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nong.Inspect;

/// <summary>
/// 一次排版的全部可配置项。
/// 用户可经 --config style.json 从文件读入覆盖；未给就走 PresetDefault 的缺省。
/// </summary>
public sealed class FormatOptions
{
    [JsonPropertyName("source")]
    public string Source { get; set; } = "auto";

    [JsonPropertyName("h1_marker")]
    public string? H1MarkerSample { get; set; }

    [JsonPropertyName("h2_marker")]
    public string? H2MarkerSample { get; set; }

    [JsonPropertyName("h3_marker")]
    public string? H3MarkerSample { get; set; }

    [JsonPropertyName("title")]
    public StyleSpec Title { get; set; } = PresetDefault.Title();

    [JsonPropertyName("h1")]
    public StyleSpec H1 { get; set; } = PresetDefault.H1();

    [JsonPropertyName("h2")]
    public StyleSpec H2 { get; set; } = PresetDefault.H2();

    [JsonPropertyName("h3")]
    public StyleSpec H3 { get; set; } = PresetDefault.H3();

    [JsonPropertyName("body")]
    public StyleSpec Body { get; set; } = PresetDefault.Body();

    [JsonPropertyName("footnote")]
    public StyleSpec Footnote { get; set; } = PresetDefault.Footnote();

    /// <summary>从 JSON 文件加载，缺失字段使用 PresetDefault 的值。</summary>
    public static FormatOptions FromJsonFile(string path)
    {
        var json = System.IO.File.ReadAllText(path);
        var opts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        var result = JsonSerializer.Deserialize<FormatOptions>(json, opts);
        return result ?? new FormatOptions();
    }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
    }
}

/// <summary>
/// 各角色的内建默认。与 v2 固化的公文规范一致：
///   标题 方正小标宋 二号 居中 固定 28 磅
///   一级 黑体 三号 段前段后 6 磅 1.5倍 首行 2 字符
///   二级 楷体_GB2312 三号 加粗 1.5倍 首行 2 字符
///   三级 仿宋_GB2312 三号 加粗 1.5倍 首行 2 字符
///   正文 仿宋_GB2312 三号 1.5倍 首行 2 字符
///   脚注 仿宋_GB2312 四号 单倍 首行 0
/// </summary>
public static class PresetDefault
{
    public static StyleSpec Title() => new()
    {
        Font = "方正小标宋简体",
        SizePt = 22, Bold = false, Italic = false,
        Alignment = "center",
        LineSpacingMode = "exact", LineSpacingValue = 28,   // 固定值 28 磅
        SpacingBeforePt = 0, SpacingAfterPt = 0,
        FirstLineIndentChars = 0,
    };

    public static StyleSpec H1() => new()
    {
        Font = "黑体",
        SizePt = 16, Bold = false, Italic = false,
        Alignment = "",
        LineSpacingMode = "multiple", LineSpacingValue = 1.5,
        SpacingBeforePt = 6, SpacingAfterPt = 6,
        FirstLineIndentChars = 2,
    };

    public static StyleSpec H2() => new()
    {
        Font = "楷体_GB2312",
        SizePt = 16, Bold = true, Italic = false,
        Alignment = "",
        LineSpacingMode = "multiple", LineSpacingValue = 1.5,
        SpacingBeforePt = 0, SpacingAfterPt = 0,
        FirstLineIndentChars = 2,
    };

    public static StyleSpec H3() => new()
    {
        Font = "仿宋_GB2312",
        SizePt = 16, Bold = true, Italic = false,
        Alignment = "",
        LineSpacingMode = "multiple", LineSpacingValue = 1.5,
        SpacingBeforePt = 0, SpacingAfterPt = 0,
        FirstLineIndentChars = 2,
    };

    public static StyleSpec Body() => new()
    {
        Font = "仿宋_GB2312",
        SizePt = 16, Bold = false, Italic = false,
        Alignment = "",
        LineSpacingMode = "multiple", LineSpacingValue = 1.5,
        SpacingBeforePt = 0, SpacingAfterPt = 0,
        FirstLineIndentChars = 2,
    };

    public static StyleSpec Footnote() => new()
    {
        Font = "仿宋_GB2312",
        SizePt = 14, Bold = false, Italic = false,
        Alignment = "",
        LineSpacingMode = "multiple", LineSpacingValue = 1.0,
        SpacingBeforePt = 0, SpacingAfterPt = 0,
        FirstLineIndentChars = 0,
    };
}
