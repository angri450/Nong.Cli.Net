using OpenCvSharp;

namespace MultiModalCore;

/// <summary>PP-OCRv6 共享类型 (原 PpOcrV5 命名保留兼容)。</summary>

public enum PpOcrV5InferenceMode { Fast, Safe }

public sealed record PpOcrV5EnvironmentStatus(
    bool Available, string Engine, string ModelId, string Runtime, string Message);

public sealed record PpOcrV5Result
{
    public string Engine { get; set; } = "pp-ocrv6-dotnet-sdcb";
    public string ModelId { get; set; } = "pp-ocrv6-medium";
    public string InferenceMode { get; set; } = "fast-cpu";
    public bool NumericFallbackAttempted { get; set; }
    public bool NumericFallbackApplied { get; set; }
    public string? NumericFallbackReason { get; set; }
    public List<PpOcrV5Page> Pages { get; set; } = new();
    public int InvalidConfidenceBlocks => Pages.Sum(p => p.Blocks.Count(b => !b.ConfidenceValid));
    public int InvalidGeometryBlocks => Pages.Sum(p => p.Blocks.Count(b => !b.GeometryValid));
    public int NumericIssueCount => InvalidConfidenceBlocks + InvalidGeometryBlocks;
    public bool HasNumericIssues => NumericIssueCount > 0;
    public int BlockCount => Pages.Sum(p => p.Blocks.Count);
    public int TextLength => Pages.Sum(p => p.Blocks.Sum(b => b.Text.Length));
}

public sealed record PpOcrV5Page
{
    public int Page { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public List<PpOcrV5Block> Blocks { get; set; } = new();
}

public sealed record PpOcrV5Block
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
    public double? Confidence { get; set; }
    public bool ConfidenceValid => Confidence.HasValue;
    public bool GeometryValid { get; set; } = true;
    public string? NumericIssue { get; set; }
    public float[] Bbox { get; set; } = Array.Empty<float>();
    public float[][]? Polygon { get; set; }
}
