using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Angri450.Nong.Metaso;

/// <summary>
/// Metaso AI Search API client.
/// Auth: API key from https://metaso.cn/ set via NONG_LIT_METASO_KEY or METASO_API_KEY env var.
/// Base URL: https://metaso.cn
/// </summary>
public sealed class MetasoClient
{
    const string BaseUrl = "https://metaso.cn";
    readonly HttpClient _client;
    readonly Func<string, string?> _getEnv;

    public MetasoClient() : this(new HttpClient { Timeout = TimeSpan.FromSeconds(120) }) { }

    public MetasoClient(HttpClient client, Func<string, string?>? getEnv = null)
    {
        _client = client;
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("Nong-Metaso/12.1");
        _getEnv = getEnv ?? Environment.GetEnvironmentVariable;
    }

    string? Key => _getEnv("NONG_LIT_METASO_KEY") ?? _getEnv("METASO_API_KEY");

    // ════════════════════════════════════════════════════
    // 1. 搜索 (Search) — 纯搜索，无 AI 对话
    // ════════════════════════════════════════════════════

    /// <summary>
    /// Search across web/academic/images/video/podcast.
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="scope">webpage | document | scholar | image | video | podcast</param>
    /// <param name="size">Result count (max 50)</param>
    /// <param name="includeSummary">Include AI-generated summary of results</param>
    /// <param name="conciseSnippet">Return concise snippet text (shorter, faster)</param>
    public async Task<MetasoSearchResult> SearchAsync(
        string query,
        string scope = "scholar",
        int size = 10,
        bool includeSummary = false,
        bool conciseSnippet = true,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(Key))
            return MetasoSearchResult.Fail("missing_key", "Set NONG_LIT_METASO_KEY env var (https://metaso.cn).");

        try
        {
            var body = new Dictionary<string, object>
            {
                ["q"] = query,
                ["scope"] = scope,
                ["size"] = size
            };
            if (includeSummary) body["includeSummary"] = true;
            if (conciseSnippet) body["conciseSnippet"] = true;

            var json = JsonSerializer.Serialize(body);
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/v1/search")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {Key}");
            req.Headers.TryAddWithoutValidation("Accept", "application/json");
            req.Headers.TryAddWithoutValidation("Content-Type", "application/json");

            using var resp = await _client.SendAsync(req, ct).ConfigureAwait(false);
            var text = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;

            var items = new List<MetasoSearchItem>();
            // scholar scope → "scholars" key
            if (root.TryGetProperty("scholars", out var scholars) && scholars.ValueKind == JsonValueKind.Array)
                foreach (var item in scholars.EnumerateArray())
                    items.Add(MapSearchItem(item));
            // webpage/document scope → "items" or "results" key
            if (root.TryGetProperty("items", out var webItems) && webItems.ValueKind == JsonValueKind.Array)
                foreach (var item in webItems.EnumerateArray())
                    items.Add(MapSearchItem(item));
            // image/video/podcast → also "items"
            if (root.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
                foreach (var item in results.EnumerateArray())
                    items.Add(MapSearchItem(item));

            return new MetasoSearchResult
            {
                Success = true,
                Items = items,
                Total = GetInt(root, "total") ?? items.Count,
                Credits = GetInt(root, "credits"),
                Summary = GetString(root, "summary") ?? GetString(root, "answer")
            };
        }
        catch (Exception ex) { return MetasoSearchResult.Fail("error", ex.Message); }
    }

    // ════════════════════════════════════════════════════
    // 2. 网页读取 (Reader) — 抓取网页内容
    // ════════════════════════════════════════════════════

    /// <summary>
    /// Fetch web page content.
    /// </summary>
    /// <param name="url">URL to fetch</param>
    /// <param name="format">"json" or "markdown" — controls Accept header. json → structured, markdown → clean MD text.</param>
    public async Task<MetasoReaderResult> ReadAsync(
        string url,
        string format = "json",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(Key))
            return MetasoReaderResult.Fail("missing_key", "Set NONG_LIT_METASO_KEY env var.");

        try
        {
            var body = JsonSerializer.Serialize(new { url });
            var acceptHeader = format.Equals("markdown", StringComparison.OrdinalIgnoreCase)
                ? "text/plain"
                : "application/json";

            using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/v1/reader")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {Key}");
            req.Headers.TryAddWithoutValidation("Accept", acceptHeader);
            req.Headers.TryAddWithoutValidation("Content-Type", "application/json");

            using var resp = await _client.SendAsync(req, ct).ConfigureAwait(false);
            var text = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            string? title = null, content = text;
            if (acceptHeader == "application/json")
            {
                try
                {
                    using var doc = JsonDocument.Parse(text);
                    var root = doc.RootElement;
                    title = GetString(root, "title");
                    content = GetString(root, "content") ?? GetString(root, "text") ?? text;
                }
                catch { /* not valid JSON, return raw text */ }
            }

            return new MetasoReaderResult
            {
                Success = true,
                Title = title,
                Content = content ?? text,
                Format = format,
                RawLength = text.Length
            };
        }
        catch (Exception ex) { return MetasoReaderResult.Fail("error", ex.Message); }
    }

    // ════════════════════════════════════════════════════
    // 3. RAG 对话 (Chat) — 搜索 + AI 回答
    // ════════════════════════════════════════════════════

    /// <summary>
    /// AI-powered research chat with search-backed answers.
    /// </summary>
    /// <param name="question">Question to research</param>
    /// <param name="model">fast | fast_thinking | ds-r1 (append -scholar for academic scope)</param>
    /// <param name="scope">scholar | webpage — search scope</param>
    /// <param name="stream">Enable SSE streaming output</param>
    /// <param name="conciseSnippet">Return concise original-text matches</param>
    /// <param name="onChunk">Called for each SSE chunk when streaming. Pass null to collect full answer.</param>
    public async Task<MetasoChatResult> ChatAsync(
        string question,
        string model = "fast_thinking",
        string scope = "scholar",
        bool stream = false,
        bool conciseSnippet = true,
        Func<string, Task>? onChunk = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(Key))
            return MetasoChatResult.Fail("missing_key", "Set NONG_LIT_METASO_KEY env var.");

        try
        {
            var body = new Dictionary<string, object>
            {
                ["model"] = model,
                ["scope"] = scope,
                ["stream"] = stream,
                ["messages"] = new[] { new { role = "user", content = question } }
            };
            if (conciseSnippet) body["conciseSnippet"] = true;

            var json = JsonSerializer.Serialize(body);
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/v1/chat/completions")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {Key}");
            req.Headers.TryAddWithoutValidation("Accept", "application/json");
            req.Headers.TryAddWithoutValidation("Content-Type", "application/json");

            if (stream && onChunk != null)
            {
                // SSE streaming mode: parse data: lines
                using var resp = await _client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                resp.EnsureSuccessStatusCode();

                using var responseStream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                using var reader = new StreamReader(responseStream, Encoding.UTF8);

                var fullAnswer = new StringBuilder();
                string? modelUsed = null, id = null;

                while (!reader.EndOfStream)
                {
                    var line = (await reader.ReadLineAsync(ct).ConfigureAwait(false))?.Trim();
                    if (string.IsNullOrEmpty(line)) continue;
                    if (!line.StartsWith("data:")) continue;

                    var data = line[5..].Trim();
                    if (data == "[DONE]") break;

                    try
                    {
                        using var chunk = JsonDocument.Parse(data);
                        var root = chunk.RootElement;
                        modelUsed ??= GetString(root, "model");
                        id ??= GetString(root, "id");

                        if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var c in choices.EnumerateArray())
                            {
                                var delta = c.TryGetProperty("delta", out var d) ? GetString(d, "content")
                                    : c.TryGetProperty("message", out var m) ? GetString(m, "content") : null;
                                if (delta != null)
                                {
                                    fullAnswer.Append(delta);
                                    await onChunk(delta).ConfigureAwait(false);
                                }
                            }
                        }
                    }
                    catch { /* skip non-JSON SSE data */ }
                }

                return new MetasoChatResult
                {
                    Success = true,
                    Answer = fullAnswer.ToString(),
                    Model = modelUsed ?? model,
                    Id = id,
                    Streamed = true
                };
            }
            else
            {
                // Non-streaming mode: single response
                using var resp = await _client.SendAsync(req, ct).ConfigureAwait(false);
                var text = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;

                var answer = "";
                if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array)
                {
                    foreach (var choice in choices.EnumerateArray())
                    {
                        if (choice.TryGetProperty("message", out var msg) &&
                            msg.TryGetProperty("content", out var content))
                            answer = content.GetString() ?? "";
                    }
                }

                return new MetasoChatResult
                {
                    Success = true,
                    Answer = answer,
                    Model = GetString(root, "model") ?? model,
                    Id = GetString(root, "id")
                };
            }
        }
        catch (Exception ex) { return MetasoChatResult.Fail("error", ex.Message); }
    }

    // ════════════════════════════════════════════════════
    // JSON helpers
    // ════════════════════════════════════════════════════

    static MetasoSearchItem MapSearchItem(JsonElement e)
    {
        var item = new MetasoSearchItem
        {
            Title = GetString(e, "title") ?? "",
            Link = GetString(e, "link") ?? GetString(e, "url") ?? "",
            Snippet = GetString(e, "snippet") ?? GetString(e, "description") ?? GetString(e, "content") ?? ""
        };

        // thumbnail for image/video scopes
        item.Thumbnail = GetString(e, "thumbnail") ?? GetString(e, "image") ?? GetString(e, "cover");

        // date
        item.Date = GetString(e, "date") ?? GetString(e, "pub_date") ?? GetString(e, "published") ?? "";
        if (item.Date.Length >= 4 && int.TryParse(item.Date[..4], out var y))
            item.Year = y;

        // authors
        if (e.TryGetProperty("authors", out var authors) && authors.ValueKind == JsonValueKind.Array)
            item.Authors = authors.EnumerateArray()
                .Select(a => a.ValueKind == JsonValueKind.String ? a.GetString() : GetString(a, "name"))
                .Where(a => a != null).Select(a => a!).ToList();

        // keywords
        if (e.TryGetProperty("keywords", out var keywords) && keywords.ValueKind == JsonValueKind.Array)
            item.Keywords = keywords.EnumerateArray()
                .Select(k => k.GetString()).Where(k => k != null).Select(k => k!).ToList();

        // source
        item.Source = GetString(e, "source") ?? GetString(e, "domain") ?? "";

        return item;
    }

    static string? GetString(JsonElement e, string p) =>
        e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    static int? GetInt(JsonElement e, string p) =>
        e.TryGetProperty(p, out var v) && v.ValueKind != JsonValueKind.Null && v.TryGetInt32(out var n) ? n : null;
}

// ════════════════════════════════════════════════════
// Models
// ════════════════════════════════════════════════════

public sealed class MetasoSearchResult
{
    public bool Success { get; init; }
    public IReadOnlyList<MetasoSearchItem> Items { get; init; } = Array.Empty<MetasoSearchItem>();
    public int Total { get; init; }
    public int? Credits { get; init; }
    public string? Summary { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static MetasoSearchResult Fail(string code, string msg) => new()
    { Success = false, ErrorCode = code, ErrorMessage = msg };
}

public sealed class MetasoSearchItem
{
    public string Title { get; set; } = "";
    public string Link { get; set; } = "";
    public string Snippet { get; set; } = "";
    public string Date { get; set; } = "";
    public int? Year { get; set; }
    public List<string> Authors { get; set; } = new();
    public List<string> Keywords { get; set; } = new();
    public string? Thumbnail { get; set; }
    public string Source { get; set; } = "";
    public string Display => $"{Title} ({Year})";
}

public sealed class MetasoReaderResult
{
    public bool Success { get; init; }
    public string? Title { get; init; }
    public string? Content { get; init; }
    public string Format { get; init; } = "json";
    public int RawLength { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static MetasoReaderResult Fail(string code, string msg) => new()
    { Success = false, ErrorCode = code, ErrorMessage = msg };
}

public sealed class MetasoChatResult
{
    public bool Success { get; init; }
    public string Answer { get; init; } = "";
    public string? Model { get; init; }
    public string? Id { get; init; }
    public bool Streamed { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static MetasoChatResult Fail(string code, string msg) => new()
    { Success = false, ErrorCode = code, ErrorMessage = msg };
}
