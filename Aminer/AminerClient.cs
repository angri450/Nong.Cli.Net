using System.Net;
using System.Text;
using System.Text.Json;

namespace Angri450.Nong.Aminer;

/// <summary>
/// AMiner REST API — real API specs from open.aminer.cn docs (2025-2026).
/// BASE: https://datacenter.aminer.cn/gateway/open_platform
/// AUTH: JWT via NONG_LIT_AMINER_KEY / AMINER_API_KEY (no Bearer prefix)
/// </summary>
public sealed class AminerClient
{
    const string B = "https://datacenter.aminer.cn/gateway/open_platform";
    readonly HttpClient _c;
    readonly Func<string, string?> _env;
    readonly string? _cachedToken;
    static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);
    const int MaxRetries = 2;

    public AminerClient(HttpClient? c = null, Func<string, string?>? env = null)
    {
        _c = c ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _c.DefaultRequestHeaders.UserAgent.ParseAdd("Nong-Aminer/12.1");
        _env = env ?? Environment.GetEnvironmentVariable;
        _cachedToken = _env("NONG_LIT_AMINER_KEY") ?? _env("AMINER_API_KEY");
    }
    string? Tk => _cachedToken;

    // ══════════════════════════════════════════════════════
    // FREE — 9 endpoints
    // ══════════════════════════════════════════════════════

    /// <summary>学者搜索 [免费] POST /api/person/search  params: name, org, org_id, offset, size</summary>
    public Task<AminerResult<AminerScholar>> SearchScholarsAsync(
        string? name = null, string? org = null, string[]? orgIds = null,
        int offset = 0, int size = 10, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object> { ["offset"] = offset, ["size"] = Math.Clamp(size, 1, 10) };
        if (!string.IsNullOrWhiteSpace(name)) body["name"] = name;
        if (!string.IsNullOrWhiteSpace(org)) body["org"] = org;
        if (orgIds?.Length > 0) body["org_id"] = orgIds;
        return PostAsync("/api/person/search", body, MapScholar, ct);
    }

    /// <summary>论文搜索 [免费] GET /api/paper/search  params: title, page(从1开始), size(max20)</summary>
    public Task<AminerResult<AminerPaper>> SearchPapersAsync(
        string title, int page = 1, int size = 10, CancellationToken ct = default)
        => GetAsync($"/api/paper/search?title={Esc(title)}&page={Math.Max(1, page)}&size={Math.Clamp(size, 1, 20)}", MapPaperBasic, ct);

    /// <summary>论文推荐 [免费] POST /api/paper/rec5  params: author_name|topics|aminer_author_id(三选一), size(max1000)</summary>
    public Task<AminerResult<AminerRecPaper>> RecommendPapersAsync(
        string? authorName = null, string? authorOrg = null, string[]? topics = null,
        string? authorId = null, int? startYear = null, int? endYear = null,
        string? languageSort = null, int size = 5, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object> { ["size"] = Math.Clamp(size, 1, 20) };
        if (!string.IsNullOrWhiteSpace(authorName)) body["author_name"] = authorName;
        if (!string.IsNullOrWhiteSpace(authorOrg)) body["author_org"] = authorOrg;
        if (!string.IsNullOrWhiteSpace(authorId)) body["aminer_author_id"] = authorId;
        if (topics?.Length > 0) body["topics"] = topics;
        if (startYear.HasValue) body["start_year"] = startYear.Value;
        if (endYear.HasValue) body["end_year"] = endYear.Value;
        if (!string.IsNullOrWhiteSpace(languageSort)) body["language_sort"] = languageSort;
        return PostAsync("/api/paper/rec5", body, MapRecPaper, ct);
    }

    /// <summary>专利搜索 [免费] POST /api/patent/search  params: query, page, size</summary>
    public Task<AminerResult<AminerPatent>> SearchPatentsAsync(
        string query, int page = 0, int size = 20, CancellationToken ct = default)
        => PostAsync("/api/patent/search", new { query, page, size }, MapPatentBasic, ct);

    /// <summary>机构搜索 [免费] POST /api/organization/search  params: orgs[]</summary>
    public Task<AminerResult<AminerOrg>> SearchOrgsAsync(
        string[] orgs, CancellationToken ct = default)
        => PostAsync("/api/organization/search", new { orgs }, MapOrgBasic, ct);

    /// <summary>期刊搜索 [免费] POST /api/venue/search  params: name</summary>
    public Task<AminerResult<AminerVenue>> SearchVenuesAsync(
        string name, CancellationToken ct = default)
        => PostAsync("/api/venue/search", new { name }, MapVenueBasic, ct);

    /// <summary>论文信息(批量) [免费] POST /api/paper/info  params: ids[](max100)</summary>
    public Task<AminerResult<AminerPaper>> GetPaperInfoAsync(
        string[] ids, CancellationToken ct = default)
        => PostAsync("/api/paper/info", new { ids = ids.Take(100).ToArray() }, MapPaperInfo, ct);

    /// <summary>专利信息 [免费] GET /api/patent/info  params: id</summary>
    public Task<AminerResult<AminerPatent>> GetPatentInfoAsync(
        string id, CancellationToken ct = default)
        => GetAsync($"/api/patent/info?id={Esc(id)}", MapPatentInfo, ct);

    // ══════════════════════════════════════════════════════
    // PAID — 论文类
    // ══════════════════════════════════════════════════════

    /// <summary>论文搜索Pro [¥0.01] GET /api/paper/search/pro  params: title,keyword,abstract,author,org,venue,order,page,size</summary>
    public Task<AminerResult<AminerPaper>> SearchPapersProAsync(
        string? title = null, string? keyword = null, string? abs = null,
        string? author = null, string? org = null, string? venue = null,
        string? order = null, int page = 0, int size = 5, CancellationToken ct = default)
    {
        var sb = new StringBuilder($"/api/paper/search/pro?page={page}&size={Math.Clamp(size, 1, 100)}");
        if (!string.IsNullOrWhiteSpace(title)) sb.Append($"&title={Esc(title)}");
        if (!string.IsNullOrWhiteSpace(keyword)) sb.Append($"&keyword={Esc(keyword)}");
        if (!string.IsNullOrWhiteSpace(abs)) sb.Append($"&abstract={Esc(abs)}");
        if (!string.IsNullOrWhiteSpace(author)) sb.Append($"&author={Esc(author)}");
        if (!string.IsNullOrWhiteSpace(org)) sb.Append($"&org={Esc(org)}");
        if (!string.IsNullOrWhiteSpace(venue)) sb.Append($"&venue={Esc(venue)}");
        if (!string.IsNullOrWhiteSpace(order)) sb.Append($"&order={Esc(order)}");
        return GetAsync(sb.ToString(), MapPaperBasic, ct);
    }

    /// <summary>论文详情 [¥0.01] GET /api/paper/detail  params: id</summary>
    public Task<AminerResult<AminerPaper>> GetPaperDetailAsync(
        string id, CancellationToken ct = default)
        => GetAsync($"/api/paper/detail?id={Esc(id)}", MapPaperDetail, ct);

    /// <summary>论文引用关系 [¥0.10] GET /api/paper/relation  params: id</summary>
    public Task<AminerResult<AminerPaper>> GetPaperCitationsAsync(
        string id, CancellationToken ct = default)
        => GetAsync($"/api/paper/relation?id={Esc(id)}", MapPaperRelation, ct);

    /// <summary>论文QA搜索 [¥0.05] POST /api/paper/qa/search  详细参数见文档</summary>
    public Task<AminerResult<AminerPaper>> SearchPapersQaAsync(
        string? query = null, bool useTopic = true, string? topicHigh = null,
        string? topicMiddle = null, string? topicLow = null, string[]? titles = null,
        string? doi = null, int[]? years = null, bool? sciOnly = null,
        bool? citationBoost = null, int size = 10, int offset = 0,
        bool? forceCitationSort = null, bool? forceYearSort = null,
        string[]? authorTerms = null, string[]? orgTerms = null,
        string[]? authorIds = null, string[]? orgIds = null,
        string[]? venueIds = null, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object> { ["use_topic"] = useTopic, ["size"] = Math.Clamp(size, 1, 100), ["offset"] = Math.Min(offset, 10000) };
        if (!string.IsNullOrWhiteSpace(query)) body["query"] = query;
        if (!string.IsNullOrWhiteSpace(topicHigh)) body["topic_high"] = topicHigh;
        if (!string.IsNullOrWhiteSpace(topicMiddle)) body["topic_middle"] = topicMiddle;
        if (!string.IsNullOrWhiteSpace(topicLow)) body["topic_low"] = topicLow;
        if (titles?.Length > 0) body["title"] = titles;
        if (!string.IsNullOrWhiteSpace(doi)) body["doi"] = doi;
        if (years?.Length > 0) body["year"] = years;
        if (sciOnly.HasValue) body["sci_flag"] = sciOnly.Value;
        if (citationBoost.HasValue) body["n_citation_flag"] = citationBoost.Value;
        if (forceCitationSort.HasValue) body["force_citation_sort"] = forceCitationSort.Value;
        if (forceYearSort.HasValue) body["force_year_sort"] = forceYearSort.Value;
        if (authorTerms?.Length > 0) body["author_terms"] = authorTerms;
        if (orgTerms?.Length > 0) body["org_terms"] = orgTerms;
        if (authorIds?.Length > 0) body["author_id"] = authorIds;
        if (orgIds?.Length > 0) body["org_id"] = orgIds;
        if (venueIds?.Length > 0) body["venue_ids"] = venueIds;
        return PostAsync("/api/paper/qa/search", body, MapPaperBasic, ct);
    }

    /// <summary>AMiner沉思(SSE) [¥0.80] POST /api/paper/deep_research</summary>
    public async Task<AminerResult<string>> DeepResearchAsync(
        string message, int type = 1, bool webSearch = false,
        Func<string, Task>? onChunk = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(Tk)) return AminerResult<string>.NoToken;
        var url = B + "/api/paper/deep_research";
        var body = JsonSerializer.Serialize(new { message, type, web_search = webSearch });

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            req.Headers.TryAddWithoutValidation("Authorization", Tk);
            using var resp = await _c.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
            {
                var err = StatusToError(resp.StatusCode);
                return AminerResult<string>.Fail(err.code, err.msg);
            }

            using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var reader = new StreamReader(stream);
            var sb = new StringBuilder();
            while (!reader.EndOfStream)
            {
                var line = (await reader.ReadLineAsync(ct).ConfigureAwait(false))?.Trim();
                if (string.IsNullOrEmpty(line) || !line.StartsWith("data:")) continue;
                var data = line[5..].Trim();
                if (data == "[DONE]") break;
                sb.Append(data);
                if (onChunk != null) await onChunk(data).ConfigureAwait(false);
            }
            return new AminerResult<string> { Success = true, Items = new List<string> { sb.ToString() }, Total = 1 };
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return AminerResult<string>.Err("Deep research request timed out.");
        }
        catch (HttpRequestException ex)
        {
            return AminerResult<string>.Err($"Deep research network error: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            return AminerResult<string>.Err("Deep research request cancelled.");
        }
    }

    // ══════════════════════════════════════════════════════
    // PAID — 学者类
    // ══════════════════════════════════════════════════════

    /// <summary>学者详情 [¥1.00] GET /api/person/detail  params: id</summary>
    public Task<AminerResult<AminerScholar>> GetScholarDetailAsync(
        string id, CancellationToken ct = default)
        => GetAsync($"/api/person/detail?id={Esc(id)}", MapScholarDetail, ct);

    /// <summary>学者画像 [¥0.50] GET /api/person/figure  params: id</summary>
    public Task<AminerResult<AminerScholar>> GetScholarPortraitAsync(
        string id, CancellationToken ct = default)
        => GetAsync($"/api/person/figure?id={Esc(id)}", MapScholarFigure, ct);

    /// <summary>学者统计 [¥0.50] POST /api/person/stat  params: id</summary>
    public Task<AminerResult<AminerScholar>> GetScholarStatsAsync(
        string id, CancellationToken ct = default)
        => PostAsync("/api/person/stat", new { id }, MapScholarStat, ct);

    /// <summary>学者论文 [¥1.50] GET /api/person/paper/relation  params: id</summary>
    public Task<AminerResult<AminerPaper>> GetScholarPapersAsync(
        string id, CancellationToken ct = default)
        => GetAsync($"/api/person/paper/relation?id={Esc(id)}", MapPersonPaper, ct);

    /// <summary>学者专利 [¥1.50] GET /api/person/patent/relation  params: id</summary>
    public Task<AminerResult<AminerPatent>> GetScholarPatentsAsync(
        string id, CancellationToken ct = default)
        => GetAsync($"/api/person/patent/relation?id={Esc(id)}", MapPatentBasic, ct);

    /// <summary>学者项目 [¥1.50] GET /api/project/person/v3/open  params: id</summary>
    public Task<AminerResult<AminerProject>> GetScholarProjectsAsync(
        string id, CancellationToken ct = default)
        => GetAsync($"/api/project/person/v3/open?id={Esc(id)}", MapProject, ct);

    // ══════════════════════════════════════════════════════
    // PAID — 机构/期刊/专利
    // ══════════════════════════════════════════════════════

    /// <summary>机构详情 [¥0.01] POST /api/organization/detail  params: ids[]</summary>
    public Task<AminerResult<AminerOrg>> GetOrgDetailAsync(
        string[] ids, CancellationToken ct = default)
        => PostAsync("/api/organization/detail", new { ids }, MapOrgDetail, ct);

    /// <summary>机构专利 [¥0.10] GET /api/organization/patent/relation  params: id,page,page_size,source</summary>
    public Task<AminerResult<AminerPatent>> GetOrgPatentsAsync(
        string orgId, int page = 1, int pageSize = 100, string? source = "ass", CancellationToken ct = default)
        => GetAsync($"/api/organization/patent/relation?id={Esc(orgId)}&page={page}&page_size={Math.Min(pageSize, 10000)}&source={source ?? "ass"}", MapPatentBasic, ct);

    /// <summary>专利详情 [¥0.01] GET /api/patent/detail  params: id</summary>
    public Task<AminerResult<AminerPatent>> GetPatentDetailAsync(
        string id, CancellationToken ct = default)
        => GetAsync($"/api/patent/detail?id={Esc(id)}", MapPatentDetail, ct);

    // ══════════════════════════════════════════════════════
    // HTTP
    // ══════════════════════════════════════════════════════

    async Task<AminerResult<T>> GetAsync<T>(string path, Func<JsonElement, T> map, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Tk)) return AminerResult<T>.NoToken;
        var url = B + path;
        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.TryAddWithoutValidation("Authorization", Tk);
                using var r = await _c.SendAsync(req, ct).ConfigureAwait(false);

                if (!r.IsSuccessStatusCode)
                {
                    var statusErr = StatusToError(r.StatusCode);
                    if (IsRetryable((int)r.StatusCode) && attempt < MaxRetries) { await Task.Delay(RetryDelay, ct); continue; }
                    return AminerResult<T>.Fail(statusErr.code, statusErr.msg);
                }

                var t = await r.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                using var d = JsonDocument.Parse(t);
                return Parse(d.RootElement, map);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested) // timeout
            {
                if (attempt < MaxRetries) { await Task.Delay(RetryDelay, ct); continue; }
                return AminerResult<T>.Err("Request timed out after retries.");
            }
            catch (HttpRequestException ex)
            {
                if (attempt < MaxRetries) { await Task.Delay(RetryDelay, ct); continue; }
                return AminerResult<T>.Err($"Network error: {ex.Message}");
            }
            catch (JsonException ex)
            {
                return AminerResult<T>.Err($"JSON parse error: {ex.Message}");
            }
            catch (OperationCanceledException)
            {
                return AminerResult<T>.Err("Request cancelled.");
            }
        }
        return AminerResult<T>.Err("Max retries exhausted.");
    }

    async Task<AminerResult<T>> PostAsync<T>(string path, object body, Func<JsonElement, T> map, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Tk)) return AminerResult<T>.NoToken;
        var url = B + path;
        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, url)
                { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };
                req.Headers.TryAddWithoutValidation("Authorization", Tk);
                using var r = await _c.SendAsync(req, ct).ConfigureAwait(false);

                if (!r.IsSuccessStatusCode)
                {
                    var statusErr = StatusToError(r.StatusCode);
                    if (IsRetryable((int)r.StatusCode) && attempt < MaxRetries) { await Task.Delay(RetryDelay, ct); continue; }
                    return AminerResult<T>.Fail(statusErr.code, statusErr.msg);
                }

                var t = await r.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                using var d = JsonDocument.Parse(t);
                return Parse(d.RootElement, map);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested) // timeout
            {
                if (attempt < MaxRetries) { await Task.Delay(RetryDelay, ct); continue; }
                return AminerResult<T>.Err("Request timed out after retries.");
            }
            catch (HttpRequestException ex)
            {
                if (attempt < MaxRetries) { await Task.Delay(RetryDelay, ct); continue; }
                return AminerResult<T>.Err($"Network error: {ex.Message}");
            }
            catch (JsonException ex)
            {
                return AminerResult<T>.Err($"JSON parse error: {ex.Message}");
            }
            catch (OperationCanceledException)
            {
                return AminerResult<T>.Err("Request cancelled.");
            }
        }
        return AminerResult<T>.Err("Max retries exhausted.");
    }

    static (string code, string msg) StatusToError(HttpStatusCode status) => (int)status switch
    {
        400 => ("aminer_400", "Bad request — check parameters."),
        401 => ("aminer_401", "Unauthorized. Check NONG_LIT_AMINER_KEY (JWT from https://open.aminer.cn)."),
        403 => ("aminer_403", "Forbidden. Your API key may lack permission for this endpoint."),
        404 => ("aminer_404", "Not found."),
        429 => ("aminer_429", "Rate limited. Retry after a pause."),
        >= 500 => ("aminer_5xx", $"Server error ({(int)status}). AMiner may be temporarily unavailable."),
        _ => ($"aminer_{(int)status}", $"HTTP {(int)status}.")
    };

    static bool IsRetryable(int status) => status == 429 || status >= 500 || status == 0;

    static AminerResult<T> Parse<T>(JsonElement root, Func<JsonElement, T> map)
    {
        var code = GetInt(root, "code");
        var success = root.TryGetProperty("success", out var sv) && sv.ValueKind == JsonValueKind.True;
        // Some endpoints (stat) only return success:true, no code field at all
        if (code.HasValue && code != 200 && code != 0)
            return AminerResult<T>.Fail($"aminer_{code}", GetS(root, "msg") ?? GetS(root, "message") ?? $"code={code}");
        if (!code.HasValue && !success)
            return AminerResult<T>.Fail("aminer_unknown", GetS(root, "msg") ?? GetS(root, "message") ?? "Unknown error");
        var list = new List<T>();
        if (root.TryGetProperty("data", out var data))
        {
            if (data.ValueKind == JsonValueKind.Array)
                foreach (var e in data.EnumerateArray()) list.Add(map(e));
            else if (data.ValueKind == JsonValueKind.Object)
                list.Add(map(data)); // single-object response (detail, figure, stat)
        }
        return new AminerResult<T> { Success = true, Items = list, Total = GetInt(root, "total") ?? list.Count };
    }

    // ══════════════════════════════════════════════════════
    // Mappers — exact match to real AMiner JSON fields
    // ══════════════════════════════════════════════════════

    static AminerScholar MapScholar(JsonElement e) => new()
    {
        Id = GetS(e, "id"), Name = GetS(e, "name"), NameZh = GetS(e, "name_zh"),
        CitationCount = GetInt(e, "n_citation") ?? 0,
        Interests = GetStrList(e, "interests"),
        Org = GetS(e, "org"), OrgId = GetS(e, "org_id"), OrgZh = GetS(e, "org_zh"),
        Nation = GetS(e, "nation"),
    };

    static AminerScholar MapScholarDetail(JsonElement e) => new()
    {
        Id = GetS(e, "id"), Name = GetS(e, "name"), NameZh = GetS(e, "name_zh"),
        Bio = GetS(e, "bio"), BioZh = GetS(e, "bio_zh"),
        Education = GetS(e, "edu"), EducationZh = GetS(e, "edu_zh"),
        Position = GetS(e, "position"), PositionZh = GetS(e, "position_zh"),
        Orgs = GetStrList(e, "orgs"), OrgZhs = GetStrList(e, "org_zhs"),
        Honors = ParseHonors(e),
    };

    static AminerScholar MapScholarFigure(JsonElement e) => new()
    {
        Id = GetS(e, "id"),
        AiDomains = ParseDomainItems(e, "ai_domain"),
        AiInterests = ParseDomainItems(e, "ai_interests"),
        Educations = ParseCareerItems(e, "edus"),
        Works = ParseCareerItems(e, "works"),
    };

    static AminerScholar MapScholarStat(JsonElement e) => new()
    {
        Id = GetS(e, "id"),
        PubNum = GetInt(e, "pub_num") ?? 0, CitationCount = GetInt(e, "citation_num") ?? 0,
        HIndex = GetInt(e, "h_index") ?? 0, GIndex = GetInt(e, "g_index") ?? 0,
        Activity = GetFloat(e, "activity"), Diversity = GetFloat(e, "diversity"),
        Sociability = GetFloat(e, "sociability"),
    };

    // Paper basic (search/pro results)
    static AminerPaper MapPaperBasic(JsonElement e) => new()
    {
        Id = GetS(e, "id"), Title = GetS(e, "title"), TitleZh = GetS(e, "title_zh"),
        Doi = GetS(e, "doi"), Year = GetInt(e, "year"),
        CitationBucket = GetS(e, "n_citation_bucket"), CitationCount = GetInt(e, "n_citation") ?? 0,
        FirstAuthor = GetS(e, "first_author"), VenueName = GetS(e, "venue_name"),
    };

    // Paper info (free batch)
    static AminerPaper MapPaperInfo(JsonElement e) => new()
    {
        Id = GetS(e, "id"), Title = GetS(e, "title"),
        AbstractSlice = GetS(e, "abstract_slice"), AuthorCount = GetInt(e, "author_count"),
        Issue = GetS(e, "issue"), Year = GetInt(e, "year"),
        VenueId = GetS(e, "venue_id"),
        Authors = e.TryGetProperty("authors", out var au) && au.ValueKind == JsonValueKind.Array
            ? au.EnumerateArray().Select(a => GetS(a, "name")).Where(n => n != null).Select(n => n!).ToList()
            : new List<string>(),
        VenueRaw = e.TryGetProperty("venue", out var v) ? GetS(v, "raw") : null,
    };

    // Paper detail (paid)
    static AminerPaper MapPaperDetail(JsonElement e) => new()
    {
        Id = GetS(e, "id"), Title = GetS(e, "title"), TitleZh = GetS(e, "title_zh"),
        Abstract = GetS(e, "abstract"), AbstractZh = GetS(e, "abstract_zh"),
        Doi = GetS(e, "doi"), Issn = GetS(e, "issn"), Issue = GetS(e, "issue"),
        Volume = GetS(e, "volume"), Year = GetInt(e, "year"),
        Keywords = GetStrList(e, "keywords"), KeywordsZh = GetStrList(e, "keywords_zh"),
        Authors = e.TryGetProperty("authors", out var au) && au.ValueKind == JsonValueKind.Array
            ? au.EnumerateArray().Select(a => GetS(a, "name")).Where(n => n != null).Select(n => n!).ToList()
            : new List<string>(),
        AuthorOrgs = e.TryGetProperty("authors", out var au2) && au2.ValueKind == JsonValueKind.Array
            ? au2.EnumerateArray().Select(a => GetS(a, "org")).Where(o => o != null).Select(o => o!).ToList()
            : new List<string>(),
        VenueRaw = e.TryGetProperty("venue", out var v) ? GetS(v, "raw") : null,
    };

    // Paper relation (citations)
    static AminerPaper MapPaperRelation(JsonElement e) => new()
    {
        Id = GetS(e, "_id"), Title = GetS(e, "title"),
        CitationCount = GetInt(e, "n_citation") ?? 0,
    };

    // Person paper (paid)
    static AminerPaper MapPersonPaper(JsonElement e) => new()
    {
        Id = GetS(e, "id"), Title = GetS(e, "title"), TitleZh = GetS(e, "title_zh"),
        AuthorId = GetS(e, "author_id"),
    };

    // Patent basic (search)
    static AminerPatent MapPatentBasic(JsonElement e) => new()
    {
        Id = GetS(e, "id"), Title = GetS(e, "title"), TitleZh = GetS(e, "title_zh"),
        PubYear = GetS(e, "pub_year"), AppYear = GetS(e, "app_year"),
        InventorName = GetS(e, "inventor_name"),
    };

    // Patent info (free)
    static AminerPatent MapPatentInfo(JsonElement e)
    {
        var p = new AminerPatent
        {
            Id = GetS(e, "id"), AppNum = GetS(e, "app_num"), AppYear = GetS(e, "app_year"),
            Country = GetS(e, "country"), PubKind = GetS(e, "pub_kind"),
            PubNum = GetS(e, "pub_num"), PubYear = GetS(e, "pub_year"),
        };
        if (e.TryGetProperty("title", out var tv) && tv.ValueKind == JsonValueKind.Object)
            p.Title = GetStrList(tv, "en")?.FirstOrDefault() ?? GetS(tv, "zh") ?? "";
        if (e.TryGetProperty("inventor", out var inv) && inv.ValueKind == JsonValueKind.Array)
            p.Inventors = inv.EnumerateArray().Select(i => GetS(i, "name")).Where(n => n != null).Select(n => n!).ToList();
        return p;
    }

    // Patent detail (paid)
    static AminerPatent MapPatentDetail(JsonElement e)
    {
        var p = new AminerPatent
        {
            Id = GetS(e, "id"), AppNum = GetS(e, "app_num"), Country = GetS(e, "country"),
            PubKind = GetS(e, "pub_kind"), PubNum = GetS(e, "pub_num"), Description = GetS(e, "description"),
        };
        if (e.TryGetProperty("title", out var tv) && tv.ValueKind == JsonValueKind.Object)
            p.Title = GetStrList(tv, "en")?.FirstOrDefault() ?? "";
        if (e.TryGetProperty("abstract", out var av) && av.ValueKind == JsonValueKind.Object)
            p.Abstract = string.Join("\n", GetStrList(av, "en") ?? new List<string>());
        if (e.TryGetProperty("app_date", out var ad)) p.AppDate = ParseTimestamp(ad);
        if (e.TryGetProperty("pub_date", out var pd)) p.PubDate = ParseTimestamp(pd);
        if (e.TryGetProperty("inventor", out var inv) && inv.ValueKind == JsonValueKind.Array)
            p.Inventors = inv.EnumerateArray().Select(i => GetS(i, "name")).Where(n => n != null).Select(n => n!).ToList();
        if (e.TryGetProperty("assignee", out var ass) && ass.ValueKind == JsonValueKind.Array)
            p.Assignees = ass.EnumerateArray().Select(a => GetS(a, "name")).Where(n => n != null).Select(n => n!).ToList();
        if (e.TryGetProperty("ipc", out var ipc) && ipc.ValueKind == JsonValueKind.Array)
            p.Ipc = ipc.EnumerateArray().Select(i => GetS(i, "l4") ?? string.Join("/", new[] { GetS(i, "l1"), GetS(i, "l2"), GetS(i, "l3"), GetS(i, "l4") }.Where(x => x != null))).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).ToList();
        return p;
    }

    // Org basic (search)
    static AminerOrg MapOrgBasic(JsonElement e) => new()
    {
        Id = GetS(e, "org_id"), Name = GetS(e, "org_name"),
        Aliases = GetStrList(e, "aliases"),
    };

    // Org detail (paid)
    static AminerOrg MapOrgDetail(JsonElement e) => new()
    {
        Id = GetS(e, "id"), Name = GetS(e, "name"), NameEn = GetS(e, "name_en"), NameZh = GetS(e, "name_zh"),
        Type = GetS(e, "type"), Aliases = GetStrList(e, "aliases"),
        Acronyms = GetStrList(e, "acronyms"),
    };

    // Venue basic (search)
    static AminerVenue MapVenueBasic(JsonElement e) => new()
    {
        Id = GetS(e, "id"), Name = GetS(e, "name_en"), NameZh = GetS(e, "name_zh"),
        Aliases = GetStrList(e, "aliases"), VenueType = GetS(e, "venue_type"),
    };

    // Project
    static AminerProject MapProject(JsonElement e) => new()
    {
        Id = GetS(e, "id"), Country = GetS(e, "country"),
        FundAmount = GetFloat(e, "fund_amount"), FundCurrency = GetS(e, "fund_currency"),
        ProjectSource = GetS(e, "project_source"),
        Titles = GetS(e, "titles"),
        StartDate = ParseTimestamp(e, "start_date"), EndDate = ParseTimestamp(e, "end_date"),
    };

    // Recommendation paper
    static AminerRecPaper MapRecPaper(JsonElement e)
    {
        var papers = new List<AminerPaper>();
        if (e.TryGetProperty("papers", out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var p in arr.EnumerateArray())
                papers.Add(new AminerPaper
                {
                    Id = GetS(p, "paper_id"), Title = GetS(p, "title"), Year = GetInt(p, "year"),
                    Summary = GetS(p, "summary"), PdfLink = GetS(p, "pdf") ?? GetNestedS(p, "links", "pdf"),
                    PaperUrl = GetS(p, "paper_url") ?? GetNestedS(p, "links", "aminer"),
                    Authors = GetStrList(p, "authors"), Keywords = GetStrList(p, "keywords"),
                });
        return new AminerRecPaper { Offset = GetInt(e, "offset") ?? 0, Size = GetInt(e, "size") ?? 0, Total = GetInt(e, "total") ?? 0, Papers = papers };
    }

    // ══════════════════════════════════════════════════════
    // JSON leaf helpers
    // ══════════════════════════════════════════════════════

    static string? GetS(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    static int? GetInt(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind != JsonValueKind.Null && v.TryGetInt32(out var n) ? n : null;
    static float? GetFloat(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind != JsonValueKind.Null && v.TryGetSingle(out var f) ? f : null;
    static string? GetNestedS(JsonElement e, string p1, string p2) => e.TryGetProperty(p1, out var v1) && v1.TryGetProperty(p2, out var v2) && v2.ValueKind == JsonValueKind.String ? v2.GetString() : null;

    static List<string>? GetStrList(JsonElement e, string p)
    {
        if (!e.TryGetProperty(p, out var arr) || arr.ValueKind != JsonValueKind.Array) return null;
        var l = new List<string>();
        foreach (var x in arr.EnumerateArray())
            if (x.ValueKind == JsonValueKind.String) l.Add(x.GetString()!);
        return l;
    }

    static string? ParseTimestamp(JsonElement e, string p)
    {
        if (!e.TryGetProperty(p, out var v)) return null;
        if (v.TryGetProperty("seconds", out var s) && s.TryGetInt64(out var sec))
            return DateTimeOffset.FromUnixTimeSeconds(sec).ToString("yyyy-MM-dd");
        return null;
    }
    static string? ParseTimestamp(JsonElement v)
    {
        if (v.TryGetProperty("seconds", out var s) && s.TryGetInt64(out var sec))
            return DateTimeOffset.FromUnixTimeSeconds(sec).ToString("yyyy-MM-dd");
        return null;
    }

    static List<AminerDomainItem>? ParseDomainItems(JsonElement e, string p)
    {
        if (!e.TryGetProperty(p, out var arr) || arr.ValueKind != JsonValueKind.Array) return null;
        var l = new List<AminerDomainItem>();
        foreach (var x in arr.EnumerateArray())
            l.Add(new AminerDomainItem { Name = GetS(x, "name"), NameZh = GetS(x, "name_zh"), Order = GetInt(x, "order") ?? 0 });
        return l;
    }

    static List<AminerCareerItem>? ParseCareerItems(JsonElement e, string p)
    {
        if (!e.TryGetProperty(p, out var arr) || arr.ValueKind != JsonValueKind.Array) return null;
        var l = new List<AminerCareerItem>();
        foreach (var x in arr.EnumerateArray())
            l.Add(new AminerCareerItem
            {
                Org = GetS(x, "org"), Department = GetS(x, "department"),
                StartYear = GetInt(x, "start_year"), EndYear = GetInt(x, "end_year"),
                Position = GetInt(x, "position") ?? 0, PositionExtra = GetS(x, "position_extra"),
            });
        return l;
    }

    static List<AminerHonorItem>? ParseHonors(JsonElement e)
    {
        if (!e.TryGetProperty("honor", out var arr) || arr.ValueKind != JsonValueKind.Array) return null;
        var l = new List<AminerHonorItem>();
        foreach (var x in arr.EnumerateArray())
            l.Add(new AminerHonorItem { Award = GetS(x, "award"), Year = GetInt(x, "year"), Reason = GetS(x, "reason") });
        return l;
    }

    static string Esc(string s) => Uri.EscapeDataString(s);
}

// ══════════════════════════════════════════════════════
// Models — exact match to real AMiner JSON
// ══════════════════════════════════════════════════════

public sealed class AminerResult<T>
{
    public bool Success { get; init; }
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int Total { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public static AminerResult<T> Fail(string code, string msg) => new() { ErrorCode = code, ErrorMessage = msg };
    public static AminerResult<T> Err(string msg) => new() { ErrorCode = "error", ErrorMessage = msg };
    public static readonly AminerResult<T> NoToken = new() { ErrorCode = "missing_token", ErrorMessage = "Set NONG_LIT_AMINER_KEY env var (JWT from https://open.aminer.cn)." };
}

public sealed class AminerScholar
{
    public string? Id { get; set; } public string? Name { get; set; } public string? NameZh { get; set; }
    // Free search
    public List<string>? Interests { get; set; } public int CitationCount { get; set; }
    public string? Org { get; set; } public string? OrgId { get; set; } public string? OrgZh { get; set; } public string? Nation { get; set; }
    // Paid detail
    public string? Bio { get; set; } public string? BioZh { get; set; }
    public string? Education { get; set; } public string? EducationZh { get; set; }
    public string? Position { get; set; } public string? PositionZh { get; set; }
    public List<string>? Orgs { get; set; } public List<string>? OrgZhs { get; set; }
    public List<AminerHonorItem>? Honors { get; set; }
    // Paid figure
    public List<AminerDomainItem>? AiDomains { get; set; } public List<AminerDomainItem>? AiInterests { get; set; }
    public List<AminerCareerItem>? Educations { get; set; } public List<AminerCareerItem>? Works { get; set; }
    // Paid stat
    public int PubNum { get; set; } public int HIndex { get; set; } public int GIndex { get; set; }
    public float? Activity { get; set; } public float? Diversity { get; set; } public float? Sociability { get; set; }
    // Display
    public string DisplayName => NameZh ?? Name ?? Id ?? "?";
    public string ProfileUrl => Id != null ? $"https://www.aminer.cn/profile/{Id}" : "";
}

public sealed class AminerHonorItem { public string? Award { get; set; } public int? Year { get; set; } public string? Reason { get; set; } }
public sealed class AminerDomainItem { public string? Name { get; set; } public string? NameZh { get; set; } public int Order { get; set; } }
public sealed class AminerCareerItem { public string? Org { get; set; } public string? Department { get; set; } public int? StartYear { get; set; } public int? EndYear { get; set; } public int Position { get; set; } public string? PositionExtra { get; set; } }

public sealed class AminerPaper
{
    public string? Id { get; set; } public string? Title { get; set; } public string? TitleZh { get; set; }
    // Basic search
    public string? Doi { get; set; } public int? Year { get; set; }
    public string? CitationBucket { get; set; } public int CitationCount { get; set; }
    public string? FirstAuthor { get; set; } public string? VenueName { get; set; }
    // Info (free batch)
    public string? AbstractSlice { get; set; } public int? AuthorCount { get; set; }
    public string? Issue { get; set; } public string? Volume { get; set; } public string? VenueId { get; set; }
    public string? VenueRaw { get; set; }
    public List<string> Authors { get; set; } = new();
    public List<string> AuthorOrgs { get; set; } = new();
    // Detail (paid)
    public string? Abstract { get; set; } public string? AbstractZh { get; set; }
    public string? Issn { get; set; }
    public List<string>? Keywords { get; set; } public List<string>? KeywordsZh { get; set; }
    // Relation
    public string? AuthorId { get; set; }
    // Rec
    public string? Summary { get; set; } public string? PdfLink { get; set; } public string? PaperUrl { get; set; }
    // Display
    public string DisplayTitle => TitleZh ?? Title ?? "?";
    public string PubUrl => Id != null ? $"https://www.aminer.cn/pub/{Id}" : "";
}

public sealed class AminerRecPaper
{
    public int Offset { get; set; } public int Size { get; set; } public int Total { get; set; }
    public List<AminerPaper> Papers { get; set; } = new();
}

public sealed class AminerPatent
{
    public string? Id { get; set; } public string? Title { get; set; } public string? TitleZh { get; set; }
    // Basic
    public string? PubYear { get; set; } public string? AppYear { get; set; } public string? InventorName { get; set; }
    // Info (free)
    public string? AppNum { get; set; } public string? Country { get; set; } public string? PubKind { get; set; } public string? PubNum { get; set; }
    public List<string>? Inventors { get; set; }
    // Detail (paid)
    public string? Abstract { get; set; } public string? Description { get; set; }
    public string? AppDate { get; set; } public string? PubDate { get; set; }
    public List<string>? Assignees { get; set; } public List<string>? Ipc { get; set; }
    // Display
    public string DisplayTitle => TitleZh ?? Title ?? "?";
    public string PatentUrl => Id != null ? $"https://www.aminer.cn/patent/{Id}" : "";
}

public sealed class AminerOrg
{
    public string? Id { get; set; } public string? Name { get; set; }
    public string? NameEn { get; set; } public string? NameZh { get; set; }
    public string? Type { get; set; }
    public List<string>? Aliases { get; set; } public List<string>? Acronyms { get; set; }
    public string DisplayName => NameZh ?? Name ?? Id ?? "?";
}

public sealed class AminerVenue
{
    public string? Id { get; set; } public string? Name { get; set; } public string? NameZh { get; set; }
    public string? VenueType { get; set; }
    public List<string>? Aliases { get; set; }
    public string DisplayName => NameZh ?? Name ?? Id ?? "?";
}

public sealed class AminerProject
{
    public string? Id { get; set; } public string? Country { get; set; }
    public float? FundAmount { get; set; } public string? FundCurrency { get; set; }
    public string? ProjectSource { get; set; } public string? Titles { get; set; }
    public string? StartDate { get; set; } public string? EndDate { get; set; }
}
