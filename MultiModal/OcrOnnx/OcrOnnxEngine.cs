using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace MultiModalCore;

/// <summary>
/// PP-OCRv6 ONNX Runtime inference engine.
/// Replaces PpOcrV6Client (Sdcb.PaddleOCR + PaddleInference C API).
/// Uses SkiaSharp for image loading, pure C# pre/post-processing.
/// </summary>
public sealed class OcrOnnxEngine : IDisposable
{
    readonly InferenceSession _detSession;
    readonly InferenceSession _recSession;
    readonly string[] _dict;         // index → character
    readonly int _detLimit;
    readonly string _modelId;

    const float DetThresh = 0.3f;
    const float DetBoxThresh = 0.6f;
    const int RecHeight = 48;
    const int RecWidth = 320;

    public string ModelId => _modelId;
    public string DictPath { get; }
    public int DictSize => _dict.Length;

    public OcrOnnxEngine(string modelDir, string modelId = "pp-ocrv6-medium")
    {
        _modelId = modelId;
        var detPath = Path.Combine(modelDir, "det.onnx");
        var recPath = Path.Combine(modelDir, "rec.onnx");
        DictPath = Path.Combine(modelDir, "dict.txt");

        if (!File.Exists(detPath))
            throw new FileNotFoundException($"det.onnx not found: {detPath}");
        if (!File.Exists(recPath))
            throw new FileNotFoundException($"rec.onnx not found: {recPath}");
        if (!File.Exists(DictPath))
            throw new FileNotFoundException($"dict.txt not found: {DictPath}");

        _dict = File.ReadAllLines(DictPath);
        if (_dict.Length < 100)
            throw new InvalidDataException($"dict.txt too short: {DictPath}");

        var opts = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
        };
        opts.IntraOpNumThreads = Math.Max(1, Math.Min(Environment.ProcessorCount, 4));
        opts.InterOpNumThreads = 1;

        _detSession = new InferenceSession(detPath, opts);
        _recSession = new InferenceSession(recPath, opts);
        _detLimit = 960;
    }

    public PpOcrV5Result Recognize(string imagePath)
    {
        using var bitmap = SKBitmap.Decode(imagePath)
            ?? throw new InvalidOperationException($"Cannot decode image: {imagePath}");

        var sw = System.Diagnostics.Stopwatch.StartNew();

        // === 1. Detection ===
        var (detTensor, scaleX, scaleY, padLeft, padTop, detH, detW) = OcrOnnxPreprocess.DetPreprocess(bitmap, _detLimit);
        using var detResults = _detSession.Run(new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("x", detTensor)
        });
        var detOut = (DenseTensor<float>)detResults.ToList()[0].AsTensor<float>();
        var boxes = OcrOnnxPostprocess.DetPostprocess(detOut, detH, detW,
            scaleX, scaleY, padLeft, padTop, bitmap.Width, bitmap.Height,
            DetThresh, DetBoxThresh);

        // === 2. Recognition for each box ===
        var page = new PpOcrV5Page
        {
            Page = 1,
            Width = bitmap.Width,
            Height = bitmap.Height
        };

        int idx = 0;
        foreach (var box in boxes)
        {
            idx++;
            var (recTensor, _) = OcrOnnxPreprocess.RecPreprocess(bitmap, box);
            using var recResults = _recSession.Run(new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("x", recTensor)
            });
            var recOut = (DenseTensor<float>)recResults.ToList()[0].AsTensor<float>();
            var (text, confidence) = OcrOnnxPostprocess.CtcDecode(recOut, _dict);

            if (!string.IsNullOrEmpty(text))
            {
                page.Blocks.Add(new PpOcrV5Block
                {
                    Id = $"ocr{idx:D4}",
                    Text = text,
                    Confidence = confidence,
                    Bbox = new[] { box.X1, box.Y1, box.X2, box.Y2 },
                    Polygon = new[]
                    {
                        new[] { box.X1, box.Y1 },
                        new[] { box.X2, box.Y1 },
                        new[] { box.X2, box.Y2 },
                        new[] { box.X1, box.Y2 }
                    },
                    GeometryValid = true
                });
            }
        }

        sw.Stop();
        return new PpOcrV5Result
        {
            Engine = "pp-ocrv6-onnx",
            ModelId = _modelId,
            InferenceMode = "onnx-cpu",
            Pages = new List<PpOcrV5Page> { page }
        };
    }

    public static PpOcrV5EnvironmentStatus CheckEnvironment(string modelDir)
    {
        try
        {
            if (!Directory.Exists(modelDir))
                return new PpOcrV5EnvironmentStatus(false, "pp-ocrv6-onnx", "unknown", "unknown",
                    $"Model directory not found: {modelDir}");
            var detPath = Path.Combine(modelDir, "det.onnx");
            var recPath = Path.Combine(modelDir, "rec.onnx");
            if (!File.Exists(detPath) || !File.Exists(recPath))
                return new PpOcrV5EnvironmentStatus(false, "pp-ocrv6-onnx", "unknown", "unknown",
                    $"ONNX models missing in {modelDir}");

            using var s = new InferenceSession(detPath, new SessionOptions());
            return new PpOcrV5EnvironmentStatus(true, "pp-ocrv6-onnx", "pp-ocrv6-medium",
                System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                $"ONNX Runtime available. Models: {modelDir}");
        }
        catch (Exception ex)
        {
            return new PpOcrV5EnvironmentStatus(false, "pp-ocrv6-onnx", "unknown", "unknown",
                $"ONNX Runtime check failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _detSession?.Dispose();
        _recSession?.Dispose();
    }
}

/// <summary>Detected text box.</summary>
public sealed class DetBox
{
    public float X1, Y1, X2, Y2;
}
