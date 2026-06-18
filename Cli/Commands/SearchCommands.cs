using System.CommandLine;
using System.Text.Json;
using Angri450.Nong;
using Angri450.Nong.Data;
using Nong.Cli.Common;

namespace Nong.Cli.Commands;

/// <summary>
/// nong search command — semantic vector search over all ingested document blocks.
/// Uses jina-embeddings-v5-nano ONNX model for local CPU inference.
/// </summary>
public static class SearchCommands
{
    public static Command Create(Option<bool> jsonOpt)
    {
        var cmd = new Command("search", "Semantic search across ingested document blocks. Sources must be ingested first with --ingest flag.");

        var queryArg = new Argument<string>("query", "Search query text");
        var limitOpt = new Option<int>("--limit", () => 5, "Max results (1-20)");
        var formatOpt = new Option<string?>("--format", "Filter by document format: docx, pdf, xlsx, pptx");
        var scoresOpt = new Option<bool>("--scores", () => false, "Include similarity scores");

        cmd.AddArgument(queryArg);
        cmd.AddOption(limitOpt);
        cmd.AddOption(formatOpt);
        cmd.AddOption(scoresOpt);

        cmd.SetHandler((string query, int limit, string? format, bool scores, bool json) =>
        {
            try
            {
                limit = Math.Clamp(limit, 1, 20);

                // Detect model path
                var modelDir = ResolveModelDir();
                if (!Directory.Exists(modelDir))
                {
                    var msg = $"Embedding model not found at {modelDir}. Run 'nong nongcli install-embedding' first.";
                    if (json)
                        CliHelpers.WriteError("search", ErrorCodes.DependencyMissing with { Message = msg }, json: true);
                    else
                        Console.Error.WriteLine(msg);
                    return;
                }

                var sw = System.Diagnostics.Stopwatch.StartNew();

                // Load engine
                using var engine = new EmbeddingEngine(modelDir);

                // Get query embedding
                var queryVec = engine.EmbedQuery(query);

                // Read all blocks from NongDb (both dissected docs and --ingest results)
                using var ctx = new IngestionContext();
                var allResults = new List<(string DocName, string BlockId, string BlockType, string Text, float Score)>();

                // Query via both paths: registered documents + virtual ingest documents
                var allDocs = ctx.QueryDocuments(format);
                var docIds = new HashSet<string>();

                foreach (var doc in allDocs)
                {
                    docIds.Add(doc.Id.ToString());
                    var blocks = ctx.QueryBlocks(doc.Id.ToString());
                    foreach (var block in blocks)
                    {
                        if (string.IsNullOrWhiteSpace(block.Text)) continue;
                        var blockVec = engine.EmbedPassage(block.Text);
                        var score = EmbeddingEngine.Cosine(queryVec, blockVec);
                        allResults.Add((doc.FileName, block.BlockId ?? block.Id.ToString(),
                            block.BlockType, block.Text, score));
                    }
                }

                // Also search blocks from --ingest (non-document sources like lit/aminer/metaso/inspect)
                var allBlocks = ctx.Db.Blocks.FindAll().ToList();
                foreach (var block in allBlocks)
                {
                    if (docIds.Contains(block.DocumentId)) continue; // Already processed
                    if (string.IsNullOrWhiteSpace(block.Text)) continue;
                    if (format != null && !string.Equals(format, block.BlockType, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(format, "docx", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
                        continue;
                    var blockVec = engine.EmbedPassage(block.Text);
                    var score = EmbeddingEngine.Cosine(queryVec, blockVec);
                    allResults.Add((block.DocumentId, block.BlockId ?? block.Id.ToString(),
                        block.BlockType, block.Text, score));
                }

                // Sort and take top K
                allResults.Sort((a, b) => b.Score.CompareTo(a.Score));
                var topK = allResults.Take(limit).ToList();

                sw.Stop();

                if (json)
                {
                    var items = topK.Select(r =>
                    {
                        var item = new Dictionary<string, object?>
                        {
                            ["source"] = r.DocName,
                            ["blockId"] = r.BlockId,
                            ["type"] = r.BlockType,
                            ["text"] = r.Text,
                        };
                        if (scores) item["score"] = MathF.Round(r.Score, 4);
                        return item;
                    }).ToList();

                    var data = new Dictionary<string, object?>
                    {
                        ["query"] = query,
                        ["count"] = items.Count,
                        ["items"] = items,
                    };
                    var o = JsonOutput.Ok("search",
                        $"{items.Count} results in {sw.ElapsedMilliseconds}ms", data);
                    o.Metrics["totalBlocks"] = allResults.Count;
                    o.Metrics["durationMs"] = sw.ElapsedMilliseconds;
                    Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"{topK.Count} results ({sw.ElapsedMilliseconds}ms):");
                    Console.WriteLine();
                    for (int i = 0; i < topK.Count; i++)
                    {
                        var r = topK[i];
                        var prefix = scores
                            ? $"[{i + 1}] [{r.Score:F4}] {r.DocName} / {r.BlockType}"
                            : $"[{i + 1}] {r.DocName} / {r.BlockType}";
                        Console.WriteLine(prefix);
                        var displayText = r.Text.Length > 300
                            ? r.Text[..297] + "..."
                            : r.Text;
                        Console.WriteLine($"  {displayText}");
                        Console.WriteLine();
                    }
                }

                // Hint when empty
                if (topK.Count == 0 && allResults.Count == 0)
                {
                    var hint = "No documents ingested. First run:\n" +
                               "  nong word dissect paper.docx -o slice --ingest\n" +
                               "  nong pdf dissect paper.pdf -o slice --ingest\n" +
                               "  nong excel dissect data.xlsx -o slice --ingest\n" +
                               "  nong pptx dissect slides.pptx -o slice --ingest\n" +
                               "  nong inspect diagnose paper.txt --ingest\n" +
                               "  nong lit search \"query\" --ingest\n" +
                               "  nong ocr local image.png --ingest";
                    if (json)
                    {
                        var issues = new List<Issue> { new() { Id = "empty_index", Severity = "Info", Message = hint } };
                        Console.Error.WriteLine(JsonSerializer.Serialize(new { status = "ok", command = "search", summary = "0 results", data = new { count = 0, items = Array.Empty<object>() }, issues, artifacts = new { }, metrics = new { totalBlocks = 0, durationMs = sw.ElapsedMilliseconds }, errors = Array.Empty<object>(), meta = new { durationMs = 0, version = "4.5.0" } }, CliHelpers.JsonOpts));
                    }
                    else
                    {
                        Console.Error.WriteLine(hint);
                    }
                }
            }
            catch (FileNotFoundException ex)
            {
                CliHelpers.WriteError("search",
                    ErrorCodes.DependencyMissing with { Message = ex.Message }, json);
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("search",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
            }
        }, queryArg, limitOpt, formatOpt, scoresOpt, jsonOpt);

        return cmd;
    }

    static string ResolveModelDir()
    {
        var root = NongWorkplace.Dir;
        return Path.Combine(root, "models", "jina-v5-nano");
    }
}
