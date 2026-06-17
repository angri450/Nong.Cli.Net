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
        var cmd = new Command("search", "Semantic search across document blocks");

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
                var queryVec = engine.Embed(query);

                // Read all blocks from NongDb
                using var ctx = new IngestionContext();
                var allDocs = ctx.QueryDocuments(format);
                var allResults = new List<(string DocName, string BlockId, string BlockType, string Text, float Score)>();

                foreach (var doc in allDocs)
                {
                    var blocks = ctx.QueryBlocks(doc.Id.ToString());
                    foreach (var block in blocks)
                    {
                        if (string.IsNullOrWhiteSpace(block.Text)) continue;
                        var blockVec = engine.Embed(block.Text);
                        var score = EmbeddingEngine.Cosine(queryVec, blockVec);
                        allResults.Add((doc.FileName, block.BlockId ?? block.Id.ToString(),
                            block.BlockType, block.Text, score));
                    }
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
                        // Truncate long text
                        var displayText = r.Text.Length > 300
                            ? r.Text[..297] + "..."
                            : r.Text;
                        Console.WriteLine($"  {displayText}");
                        Console.WriteLine();
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
