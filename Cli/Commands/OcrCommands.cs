using System.CommandLine;
using System.Text.Json;
using MultiModalCore;
using Nong.Cli.Common;

namespace Nong.Cli.Commands
{

public static class OcrCommands
{
    public static Command Create(Option<bool> jsonOpt)
    {
        var cmd = new Command("ocr", "OCR operations");
        cmd.AddCommand(CreateCloud(jsonOpt));
        cmd.AddCommand(CreateLocal(jsonOpt));
        cmd.AddCommand(CreateToWord(jsonOpt));
        cmd.AddCommand(CreateCheckEnv(jsonOpt));
        cmd.AddCommand(CreateAnalyzeImage(jsonOpt));
        cmd.AddCommand(CreateModels(jsonOpt));
        cmd.AddCommand(CreateInstallModel(jsonOpt));
        return cmd;
    }

    // ===== ocr cloud =====

    static Command CreateCloud(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to image or PDF file");
        var outOpt = new Option<string>("-o", "Output directory") { IsRequired = true };
        var cmd = new Command("cloud", "Cloud PaddleOCR-VL-1.6 via PADDLEOCR_ACCESS_TOKEN") { fileArg, outOpt };

        cmd.SetHandler((string file, string outputDir, bool json) =>
        {
            if (string.IsNullOrWhiteSpace(file))
            {
                CliHelpers.WriteError("ocr cloud", ErrorCodes.MissingArgument with { Message = "File path is required." }, json);
                return;
            }
            if (!File.Exists(file))
            {
                CliHelpers.WriteError("ocr cloud", ErrorCodes.FileNotFound with { Message = $"File not found: {file}" }, json);
                return;
            }

            var token = ResolveCloudToken();
            if (token == null)
            {
                CliHelpers.WriteError("ocr cloud",
                    ErrorCodes.DependencyMissing with { Message = "PaddleOCR access token not found. Set PADDLEOCR_ACCESS_TOKEN environment variable." }, json);
                return;
            }

            try
            {
                CliHelpers.EnsureParentDir(Path.Combine(outputDir, ".keep"));

                using var client = new PaddleOcrVlClient(token);

                var (ocrResult, elapsed) = CliHelpers.Time(() =>
                {
                    // Use structured download for richer output
                    var task = ProcessStructuredAsync(client, file, outputDir);
                    task.Wait();
                    return task.Result;
                });

                if (json)
                {
                    var data = new
                    {
                        pages = ocrResult.Pages.Count,
                        blocks = ocrResult.Pages.Sum(p => p.Blocks.Count),
                        pageDetails = ocrResult.Pages.Select(p => new
                        {
                            page = p.PageNumber,
                            width = p.Width,
                            height = p.Height,
                            blockCount = p.Blocks.Count,
                            blocks = p.Blocks.Select(b => new
                            {
                                label = b.BlockLabel,
                                content = b.BlockContent.Length > 200 ? b.BlockContent[..200] : b.BlockContent,
                                bbox = b.BlockBbox
                            })
                        })
                    };
                    var output = JsonOutput.Ok("ocr cloud",
                        $"OCR complete: {ocrResult.Pages.Count} pages, {ocrResult.Pages.Sum(p => p.Blocks.Count)} blocks",
                        data);
                    output.Artifacts["dir"] = Path.GetFullPath(outputDir);
                    output.Metrics["pages"] = ocrResult.Pages.Count;
                    output.Metrics["blocks"] = ocrResult.Pages.Sum(p => p.Blocks.Count);
                    output.Meta.DurationMs = elapsed;

                    // Deprecation warning for old token env var
                    if (Environment.GetEnvironmentVariable("PADDLEOCR_TOKEN") != null &&
                        Environment.GetEnvironmentVariable("PADDLEOCR_ACCESS_TOKEN") == null)
                    {
                        output.Issues.Add(new Issue
                        {
                            Id = "deprecated_token",
                            Severity = "Warning",
                            Message = "PADDLEOCR_TOKEN is deprecated, use PADDLEOCR_ACCESS_TOKEN instead."
                        });
                    }

                    Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"OK: {ocrResult.Pages.Count} pages saved to {Path.GetFullPath(outputDir)}");
                    foreach (var p in ocrResult.Pages)
                        Console.WriteLine($"  Page {p.PageNumber}: {p.Blocks.Count} blocks");
                }
            }
            catch (AggregateException ae) when (ae.InnerException != null)
            {
                WriteCloudError(ae.InnerException, json);
            }
            catch (Exception ex)
            {
                WriteCloudError(ex, json);
            }

        }, fileArg, outOpt, jsonOpt);

        return cmd;
    }

    static string? ResolveCloudToken()
    {
        var accessToken = Environment.GetEnvironmentVariable("PADDLEOCR_ACCESS_TOKEN");
        if (!string.IsNullOrWhiteSpace(accessToken))
            return accessToken;
        return Environment.GetEnvironmentVariable("PADDLEOCR_TOKEN");
    }

    static void WriteCloudError(Exception ex, bool json)
    {
        var msg = ex.Message;
        // Don't leak token in error messages
        if (msg.Contains("bearer ", StringComparison.OrdinalIgnoreCase))
            msg = "Authentication failed. Check your PADDLEOCR_ACCESS_TOKEN.";

        if (msg.Contains("401") || msg.Contains("403"))
        {
            CliHelpers.WriteError("ocr cloud",
                ErrorCodes.DependencyMissing with { Message = "Authentication failed. PADDLEOCR_ACCESS_TOKEN is invalid or expired." }, json);
        }
        else if (msg.Contains("429"))
        {
            var output = new JsonOutput
            {
                Status = "error",
                Command = "ocr cloud",
                Summary = "Rate limited",
                Meta = new MetaInfo { Version = "3.1.0" }
            };
            output.Errors.Add(ErrorCodes.DependencyMissing with { Message = "PaddleOCR API rate limit reached. Retry later." });
            output.Issues.Add(new Issue { Id = "rate_limited", Severity = "Warning", Message = "HTTP 429: rate limited" });
            Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            Environment.ExitCode = 1;
        }
        else if (msg.Contains("503") || msg.Contains("504"))
        {
            var output = new JsonOutput
            {
                Status = "error",
                Command = "ocr cloud",
                Summary = "Service unavailable",
                Meta = new MetaInfo { Version = "3.1.0" }
            };
            output.Errors.Add(ErrorCodes.DependencyMissing with { Message = "PaddleOCR API service unavailable. Retry later." });
            output.Issues.Add(new Issue { Id = "service_unavailable", Severity = "Warning", Message = "HTTP 503/504: service unavailable" });
            Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            Environment.ExitCode = 1;
        }
        else if (msg.Contains("400"))
        {
            CliHelpers.WriteError("ocr cloud",
                ErrorCodes.ValidationFailed with { Message = $"Invalid request: {msg}" }, json);
        }
        else if (ex is IOException)
        {
            CliHelpers.WriteError("ocr cloud",
                ErrorCodes.WriteFailed with { Message = $"Failed to write output: {msg}" }, json);
        }
        else
        {
            CliHelpers.WriteError("ocr cloud",
                ErrorCodes.InternalError with { Message = $"PaddleOCR API failed: {msg}" }, json);
        }
    }

    static async Task<OcrResult> ProcessStructuredAsync(PaddleOcrVlClient client, string file, string outputDir)
    {
        var kind = ClassifyInput(file);
        var jobId = kind switch
        {
            "url" => await client.SubmitUrlAsync(file),
            _ => await client.SubmitFileAsync(file)
        };
        var resultUrl = await client.WaitForJobAsync(jobId, TimeSpan.FromSeconds(5));
        return await client.DownloadResultsStructuredAsync(resultUrl, outputDir);
    }

    static string ClassifyInput(string input)
    {
        if (input.StartsWith("http://") || input.StartsWith("https://"))
            return "url";
        return "localFile";
    }

    // ===== ocr local =====

    static Command CreateLocal(Option<bool> jsonOpt)
    {
        var imageArg = new Argument<string>("image", "Path to image file");
        var cmd = new Command("local", "Local PP-OCRv5 text recognition") { imageArg };

        cmd.SetHandler((string image, bool json) =>
        {
            var cachePath = GetPpOcrV5ModelCachePath();

            if (Directory.Exists(cachePath) && File.Exists(Path.Combine(cachePath, "manifest.json")))
            {
                CliHelpers.WriteError("ocr local",
                    ErrorCodes.NotImplemented with { Message = "PP-OCRv5 ONNX inference not yet implemented." }, json);
            }
            else
            {
                var msg = $"PP-OCRv5 model not found at {cachePath}. "
                    + "Run 'nong ocr install-model pp-ocrv5-mobile' to download, "
                    + "or use 'nong ocr cloud' for cloud-based OCR with PADDLEOCR_ACCESS_TOKEN.";
                CliHelpers.WriteError("ocr local",
                    ErrorCodes.DependencyMissing with { Message = msg }, json);
            }
        }, imageArg, jsonOpt);

        return cmd;
    }

    // ===== ocr to-word =====

    static Command CreateToWord(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to image or PDF file");
        var outOpt = new Option<string>("-o", "Output .docx path") { IsRequired = true };
        var pagesOpt = new Option<string>("--pages", "Page range (e.g. \"1-5,10\")");
        var cmd = new Command("to-word", "Convert image/PDF to Word document via PaddleOCR-VL-1.6") { fileArg, outOpt, pagesOpt };

        cmd.SetHandler((string file, string outputPath, string? pages, bool json) =>
        {
            if (string.IsNullOrWhiteSpace(file))
            {
                CliHelpers.WriteError("ocr to-word", ErrorCodes.MissingArgument with { Message = "File path is required." }, json);
                return;
            }
            if (!File.Exists(file))
            {
                CliHelpers.WriteError("ocr to-word", ErrorCodes.FileNotFound with { Message = $"File not found: {file}" }, json);
                return;
            }

            var token = ResolveCloudToken();
            if (token == null)
            {
                CliHelpers.WriteError("ocr to-word",
                    ErrorCodes.DependencyMissing with { Message = "PaddleOCR access token not found. Set PADDLEOCR_ACCESS_TOKEN environment variable." }, json);
                return;
            }

            try
            {
                CliHelpers.EnsureParentDir(Path.GetFullPath(outputPath));

                using var client = new PaddleOcrVlClient(token);

                var (docxPath, elapsed) = CliHelpers.Time(() =>
                {
                    var task = client.ProcessToWordAsync(file, Path.GetFullPath(outputPath));
                    task.Wait();
                    return task.Result;
                });

                var aerr = CliHelpers.CheckArtifact(docxPath, "DOCX");
                if (aerr != null) { CliHelpers.WriteError("ocr to-word", aerr, json); return; }

                if (json)
                {
                    var output = JsonOutput.Ok("ocr to-word",
                        $"Word document created: {docxPath}");
                    output.Artifacts["docx"] = Path.GetFullPath(docxPath);
                    output.Meta.DurationMs = elapsed;

                    if (Environment.GetEnvironmentVariable("PADDLEOCR_TOKEN") != null &&
                        Environment.GetEnvironmentVariable("PADDLEOCR_ACCESS_TOKEN") == null)
                    {
                        output.Issues.Add(new Issue
                        {
                            Id = "deprecated_token",
                            Severity = "Warning",
                            Message = "PADDLEOCR_TOKEN is deprecated, use PADDLEOCR_ACCESS_TOKEN instead."
                        });
                    }

                    Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"OK: Word document saved to {Path.GetFullPath(docxPath)}");
                }
            }
            catch (AggregateException ae) when (ae.InnerException != null)
            {
                WriteToWordError(ae.InnerException, json);
            }
            catch (Exception ex)
            {
                WriteToWordError(ex, json);
            }

        }, fileArg, outOpt, pagesOpt, jsonOpt);

        return cmd;
    }

    static void WriteToWordError(Exception ex, bool json)
    {
        var msg = ex.Message;
        if (msg.Contains("bearer ", StringComparison.OrdinalIgnoreCase))
            msg = "Authentication failed. Check your PADDLEOCR_ACCESS_TOKEN.";

        if (msg.Contains("401") || msg.Contains("403"))
        {
            CliHelpers.WriteError("ocr to-word",
                ErrorCodes.DependencyMissing with { Message = "Authentication failed. PADDLEOCR_ACCESS_TOKEN is invalid or expired." }, json);
        }
        else if (ex is IOException)
        {
            CliHelpers.WriteError("ocr to-word",
                ErrorCodes.WriteFailed with { Message = $"Failed to write output: {msg}" }, json);
        }
        else
        {
            CliHelpers.WriteError("ocr to-word",
                ErrorCodes.InternalError with { Message = $"OCR to Word failed: {msg}" }, json);
        }
    }

    // ===== ocr check-env =====

    static Command CreateCheckEnv(Option<bool> jsonOpt)
    {
        var cmd = new Command("check-env", "Check OCR environment status");

        cmd.SetHandler((bool json) =>
        {
            // ImageAnalyzer: always available (pure .NET + SkiaSharp)
            var imageAnalyzerOk = true;

            // Cloud token
            var accessToken = Environment.GetEnvironmentVariable("PADDLEOCR_ACCESS_TOKEN");
            var legacyToken = Environment.GetEnvironmentVariable("PADDLEOCR_TOKEN");
            string tokenStatus;
            if (!string.IsNullOrWhiteSpace(accessToken))
                tokenStatus = "set";
            else if (!string.IsNullOrWhiteSpace(legacyToken))
                tokenStatus = "deprecated";
            else
                tokenStatus = "missing";

            // Local model
            var cacheDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Angri450.Nong", "models", "pp-ocrv5-mobile");
            string modelStatus;
            if (Directory.Exists(cacheDir) && File.Exists(Path.Combine(cacheDir, "manifest.json")))
                modelStatus = "present";
            else
                modelStatus = "missing";

            // Python fallback
            var pythonFallback = "unavailable";

            if (json)
            {
                var data = new
                {
                    imageAnalyzer = imageAnalyzerOk ? "ok" : "error",
                    cloudToken = tokenStatus,
                    localModel = new { ppOcrV5Mobile = modelStatus },
                    pythonFallback
                };
                var output = JsonOutput.Ok("ocr check-env",
                    $"imageAnalyzer={data.imageAnalyzer}, token={tokenStatus}, model={modelStatus}",
                    data);
                output.Metrics["imageAnalyzer"] = imageAnalyzerOk ? 1 : 0;
                output.Metrics["cloudToken"] = tokenStatus == "missing" ? 0 : 1;

                if (tokenStatus == "deprecated")
                {
                    output.Issues.Add(new Issue
                    {
                        Id = "deprecated_token",
                        Severity = "Warning",
                        Message = "PADDLEOCR_TOKEN is deprecated, use PADDLEOCR_ACCESS_TOKEN instead."
                    });
                }

                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            else
            {
                Console.WriteLine($"ImageAnalyzer: {(imageAnalyzerOk ? "ok" : "error")}");
                Console.WriteLine($"Cloud token: {tokenStatus}");
                Console.WriteLine($"Local model (pp-ocrv5-mobile): {modelStatus}");
                Console.WriteLine($"Python fallback: {pythonFallback}");
            }
        }, jsonOpt);

        return cmd;
    }

    // ===== ocr analyze-image =====

    static Command CreateAnalyzeImage(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("image", "Path to image file");
        var outOpt = new Option<string>("-o", "Output directory") { IsRequired = true };
        var cmd = new Command("analyze-image", "Analyze image structure (no OCR, no token required)") { fileArg, outOpt };

        cmd.SetHandler((string image, string outputDir, bool json) =>
        {
            if (string.IsNullOrWhiteSpace(image))
            {
                CliHelpers.WriteError("ocr analyze-image",
                    ErrorCodes.MissingArgument with { Message = "Image path is required." }, json);
                return;
            }
            if (!File.Exists(image))
            {
                CliHelpers.WriteError("ocr analyze-image",
                    ErrorCodes.FileNotFound with { Message = $"Image not found: {image}" }, json);
                return;
            }

            try
            {
                CliHelpers.EnsureParentDir(Path.Combine(outputDir, ".keep"));

                var analyzer = new ImageAnalyzer();
                var (layout, elapsed) = CliHelpers.Time(() => analyzer.Analyze(image));

                var analysisPath = Path.Combine(outputDir, "image-analysis.json");
                var mapPath = Path.Combine(outputDir, "image.map.txt");

                var analysisData = new
                {
                    width = layout.OriginalWidth,
                    height = layout.OriginalHeight,
                    sampleWidth = layout.SampleWidth,
                    sampleHeight = layout.SampleHeight,
                    whitespaceRatio = layout.WhitespaceRatio,
                    blackPixelCount = layout.BlackPixelCount,
                    graphicPixelCount = layout.GraphicPixelCount,
                    edgePixelCount = layout.EdgePixelCount,
                    contentBox = layout.ContentWidth > 0
                        ? new { x = layout.ContentMinX, y = layout.ContentMinY, width = layout.ContentWidth, height = layout.ContentHeight }
                        : null,
                    regions = layout.Regions.Select(r => new
                    {
                        x = r.X, y = r.Y, width = r.Width, height = r.Height,
                        type = r.Type.ToString(), pixelCount = r.PixelCount
                    })
                };
                File.WriteAllText(analysisPath, JsonSerializer.Serialize(analysisData, CliHelpers.JsonOpts));
                File.WriteAllText(mapPath, layout.AsciiMap);

                var aerr = CliHelpers.CheckArtifact(analysisPath, "JSON");
                if (aerr != null) { CliHelpers.WriteError("ocr analyze-image", aerr, json); return; }

                if (json)
                {
                    var output = JsonOutput.Ok("ocr analyze-image",
                        $"Analyzed: {layout.OriginalWidth}x{layout.OriginalHeight}, whitespace={layout.WhitespaceRatio:P0}",
                        analysisData);
                    output.Artifacts["analysisJson"] = Path.GetFullPath(analysisPath);
                    output.Artifacts["asciiMap"] = Path.GetFullPath(mapPath);
                    output.Metrics["whitespaceRatio"] = layout.WhitespaceRatio;
                    output.Metrics["regionCount"] = layout.Regions.Count;
                    output.Meta.DurationMs = elapsed;

                    if (layout.WhitespaceRatio > 0.95)
                        output.Issues.Add(new Issue { Id = "mostly_blank", Severity = "Info", Message = "Image is mostly blank." });

                    Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"{layout.OriginalWidth}x{layout.OriginalHeight}, whitespace={layout.WhitespaceRatio:P0}, {layout.Regions.Count} regions");
                    Console.WriteLine($"Analysis: {analysisPath}");
                    Console.WriteLine($"Map: {mapPath}");
                }
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex.Message.Contains("decode"))
            {
                CliHelpers.WriteError("ocr analyze-image",
                    ErrorCodes.ReadFailed with { Message = $"Cannot decode image: {ex.Message}" }, json);
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("ocr analyze-image",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
            }

        }, fileArg, outOpt, jsonOpt);

        return cmd;
    }

    // ===== ocr models =====

    static Command CreateModels(Option<bool> jsonOpt)
    {
        var cmd = new Command("models", "List installed PP-OCRv5 models");

        cmd.SetHandler((bool json) =>
        {
            var models = new List<object>();
            var cachePath = PpOcrV5ModelResolver.GetCachePath();

            if (Directory.Exists(cachePath))
            {
                string? version = null;
                string? checksum = null;

                var manifestPath = Path.Combine(cachePath, "manifest.json");
                if (File.Exists(manifestPath))
                {
                    try
                    {
                        var manifestJson = File.ReadAllText(manifestPath);
                        using var doc = JsonDocument.Parse(manifestJson);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("version", out var verProp))
                            version = verProp.GetString();
                    }
                    catch { /* ignore malformed manifest */ }
                }

                var checksumFile = Path.Combine(cachePath, "checksums.sha256");
                if (File.Exists(checksumFile))
                {
                    try
                    {
                        checksum = File.ReadAllText(checksumFile).Trim();
                    }
                    catch { /* ignore missing/unreadable checksum */ }
                }

                models.Add(new
                {
                    id = "pp-ocrv5-mobile",
                    version = version ?? "unknown",
                    path = cachePath,
                    checksum = checksum ?? "unknown"
                });
            }

            if (json)
            {
                var data = new { models };
                var output = JsonOutput.Ok("ocr models",
                    $"Found {models.Count} model(s)", data);
                output.Metrics["modelCount"] = models.Count;
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            else
            {
                if (models.Count == 0)
                {
                    Console.WriteLine("No PP-OCRv5 models installed.");
                }
                else
                {
                    foreach (dynamic m in models)
                        Console.WriteLine($"{m.id} v{m.version} at {m.path}");
                }
            }
        }, jsonOpt);

        return cmd;
    }

    // ===== ocr install-model =====

    static Command CreateInstallModel(Option<bool> jsonOpt)
    {
        var modelIdArg = new Argument<string>("model-id", "Model ID to install (e.g. pp-ocrv5-mobile)");
        var cmd = new Command("install-model", "Install PP-OCRv5 model files") { modelIdArg };

        cmd.SetHandler((string modelId, bool json) =>
        {
            if (modelId != "pp-ocrv5-mobile")
            {
                CliHelpers.WriteError("ocr install-model",
                    ErrorCodes.ValidationFailed with { Message = $"Unknown model ID: {modelId}. Supported: pp-ocrv5-mobile" }, json);
                return;
            }

            var cachePath = PpOcrV5ModelResolver.GetCachePath();
            CliHelpers.WriteError("ocr install-model",
                ErrorCodes.NotImplemented with { Message = $"Model download not yet implemented. Place PP-OCRv5 ONNX model files in {cachePath} with manifest.json." }, json);
        }, modelIdArg, jsonOpt);

        return cmd;
    }

    // ===== helpers =====

    static string GetPpOcrV5ModelCachePath() => PpOcrV5ModelResolver.GetCachePath();
    }
}
