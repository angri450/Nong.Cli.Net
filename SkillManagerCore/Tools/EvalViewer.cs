using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using SkillManager.Cli.Models;

namespace SkillManager.Cli.Tools;

public class EvalViewer
{
    private readonly string _evalsDir, _workspaceDir, _recordsDir, _skillName;
    private readonly HttpListener _listener;
    private readonly int _port;

    public EvalViewer(string skillDir, string? skillName = null, int port = 3117)
    {
        _skillName = skillName ?? Path.GetFileName(Path.GetFullPath(skillDir));
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var dir = Path.Combine(docs, "skill-manager");
        _evalsDir = Path.Combine(dir, "evals"); Directory.CreateDirectory(_evalsDir);
        _workspaceDir = Path.Combine(dir, "workspace"); Directory.CreateDirectory(_workspaceDir);
        _recordsDir = Path.Combine(dir, "session-records"); Directory.CreateDirectory(_recordsDir);
        _port = port;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
    }

    public int Port => _port;

    public async Task ServeAsync(CancellationToken ct = default)
    {
        _listener.Start();
        Console.WriteLine($"  http://localhost:{_port}/");
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var ctxTask = _listener.GetContextAsync();
                var delayTask = Task.Delay(-1, ct);
                var completed = await Task.WhenAny(ctxTask, delayTask);
                if (completed == delayTask) break;
                var ctx = await ctxTask;
                _ = Task.Run(() => Handle(ctx));
            }
        }
        catch (OperationCanceledException) { }
        finally { _listener.Stop(); _listener.Close(); }
    }

    async Task Handle(HttpListenerContext c)
    {
        try
        {
            var m = c.Request.HttpMethod;
            var p = c.Request.Url!.AbsolutePath.TrimEnd('/');
            if (p == "" || p == "/") await View(c);
            else if (p == "/api/evals" && m == "GET") await ListE(c);
            else if (p == "/api/evals" && m == "POST") await CreateE(c);
            else if (p.StartsWith("/api/evals/") && m == "DELETE") await DelE(c, p);
            else if (p.StartsWith("/api/evals/") && m == "GET") await GetE(c, p);
            else if (p == "/api/feedback" && m == "GET") await GetFb(c);
            else if (p == "/api/feedback" && m == "POST") await SaveFb(c);
            else if (p == "/api/workspace/runs" && m == "GET") await ListR(c);
            else if (p == "/api/session-records" && m == "GET") await ListRec(c);
            else if (p == "/api/session-records/zip" && m == "GET") await ZipRec(c);
            else await J(c, 404, new { error = "not found" });
        }
        catch (Exception e) { await J(c, 500, new { error = e.Message }); }
    }

    async Task View(HttpListenerContext c)
    {
        var a = Assembly.GetExecutingAssembly();
        var r = a.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("viewer.html")) ?? throw new Exception("viewer.html not embedded");
        using var s = a.GetManifestResourceStream(r)!;
        var html = await new StreamReader(s, Encoding.UTF8).ReadToEndAsync();
        var evals = LE();
        var runs = CR();
        html = html.Replace("/*__EMBEDDED_DATA__*/", $"const EMBEDDED_DATA = {JsonSerializer.Serialize(new { skill_name = _skillName, evals, runs, mode = "serve" })};");
        await H(c, 200, html);
    }

    object[] LE()
    {
        if (!Directory.Exists(_evalsDir)) return Array.Empty<object>();
        return Directory.GetFiles(_evalsDir, "*.json").Select(f =>
        {
            try { var s = JsonSerializer.Deserialize<EvalSet>(File.ReadAllText(f), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); return new { file = Path.GetFileName(f), skill_name = s?.SkillName ?? "?", eval_count = s?.Evals?.Count ?? 0, size = new FileInfo(f).Length }; }
            catch { return new { file = Path.GetFileName(f), skill_name = "(invalid)", eval_count = 0, size = new FileInfo(f).Length }; }
        }).ToArray<object>();
    }

    object[] CR()
    {
        if (!Directory.Exists(_workspaceDir)) return Array.Empty<object>();
        var result = new List<object>();
        foreach (var d in Directory.EnumerateDirectories(_workspaceDir))
        {
            var od = Path.Combine(d, "outputs"); if (!Directory.Exists(od)) continue;
            var prompt = ""; var mp = Path.Combine(d, "eval_metadata.json");
            if (File.Exists(mp)) try { var meta = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(mp)); JsonElement pe = default; meta?.TryGetValue("prompt", out pe); prompt = pe.ValueKind != JsonValueKind.Undefined ? (pe.GetString() ?? "") : ""; } catch { }
            result.Add(new { id = Path.GetFileName(d), prompt, outputs = Directory.GetFiles(od).Select(f => { var e = Path.GetExtension(f).ToLower(); var isT = e is ".txt" or ".md" or ".json" or ".csv" or ".html" or ".css" or ".js" or ".xml" or ".yaml" or ".yml" or ".sh" or ".c"; if (isT) try { return new { name = Path.GetFileName(f), type = "text", content = File.ReadAllText(f) } as object; } catch { return new { name = Path.GetFileName(f), type = "error" } as object; } return new { name = Path.GetFileName(f), type = "binary" } as object; }).ToList() });
        }
        return result.ToArray();
    }

    async Task ListE(HttpListenerContext c) { await J(c, 200, LE()); }
    async Task GetE(HttpListenerContext c, string p) { var fp = Path.Combine(_evalsDir, p.Replace("/api/evals/", "") + ".json"); if (!File.Exists(fp)) { await J(c, 404, new { error = "not found" }); return; } await R(c, 200, await File.ReadAllTextAsync(fp), "application/json"); }
    async Task CreateE(HttpListenerContext c) { var b = await new StreamReader(c.Request.InputStream).ReadToEndAsync(); EvalSet? s; try { s = JsonSerializer.Deserialize<EvalSet>(b, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); } catch { await J(c, 400, new { error = "invalid JSON" }); return; } if (s == null || string.IsNullOrWhiteSpace(s.SkillName)) { await J(c, 400, new { error = "skill_name required" }); return; } var fn = SF(s.SkillName) + ".json"; await File.WriteAllTextAsync(Path.Combine(_evalsDir, fn), JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true })); await J(c, 200, new { ok = true, file = fn }); }
    async Task DelE(HttpListenerContext c, string p) { var fp = Path.Combine(_evalsDir, p.Replace("/api/evals/", "") + ".json"); if (!File.Exists(fp)) { await J(c, 404, new { error = "not found" }); return; } File.Delete(fp); await J(c, 200, new { ok = true }); }
    async Task GetFb(HttpListenerContext c) { var fp = Path.Combine(_workspaceDir, "feedback.json"); if (!File.Exists(fp)) { await J(c, 200, new { reviews = Array.Empty<object>() }); return; } await R(c, 200, await File.ReadAllTextAsync(fp), "application/json"); }
    async Task SaveFb(HttpListenerContext c) { var b = await new StreamReader(c.Request.InputStream).ReadToEndAsync(); await File.WriteAllTextAsync(Path.Combine(_workspaceDir, "feedback.json"), b); await J(c, 200, new { ok = true }); }
    async Task ListR(HttpListenerContext c) { await J(c, 200, CR()); }
    async Task ListRec(HttpListenerContext c)
    {
        if (!Directory.Exists(_recordsDir)) { await J(c, 200, new { dates = Array.Empty<object>() }); return; }
        var dates = new List<object>();
        foreach (var f in Directory.GetFiles(_recordsDir, "*.jsonl").OrderDescending())
        {
            var lines = new List<Dictionary<string, JsonElement>>();
            foreach (var l in await File.ReadAllLinesAsync(f)) { if (string.IsNullOrWhiteSpace(l)) continue; try { var o = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(l); if (o != null) lines.Add(o); } catch { } }
            if (lines.Count == 0) continue;
            var skills = lines.Select(l => l.TryGetValue("skill", out var s) ? s.GetString() ?? "?" : "?").Where(s => s != "?").Distinct().ToArray();
            var resolved = lines.Count(l => l.TryGetValue("r", out var r) && r.ValueKind == JsonValueKind.True);
            dates.Add(new { date = Path.GetFileNameWithoutExtension(f), file = Path.GetFileName(f), lines = lines.Count, skills, resolved, unresolved = lines.Count - resolved });
        }
        await J(c, 200, new { dates });
    }
    async Task ZipRec(HttpListenerContext c)
    {
        var zp = Path.Combine(_recordsDir, "all-records.zip"); if (File.Exists(zp)) File.Delete(zp);
        using (var z = System.IO.Compression.ZipFile.Open(zp, System.IO.Compression.ZipArchiveMode.Create)) foreach (var f in Directory.GetFiles(_recordsDir, "*.jsonl")) { var e = z.CreateEntry(Path.GetFileName(f)); using var es = e.Open(); using var fs = File.OpenRead(f); fs.CopyTo(es); }
        c.Response.StatusCode = 200; c.Response.ContentType = "application/zip"; c.Response.AddHeader("Content-Disposition", "attachment; filename=session-records.zip");
        var bytes = await File.ReadAllBytesAsync(zp); c.Response.ContentLength64 = bytes.Length; await c.Response.OutputStream.WriteAsync(bytes); c.Response.OutputStream.Close(); File.Delete(zp);
    }

    static string SF(string n) { var inv = Path.GetInvalidFileNameChars(); var s = string.Join("_", n.Split(inv, StringSplitOptions.RemoveEmptyEntries)).Trim('_'); return string.IsNullOrWhiteSpace(s) ? "evals" : s; }
    static async Task H(HttpListenerContext c, int code, string html) { var b = Encoding.UTF8.GetBytes(html); c.Response.StatusCode = code; c.Response.ContentType = "text/html; charset=utf-8"; c.Response.ContentLength64 = b.Length; await c.Response.OutputStream.WriteAsync(b); c.Response.OutputStream.Close(); }
    static async Task J(HttpListenerContext c, int code, object d) { var j = JsonSerializer.Serialize(d); await R(c, code, j, "application/json"); }
    static async Task R(HttpListenerContext c, int code, string body, string ct) { var b = Encoding.UTF8.GetBytes(body); c.Response.StatusCode = code; c.Response.ContentType = ct; c.Response.ContentLength64 = b.Length; await c.Response.OutputStream.WriteAsync(b); c.Response.OutputStream.Close(); }
}