using System.Runtime.InteropServices;

namespace MultiModalCore;

/// <summary>
/// PP-OCRv6 模型解析器。v6 没有 NuGet 内置模型包，
/// 模型文件从 PaddleOCR CDN 下载到本地缓存目录。
/// 字典文件从嵌入式资源提取。
/// </summary>
public static class PpOcrV6ModelResolver
{
    public const string DefaultSize = "medium";

    public static readonly IReadOnlyList<string> SupportedSizes = new[] { "medium", "small", "tiny" };

    public static readonly IReadOnlyList<string> AllModelIds = new[]
    {
        "pp-ocrv6",           // alias -> medium
        "pp-ocrv6-medium",
        "pp-ocrv6-small",
        "pp-ocrv6-tiny",
    };

    /// <summary>
    /// 将 model-id 解析为 (family, size)。
    /// pp-ocrv6 → ("pp-ocrv6", "medium")
    /// pp-ocrv6-medium → ("pp-ocrv6", "medium")
    /// pp-ocrv5-mobile → ("pp-ocrv5", "mobile")
    /// </summary>
    public static (string Family, string Size) ParseModelId(string modelId)
    {
        if (modelId == "pp-ocrv6")
            return ("pp-ocrv6", "medium");

        if (modelId == "pp-ocrv5-mobile")
            return ("pp-ocrv5", "mobile");

        if (modelId.StartsWith("pp-ocrv6-"))
            return ("pp-ocrv6", modelId["pp-ocrv6-".Length..]);

        throw new ArgumentException($"Unknown model ID: {modelId}. Supported: {string.Join(", ", AllModelIds)}");
    }

    public static bool IsV6ModelId(string modelId) =>
        modelId == "pp-ocrv6" || modelId.StartsWith("pp-ocrv6-");

    public static bool IsV5ModelId(string modelId) =>
        modelId == "pp-ocrv5-mobile";

    public static string CanonicalModelId(string modelId)
    {
        var (family, size) = ParseModelId(modelId);
        return family == "pp-ocrv5" ? "pp-ocrv5-mobile" : $"pp-ocrv6-{size}";
    }

    /// <summary>PaddleOCR 官方推理模型 CDN 根路径。</summary>
    public const string CdnBase = "https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0";

    public static string DetDownloadUrl(string size) =>
        $"{CdnBase}/PP-OCRv6_{size}_det_infer.tar";

    public static string RecDownloadUrl(string size) =>
        $"{CdnBase}/PP-OCRv6_{size}_rec_infer.tar";

    /// <summary>本地模型缓存目录。</summary>
    public static string GetModelCachePath(string size)
    {
        var baseDir = OperatingSystem.IsWindows()
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache");

        return Path.Combine(baseDir, "Angri450.Nong", "models", $"pp-ocrv6-{size}");
    }

    /// <summary>检测模型目录。</summary>
    public static string GetDetDir(string modelCachePath) =>
        Path.Combine(modelCachePath, "det");

    /// <summary>识别模型目录。</summary>
    public static string GetRecDir(string modelCachePath) =>
        Path.Combine(modelCachePath, "rec");

    /// <summary>字典文件路径。</summary>
    public static string GetDictPath(string modelCachePath) =>
        Path.Combine(modelCachePath, "dict.txt");

    /// <summary>根据 size 获取对应的字典资源名。</summary>
    public static string GetDictResourceName(string size) => size switch
    {
        "tiny" => "OcrModels.ppocrv6_tiny_dict.txt",
        _ => "OcrModels.ppocrv6_dict.txt",
    };

    /// <summary>从 OcrModels 包的嵌入式资源提取字典。</summary>
    public static void ExtractDict(string size, string destPath)
    {
        var resourceName = GetDictResourceName(size);
        // Resource lives in Angri450.Nong.OcrModels assembly
        var assembly = typeof(OcrModels.Placeholder).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
            throw new FileNotFoundException($"Embedded dict resource not found: {resourceName}");
        using var fs = File.Create(destPath);
        stream.CopyTo(fs);
    }

    /// <summary>验证模型缓存目录完整（det + rec + dict）。</summary>
    public static bool ValidateModelCache(string modelCachePath)
    {
        if (!Directory.Exists(modelCachePath)) return false;
        if (!Directory.Exists(GetDetDir(modelCachePath))) return false;
        if (!Directory.Exists(GetRecDir(modelCachePath))) return false;
        if (!File.Exists(GetDictPath(modelCachePath))) return false;
        return true;
    }

    /// <summary>检测 v6 模型是否已安装（任一大小的完整缓存存在）。</summary>
    public static (bool Available, string? Size, string? CachePath) DetectInstalled()
    {
        foreach (var size in SupportedSizes)
        {
            var cachePath = GetModelCachePath(size);
            if (ValidateModelCache(cachePath))
                return (true, size, cachePath);
        }
        return (false, null, null);
    }

    // ===== 平台适配（与 PpOcrV5ModelResolver 保持一致） =====

    public static string GetNativeRuntimeId()
    {
        var arch = RuntimeInformation.ProcessArchitecture;
        if (OperatingSystem.IsWindows() && arch == Architecture.X64) return "win-x64";
        if (OperatingSystem.IsLinux() && arch == Architecture.X64) return "linux-x64";
        if (OperatingSystem.IsLinux() && arch == Architecture.Arm64) return "linux-arm64";
        if (OperatingSystem.IsMacOS() && arch == Architecture.X64) return "osx-x64";
        if (OperatingSystem.IsMacOS() && arch == Architecture.Arm64) return "osx-arm64";

        var os = OperatingSystem.IsWindows() ? "win" :
            OperatingSystem.IsLinux() ? "linux" :
            OperatingSystem.IsMacOS() ? "osx" : "unknown";
        return $"{os}-{arch.ToString().ToLowerInvariant()}";
    }

    public static string GetNativeRuntimeCachePath()
    {
        var explicitPath = Environment.GetEnvironmentVariable("NONG_OCR_RUNTIME_DIR");
        if (!string.IsNullOrWhiteSpace(explicitPath))
            return Path.GetFullPath(explicitPath);

        var baseDir = OperatingSystem.IsWindows()
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache");

        return Path.Combine(baseDir, "Angri450.Nong", "runtimes", $"pp-ocrv6-{GetNativeRuntimeId()}");
    }

    public static bool ValidateNativeRuntimeDir(string dir)
    {
        if (!Directory.Exists(dir)) return false;
        return GetRequiredNativeRuntimeFiles().All(f => File.Exists(Path.Combine(dir, f)));
    }

    public static string[] GetRequiredNativeRuntimeFiles() => GetNativeRuntimeId() switch
    {
        "win-x64" => new[] { "common.dll", "libiomp5md.dll", "mkldnn.dll", "mklml.dll", "onnxruntime.dll", "onnxruntime_providers_shared.dll", "opencv_videoio_ffmpeg4110_64.dll", "OpenCvSharpExtern.dll", "paddle2onnx.dll", "paddle_inference_c.dll", "phi.dll" },
        "linux-x64" => new[] { "libcommon.so", "libonnxifi.so", "libonnxruntime.so", "libonnxruntime.so.1.11.1", "libOpenCvSharpExtern.so", "libpaddle2onnx.so", "libpaddle2onnx.so.1.0.0rc2", "libpaddle_inference_c.so", "libphi.so", "libphi_core.so" },
        "linux-arm64" => new[] { "libcommon.so", "libOpenCvSharpExtern.so", "libpaddle_inference_c.so", "libphi.so", "libphi_core.so" },
        "osx-x64" => new[] { "libcommon.dylib", "libonnxifi.dylib", "libonnxifi_dummy.dylib", "libonnxruntime.1.11.1.dylib", "libonnxruntime.dylib", "libOpenCvSharpExtern.dylib", "libpaddle2onnx.1.0.0rc2.dylib", "libpaddle2onnx.dylib", "libpaddle_inference_c.dylib", "libphi.dylib", "libphi_core.dylib" },
        "osx-arm64" => new[] { "libcommon.dylib", "libOpenCvSharpExtern.dylib", "libpaddle_inference_c.dylib", "libphi.dylib", "libphi_core.dylib" },
        _ => new[] { "__unsupported_ocr_runtime__" }
    };

    public static string[] GetNativeRuntimeLoadFiles() => GetNativeRuntimeId() switch
    {
        "win-x64" => new[] { "paddle_inference_c.dll", "mklml.dll", "mkldnn.dll", "OpenCvSharpExtern.dll" },
        "linux-x64" or "linux-arm64" => new[] { "libpaddle_inference_c.so", "libOpenCvSharpExtern.so" },
        "osx-x64" or "osx-arm64" => new[] { "libpaddle_inference_c.dylib", "libOpenCvSharpExtern.dylib" },
        _ => new[] { "__unsupported_ocr_runtime__" }
    };
}
