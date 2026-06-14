using OpenCvSharp;
using System.Runtime.InteropServices;
using Sdcb.PaddleInference;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models;
using Sdcb.PaddleOCR.Models.Details;

namespace MultiModalCore;

/// <summary>
/// PP-OCRv6 本地 .NET 推理客户端。
/// 使用 Sdcb.PaddleOCR FileDetectionModel/FileRecognizationModel 从缓存目录加载 v6 模型，
/// 不依赖 Python、pip 或外部 OCR 可执行文件。
/// </summary>
public sealed class PpOcrV6Client : IDisposable
{
    private static readonly object NativeRuntimeSync = new();
    private static bool _nativeRuntimeLoaded;
    private static string? _nativeRuntimeDir;

    // Suppress native log noise before any P/Invoke happens
    static PpOcrV6Client()
    {
        ConfigureNativeLogEnvironment();
    }

    private readonly Lazy<PaddleOcrAll> _fastEngine;
    private readonly Lazy<PaddleOcrAll> _safeEngine;
    private bool _disposed;
    private readonly string _size;
    private readonly string _modelCachePath;

    public string Size => _size;
    public string ModelCachePath => _modelCachePath;
    public string ModelId => $"pp-ocrv6-{_size}";
    public bool IsAvailable => CheckEnvironment().Available;

    public PpOcrV6Client(string? size = null) : this(size, modelCachePath: null) { }

    public PpOcrV6Client(string? size, string? modelCachePath)
    {
        _size = size ?? PpOcrV6ModelResolver.DefaultSize;
        _modelCachePath = modelCachePath ?? PpOcrV6ModelResolver.GetModelCachePath(_size);

        _fastEngine = new Lazy<PaddleOcrAll>(() => CreateEngine(PpOcrV5InferenceMode.Fast));
        _safeEngine = new Lazy<PaddleOcrAll>(() => CreateEngine(PpOcrV5InferenceMode.Safe));
    }

    /// <summary>对单张图片执行 OCR 识别。</summary>
    public Task<PpOcrV5Result> RecognizeAsync(string imagePath, CancellationToken cancel = default)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            throw new PaddleOcrException("Image path is required.");
        if (!File.Exists(imagePath))
            throw new FileNotFoundException($"Image not found: {imagePath}", imagePath);

        cancel.ThrowIfCancellationRequested();

        EnsureNativeRuntimeLoaded();

        using var mat = Cv2.ImRead(imagePath, ImreadModes.Color);
        if (mat.Empty())
            throw new PaddleOcrException($"Cannot decode image: {imagePath}");

        var result = RecognizeWithEngine(_fastEngine.Value, mat, PpOcrV5InferenceMode.Fast);
        result.ModelId = ModelId;
        if (!result.HasNumericIssues)
            return Task.FromResult(result);

        PpOcrV5Result fallback;
        try
        {
            fallback = RecognizeWithEngine(_safeEngine.Value, mat, PpOcrV5InferenceMode.Safe);
        }
        catch
        {
            result.NumericFallbackAttempted = true;
            result.NumericFallbackReason = "safe_cpu_fallback_failed";
            return Task.FromResult(result);
        }

        fallback.NumericFallbackAttempted = true;
        fallback.NumericFallbackApplied = true;
        fallback.NumericFallbackReason = "fast_cpu_inference_produced_invalid_numeric_values";

        if (ShouldUseFallback(result, fallback))
            return Task.FromResult(fallback);

        result.NumericFallbackAttempted = true;
        result.NumericFallbackReason = fallback.NumericIssueCount > result.NumericIssueCount
            ? "safe_cpu_fallback_produced_more_invalid_numeric_values"
            : "safe_cpu_fallback_did_not_preserve_text";
        return Task.FromResult(result);
    }

    private static bool ShouldUseFallback(PpOcrV5Result fast, PpOcrV5Result fallback)
    {
        if (fast.BlockCount > 0 && fallback.BlockCount == 0)
            return false;
        if (fast.TextLength > 0 && fallback.TextLength < fast.TextLength * 0.8)
            return false;
        return fallback.NumericIssueCount < fast.NumericIssueCount;
    }

    private static PpOcrV5Result RecognizeWithEngine(PaddleOcrAll engine, Mat mat, PpOcrV5InferenceMode mode)
    {
        var result = engine.Run(mat);
        var page = new PpOcrV5Page
        {
            Page = 1,
            Width = mat.Width,
            Height = mat.Height
        };

        var index = 0;
        foreach (var region in result.Regions)
        {
            index++;
            var points = region.Rect.Points();
            var finitePoints = points.Where(IsFinitePoint).ToArray();
            var confidence = ToFiniteConfidence(region.Score);
            var numericIssues = new List<string>();
            if (confidence == null)
                numericIssues.Add("invalid_confidence");
            if (finitePoints.Length != points.Length || finitePoints.Length == 0)
                numericIssues.Add("invalid_geometry");

            page.Blocks.Add(new PpOcrV5Block
            {
                Id = $"ocr{index:D4}",
                Text = region.Text ?? "",
                Confidence = confidence,
                Bbox = ToAxisAlignedBbox(finitePoints),
                Polygon = finitePoints.Select(p => new[] { p.X, p.Y }).ToArray(),
                GeometryValid = numericIssues.All(i => i != "invalid_geometry"),
                NumericIssue = numericIssues.Count == 0 ? null : string.Join(",", numericIssues)
            });
        }

        return new PpOcrV5Result
        {
            Engine = "pp-ocrv6-dotnet-sdcb",
            ModelId = $"pp-ocrv6-medium",  // static helper: reports default model
            InferenceMode = mode == PpOcrV5InferenceMode.Fast ? "fast-cpu" : "safe-cpu-blas",
            Pages = new List<PpOcrV5Page> { page }
        };
    }

    public static PpOcrV5EnvironmentStatus CheckEnvironment()
    {
        try
        {
            var nativeDir = EnsureNativeRuntimeLoaded();
            var runtimeId = PpOcrV6ModelResolver.GetNativeRuntimeId();

            _ = typeof(PaddleOcrAll).Assembly.FullName;
            _ = typeof(PaddleConfig).Assembly.FullName;

            return new PpOcrV5EnvironmentStatus(
                Available: true,
                Engine: "pp-ocrv6-dotnet-sdcb",
                ModelId: "pp-ocrv6-medium",
                Runtime: runtimeId,
                Message: $"Pure .NET PP-OCRv6 runtime is available; no Python required. Native runtime: {nativeDir}");
        }
        catch (Exception ex)
        {
            return new PpOcrV5EnvironmentStatus(
                Available: false,
                Engine: "pp-ocrv6-dotnet-sdcb",
                ModelId: "pp-ocrv6-medium",
                Runtime: PpOcrV6ModelResolver.GetNativeRuntimeId(),
                Message: $"Pure .NET PP-OCRv6 runtime unavailable: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        if (_fastEngine.IsValueCreated)
            _fastEngine.Value.Dispose();
        if (_safeEngine.IsValueCreated)
            _safeEngine.Value.Dispose();
        _disposed = true;
    }

    // ===== Engine creation (uses FileDetectionModel/FileRecognizationModel) =====

    private PaddleOcrAll CreateEngine(PpOcrV5InferenceMode mode)
    {
        EnsureNativeRuntimeLoaded();

        var detDir = PpOcrV6ModelResolver.GetDetDir(_modelCachePath);
        var recDir = PpOcrV6ModelResolver.GetRecDir(_modelCachePath);
        var dictPath = PpOcrV6ModelResolver.GetDictPath(_modelCachePath);

        if (!Directory.Exists(detDir))
            throw new PaddleOcrException($"v6 det model not found: {detDir}");
        if (!Directory.Exists(recDir))
            throw new PaddleOcrException($"v6 rec model not found: {recDir}");
        if (!File.Exists(dictPath))
            throw new PaddleOcrException($"v6 dict not found: {dictPath}");

        var detModel = new FileDetectionModel(detDir, ModelVersion.V5);
        var recModel = new FileRecognizationModel(recDir, dictPath, ModelVersion.V5);
        var fullModel = new FullOcrModel(detModel, recModel);

        Action<PaddleConfig> configure = mode == PpOcrV5InferenceMode.Fast
            ? ConfigureFastCpuDevice
            : ConfigureSafeCpuDevice;

        return new PaddleOcrAll(fullModel, configure)
        {
            AllowRotateDetection = true,
            Enable180Classification = false,
        };
    }

    // ===== Native runtime loading (shared pattern with PpOcrV5Client) =====

    private static string? ResolveNativeRuntimeDir()
    {
        var appNative = AppContext.BaseDirectory;
        if (PpOcrV6ModelResolver.ValidateNativeRuntimeDir(appNative))
            return appNative;

        var cacheNative = PpOcrV6ModelResolver.GetNativeRuntimeCachePath();
        if (PpOcrV6ModelResolver.ValidateNativeRuntimeDir(cacheNative))
            return cacheNative;

        return null;
    }

    private static string EnsureNativeRuntimeLoaded()
    {
        lock (NativeRuntimeSync)
        {
            if (_nativeRuntimeLoaded && _nativeRuntimeDir != null)
                return _nativeRuntimeDir;

            var nativeDir = ResolveNativeRuntimeDir();
            if (nativeDir == null)
                throw new PaddleOcrException($"Native OCR runtime not installed. Run 'nong ocr install-model pp-ocrv6-medium --json'. Cache: {PpOcrV6ModelResolver.GetNativeRuntimeCachePath()}");

            AddNativeDirectoryToPath(nativeDir);
            foreach (var file in PpOcrV6ModelResolver.GetNativeRuntimeLoadFiles())
                LoadNativeLibrary(nativeDir, file);

            _nativeRuntimeDir = nativeDir;
            _nativeRuntimeLoaded = true;
            return nativeDir;
        }
    }

    private static void ConfigureNativeLogEnvironment()
    {
        // Suppress GLOG, PaddlePIR, and OneDNN diagnostic output.
        // Must be called BEFORE any native library is loaded.
        Environment.SetEnvironmentVariable("GLOG_minloglevel", "3");
        Environment.SetEnvironmentVariable("GLOG_v", "0");
        Environment.SetEnvironmentVariable("FLAGS_minloglevel", "3");
        Environment.SetEnvironmentVariable("FLAGS_v", "0");
        Environment.SetEnvironmentVariable("FLAGS_logtostderr", "0");
        Environment.SetEnvironmentVariable("FLAGS_alsologtostderr", "0");
        Environment.SetEnvironmentVariable("FLAGS_stderrthreshold", "3");
        Environment.SetEnvironmentVariable("DNNL_VERBOSE", "0");
        Environment.SetEnvironmentVariable("ONEDNN_VERBOSE", "0");
        Environment.SetEnvironmentVariable("MKLDNN_VERBOSE", "0");
        Environment.SetEnvironmentVariable("PADDLE_DISABLE_SIGNAL_HANDLER", "1");
    }

    private static void ConfigureFastCpuDevice(PaddleConfig config)
    {
        var threadCount = Math.Max(1, Math.Min(Environment.ProcessorCount, 4));
        var configure = OperatingSystem.IsWindows()
            ? PaddleDevice.OneDnn(cacheCapacity: 10, cpuMathThreadCount: threadCount, memoryOptimized: true, glogEnabled: false)
            : PaddleDevice.Blas(cpuMathThreadCount: threadCount, memoryOptimized: true, glogEnabled: false);

        configure(config);
        config.GLogEnabled = false;
        PaddleConfig.GLogMinLevel = 3;
    }

    private static void ConfigureSafeCpuDevice(PaddleConfig config)
    {
        PaddleDevice.Blas(cpuMathThreadCount: 1, memoryOptimized: false, glogEnabled: false)(config);
        config.GLogEnabled = false;
        PaddleConfig.GLogMinLevel = 3;
    }

    private static void AddNativeDirectoryToPath(string nativeDir)
    {
        var current = Environment.GetEnvironmentVariable("PATH") ?? "";
        var parts = current.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Any(p => string.Equals(Path.GetFullPath(p).TrimEnd('\\', '/'),
                Path.GetFullPath(nativeDir).TrimEnd('\\', '/'),
                StringComparison.OrdinalIgnoreCase)))
            return;

        Environment.SetEnvironmentVariable("PATH", nativeDir + Path.PathSeparator + current);
    }

    private static void LoadNativeLibrary(string nativeDir, string fileName)
    {
        var path = Path.Combine(nativeDir, fileName);
        if (!File.Exists(path))
            throw new PaddleOcrException($"Native OCR dependency missing: {path}");

        try
        {
            NativeLibrary.Load(path);
        }
        catch (Exception ex)
        {
            throw new PaddleOcrException($"Cannot load native OCR dependency {fileName} from {nativeDir}: {ex.Message}", ex);
        }
    }

    // ===== Numeric helpers =====

    private static float[] ToAxisAlignedBbox(Point2f[] points)
    {
        if (points.Length == 0) return Array.Empty<float>();
        var minX = points.Min(p => p.X);
        var minY = points.Min(p => p.Y);
        var maxX = points.Max(p => p.X);
        var maxY = points.Max(p => p.Y);
        return new[] { minX, minY, maxX, maxY };
    }

    private static double? ToFiniteConfidence(double score) =>
        double.IsFinite(score) ? score : null;

    private static bool IsFinitePoint(Point2f point) =>
        float.IsFinite(point.X) && float.IsFinite(point.Y);
}
