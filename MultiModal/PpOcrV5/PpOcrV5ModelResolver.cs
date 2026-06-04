using System.Runtime.InteropServices;

namespace MultiModalCore;

/// <summary>
/// PP-OCRv5 模型文件解析器。按优先级查找模型目录：
/// 1. --model-dir 显式参数（最高优先级）
/// 2. NuGet 模型资源包目录（程序集同级）
/// 3. 用户缓存目录 %LOCALAPPDATA%/Angri450.Nong/models/pp-ocrv5-mobile/
/// </summary>
public static class PpOcrV5ModelResolver
{
    /// <summary>按优先级解析模型目录路径，返回第一个包含 manifest.json 的有效目录。</summary>
    public static string? Resolve(string? modelDir = null)
    {
        // 1. 显式 --model-dir 参数
        if (modelDir != null && ValidateModelDir(modelDir))
            return modelDir;

        // 2. NuGet 资源包目录（程序集同级）
        var assemblyDir = AppContext.BaseDirectory;
        var resourcePath = Path.Combine(assemblyDir, "models", "pp-ocrv5-mobile");
        if (ValidateModelDir(resourcePath))
            return resourcePath;

        // 3. 用户缓存目录
        var cachePath = GetCachePath();
        if (ValidateModelDir(cachePath))
            return cachePath;

        return null;
    }

    /// <summary>获取平台对应的用户缓存路径。</summary>
    public static string GetCachePath()
    {
        var baseDir = OperatingSystem.IsWindows()
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache");

        return Path.Combine(baseDir, "Angri450.Nong", "models", "pp-ocrv5-mobile");
    }

    /// <summary>验证目录是否存在且包含 manifest.json。</summary>
    public static bool ValidateModelDir(string dir) =>
        Directory.Exists(dir) && File.Exists(Path.Combine(dir, "manifest.json"));
}
