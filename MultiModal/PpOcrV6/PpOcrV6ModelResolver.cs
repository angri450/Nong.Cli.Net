namespace MultiModalCore;

/// <summary>
/// PP-OCRv6 ONNX model resolver.
/// Models are downloaded from ModelScope as ONNX files into .nong/models/.
/// No PaddleInference native runtime needed — ONNX Runtime handles everything.
/// </summary>
public static class PpOcrV6ModelResolver
{
    public const string DefaultSize = "medium";

    public static readonly IReadOnlyList<string> SupportedSizes = new[] { "medium", "small", "tiny" };

    public static readonly IReadOnlyList<string> AllModelIds = new[]
    {
        "pp-ocrv6",
        "pp-ocrv6-medium",
        "pp-ocrv6-small",
        "pp-ocrv6-tiny",
    };

    public static (string Family, string Size) ParseModelId(string modelId)
    {
        if (modelId == "pp-ocrv6")
            return ("pp-ocrv6", "medium");
        if (modelId.StartsWith("pp-ocrv6-"))
            return ("pp-ocrv6", modelId["pp-ocrv6-".Length..]);
        throw new ArgumentException($"Unknown model ID: {modelId}. Supported: {string.Join(", ", AllModelIds)}");
    }

    public static bool IsV6ModelId(string modelId) =>
        modelId == "pp-ocrv6" || modelId.StartsWith("pp-ocrv6-");

    public static string CanonicalModelId(string modelId)
    {
        var (family, size) = ParseModelId(modelId);
        return $"pp-ocrv6-{size}";
    }

    /// <summary>ModelScope repos for ONNX models (PaddlePaddle official).</summary>
    public const string DetRepoBase = "https://www.modelscope.cn/PaddlePaddle/PP-OCRv6_{0}_det_onnx.git";
    public const string RecRepoBase = "https://www.modelscope.cn/PaddlePaddle/PP-OCRv6_{0}_rec_onnx.git";

    /// <summary>Local model cache directory under .nong/models/.</summary>
    public static string GetModelCachePath(string size, string? workplaceRoot = null)
    {
        var root = workplaceRoot
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nong");
        return Path.Combine(root, "models", $"pp-ocrv6-{size}");
    }

    public static string GetDetPath(string modelDir) => Path.Combine(modelDir, "det.onnx");
    public static string GetRecPath(string modelDir) => Path.Combine(modelDir, "rec.onnx");
    public static string GetDictPath(string modelDir) => Path.Combine(modelDir, "dict.txt");

    public static bool ValidateModelCache(string modelDir)
    {
        if (!Directory.Exists(modelDir)) return false;
        if (!File.Exists(GetDetPath(modelDir))) return false;
        if (!File.Exists(GetRecPath(modelDir))) return false;
        if (!File.Exists(GetDictPath(modelDir))) return false;
        return true;
    }

    /// <summary>Detect installed ONNX model (any size).</summary>
    public static (bool Available, string? Size, string? Path) DetectInstalled(string? workplaceRoot = null)
    {
        foreach (var size in SupportedSizes)
        {
            var dir = GetModelCachePath(size, workplaceRoot);
            if (ValidateModelCache(dir))
                return (true, size, dir);
        }
        return (false, null, null);
    }

    /// <summary>Extract dictionary from OcrModels embedded resource.</summary>
    public static void ExtractDict(string size, string destPath)
    {
        var resourceName = size switch
        {
            "tiny" => "OcrModels.ppocrv6_tiny_dict.txt",
            _ => "OcrModels.ppocrv6_dict.txt",
        };
        var assembly = typeof(OcrModels.Placeholder).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Embedded dict not found: {resourceName}");
        using var fs = File.Create(destPath);
        stream.CopyTo(fs);
    }
}
