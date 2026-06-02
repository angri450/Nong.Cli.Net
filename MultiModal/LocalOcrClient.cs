using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace MultiModalCore;

/// <summary>本地 PaddleOCR 客户端。通过 Python 子进程调用，输出 JSON。</summary>
public class LocalOcrClient
{
    const string ScriptResourceName = "MultiModalCore.scripts.ocr_local.py";
    static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(3);

    readonly string _pythonExe;
    readonly string _scriptPath;
    readonly string _lang;
    readonly bool _useGpu;
    readonly TimeSpan _timeout;

    /// <param name="pythonExe">Python 可执行文件路径，默认 "python"</param>
    /// <param name="scriptPath">ocr_local.py 路径，默认在包目录下自动查找</param>
    /// <param name="lang">OCR 语言，默认 "ch"（中英文混合）</param>
    /// <param name="useGpu">是否使用 GPU，默认 false</param>
    /// <param name="timeout">单次 OCR 超时时间</param>
    public LocalOcrClient(
        string pythonExe = "python",
        string? scriptPath = null,
        string lang = "ch",
        bool useGpu = false,
        TimeSpan? timeout = null)
    {
        _pythonExe = pythonExe;
        _scriptPath = scriptPath ?? ResolveScriptPath();
        _lang = lang;
        _useGpu = useGpu;
        _timeout = timeout ?? DefaultTimeout;
    }

    /// <summary>对单张图片做 OCR，返回检测到的文本块列表。</summary>
    public async Task<List<LocalOcrBlock>> RecognizeAsync(
        string imagePath,
        CancellationToken cancel = default)
    {
        if (!File.Exists(imagePath))
            throw new FileNotFoundException($"图片文件不存在: {imagePath}");

        var args = BuildArgs(imagePath);
        var result = await RunPythonAsync(args, cancel);
        return ParseResult(result);
    }

    /// <summary>对图片字节数据做 OCR。</summary>
    public async Task<List<LocalOcrBlock>> RecognizeAsync(
        byte[] imageBytes,
        string tempFileExtension = ".png",
        CancellationToken cancel = default)
    {
        var tmpPath = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(tmpPath, imageBytes, cancel);
            return await RecognizeAsync(tmpPath, cancel);
        }
        finally
        {
            try { File.Delete(tmpPath); } catch { }
        }
    }

    /// <summary>批量识别多张图片。</summary>
    public async Task<List<List<LocalOcrBlock>>> RecognizeBatchAsync(
        IEnumerable<string> imagePaths,
        CancellationToken cancel = default)
    {
        var results = new List<List<LocalOcrBlock>>();
        foreach (var path in imagePaths)
        {
            results.Add(await RecognizeAsync(path, cancel));
        }
        return results;
    }

    /// <summary>检查 Python + PaddleOCR 环境是否可用。</summary>
    public async Task<(bool Available, string Message)> CheckEnvironmentAsync()
    {
        try
        {
            var checkScript = "import paddleocr; print('OK')";
            var psi = new ProcessStartInfo
            {
                FileName = _pythonExe,
                Arguments = $"-c \"{checkScript}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi);
            if (proc == null) return (false, "无法启动 Python 进程");
            var stdout = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();
            return proc.ExitCode == 0 && stdout.Contains("OK")
                ? (true, "PaddleOCR 环境就绪")
                : (false, "PaddleOCR 未安装，请运行: pip install paddlepaddle paddleocr");
        }
        catch (Exception ex)
        {
            return (false, $"Python 环境检查失败: {ex.Message}");
        }
    }

    string BuildArgs(string imagePath)
    {
        var sb = new StringBuilder();
        sb.Append($"\"{_scriptPath}\"");
        sb.Append($" --image \"{imagePath}\"");
        sb.Append($" --lang {_lang}");
        if (_useGpu) sb.Append(" --gpu");
        return sb.ToString();
    }

    async Task<string> RunPythonAsync(string args, CancellationToken cancel)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _pythonExe,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
        };

        using var proc = Process.Start(psi)
            ?? throw new PaddleOcrException("无法启动 Python OCR 进程");

        var stdoutTask = proc.StandardOutput.ReadToEndAsync();
        var stderrTask = proc.StandardError.ReadToEndAsync();

        if (!proc.WaitForExit((int)_timeout.TotalMilliseconds))
        {
            try { proc.Kill(); } catch { }
            throw new PaddleOcrException($"本地 OCR 超时 ({_timeout.TotalSeconds}s)");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (proc.ExitCode != 0)
            throw new PaddleOcrException($"本地 OCR 失败 (exit code {proc.ExitCode}): {stderr}");

        return stdout;
    }

    static List<LocalOcrBlock> ParseResult(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            // PaddleOCR 输出格式: [[{bbox, text, confidence}, ...]]
            var raw = JsonSerializer.Deserialize<List<List<LocalOcrBlock?>>>(json);
            if (raw == null) return [];

            var blocks = new List<LocalOcrBlock>();
            foreach (var page in raw)
            {
                if (page == null) continue;
                foreach (var block in page)
                {
                    if (block != null && block.Bbox.Length == 4)
                        blocks.Add(block);
                }
            }
            return blocks;
        }
        catch (JsonException ex)
        {
            throw new PaddleOcrException($"解析 OCR 结果失败: {ex.Message}");
        }
    }

    static string ResolveScriptPath()
    {
        // 优先查找包内 scripts 目录
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "scripts", "ocr_local.py"),
            Path.Combine(AppContext.BaseDirectory, "ocr_local.py"),
            Path.Combine(Environment.CurrentDirectory, "scripts", "ocr_local.py"),
        };
        foreach (var c in candidates)
        {
            if (File.Exists(c)) return c;
        }
        return candidates[0]; // 返回默认路径，运行时报错比静默失败好
    }
}
