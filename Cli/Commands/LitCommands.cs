using System.CommandLine;
using System.Text.Json;
using System.Text.RegularExpressions;
using Angri450.Nong.Data;
using Angri450.Nong.Literature.Dsl;
using Angri450.Nong.Literature.Export;
using Angri450.Nong.Literature.Models;
using Angri450.Nong.Literature.Pipeline;
using Angri450.Nong.Literature.Data;
using Nong.Cli.Common;
using DocxCore;
using Angri450.Nong;

namespace Nong.Cli.Commands;

public static class LitCommands
{
    public static Command Create(Option<bool> jsonOpt)
    {
        var cmd = new Command("lit", "Literature retrieval — search, cache, query, export");
        cmd.AddCommand(CreateParse(jsonOpt));
        cmd.AddCommand(CreateValidate(jsonOpt));
        cmd.AddCommand(CreatePlan(jsonOpt));
        cmd.AddCommand(CreateSearch(jsonOpt));
        cmd.AddCommand(CreateExport(jsonOpt));
        cmd.AddCommand(CreateBatch(jsonOpt));
        cmd.AddCommand(CreateCacheQuery(jsonOpt));
        cmd.AddCommand(CreateCacheStats(jsonOpt));
        cmd.AddCommand(CreateCacheExport(jsonOpt));
        cmd.AddCommand(CreateWord(jsonOpt));
        return cmd;
    }

    // ═════════════════════════════════════════════════════════
    // lit parse / validate / plan — unchanged
    // ═════════════════════════════════════════════════════════

    static Command CreateParse(Option<bool> jsonOpt)
    {
        var queryOpt = Q();
        var cmd = new Command("parse", "Parse CNKI-like literature retrieval DSL") { queryOpt };
        cmd.SetHandler((string query, bool json) =>
        {
            var (parsed, elapsed) = CliHelpers.Time(() => CnkiParser.Parse(query));
            var output = JsonOutput.Ok("lit parse", $"Parsed {parsed.Terms.Count} term(s)", new
            {
                query = parsed.Text, valid = parsed.IsValid,
                fields = CnkiQueryNormalizer.Normalize(parsed).Fields,
                terms = parsed.Terms.Select(t => new { field = t.EffectiveField, value = t.Value, phrase = t.IsPhrase, between = t.IsBetween, start = t.BetweenStart, end = t.BetweenEnd }),
                issues = parsed.Issues
            });
            output.Metrics["terms"] = parsed.Terms.Count; output.Meta.DurationMs = elapsed;
            Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
        }, queryOpt, jsonOpt);
        return cmd;
    }

    static Command CreateValidate(Option<bool> jsonOpt)
    {
        var queryOpt = Q();
        var cmd = new Command("validate", "Validate CNKI-like literature retrieval DSL syntax") { queryOpt };
        cmd.SetHandler((string query, bool json) =>
        {
            var v = CnkiDslValidator.Validate(query);
            var output = JsonOutput.Ok("lit validate", v.IsValid ? "Valid" : $"{v.Issues.Count} issue(s)", new { valid = v.IsValid, issues = v.Issues });
            Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
        }, queryOpt, jsonOpt);
        return cmd;
    }

    static Command CreatePlan(Option<bool> jsonOpt)
    {
        var queryOpt = Q(); var sourcesOpt = SourcesOption();
        var cmd = new Command("plan", "Plan provider rough queries for literature retrieval") { queryOpt, sourcesOpt };
        cmd.SetHandler((string query, string sources, bool json) =>
        {
            var (plan, elapsed) = CliHelpers.Time(() => new QueryPlanner().Plan(CnkiParser.Parse(query), ParseSources(sources)));
            var output = JsonOutput.Ok("lit plan", $"Planned {plan.Providers.Count} provider(s)", new
            {
                parsedFields = plan.ParsedFields, normalizedConcepts = plan.NormalizedConcepts,
                providers = plan.Providers.Select(p => new { p.Name, p.IsImplemented, p.HasRequiredCredential, roughQueries = p.RoughQueries, p.Limitations }), issues = plan.Issues
            });
            output.Metrics["providers"] = plan.Providers.Count; output.Meta.DurationMs = elapsed;
            Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
        }, queryOpt, sourcesOpt, jsonOpt);
        return cmd;
    }

    // ═════════════════════════════════════════════════════════
    // lit search — now with --cache flag for direct DB storage
    // ═════════════════════════════════════════════════════════

    static Command CreateSearch(Option<bool> jsonOpt)
    {
        var queryOpt = Q(); var sourcesOpt = SourcesOption(); var limitOpt = new Option<int>("--limit", () => 50, "Max records");
        var profileOpt = new Option<string>("--profile", () => "balanced", "balanced | classic | recent");
        var outOpt = new Option<string?>("-o", "Optional JSON output file");
        var modeOpt = new Option<string>("--mode", () => "strict", "strict | recall | none");
        var cacheOpt = new Option<bool>("--cache", () => false, "Store results as unified literature list object in nong.db");
        var cmd = new Command("search", "Search foreign academic literature via OpenAlex/Crossref/Unpaywall") { queryOpt, sourcesOpt, limitOpt, profileOpt, outOpt, modeOpt, cacheOpt };
        cmd.SetHandler(async (string query, string sources, int limit, string profile, string? outputPath, string mode, bool cache, bool json) =>
        {
            var pipeline = new LiteratureSearchPipeline();
            var request = new LiteratureSearchRequest
            {
                Query = query, Sources = ParseSources(sources), Limit = limit, Profile = parseProfile(profile), FilterMode = mode
            };
            var result = await pipeline.SearchAsync(request, CancellationToken.None);

            // Store as unified literature list object (stage D: execution req #4)
            string? listId = null;
            if (cache && result.Records.Count > 0)
            {
                using var ctx = new IngestionContext();
                var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(query)))[..12];
                var list = ctx.Db.RegisterLiteratureList(hash, query, string.Join(",", ParseSources(sources)), result.Records.Count);
                
                // Convert PaperRecord → DbPaper using Literature layer's mapping (avoids Data→Literature dependency)
                var dbPapers = result.Records.Select(r => LiteratureCache.FromRecord(r, hash)).ToList();
                ctx.Db.ImportPapers(dbPapers);
                
                // Create relationships: list → papers
                var papers = ctx.Db.FindPapersByHash(hash);
                foreach (var paper in papers)
                {
                    ctx.Db.Link("literature-list", list.Id.ToString(), "contains", "paper", paper.Id.ToString());
                }
                
                listId = list.Id.ToString();
            }

            if (outputPath != null)
                await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(new { records = result.Records }, CliHelpers.JsonOpts));

            var metrics = result.Metrics.ToDictionary(kv => kv.Key, kv => kv.Value);
            if (listId != null) { metrics["list_id"] = listId; metrics["cached_papers"] = result.Records.Count; }

            var output = JsonOutput.Ok("lit search", $"Literature search returned {result.Records.Count} record(s)", new { records = result.Records, metrics, issues = Array.Empty<object>() });
            output.Metrics = metrics;
            Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
        }, queryOpt, sourcesOpt, limitOpt, profileOpt, outOpt, modeOpt, cacheOpt, jsonOpt);
        return cmd;
    }

    // ═════════════════════════════════════════════════════════
    // lit export
    // ═════════════════════════════════════════════════════════

    static Command CreateExport(Option<bool> jsonOpt)
    {
        var fileOpt = new Option<string>("--input", "Path to raw results JSON") { IsRequired = true };
        var formatOpt = new Option<string>("--format", () => "markdown", "json | markdown | bibtex");
        var outOpt = new Option<string>("-o", "Output file");
        var cmd = new Command("export", "Export literature results as JSON, Markdown, or BibTeX") { fileOpt, formatOpt, outOpt };
        cmd.SetHandler(async (string inputFile, string format, string? outFile, bool json) =>
        {
            var content = await File.ReadAllTextAsync(inputFile);
            var doc = JsonSerializer.Deserialize<JsonElement>(content);
            var recs = new List<PaperRecord>();
            JsonElement records;
            if (doc.TryGetProperty("records", out records) && records.ValueKind == JsonValueKind.Array)
                foreach (var r in records.EnumerateArray()) recs.Add(ParseRecord(r));
            else if (doc.TryGetProperty("data", out var data) && data.TryGetProperty("records", out records))
                foreach (var r in records.EnumerateArray()) recs.Add(ParseRecord(r));

            var output = format.ToLowerInvariant() switch
            {
                "bibtex" => BibTeXExporter.Export(recs),
                "json" => JsonSerializer.Serialize(recs, CliHelpers.JsonOpts),
                _ => MarkdownLiteratureExporter.Export(recs)
            };

            if (outFile != null) await File.WriteAllTextAsync(outFile, output);
            var m = JsonOutput.Ok("lit export", $"Exported {recs.Count} records", new { count = recs.Count, format, file = outFile, preview = output[..Math.Min(output.Length, 300)] });
            Console.WriteLine(JsonSerializer.Serialize(m, CliHelpers.JsonOpts));
        }, fileOpt, formatOpt, outOpt, jsonOpt);
        return cmd;
    }

    // ═════════════════════════════════════════════════════════
    // lit batch
    // ═════════════════════════════════════════════════════════

    static Command CreateBatch(Option<bool> jsonOpt)
    {
        var dirArg = new Argument<string>("dir", "Directory containing .txt files with DSL queries");
        var outOpt = new Option<string>("-o", () => "报告.md", "Output markdown report");
        var sourcesOpt = SourcesOption(); var limitOpt = new Option<int>("--limit", () => 10); var profileOpt = new Option<string>("--profile", () => "balanced");
        var cmd = new Command("batch", "Batch literature search across directory of DSL files") { dirArg, outOpt, sourcesOpt, limitOpt, profileOpt };
        cmd.SetHandler(async (string dir, string output, string sources, int limit, string profile, bool json) =>
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"# Literature Report\nGenerated: {DateTime.Now:yyyy-MM-dd HH:mm}\nSources: {sources}\nResults/DSL: {limit}\n");
            var files = Directory.GetFiles(dir, "*.txt", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var lines = await File.ReadAllLinesAsync(file);
                var dsls = lines.Where(l => l.Contains('=') && (l.Contains('*') || l.Contains('+') || l.Contains('-'))).ToList();
                if (dsls.Count == 0) continue;
                sb.AppendLine($"## {Path.GetFileNameWithoutExtension(file)}");
                foreach (var dsl in dsls)
                {
                    sb.AppendLine($"### {dsl.Truncate(80)}");
                    try
                    {
                        var pipeline = new LiteratureSearchPipeline();
                        var result = await pipeline.SearchAsync(new LiteratureSearchRequest { Query = dsl.Trim(), Sources = ParseSources(sources), Limit = limit, Profile = parseProfile(profile), FilterMode = "recall" }, CancellationToken.None);
                        sb.AppendLine($"Candidates: {result.Metrics.GetValueOrDefault("candidates", 0)} → Merged: {result.Metrics.GetValueOrDefault("merged", 0)} → Returned: {result.Records.Count}");
                        for (int i = 0; i < result.Records.Count; i++)
                        {
                            var r = result.Records[i];
                            sb.AppendLine($"{i + 1}. **{r.Title?.Truncate(120) ?? ""}**. {r.Authors.FirstOrDefault()} et al. {r.Year}. [{r.RetrievedFrom.FirstOrDefault() ?? "?"}]");
                        }
                    }
                    catch (Exception ex) { sb.AppendLine($"Error: {ex.Message}"); }
                    sb.AppendLine();
                }
            }
            await File.WriteAllTextAsync(output, sb.ToString());
            var o = JsonOutput.Ok("lit batch", $"Report saved to {output}", new { file = output, length = sb.Length }); o.Artifacts["report"] = output;
            Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
        }, dirArg, outOpt, sourcesOpt, limitOpt, profileOpt, jsonOpt);
        return cmd;
    }

    // ═════════════════════════════════════════════════════════
    // lit cache-query
    // ═════════════════════════════════════════════════════════

    static Command CreateCacheQuery(Option<bool> jsonOpt)
    {
        var titleOpt = new Option<string?>("--title"); var authorOpt = new Option<string?>("--author");
        var kwOpt = new Option<string?>("--keyword"); var venueOpt = new Option<string?>("--venue");
        var minYr = new Option<int?>("--min-year"); var maxYr = new Option<int?>("--max-year");
        var minCite = new Option<int?>("--min-citations"); var limitOpt = new Option<int>("--limit", () => 20);
        var skipOpt = new Option<int>("--skip", () => 0);
        var cmd = new Command("cache-query", "Query locally cached papers") { titleOpt, authorOpt, kwOpt, venueOpt, minYr, maxYr, minCite, limitOpt, skipOpt };
        cmd.SetHandler((context) =>
        {
            var cv = context.ParseResult;
            using var cache = new LiteratureCache();
            var r = cache.Query(cv.GetValueForOption(titleOpt), cv.GetValueForOption(minYr), cv.GetValueForOption(maxYr),
                cv.GetValueForOption(minCite), cv.GetValueForOption(authorOpt), cv.GetValueForOption(venueOpt),
                cv.GetValueForOption(kwOpt), cv.GetValueForOption(limitOpt), cv.GetValueForOption(skipOpt));
            var o = JsonOutput.Ok("lit cache-query", $"{r.Count} papers", new { count = r.Count, totalInCache = cache.Count(), items = r.Select(x => x.CompactEntry()) });
            Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
        });
        return cmd;
    }

    static Command CreateCacheStats(Option<bool> jsonOpt)
    {
        var cmd = new Command("cache-stats", "Show local cache statistics");
        cmd.SetHandler((bool json) =>
        {
            using var cache = new LiteratureCache();
            var s = cache.GetStats();
            var o = JsonOutput.Ok("lit cache-stats", $"{s.TotalRecords} papers cached", new { s.TotalRecords, s.WithDoi, s.WithAbstract, s.WithPdf, s.YearMin, s.YearMax, s.Sources, s.OldestImport, s.NewestImport, dbFile = Path.Combine(NongWorkplace.Cache, "nong.db") });
            Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
        }, jsonOpt);
        return cmd;
    }

    static Command CreateCacheExport(Option<bool> jsonOpt)
    {
        var limitOpt = new Option<int>("--limit", () => 20); var maxCharsOpt = new Option<int>("--max-chars", () => 8000);
        var outOpt = new Option<string?>("-o");
        var cmd = new Command("cache-export", "Export cached papers as markdown") { limitOpt, maxCharsOpt, outOpt };
        cmd.SetHandler((int limit, int maxChars, string? outFile, bool json) =>
        {
            using var cache = new LiteratureCache();
            var md = cache.ExportMarkdown(limit: limit, maxChars: maxChars);
            if (outFile != null) File.WriteAllText(outFile, md);
            if (!json) Console.WriteLine(md);
            else { var o = JsonOutput.Ok("lit cache-export", $"{md.Length} chars", new { chars = md.Length, preview = md[..Math.Min(md.Length, 500)], file = outFile }); Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts)); }
        }, limitOpt, maxCharsOpt, outOpt, jsonOpt);
        return cmd;
    }

    // ═════════════════════════════════════════════════════════
    // lit word — direct DB → DocxTemplate, no JSON file
    // ═════════════════════════════════════════════════════════

    static Command CreateWord(Option<bool> jsonOpt)
    {
        var dslOpt = new Option<string?>("--dsl", "CNKI DSL filter (optional — without it, exports newest cached paper)");
        var modeOpt = new Option<string>("--mode", () => "strict", "strict | recall");
        var limitOpt = new Option<int>("--limit", () => 1, "Papers (1=single detailed, >1=list)");
        var templateOpt = new Option<string>("--template", "Path to .docx template") { IsRequired = true };
        var outOpt = new Option<string>("-o", () => "filled.docx", "Output docx path");

        var cmd = new Command("word", "Fill Word template directly from literature cache (no JSON file)") { dslOpt, modeOpt, limitOpt, templateOpt, outOpt };
        cmd.SetHandler((string? dsl, string mode, int limit, string template, string output, bool json) =>
        {
            if (!File.Exists(template)) { Console.Error.WriteLine($"Template not found: {template}"); return; }

            output = NongWorkplace.ResolveOutput(output);

            using var cache = new LiteratureCache();
            // CachedPaper.ToDocxData() returns Dictionary DocxTemplate consumes directly — zero serialization
            var data = limit == 1
                ? cache.AsDocxData(dsl, mode, 1)
                : cache.AsDocxList(dsl, mode, Math.Clamp(limit, 2, 50));

            DocxTemplate.Fill(template, output, data);

            var o = JsonOutput.Ok("lit word", $"Filled {output}", new { template, output, limit, dsl, hasFilters = dsl != null });
            o.Artifacts["docx"] = output;
            Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
        }, dslOpt, modeOpt, limitOpt, templateOpt, outOpt, jsonOpt);
        return cmd;
    }

    // ═════════════════════════════════════════════════════════
    // helpers
    // ═════════════════════════════════════════════════════════

    static Option<string> Q() => new(new[] { "--query", "-q" }, "CNKI-like literature query") { IsRequired = true };
    static Option<string> SourcesOption() => new("--sources", () => "openalex,crossref,unpaywall", "Comma-separated sources");
    static IReadOnlyList<string> ParseSources(string s) => s.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
    static RankProfile parseProfile(string p) => p.ToLowerInvariant() switch { "recent" => RankProfile.Recent, "classic" => RankProfile.Classic, _ => RankProfile.Balanced };

    static PaperRecord ParseRecord(JsonElement e) => new()
    {
        Title = S(e, "title"), Doi = S(e, "doi"), Year = I(e, "year"),
        CitationCount = I(e, "citationCount"), Venue = S(e, "venue"), Journal = S(e, "journal"),
        Publisher = S(e, "publisher"), Abstract = S(e, "abstract"), PdfUrl = S(e, "pdfUrl"),
        LandingPageUrl = S(e, "landingPageUrl"), IsOpenAccess = B(e, "isOpenAccess"),
        OpenAccessStatus = S(e, "openAccessStatus"),
        Keywords = L(e, "keywords"), Authors = L(e, "authors"), RetrievedFrom = L(e, "retrievedFrom"),
    };

    static string? S(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    static int? I(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind != JsonValueKind.Null && v.TryGetInt32(out var n) ? n : null;
    static bool? B(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.True ? true : v.ValueKind == JsonValueKind.False ? false : null;
    static List<string> L(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().Select(x => x.GetString()).Where(x => x != null).Select(x => x!).ToList() : new List<string>();
}

file static class StringExt { public static string Truncate(this string s, int max) => s.Length <= max ? s : s[..(max - 3)] + "..."; }
