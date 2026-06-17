using System.CommandLine;
using System.Text.Json;
using Angri450.Nong;
using Nong.Cli.Common;

namespace Nong.Cli.Commands;

/// <summary>nongcli command group: init, where.</summary>
public static class NongCliCommands
{
    public static Command Create(Option<bool> jsonOpt)
    {
        var cmd = new Command("nongcli", "Project-level workspace management");

        cmd.AddCommand(CreateInit(jsonOpt));
        cmd.AddCommand(CreateWhere(jsonOpt));
        cmd.AddCommand(CreateInstallEmbedding(jsonOpt));

        return cmd;
    }

    static Command CreateInstallEmbedding(Option<bool> jsonOpt)
    {
        var cmd = new Command("install-embedding", "Install jina-embeddings-v5-text-nano ONNX model for semantic search");
        cmd.SetHandler((bool json) =>
        {
            try
            {
                var modelDir = Path.Combine(NongWorkplace.Dir, "models", "jina-v5-nano");
                var onnxPath = Path.Combine(modelDir, "model.onnx");
                var tokPath = Path.Combine(modelDir, "tokenizer.json");

                if (File.Exists(onnxPath) && File.Exists(tokPath))
                {
                    var msg = $"Model already installed at {modelDir}";
                    if (json)
                    {
                        var oj = JsonOutput.Ok("nongcli install-embedding", msg,
                            new { path = modelDir, ready = true });
                        Console.WriteLine(JsonSerializer.Serialize(oj, CliHelpers.JsonOpts));
                    }
                    else
                    {
                        Console.WriteLine(msg);
                        Console.WriteLine("  nong search is ready to use.");
                    }
                    return;
                }

                // Model not installed — print instructions
                Directory.CreateDirectory(modelDir);

                var modelDirForward = modelDir.Replace('\\', '/');
                var releaseUrl = "https://github.com/angri450/Nong.Cli.Net/releases/download/model-v5-nano";
                var instructions = string.Join("\n",
                    $"Embedding model not found at: {modelDir}",
                    "",
                    "One-time install (choose one):",
                    "",
                    "  Option A — Download pre-built model (~434 MB):",
                    $"    curl -L {releaseUrl}/model.onnx -o {modelDirForward}/model.onnx",
                    $"    curl -L {releaseUrl}/tokenizer.json -o {modelDirForward}/tokenizer.json",
                    "",
                    "  Option B — Build from source (requires Python):",
                    "    1. git clone --depth 1 https://www.modelscope.cn/jinaai/jina-embeddings-v5-text-nano.git",
                    "    2. pip install sentence-transformers peft onnx onnxruntime",
                    "    3. Run the export script from nong docs",
                    "",
                    $"  Needed files in {modelDir}:",
                    "    - model.onnx      (~417 MB, FP16 ONNX with retrieval adapter)",
                    "    - tokenizer.json  (~17 MB)",
                    "",
                    "After install, run: nong search 'your query'");

                if (json)
                {
                    var data = new Dictionary<string, object?>
                    {
                        ["path"] = modelDir,
                        ["ready"] = false,
                        ["instructions"] = instructions,
                    };
                    var oj = JsonOutput.Ok("nongcli install-embedding",
                        "Model not installed. Follow instructions to install.", data);
                    Console.WriteLine(JsonSerializer.Serialize(oj, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine(instructions);
                }
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("nongcli install-embedding",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
            }
        }, jsonOpt);
        return cmd;
    }

    static Command CreateInit(Option<bool> jsonOpt)
    {
        var pathArg = new Argument<string?>("path", () => null, "Target directory (default: current directory)");
        var cmd = new Command("init", "Create .nong/ workspace skeleton") { pathArg };
        cmd.SetHandler((string? path, bool json) =>
        {
            try
            {
                var target = path != null ? Path.GetFullPath(path) : Directory.GetCurrentDirectory();
                var nongDir = Path.Combine(target, ".nong");

                if (Directory.Exists(nongDir))
                {
                    if (json)
                    {
                        var oj = JsonOutput.Ok("nongcli init",
                            $".nong/ already exists at {nongDir}",
                            new { path = nongDir, alreadyExists = true });
                        Console.WriteLine(JsonSerializer.Serialize(oj, CliHelpers.JsonOpts));
                    }
                    else
                    {
                        Console.WriteLine($".nong/ already exists at {nongDir}");
                    }
                    return;
                }

                Directory.CreateDirectory(nongDir);
                Directory.CreateDirectory(Path.Combine(nongDir, "cache"));
                Directory.CreateDirectory(Path.Combine(nongDir, "output"));

                if (json)
                {
                    var oj = JsonOutput.Ok("nongcli init",
                        $"Created .nong/ at {nongDir}",
                        new { path = nongDir, created = true });
                    Console.WriteLine(JsonSerializer.Serialize(oj, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"Created .nong/ at {nongDir}");
                    Console.WriteLine("  cache/");
                    Console.WriteLine("  output/");
                }
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("nongcli init",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
            }
        }, pathArg, jsonOpt);
        return cmd;
    }

    static Command CreateWhere(Option<bool> jsonOpt)
    {
        var cmd = new Command("where", "Print the resolved NongWorkplace root path");
        cmd.SetHandler((bool json) =>
        {
            try
            {
                var root = NongWorkplace.Dir;
                var found = NongWorkplace.FindProjectRoot();

                if (json)
                {
                    var obj = new Dictionary<string, object?>
                    {
                        ["root"] = root,
                        ["cache"] = NongWorkplace.Cache,
                        ["output"] = NongWorkplace.Output,
                        ["resolvedVia"] = found != null ? "project" : "fallback"
                    };
                    var oj = JsonOutput.Ok("nongcli where", $"Workplace: {root}", obj);
                    Console.WriteLine(JsonSerializer.Serialize(oj, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"Workplace: {root}");
                    Console.WriteLine($"  Cache : {NongWorkplace.Cache}");
                    Console.WriteLine($"  Output: {NongWorkplace.Output}");
                    if (found != null)
                        Console.WriteLine($"  (found via project .nong/ at {found})");
                    else
                        Console.WriteLine("  (fallback: created in current directory)");
                }
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("nongcli where",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
            }
        }, jsonOpt);
        return cmd;
    }
}
