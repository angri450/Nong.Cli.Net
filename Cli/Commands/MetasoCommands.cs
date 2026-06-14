using System.CommandLine;
using System.Text.Json;
using Angri450.Nong.Metaso;
using Nong.Cli.Common;

namespace Nong.Cli.Commands;

public static class MetasoCommands
{
    public static Command Create(Option<bool> jsonOpt)
    {
        var cmd = new Command("metaso", "Metaso AI Search — search, reader, chat (RAG)");
        cmd.AddCommand(CreateSearch(jsonOpt));
        cmd.AddCommand(CreateReader(jsonOpt));
        cmd.AddCommand(CreateChat(jsonOpt));
        return cmd;
    }

    // ── metaso search ──

    static Command CreateSearch(Option<bool> jsonOpt)
    {
        var queryOpt = new Option<string>(new[] { "--query", "-q" }, "Search query") { IsRequired = true };
        var scopeOpt = new Option<string>("--scope", () => "scholar",
            "Search scope: webpage | document | scholar | image | video | podcast");
        var sizeOpt = new Option<int>("--size", () => 10, "Result count (max 50)");
        var summaryOpt = new Option<bool>("--summary", () => false, "Include AI-generated summary of results");
        var conciseOpt = new Option<bool>("--concise", () => true, "Return concise snippet text");

        var cmd = new Command("search", "Metaso search API — multi-scope web + academic") { queryOpt, scopeOpt, sizeOpt, summaryOpt, conciseOpt };
        cmd.SetHandler(async (string q, string scope, int size, bool summary, bool concise, bool json) =>
        {
            var client = new MetasoClient();
            var result = await client.SearchAsync(q, scope, Math.Clamp(size, 1, 50), summary, concise);
            var output = JsonOutput.Ok("metaso search", $"{result.Total} results (scope={scope})", new
            {
                result.Success, result.Total, result.Credits, result.Summary,
                errorCode = result.ErrorCode, errorMessage = result.ErrorMessage,
                items = result.Items.Select(i => new
                {
                    i.Title, i.Link, i.Snippet, i.Date, i.Year,
                    i.Authors, i.Keywords, i.Thumbnail, i.Source,
                    display = i.Display
                })
            });
            output.Metrics["items"] = result.Items.Count;
            output.Metrics["credits"] = result.Credits;
            Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
        }, queryOpt, scopeOpt, sizeOpt, summaryOpt, conciseOpt, jsonOpt);
        return cmd;
    }

    // ── metaso reader ──

    static Command CreateReader(Option<bool> jsonOpt)
    {
        var urlOpt = new Option<string>("--url", "URL to fetch") { IsRequired = true };
        var formatOpt = new Option<string>("--format", () => "json",
            "Output format: json (structured) | markdown (clean text)");
        var outOpt = new Option<string?>("-o", "Save content to file");

        var cmd = new Command("reader", "Fetch web page content — JSON or Markdown") { urlOpt, formatOpt, outOpt };
        cmd.SetHandler(async (string url, string format, string? outFile, bool json) =>
        {
            var client = new MetasoClient();
            var result = await client.ReadAsync(url, format);
            var preview = result.Content?[..Math.Min(result.Content?.Length ?? 0, 2000)] ?? "";

            if (outFile != null && result.Success && result.Content != null)
                await File.WriteAllTextAsync(outFile, result.Content, System.Text.Encoding.UTF8);

            var output = JsonOutput.Ok("metaso reader", outFile != null ? $"Saved to {outFile}" : $"Page fetched ({result.RawLength} chars)", new
            {
                result.Success, result.Format, result.Title,
                contentLength = result.RawLength,
                errorCode = result.ErrorCode, errorMessage = result.ErrorMessage,
                preview
            });
            if (outFile != null) output.Artifacts["file"] = outFile;
            Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
        }, urlOpt, formatOpt, outOpt, jsonOpt);
        return cmd;
    }

    // ── metaso chat (RAG) ──

    static Command CreateChat(Option<bool> jsonOpt)
    {
        var queryOpt = new Option<string>(new[] { "--query", "-q" }, "Question to research") { IsRequired = true };
        var modelOpt = new Option<string>("--model", () => "fast_thinking",
            "Model: fast | fast_thinking | ds-r1 (+ -scholar suffix for academic scope)");
        var scopeOpt = new Option<string>("--scope", () => "scholar", "Search scope: scholar | webpage");
        var streamOpt = new Option<bool>("--stream", () => false, "Enable SSE streaming output (real-time)");
        var conciseOpt = new Option<bool>("--concise", () => true, "Return concise original-text match snippets");
        var outOpt = new Option<string?>("-o", "Save answer to file");

        var cmd = new Command("chat", "Metaso RAG — AI-powered research with citations") { queryOpt, modelOpt, scopeOpt, streamOpt, conciseOpt, outOpt };
        cmd.SetHandler(async (string q, string model, string scope, bool stream, bool concise, string? outFile, bool json) =>
        {
            var client = new MetasoClient();

            if (stream)
            {
                // Streaming mode: print chunks as they arrive
                Console.Error.WriteLine($"[streaming {model}/{scope}]");
                var answer = new System.Text.StringBuilder();

                var result = await client.ChatAsync(q, model, scope, stream: true, concise,
                    onChunk: chunk =>
                    {
                        Console.Write(chunk);
                        answer.Append(chunk);
                        return Task.CompletedTask;
                    });

                Console.Error.WriteLine($"\n[stream finished — {answer.Length} chars]");

                if (outFile != null && result.Success)
                    await File.WriteAllTextAsync(outFile, answer.ToString(), System.Text.Encoding.UTF8);

                if (json)
                {
                    var output = JsonOutput.Ok("metaso chat", $"Answer ({answer.Length} chars)", new
                    {
                        result.Success, result.Model, result.Id, result.Streamed,
                        errorCode = result.ErrorCode, errorMessage = result.ErrorMessage,
                        answer = answer.ToString()
                    });
                    if (outFile != null) output.Artifacts["file"] = outFile;
                    Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                }
            }
            else
            {
                var result = await client.ChatAsync(q, model, scope);

                if (outFile != null && result.Success)
                    await File.WriteAllTextAsync(outFile, result.Answer, System.Text.Encoding.UTF8);

                var output = JsonOutput.Ok("metaso chat", "RAG answer", new
                {
                    result.Success, result.Model, result.Id, result.Streamed,
                    errorCode = result.ErrorCode, errorMessage = result.ErrorMessage,
                    answer = result.Answer
                });
                if (outFile != null) output.Artifacts["file"] = outFile;
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
        }, queryOpt, modelOpt, scopeOpt, streamOpt, conciseOpt, outOpt, jsonOpt);
        return cmd;
    }
}
