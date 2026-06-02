using System.Text.Json.Serialization;

namespace MultiModalCore;

// === API 请求 ===

public record OcrOptions
{
    public bool UseDocOrientationClassify { get; init; }
    public bool UseDocUnwarping { get; init; }
    public bool UseChartRecognition { get; init; }
}

// === API 响应 ===

public record JobSubmitResponse
{
    [JsonPropertyName("jobId")]
    public string JobId { get; init; } = "";
}

public record JobStatusResponse
{
    [JsonPropertyName("state")]
    public string State { get; init; } = "";

    [JsonPropertyName("extractProgress")]
    public ExtractProgress? ExtractProgress { get; init; }

    [JsonPropertyName("resultUrl")]
    public ResultUrl? ResultUrl { get; init; }

    [JsonPropertyName("errorMsg")]
    public string? ErrorMsg { get; init; }
}

public record ExtractProgress
{
    [JsonPropertyName("totalPages")]
    public int TotalPages { get; init; }

    [JsonPropertyName("extractedPages")]
    public int ExtractedPages { get; init; }

    [JsonPropertyName("startTime")]
    public string? StartTime { get; init; }

    [JsonPropertyName("endTime")]
    public string? EndTime { get; init; }
}

public record ResultUrl
{
    [JsonPropertyName("jsonUrl")]
    public string JsonUrl { get; init; } = "";
}

public record ApiEnvelope<T>
{
    [JsonPropertyName("data")]
    public T Data { get; init; } = default!;
}

// === JSONL 结果 ===

public record JsonlLine
{
    [JsonPropertyName("result")]
    public JsonlResult Result { get; init; } = default!;
}

public record JsonlResult
{
    [JsonPropertyName("layoutParsingResults")]
    public List<LayoutParsingResult> LayoutParsingResults { get; init; } = [];
}

public record LayoutParsingResult
{
    [JsonPropertyName("prunedResult")]
    public PrunedResult? PrunedResult { get; init; }

    [JsonPropertyName("markdown")]
    public MarkdownData Markdown { get; init; } = default!;

    [JsonPropertyName("outputImages")]
    public Dictionary<string, string>? OutputImages { get; init; }
}

/// <summary>版面分析精炼结果，每个 block 都有坐标和标签。</summary>
public record PrunedResult
{
    [JsonPropertyName("width")]
    public int Width { get; init; }

    [JsonPropertyName("height")]
    public int Height { get; init; }

    [JsonPropertyName("parsing_res_list")]
    public List<ParsingBlock> ParsingResList { get; init; } = [];
}

public record ParsingBlock
{
    [JsonPropertyName("block_label")]
    public string BlockLabel { get; init; } = "";

    [JsonPropertyName("block_content")]
    public string BlockContent { get; init; } = "";

    [JsonPropertyName("block_bbox")]
    public float[] BlockBbox { get; init; } = [];

    [JsonPropertyName("block_id")]
    public int BlockId { get; init; }

    [JsonPropertyName("block_order")]
    public int? BlockOrder { get; init; }

    [JsonPropertyName("group_id")]
    public int GroupId { get; init; }

    [JsonPropertyName("block_polygon_points")]
    public float[][]? BlockPolygonPoints { get; init; }
}

public record MarkdownData
{
    [JsonPropertyName("text")]
    public string Text { get; init; } = "";

    [JsonPropertyName("images")]
    public Dictionary<string, string>? Images { get; init; }
}

// === 本地 OCR 结果 ===

public record LocalOcrBlock
{
    public float[] Bbox { get; init; } = [];
    public string Text { get; init; } = "";
    public float Confidence { get; init; }
}

// === 结构化结果（供 Word 生成等下游消费） ===

public record OcrResult
{
    public List<OcrPage> Pages { get; init; } = [];
}

public record OcrPage
{
    public int PageNumber { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public List<ParsingBlock> Blocks { get; init; } = [];
    public string Markdown { get; init; } = "";
    public Dictionary<string, string>? MarkdownImages { get; init; }
}

// === ProcessAsync 输入类型判别 ===

public enum InputKind
{
    LocalFile,
    Url,
    RawBytes
}

// === 异常 ===

public class PaddleOcrException : Exception
{
    public PaddleOcrException(string message) : base(message) { }
    public PaddleOcrException(string message, Exception inner) : base(message, inner) { }
}
