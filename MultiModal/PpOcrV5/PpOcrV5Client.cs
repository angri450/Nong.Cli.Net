namespace MultiModalCore;

/// <summary>
/// PP-OCRv5 本地 ONNX 推理客户端（骨架）。
/// 实际 ONNX 推理尚未实现，当前所有调用返回 E005。
/// </summary>
public class PpOcrV5Client
{
    /// <summary>已解析的模型目录路径（可能为 null）。</summary>
    public string? ModelDir { get; }

    /// <summary>模型是否可用（目录存在且包含 manifest.json）。</summary>
    public bool IsAvailable { get; }

    /// <param name="modelDir">显式指定模型目录，为 null 则自动解析。</param>
    public PpOcrV5Client(string? modelDir = null)
    {
        ModelDir = PpOcrV5ModelResolver.Resolve(modelDir);
        IsAvailable = ModelDir != null;
    }

    /// <summary>
    /// 对单张图片执行 OCR 识别。
    /// 当前为骨架实现 — ONNX 推理尚未实现，不可用时抛出 PaddleOcrException (E005)，
    /// 模型可用但推理未实现时抛出 NotImplementedException。
    /// </summary>
    public Task<PpOcrV5Result> RecognizeAsync(string imagePath, CancellationToken cancel = default)
    {
        if (!IsAvailable)
            throw new PaddleOcrException("PP-OCRv5 model not found. Please download the model package first.");

        // 实际 ONNX 推理尚未实现
        throw new NotImplementedException("PP-OCRv5 ONNX inference is not yet implemented.");
    }
}

/// <summary>PP-OCRv5 识别结果。</summary>
public sealed record PpOcrV5Result
{
    public string Engine { get; set; } = "PP-OCRv5";
    public string ModelId { get; set; } = "pp-ocrv5-mobile";
    public List<PpOcrV5Page> Pages { get; set; } = new();
}

/// <summary>单页识别结果。</summary>
public sealed record PpOcrV5Page
{
    public int Page { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public List<PpOcrV5Block> Blocks { get; set; } = new();
}

/// <summary>单个文字块识别结果。</summary>
public sealed record PpOcrV5Block
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
    public double Confidence { get; set; }
    public float[] Bbox { get; set; } = Array.Empty<float>();
    public float[][]? Polygon { get; set; }
}
