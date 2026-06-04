using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MultiModalCore;

/// <summary>
/// PaddleOCR-VL-1.6 云端 API 客户端。提交图片/PDF，轮询结果，下载 Markdown + 图片。
/// </summary>
public class PaddleOcrVlClient : IDisposable
{
    const string DefaultBaseUrl = "https://paddleocr.aistudio-app.com/api/v2/ocr";
    static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(5);

    readonly HttpClient _http;
    readonly HttpClient _downloadHttp; // 不带认证头，用于下载 BOS/CDN 资源
    readonly string _token;
    readonly string _baseUrl;

    public PaddleOcrVlClient(string? token = null, string? baseUrl = null, HttpClient? http = null)
    {
        _token = token
            ?? Environment.GetEnvironmentVariable("PADDLEOCR_ACCESS_TOKEN")
            ?? Environment.GetEnvironmentVariable("PADDLEOCR_TOKEN")
            ?? throw new ArgumentNullException(nameof(token),
                "Token 未提供且环境变量 PADDLEOCR_ACCESS_TOKEN / PADDLEOCR_TOKEN 均未设置");
        _baseUrl = baseUrl ?? DefaultBaseUrl;
        _http = http ?? new HttpClient();
        _downloadHttp = new HttpClient(); // 不加认证头，BOS/CDN 不需要
        _http.DefaultRequestHeaders.Add("Authorization", $"bearer {_token}");
    }

    // === 一键处理 ===

    /// <summary>处理本地文件或 URL，等待结果，保存 Markdown + 图片到 outputDir。</summary>
    public async Task<List<string>> ProcessAsync(
        string input,
        string outputDir,
        OcrOptions? options = null,
        TimeSpan? pollInterval = null,
        CancellationToken cancel = default)
    {
        var kind = ClassifyInput(input);
        var jobId = kind switch
        {
            InputKind.Url => await SubmitUrlAsync(input, options, cancel),
            InputKind.LocalFile => await SubmitFileAsync(input, options, cancel),
            _ => throw new PaddleOcrException($"无法识别输入类型: {input}")
        };
        var result = await WaitForJobAsync(jobId, pollInterval ?? DefaultPollInterval, cancel);
        return await DownloadResultsAsync(result, outputDir, cancel);
    }

    /// <summary>处理内存中的文件数据。</summary>
    public async Task<List<string>> ProcessAsync(
        byte[] fileBytes,
        string fileName,
        string outputDir,
        OcrOptions? options = null,
        TimeSpan? pollInterval = null,
        CancellationToken cancel = default)
    {
        var jobId = await SubmitBytesAsync(fileBytes, fileName, options, cancel);
        var result = await WaitForJobAsync(jobId, pollInterval ?? DefaultPollInterval, cancel);
        return await DownloadResultsAsync(result, outputDir, cancel);
    }

    // === 分步 API ===

    public async Task<string> SubmitFileAsync(string filePath, OcrOptions? options = null, CancellationToken cancel = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"文件不存在: {filePath}");

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("PaddleOCR-VL-1.6"), "model");
        content.Add(new StringContent(JsonSerializer.Serialize(options ?? new OcrOptions())), "optionalPayload");
        using var fileStream = File.OpenRead(filePath);
        content.Add(new StreamContent(fileStream), "file", Path.GetFileName(filePath));

        using var response = await _http.PostAsync($"{_baseUrl}/jobs", content, cancel);
        await EnsureSuccess(response, "提交文件失败");
        var envelope = await response.Content.ReadFromJsonAsync(ApiContext.Default.ApiEnvelopeJobSubmitResponse, cancel);
        return envelope!.Data.JobId;
    }

    public async Task<string> SubmitBytesAsync(byte[] fileBytes, string fileName, OcrOptions? options = null, CancellationToken cancel = default)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("PaddleOCR-VL-1.6"), "model");
        content.Add(new StringContent(JsonSerializer.Serialize(options ?? new OcrOptions())), "optionalPayload");
        content.Add(new ByteArrayContent(fileBytes), "file", fileName);

        using var response = await _http.PostAsync($"{_baseUrl}/jobs", content, cancel);
        await EnsureSuccess(response, "提交文件失败");
        var envelope = await response.Content.ReadFromJsonAsync(ApiContext.Default.ApiEnvelopeJobSubmitResponse, cancel);
        return envelope!.Data.JobId;
    }

    public async Task<string> SubmitUrlAsync(string url, OcrOptions? options = null, CancellationToken cancel = default)
    {
        var payload = new
        {
            fileUrl = url,
            model = "PaddleOCR-VL-1.6",
            optionalPayload = options ?? new OcrOptions()
        };
        using var requestContent = new StringContent(
            JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _http.PostAsync($"{_baseUrl}/jobs", requestContent, cancel);
        await EnsureSuccess(response, "提交 URL 失败");
        var envelope = await response.Content.ReadFromJsonAsync(ApiContext.Default.ApiEnvelopeJobSubmitResponse, cancel);
        return envelope!.Data.JobId;
    }

    /// <summary>轮询等待任务完成，返回结果下载 URL。</summary>
    public async Task<ResultUrl> WaitForJobAsync(
        string jobId,
        TimeSpan pollInterval,
        CancellationToken cancel = default)
    {
        while (true)
        {
            cancel.ThrowIfCancellationRequested();

            using var response = await _http.GetAsync($"{_baseUrl}/jobs/{jobId}", cancel);
            await EnsureSuccess(response, "查询任务状态失败");
            var envelope = await response.Content.ReadFromJsonAsync(ApiContext.Default.ApiEnvelopeJobStatusResponse, cancel);
            var status = envelope!.Data;

            switch (status.State)
            {
                case "done":
                    return status.ResultUrl!;
                case "failed":
                    throw new PaddleOcrException($"任务失败: {status.ErrorMsg}");
                case "pending":
                    Debug.WriteLine($"Job {jobId}: pending...");
                    break;
                case "running":
                    var progress = status.ExtractProgress;
                    if (progress != null)
                        Debug.WriteLine($"Job {jobId}: {progress.ExtractedPages}/{progress.TotalPages} pages");
                    else
                        Debug.WriteLine($"Job {jobId}: running...");
                    break;
            }
            await Task.Delay(pollInterval, cancel);
        }
    }

    /// <summary>下载 JSONL 结果，保存 Markdown + 图片，返回 Markdown 文件路径列表。</summary>
    public async Task<List<string>> DownloadResultsAsync(
        ResultUrl resultUrl,
        string outputDir,
        CancellationToken cancel = default)
    {
        var ocrResult = await DownloadResultsStructuredAsync(resultUrl, outputDir, cancel);
        return ocrResult.Pages.Select(p =>
        {
            var mdPath = Path.Combine(outputDir, $"doc_{p.PageNumber}.md");
            File.WriteAllText(mdPath, p.Markdown, Encoding.UTF8);
            return mdPath;
        }).ToList();
    }

    /// <summary>下载 JSONL 结果，返回完整结构化数据 + 保存 Markdown/图片。</summary>
    public async Task<OcrResult> DownloadResultsStructuredAsync(
        ResultUrl resultUrl,
        string outputDir,
        CancellationToken cancel = default)
    {
        Directory.CreateDirectory(outputDir);

        using var jsonlResponse = await _downloadHttp.GetAsync(resultUrl.JsonUrl, cancel);
        await EnsureSuccess(jsonlResponse, "下载结果失败");
        var jsonlText = await jsonlResponse.Content.ReadAsStringAsync(cancel);

        var pages = new List<OcrPage>();
        var lines = jsonlText.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        for (int pageNum = 0; pageNum < lines.Length; pageNum++)
        {
            var jsonLine = JsonSerializer.Deserialize(lines[pageNum], ApiContext.Default.JsonlLine);
            if (jsonLine?.Result.LayoutParsingResults == null) continue;

            foreach (var layout in jsonLine.Result.LayoutParsingResults)
            {
                var pruned = layout.PrunedResult;
                var blocks = pruned?.ParsingResList ?? [];

                // 保存 Markdown
                var mdPath = Path.Combine(outputDir, $"doc_{pageNum}.md");
                await File.WriteAllTextAsync(mdPath, layout.Markdown.Text, Encoding.UTF8, cancel);

                // 下载内嵌图片
                if (layout.Markdown.Images != null)
                {
                    foreach (var (imgPath, imgUrl) in layout.Markdown.Images)
                    {
                        var fullImgPath = Path.Combine(outputDir, imgPath);
                        Directory.CreateDirectory(Path.GetDirectoryName(fullImgPath)!);
                        try
                        {
                            var imgBytes = await _downloadHttp.GetByteArrayAsync(imgUrl, cancel);
                            await File.WriteAllBytesAsync(fullImgPath, imgBytes, cancel);
                        }
                        catch { /* 图片下载失败不阻塞主流程 */ }
                    }
                }

                // 下载输出图片
                if (layout.OutputImages != null)
                {
                    foreach (var (imgName, imgUrl) in layout.OutputImages)
                    {
                        try
                        {
                            var imgResp = await _downloadHttp.GetAsync(imgUrl, cancel);
                            if (imgResp.IsSuccessStatusCode)
                            {
                                var filename = Path.Combine(outputDir, $"{imgName}_{pageNum}.jpg");
                                var imgBytes = await imgResp.Content.ReadAsByteArrayAsync(cancel);
                                await File.WriteAllBytesAsync(filename, imgBytes, cancel);
                            }
                        }
                        catch { }
                    }
                }

                pages.Add(new OcrPage
                {
                    PageNumber = pageNum,
                    Width = pruned?.Width ?? 0,
                    Height = pruned?.Height ?? 0,
                    Blocks = blocks,
                    Markdown = layout.Markdown.Text,
                    MarkdownImages = layout.Markdown.Images,
                });
            }
        }

        return new OcrResult { Pages = pages };
    }

    /// <summary>一键处理并直接产出 Word 文档。</summary>
    public async Task<string> ProcessToWordAsync(
        string input,
        string outputDocxPath,
        OcrOptions? options = null,
        CancellationToken cancel = default)
    {
        var kind = ClassifyInput(input);
        var jobId = kind switch
        {
            InputKind.Url => await SubmitUrlAsync(input, options, cancel),
            InputKind.LocalFile => await SubmitFileAsync(input, options, cancel),
            _ => throw new PaddleOcrException($"无法识别输入类型: {input}")
        };
        var resultUrl = await WaitForJobAsync(jobId, DefaultPollInterval, cancel);
        var tempDir = Path.Combine(Path.GetTempPath(), $"ocr_{Guid.NewGuid():N}");
        var ocrResult = await DownloadResultsStructuredAsync(resultUrl, tempDir, cancel);

        LayoutToWordConverter.Convert(ocrResult, outputDocxPath);

        // 清理临时目录
        try { Directory.Delete(tempDir, true); } catch { }
        return outputDocxPath;
    }

    // === 辅助 ===

    static InputKind ClassifyInput(string input)
    {
        if (input.StartsWith("http://") || input.StartsWith("https://"))
            return InputKind.Url;
        if (File.Exists(input))
            return InputKind.LocalFile;
        throw new FileNotFoundException($"输入不是有效 URL 也不是存在的文件: {input}");
    }

    static async Task EnsureSuccess(HttpResponseMessage response, string context)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new PaddleOcrException($"{context} (HTTP {(int)response.StatusCode}): {body}");
        }
    }

    public void Dispose()
    {
        _http.Dispose();
        _downloadHttp.Dispose();
    }
}

/// <summary>System.Text.Json 源生成上下文，避免 IL 裁剪问题。</summary>
[JsonSerializable(typeof(ApiEnvelope<JobSubmitResponse>))]
[JsonSerializable(typeof(ApiEnvelope<JobStatusResponse>))]
[JsonSerializable(typeof(JsonlLine))]
internal partial class ApiContext : JsonSerializerContext { }
