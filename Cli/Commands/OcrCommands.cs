using System.CommandLine;
using System.Text.Json;
using System.IO.Compression;
using System.Globalization;
using MultiModalCore;
using OpenCvSharp;
using System.Drawing;
using System.Drawing.Imaging;
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
        cmd.AddCommand(CreateBatch(jsonOpt));
        cmd.AddCommand(CreateVideo(jsonOpt));
        cmd.AddCommand(CreateScreen(jsonOpt));
        cmd.AddCommand(CreateCamera(jsonOpt));
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
                Meta = new MetaInfo { Version = CliVersion.Current }
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
                Meta = new MetaInfo { Version = CliVersion.Current }
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
        var forceOpt = new Option<bool>("--force", "Run local OCR even if image preflight flags QR/code/graphic-heavy input");
        var cmd = new Command("local", "Local PP-OCRv6 recognition with pure .NET runtime (no Python)") { imageArg, forceOpt };

        cmd.SetHandler((string image, bool force, bool json) =>
        {
            if (string.IsNullOrWhiteSpace(image))
            {
                CliHelpers.WriteError("ocr local", ErrorCodes.MissingArgument with { Message = "Image path is required." }, json);
                return;
            }
            if (!File.Exists(image))
            {
                CliHelpers.WriteError("ocr local", ErrorCodes.FileNotFound with { Message = $"Image not found: {image}" }, json);
                return;
            }
            if (Path.GetExtension(image).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                CliHelpers.WriteError("ocr local",
                    ErrorCodes.UnsupportedFormat with
                    {
                        Message = "ocr local accepts single image files only. For PDF, multi-page layout, tables, Word output, or pandoc/Word annotation alignment, use 'nong ocr cloud' or 'nong ocr to-word' with PADDLEOCR_ACCESS_TOKEN."
                    }, json);
                return;
            }

            try
            {
                var preflight = LocalOcrInputPreflight.Analyze(image);
                // Barcode/QR decoded by ZXing → block (OCR won't help)
                if (preflight.ShouldSkip && !force)
                {
                    WriteLocalOcrPreflightSkip(image, preflight, json);
                    return;
                }
                // Heuristic warnings → show but continue (exam papers etc. often misflagged)
                if (!string.IsNullOrEmpty(preflight.Classification) && preflight.Classification != "text_candidate")
                {
                    WriteLocalOcrPreflightWarning(preflight, json);
                }

                using var client = new PpOcrV6Client();
                var (result, elapsed) = CliHelpers.Time(() =>
                    client.RecognizeAsync(image).GetAwaiter().GetResult());
                var page = result.Pages.FirstOrDefault();
                var blocks = page?.Blocks ?? new List<PpOcrV5Block>();
                var invalidConfidenceBlocks = result.InvalidConfidenceBlocks;
                var invalidGeometryBlocks = result.InvalidGeometryBlocks;

                if (json)
                {
                    var data = new
                    {
                        engine = result.Engine,
                        modelId = result.ModelId,
                        runtime = new
                        {
                            inferenceMode = result.InferenceMode,
                            numericFallbackAttempted = result.NumericFallbackAttempted,
                            numericFallbackApplied = result.NumericFallbackApplied,
                            numericFallbackReason = result.NumericFallbackReason
                        },
                        capabilities = new
                        {
                            mode = "local-text-ocr",
                            input = "single-image",
                            pdf = false,
                            layoutAnalysis = false,
                            tableStructure = false,
                            wordFormatting = false,
                            pandocAnnotations = false,
                            combineWithCloud = "Use ocr cloud/to-word for PDF, multi-page layout, tables, Word output, and pandoc/Word annotation alignment."
                        },
                        image = Path.GetFullPath(image),
                        preflight,
                        width = page?.Width,
                        height = page?.Height,
                        blocks = blocks.Select((b, i) => new
                        {
                            id = b.Id,
                            text = b.Text,
                            confidence = b.Confidence,
                            confidenceValid = b.ConfidenceValid,
                            bbox = b.Bbox,
                            polygon = b.Polygon,
                            geometryValid = b.GeometryValid,
                            numericIssue = b.NumericIssue
                        }).ToList()
                    };
                    var output = JsonOutput.Ok("ocr local",
                        $"Local OCR complete: {blocks.Count} text block(s)", data);
                    output.Metrics["pages"] = result.Pages.Count;
                    output.Metrics["blocks"] = blocks.Count;
                    output.Metrics["invalidConfidenceBlocks"] = invalidConfidenceBlocks;
                    output.Metrics["invalidGeometryBlocks"] = invalidGeometryBlocks;
                    output.Metrics["preflightSkipped"] = 0;
                    output.Meta.DurationMs = elapsed;

                    AddLocalOcrNumericIssues(output, result, invalidConfidenceBlocks, invalidGeometryBlocks);
                    // Preflight heuristic warning as JSON issue (already printed to stderr in text mode)
                    if (!string.IsNullOrEmpty(preflight.Classification) && preflight.Classification != "text_candidate")
                    {
                        output.Issues.Add(new Issue
                        {
                            Id = "local_ocr_preflight_warning",
                            Severity = "Warning",
                            Message = $"Preflight classification: {preflight.Classification}. {preflight.Reason} {preflight.Recommendation}"
                        });
                    }
                    Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                }
                else
                {
                    foreach (var b in blocks)
                        Console.WriteLine($"{b.Text}\t{FormatConfidence(b.Confidence)}");
                    WriteLocalOcrNumericWarningsToStderr(result, invalidConfidenceBlocks, invalidGeometryBlocks);
                }
            }
            catch (PaddleOcrException ex)
            {
                if (IsCannotDecodeImageException(ex))
                {
                    CliHelpers.WriteError("ocr local",
                        ErrorCodes.UnsupportedFormat with
                        {
                            Message = $"Cannot decode image. ocr local supports single image files only; use ocr cloud/to-word for PDF or document layout work. Detail: {ex.Message}"
                        }, json);
                }
                else
                {
                    CliHelpers.WriteError("ocr local",
                        ErrorCodes.DependencyMissing with
                        {
                            Message = $"Local PP-OCRv6 .NET runtime is unavailable: {ex.Message}. Run 'nong ocr install-model pp-ocrv6-medium --json'. No Python is required."
                        }, json);
                }
            }
            catch (Exception ex) when (IsCannotDecodeImageException(ex))
            {
                CliHelpers.WriteError("ocr local",
                    ErrorCodes.UnsupportedFormat with
                    {
                        Message = $"Cannot decode image. ocr local supports single image files only; use ocr cloud/to-word for PDF or document layout work. Detail: {ex.Message}"
                    }, json);
            }
            catch (Exception ex) when (IsLocalOcrDependencyException(ex))
            {
                CliHelpers.WriteError("ocr local",
                    ErrorCodes.DependencyMissing with
                    {
                        Message = $"Local PP-OCRv6 .NET runtime is unavailable: {ex.Message}. Run 'nong ocr install-model pp-ocrv6-medium --json'. No Python is required."
                    }, json);
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("ocr local",
                    ErrorCodes.InternalError with { Message = $"Local OCR failed: {ex.Message}" }, json);
            }
        }, imageArg, forceOpt, jsonOpt);

        return cmd;
    }

    static void WriteLocalOcrPreflightSkip(string image, LocalOcrInputPreflightResult preflight, bool json)
    {
        var error = ErrorCodes.ValidationFailed with
        {
            Message = $"Local OCR skipped before PP-OCRv6 inference: {preflight.Reason} {preflight.Recommendation}"
        };

        Environment.ExitCode = 1;
        if (!json)
        {
            Console.Error.WriteLine($"[{error.Code}] {error.Name}: {error.Message}");
            return;
        }

        var output = JsonOutput.Fail("ocr local", new List<ErrorEntry> { error });
        output.Summary = "Local OCR preflight skipped non-text image";
        output.Data = new
        {
            image = Path.GetFullPath(image),
            preflight
        };
        output.Issues.Add(new Issue
        {
            Id = "local_ocr_preflight_skipped",
            Severity = "Warning",
            Message = error.Message
        });
        output.Metrics["preflightSkipped"] = 1;
        output.Metrics["width"] = preflight.Width;
        output.Metrics["height"] = preflight.Height;
        output.Metrics["regionCount"] = preflight.RegionCount;
        output.Metrics["largestRegionRatio"] = preflight.LargestRegionRatio;
        Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
    }

    /// <summary>Print preflight heuristic warning without blocking OCR inference.</summary>
    static void WriteLocalOcrPreflightWarning(LocalOcrInputPreflightResult preflight, bool json)
    {
        if (json) return; // JSON output includes preflight in data, warning is in issues

        Console.Error.WriteLine($"[preflight] {preflight.Classification}: {preflight.Reason}");
        Console.Error.WriteLine($"[preflight] {preflight.Recommendation}");
    }

    static void AddLocalOcrNumericIssues(JsonOutput output, PpOcrV5Result result, int invalidConfidenceBlocks, int invalidGeometryBlocks)
    {
        if (result.NumericFallbackAttempted)
        {
            output.Issues.Add(new Issue
            {
                Id = "local_ocr_numeric_fallback",
                Severity = "Warning",
                Message = result.NumericFallbackApplied
                    ? "Fast local OCR inference produced invalid numeric values; Nong reran the image with conservative CPU/BLAS mode."
                    : "Fast local OCR inference produced invalid numeric values; conservative fallback was attempted but the sanitized fast result was retained."
            });
        }

        if (invalidConfidenceBlocks > 0)
        {
            output.Issues.Add(new Issue
            {
                Id = "local_ocr_invalid_confidence",
                Severity = "Warning",
                Message = $"{invalidConfidenceBlocks} OCR block(s) had NaN/Infinity confidence; JSON uses confidence:null and confidenceValid:false."
            });
        }

        if (invalidGeometryBlocks > 0)
        {
            output.Issues.Add(new Issue
            {
                Id = "local_ocr_invalid_geometry",
                Severity = "Warning",
                Message = $"{invalidGeometryBlocks} OCR block(s) had NaN/Infinity geometry; invalid points were removed before JSON serialization."
            });
        }
    }

    static void WriteLocalOcrNumericWarningsToStderr(PpOcrV5Result result, int invalidConfidenceBlocks, int invalidGeometryBlocks)
    {
        if (result.NumericFallbackAttempted)
        {
            var fallback = result.NumericFallbackApplied ? "applied" : "attempted";
            Console.Error.WriteLine($"[local_ocr_numeric_fallback] {fallback}: {result.NumericFallbackReason}");
        }
        if (invalidConfidenceBlocks > 0)
            Console.Error.WriteLine($"[local_ocr_invalid_confidence] {invalidConfidenceBlocks} block(s); confidence shown as n/a.");
        if (invalidGeometryBlocks > 0)
            Console.Error.WriteLine($"[local_ocr_invalid_geometry] {invalidGeometryBlocks} block(s); invalid points removed.");
    }

    static string FormatConfidence(double? confidence) =>
        confidence.HasValue
            ? confidence.Value.ToString("0.###", CultureInfo.InvariantCulture)
            : "n/a";

    static bool IsCannotDecodeImageException(Exception ex) =>
        ex.Message.Contains("Cannot decode image", StringComparison.OrdinalIgnoreCase);

    static bool IsLocalOcrDependencyException(Exception ex)
    {
        var text = ex.ToString();
        return ex is DllNotFoundException
            || ex is BadImageFormatException
            || text.Contains("OpenCvSharp", StringComparison.OrdinalIgnoreCase)
            || text.Contains("paddle_inference", StringComparison.OrdinalIgnoreCase)
            || text.Contains("PaddleInference", StringComparison.OrdinalIgnoreCase)
            || text.Contains("NativeMethods", StringComparison.OrdinalIgnoreCase);
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
            // ImageAnalyzer probes SkiaSharp native DLL availability
            var imageAnalyzerOk = IsImageAnalyzerAvailable();

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

            // Optional legacy model cache; current local OCR uses managed .NET V5 model metadata.
            var cacheDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Angri450.Nong", "models", "pp-ocrv5-mobile");
            string modelStatus;
            if (Directory.Exists(cacheDir) && File.Exists(Path.Combine(cacheDir, "manifest.json")))
                modelStatus = "present";
            else
                modelStatus = "bundled";

            var localDotNet = PpOcrV6Client.CheckEnvironment();
            var localDotNetStatus = localDotNet.Available ? "ok" : "missing";

            // v6 auto-detection
            var (v6Available, v6Size, v6CachePath) = PpOcrV6ModelResolver.DetectInstalled();
            var v6Status = v6Available ? "ok" : "missing";

            if (json)
            {
                var data = new
                {
                    imageAnalyzer = imageAnalyzerOk ? "ok" : "error",
                    cloudToken = tokenStatus,
                    localModel = new
                    {
                        ppOcrV5Mobile = modelStatus,
                        deployment = "managed-model-bundled-native-runtime-cache",
                        mirrorHint = "Run 'nong ocr install-model pp-ocrv5-mobile --source https://mirrors.huaweicloud.com/repository/nuget/v3/index.json --json' to deploy the current-platform Angri450.Nong.OcrRuntime.* native runtime bundle."
                    },
                    localDotNetPpOcrV5 = new
                    {
                        status = localDotNetStatus,
                        engine = localDotNet.Engine,
                        modelId = localDotNet.ModelId,
                        runtime = localDotNet.Runtime,
                        noPython = true,
                        message = localDotNet.Message
                    },
                    localDotNetPpOcrV6 = new
                    {
                        status = v6Status,
                        engine = v6Available ? "pp-ocrv6-dotnet-sdcb" : "unavailable",
                        modelId = v6Available ? $"pp-ocrv6-{v6Size}" : "pp-ocrv6-medium",
                        modelSize = v6Available ? v6Size : null,
                        modelCachePath = v6Available ? v6CachePath : null,
                        noPython = true,
                        isDefault = true,
                        message = v6Available
                            ? $"PP-OCRv6 {v6Size} is installed and ready."
                            : "PP-OCRv6 model is not installed. Run nong ocr install-model pp-ocrv6-medium --json. No Python is required."
                    }
                };
                var output = JsonOutput.Ok("ocr check-env",
                    $"imageAnalyzer={data.imageAnalyzer}, token={tokenStatus}, v5={localDotNetStatus}, v6={v6Status}",
                    data);
                output.Metrics["imageAnalyzer"] = imageAnalyzerOk ? 1 : 0;
                output.Metrics["cloudToken"] = tokenStatus == "missing" ? 0 : 1;
                output.Metrics["localDotNetPpOcrV5"] = localDotNet.Available ? 1 : 0;
                output.Metrics["localDotNetPpOcrV6"] = v6Available ? 1 : 0;

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
                Console.WriteLine($"PP-OCRv5 model: {modelStatus}");
                Console.WriteLine($"Local .NET PP-OCRv5: {localDotNetStatus} ({localDotNet.Message})");
                Console.WriteLine($"Local .NET PP-OCRv6: {v6Status}" + (v6Available ? $" ({v6Size})" : " (not installed)"));
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
        var cmd = new Command("models", "List available local OCR models");

        cmd.SetHandler((bool json) =>
        {
            var models = new List<object>();
            var cachePath = PpOcrV6ModelResolver.GetModelCachePath("medium");
            var env = PpOcrV6Client.CheckEnvironment();

            models.Add(new
            {
                id = "pp-ocrv5-mobile",
                engine = env.Engine,
                runtime = env.Runtime,
                deployment = "managed-model-bundled-native-runtime-cache",
                language = "chinese-v5",
                available = env.Available,
                noPython = true,
                domesticMirror = "Run nong ocr install-model pp-ocrv5-mobile with Huawei Cloud NuGet v3 source for first-party native runtime deployment.",
                message = env.Message
            });

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
                    engine = "legacy-cache",
                    version = version ?? "unknown",
                    path = cachePath,
                    checksum = checksum ?? "unknown"
                });
            }
            // v6 models
            foreach (var size in PpOcrV6ModelResolver.SupportedSizes)
            {
                var v6Cache = PpOcrV6ModelResolver.GetModelCachePath(size);
                var v6Available = PpOcrV6ModelResolver.ValidateModelCache(v6Cache);
                models.Add(new
                {
                    id = $"pp-ocrv6-{size}",
                    engine = "pp-ocrv6-dotnet-sdcb",
                    modelSize = size,
                    deployment = "cdn-download-pir-model",
                    language = size == "tiny" ? "chinese-v6-tiny" : "chinese-v6-multilingual",
                    available = v6Available,
                    isDefault = size == "medium",
                    noPython = true,
                    installCommand = $"nong ocr install-model pp-ocrv6-{size} --json",
                    modelCachePath = v6Cache,
                    message = v6Available
                        ? $"PP-OCRv6 {size} model is installed."
                        : $"PP-OCRv6 {size} model is not installed. Run nong ocr install-model pp-ocrv6-{size} --json."
                });
            }

            if (json)
            {
                var data = new { models, defaultModel = "pp-ocrv6-medium" };
                var output = JsonOutput.Ok("ocr models",
                    $"Found {models.Count} model(s)", data);
                output.Metrics["modelCount"] = models.Count;
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            else
            {
                if (models.Count == 0)
                {
                    Console.WriteLine("No OCR models installed.");
                }
                else
                {
                    foreach (dynamic m in models)
                        Console.WriteLine(JsonSerializer.Serialize(m, CliHelpers.JsonOpts));
                }
            }
        }, jsonOpt);

        return cmd;
    }

    // ===== ocr install-model =====

    static Command CreateInstallModel(Option<bool> jsonOpt)
    {
        var modelIdArg = new Argument<string>("model-id", "Model ID: pp-ocrv6 (default=medium), pp-ocrv6-medium, pp-ocrv6-small, pp-ocrv6-tiny");
        var dryRunOpt = new Option<bool>("--dry-run", () => false, "Report the .NET native runtime deployment plan without changing the machine");
        var sourceOpt = new Option<string>("--source",
            () => "https://mirrors.huaweicloud.com/repository/nuget/v3/index.json",
            "NuGet v3 source for native runtime packages; use a domestic mirror for client deployment");
        var allowUpstreamFallbackOpt = new Option<bool>("--allow-upstream-fallback", () => false,
            "Allow fallback to upstream Sdcb/OpenCvSharp native runtime packages when the first-party Nong runtime bundle is unavailable");
        var cmd = new Command("install-model", "Install/check pure .NET PP-OCRv6 native runtime")
        {
            modelIdArg,
            dryRunOpt,
            sourceOpt,
            allowUpstreamFallbackOpt
        };

        cmd.SetHandler((string modelId, bool dryRun, string source, bool allowUpstreamFallback, bool json) =>
        {
            if (!PpOcrV6ModelResolver.AllModelIds.Contains(modelId))
            {
                CliHelpers.WriteError("ocr install-model",
                    ErrorCodes.ValidationFailed with { Message = $"Unknown model ID: {modelId}. Supported: {string.Join(", ", PpOcrV6ModelResolver.AllModelIds)}" }, json);
                return;
            }

            if (PpOcrV6ModelResolver.IsV6ModelId(modelId))
            {
                InstallV6Model(modelId, dryRun, source, allowUpstreamFallback, json);
                return;
            }

            var cachePath = PpOcrV6ModelResolver.GetModelCachePath("medium");
            var env = PpOcrV6Client.CheckEnvironment();
            var runtimeCache = PpOcrV6ModelResolver.GetNativeRuntimeCachePath();
            var runtimePlan = GetNativeRuntimePlan();
            var domesticNuGetSources = new[]
            {
                "https://mirrors.huaweicloud.com/repository/nuget/v3/index.json"
            };
            var installCommand = "dotnet tool install --global Angri450.Nong.Cli --add-source https://mirrors.huaweicloud.com/repository/nuget/v3/index.json";
            var runtimeInstallCommand = "nong ocr install-model pp-ocrv5-mobile --source https://mirrors.huaweicloud.com/repository/nuget/v3/index.json --json";
            var upstreamFallbackCommand = runtimeInstallCommand + " --allow-upstream-fallback";

            if (dryRun)
            {
                var data = new
                {
                    modelId,
                    engine = "pp-ocrv5-dotnet-sdcb",
                    deployment = "managed-model-bundled-native-runtime-cache",
                    installCommand,
                    runtimeInstallCommand,
                    domesticNuGetSources,
                    cachePath,
                    runtimeCache,
                    runtimeId = runtimePlan.RuntimeId,
                    runtimePackage = runtimePlan.BundlePackage,
                    fallbackPackages = runtimePlan.FallbackPackages,
                    allowUpstreamFallback,
                    upstreamFallbackDefault = "disabled",
                    upstreamFallbackCommand,
                    source,
                    noPython = true,
                    note = "Local OCR uses managed .NET model metadata plus first-party Nong NuGet native runtime bundles. Client machines do not need Python, pip, local model builds, or an external OCR executable. Upstream runtime packages are used only when --allow-upstream-fallback is explicitly set."
                };
                var output = JsonOutput.Ok("ocr install-model", "Dry run: pure .NET PP-OCRv5 deployment plan", data);
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                return;
            }

            if (env.Available)
            {
                var downloadCleanup = CleanupRuntimeDownloads(runtimeCache);
                var output = JsonOutput.Ok("ocr install-model",
                    "Pure .NET PP-OCRv5 is available",
                    new
                    {
                        modelId,
                        engine = env.Engine,
                        runtime = env.Runtime,
                        deployment = "managed-model-bundled-native-runtime-cache",
                        domesticNuGetSources,
                        allowUpstreamFallback,
                        upstreamFallbackDefault = "disabled",
                        cachePath,
                        runtimeCache,
                        downloadCleanup,
                        noPython = true,
                        message = env.Message
                    });
                AddCleanupWarning(output, downloadCleanup);
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                return;
            }

            if (runtimePlan.BundlePackage == null && runtimePlan.FallbackPackages.Count == 0)
            {
                CliHelpers.WriteError("ocr install-model",
                    ErrorCodes.DependencyMissing with
                    {
                        Message = $"Local PP-OCRv5 runtime installer has no supported runtime package for this platform: {runtimePlan.RuntimeId}. Supported first-party packages are WinX64, LinuxX64, LinuxArm64, OsxX64, and OsxArm64."
                    }, json);
                return;
            }

            try
            {
                var (installed, elapsed) = CliHelpers.Time(() =>
                    InstallNativeRuntime(runtimePlan, runtimeCache, source, allowUpstreamFallback));

                var after = PpOcrV6Client.CheckEnvironment();
                if (!after.Available)
                {
                    CliHelpers.WriteError("ocr install-model",
                        ErrorCodes.DependencyMissing with
                        {
                            Message = $"Native runtime files were installed, but local PP-OCRv5 is still unavailable: {after.Message}"
                        }, json);
                    return;
                }

                var downloadCleanup = CleanupRuntimeDownloads(runtimeCache);
                var output = JsonOutput.Ok("ocr install-model",
                    "Pure .NET PP-OCRv5 native runtime installed",
                    new
                    {
                        modelId,
                        engine = after.Engine,
                        runtime = after.Runtime,
                        runtimeId = runtimePlan.RuntimeId,
                        source,
                        runtimeCache,
                        installed,
                        downloadCleanup,
                        allowUpstreamFallback,
                        upstreamFallbackDefault = "disabled",
                        noPython = true,
                        message = after.Message
                    });
                AddCleanupWarning(output, downloadCleanup);
                output.Artifacts["runtimeDir"] = runtimeCache;
                output.Meta.DurationMs = elapsed;
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            catch (Exception ex)
            {
                var downloadCleanup = CleanupRuntimeDownloads(runtimeCache);
                var cleanupMessage = downloadCleanup.Warning == null
                    ? ""
                    : $" Cleanup warning: {downloadCleanup.Warning}";
                CliHelpers.WriteError("ocr install-model",
                    ErrorCodes.DependencyMissing with
                    {
                        Message = $"Failed to install pure .NET PP-OCRv5 native runtime from NuGet source '{source}': {ex.Message}{cleanupMessage}"
                    }, json);
            }
        }, modelIdArg, dryRunOpt, sourceOpt, allowUpstreamFallbackOpt, jsonOpt);

        return cmd;
    }

    // ===== helpers =====

    // ===== v6 model install =====

    static void InstallV6Model(string modelId, bool dryRun, string source, bool allowUpstreamFallback, bool json)
    {
        var (_, size) = PpOcrV6ModelResolver.ParseModelId(modelId);
        var modelCachePath = PpOcrV6ModelResolver.GetModelCachePath(size);
        var runtimeCache = PpOcrV6ModelResolver.GetNativeRuntimeCachePath();
        var runtimePlan = GetNativeRuntimePlan();

        if (dryRun)
        {
            var data = new
            {
                modelId = PpOcrV6ModelResolver.CanonicalModelId(modelId),
                size,
                engine = "pp-ocrv6-dotnet-sdcb",
                deployment = "cdn-download-pir-model",
                detUrl = PpOcrV6ModelResolver.DetDownloadUrl(size),
                recUrl = PpOcrV6ModelResolver.RecDownloadUrl(size),
                modelCachePath,
                noPython = true,
                runtime = new
                {
                    runtimeId = runtimePlan.RuntimeId,
                    runtimeCache,
                    bundlePackage = runtimePlan.BundlePackage,
                    fallbackPackages = runtimePlan.FallbackPackages,
                    allowUpstreamFallback,
                    source,
                },
                note = "PP-OCRv6 models are downloaded from PaddleOCR CDN (PIR format, Paddle 3.0). Native runtime DLLs are deployed from the Nong.OcrRuntime NuGet package family. No Python, pip, or external OCR executable required."
            };
            var output = JsonOutput.Ok("ocr install-model", $"Dry run: PP-OCRv6 {size} + native runtime deployment plan", data);
            Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            return;
        }

        bool modelOk = PpOcrV6ModelResolver.ValidateModelCache(modelCachePath);
        bool runtimeOk = PpOcrV6ModelResolver.ValidateNativeRuntimeDir(runtimeCache);

        // Both already installed
        if (modelOk && runtimeOk)
        {
            var env = PpOcrV6Client.CheckEnvironment();
            var downloadCleanup = CleanupRuntimeDownloads(runtimeCache);
            var output = JsonOutput.Ok("ocr install-model",
                $"PP-OCRv6 {size} is already installed and ready",
                new
                {
                    modelId = PpOcrV6ModelResolver.CanonicalModelId(modelId),
                    size,
                    engine = env.Engine,
                    runtime = env.Runtime,
                    modelCachePath,
                    runtimeCache,
                    downloadCleanup,
                    noPython = true,
                    message = env.Message
                });
            AddCleanupWarning(output, downloadCleanup);
            Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            return;
        }

        // Model-only install (no runtime): used when model cache dir has files but runtime cache is empty
        if (modelOk && !runtimeOk)
        {
            InstallV6NativeRuntime(size, modelCachePath, runtimeCache, runtimePlan, source, allowUpstreamFallback, json);
            return;
        }

        // Full install: model + native runtime
        try
        {
            // Step 1: download model from CDN
            var modelElapsed = CliHelpers.Time(() =>
            {
                DownloadAndExtractV6Model(size, modelCachePath);
            });

            if (!PpOcrV6ModelResolver.ValidateModelCache(modelCachePath))
            {
                CliHelpers.WriteError("ocr install-model",
                    ErrorCodes.DependencyMissing with
                    {
                        Message = $"Model files were downloaded but cache is still incomplete: {modelCachePath}"
                    }, json);
                return;
            }

            // Step 2: deploy native runtime from NuGet
            if (!runtimeOk)
            {
                InstallV6NativeRuntime(size, modelCachePath, runtimeCache, runtimePlan, source, allowUpstreamFallback, json);
                return;
            }

            // runtime already OK, model only
            var after = PpOcrV6Client.CheckEnvironment();
            var downloadCleanup = CleanupRuntimeDownloads(runtimeCache);
            var output = JsonOutput.Ok("ocr install-model",
                $"PP-OCRv6 {size} installed",
                new
                {
                    modelId = PpOcrV6ModelResolver.CanonicalModelId(modelId),
                    size,
                    engine = after.Engine,
                    runtime = after.Runtime,
                    modelCachePath,
                    runtimeCache,
                    downloadCleanup,
                    noPython = true,
                    message = after.Message
                });
            AddCleanupWarning(output, downloadCleanup);
            output.Artifacts["modelDir"] = modelCachePath;
            output.Meta.DurationMs = modelElapsed;
            Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
        }
        catch (Exception ex)
        {
            CliHelpers.WriteError("ocr install-model",
                ErrorCodes.DependencyMissing with
                {
                    Message = $"Failed to install PP-OCRv6 {size}: {ex.Message}"
                }, json);
        }
    }

    /// <summary>
    /// Install native runtime DLLs into the pp-ocrv6-{rid} cache directory.
    /// Called from InstallV6Model when model is present but runtime is missing.
    /// </summary>
    static void InstallV6NativeRuntime(string size, string modelCachePath, string runtimeCache, NativeRuntimePlan runtimePlan, string source, bool allowUpstreamFallback, bool json)
    {
        if (runtimePlan.BundlePackage == null && runtimePlan.FallbackPackages.Count == 0)
        {
            CliHelpers.WriteError("ocr install-model",
                ErrorCodes.DependencyMissing with
                {
                    Message = $"No native runtime package configured for this platform: {runtimePlan.RuntimeId}. PP-OCRv6 {size} model is installed but native inference DLLs are missing."
                }, json);
            return;
        }

        try
        {
            var (installed, runtimeElapsed) = CliHelpers.Time(() =>
                InstallNativeRuntime(runtimePlan, runtimeCache, source, allowUpstreamFallback));

            var after = PpOcrV6Client.CheckEnvironment();
            if (!after.Available)
            {
                CliHelpers.WriteError("ocr install-model",
                    ErrorCodes.DependencyMissing with
                    {
                        Message = $"PP-OCRv6 {size} model is installed but native runtime is still unavailable. Runtime cache: {runtimeCache}. Detail: {after.Message}"
                    }, json);
                return;
            }

            var downloadCleanup = CleanupRuntimeDownloads(runtimeCache);
            var output = JsonOutput.Ok("ocr install-model",
                $"PP-OCRv6 {size} + native runtime installed",
                new
                {
                    modelId = $"pp-ocrv6-{size}",
                    size,
                    engine = after.Engine,
                    runtime = after.Runtime,
                    runtimeId = runtimePlan.RuntimeId,
                    source,
                    modelCachePath,
                    runtimeCache,
                    installed,
                    downloadCleanup,
                    allowUpstreamFallback,
                    noPython = true,
                    message = after.Message
                });
            AddCleanupWarning(output, downloadCleanup);
            output.Artifacts["modelDir"] = modelCachePath;
            output.Artifacts["runtimeDir"] = runtimeCache;
            output.Meta.DurationMs = runtimeElapsed;
            Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
        }
        catch (Exception ex)
        {
            var downloadCleanup = CleanupRuntimeDownloads(runtimeCache);
            var cleanupMessage = downloadCleanup.Warning == null
                ? ""
                : $" Cleanup warning: {downloadCleanup.Warning}";
            CliHelpers.WriteError("ocr install-model",
                ErrorCodes.DependencyMissing with
                {
                    Message = $"PP-OCRv6 {size} model is installed but native runtime deployment failed from NuGet source '{source}': {ex.Message}{cleanupMessage}"
                }, json);
        }
    }

    static void DownloadAndExtractV6Model(string size, string modelCachePath)
    {
        var detUrl = PpOcrV6ModelResolver.DetDownloadUrl(size);
        var recUrl = PpOcrV6ModelResolver.RecDownloadUrl(size);

        var tmpDir = Path.Combine(Path.GetTempPath(), $"ppocrv6-{size}-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tmpDir);
            Directory.CreateDirectory(modelCachePath);

            using var http = new HttpClient();

            // Download det
            var detTar = Path.Combine(tmpDir, "det.tar");
            Console.Error.WriteLine($"[ppocrv6] Downloading {detUrl}");
            using (var detStream = http.GetStreamAsync(detUrl).GetAwaiter().GetResult())
            using (var detFs = File.Create(detTar))
                detStream.CopyTo(detFs);

            // Download rec
            var recTar = Path.Combine(tmpDir, "rec.tar");
            Console.Error.WriteLine($"[ppocrv6] Downloading {recUrl}");
            using (var recStream = http.GetStreamAsync(recUrl).GetAwaiter().GetResult())
            using (var recFs = File.Create(recTar))
                recStream.CopyTo(recFs);

            // Extract det
            var detDir = PpOcrV6ModelResolver.GetDetDir(modelCachePath);
            Directory.CreateDirectory(detDir);
            Console.Error.WriteLine($"[ppocrv6] Extracting det model to {detDir}");
            ExtractTar(detTar, detDir);

            // Extract rec
            var recDir = PpOcrV6ModelResolver.GetRecDir(modelCachePath);
            Directory.CreateDirectory(recDir);
            Console.Error.WriteLine($"[ppocrv6] Extracting rec model to {recDir}");
            ExtractTar(recTar, recDir);

            // Extract dict from embedded resource
            var dictPath = PpOcrV6ModelResolver.GetDictPath(modelCachePath);
            Console.Error.WriteLine($"[ppocrv6] Extracting dict to {dictPath}");
            PpOcrV6ModelResolver.ExtractDict(size, dictPath);

            // Write manifest
            var manifestPath = Path.Combine(modelCachePath, "manifest.json");
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(new
            {
                schemaVersion = "nong-ocr-model/v1",
                modelId = $"pp-ocrv6-{size}",
                engine = "pp-ocrv6-dotnet-sdcb",
                size,
                installedAt = DateTimeOffset.UtcNow,
                detUrl,
                recUrl,
                noPython = true
            }, CliHelpers.JsonOpts));
        }
        finally
        {
            try { Directory.Delete(tmpDir, recursive: true); } catch { }
        }
    }

    static void ExtractTar(string tarPath, string destDir)
    {
        // Use System.Formats.Tar (available in .NET 7+)
        // Tar entries are like "PP-OCRv6_tiny_det_infer/inference.json"
        // We strip the top-level directory to extract directly into destDir.
        using var fs = File.OpenRead(tarPath);
        using var reader = new System.Formats.Tar.TarReader(fs);
        while (reader.GetNextEntry() is { } entry)
        {
            var name = entry.Name.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            // Strip leading directory (e.g. "PP-OCRv6_tiny_det_infer/")
            var slash = name.IndexOf(Path.DirectorySeparatorChar);
            var relativeName = slash >= 0 && slash < name.Length - 1
                ? name[(slash + 1)..]
                : name;

            var outPath = Path.Combine(destDir, relativeName);
            if (entry.EntryType == System.Formats.Tar.TarEntryType.Directory)
            {
                Directory.CreateDirectory(outPath);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
                entry.ExtractToFile(outPath, overwrite: true);
            }
        }
    }

    static string GetPpOcrV6ModelCachePath() => PpOcrV6ModelResolver.GetModelCachePath("medium");

    static bool IsImageAnalyzerAvailable()
    {
        try
        {
            // 1x1 white PNG — minimal probe for SkiaSharp native DLL loading
            var testPng = Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
            using var bmp = SkiaSharp.SKBitmap.Decode(testPng);
            return bmp != null;
        }
        catch
        {
            return false;
        }
    }

    sealed record NativeRuntimePackage(string Id, string Version, string NativePrefix);
    sealed record NativeRuntimePlan(string RuntimeId, NativeRuntimePackage? BundlePackage, IReadOnlyList<NativeRuntimePackage> FallbackPackages);
    sealed record RuntimeDownloadCleanup(bool Attempted, bool Cleaned, string Path, string? Warning);

    static NativeRuntimePlan GetNativeRuntimePlan()
    {
        var runtimeId = PpOcrV6ModelResolver.GetNativeRuntimeId();
        return runtimeId switch
        {
            "win-x64" => new NativeRuntimePlan(
                runtimeId,
                new NativeRuntimePackage("Angri450.Nong.OcrRuntime.WinX64", OcrRuntimeVersion.Current, "runtimes/win-x64/native/"),
                new[]
                {
                    new NativeRuntimePackage("Sdcb.PaddleInference.runtime.win64.mkl", "3.3.1.70", "runtimes/win-x64/native/"),
                    new NativeRuntimePackage("OpenCvSharp4.runtime.win", "4.11.0.20250507", "runtimes/win-x64/native/")
                }),
            "linux-x64" => new NativeRuntimePlan(
                runtimeId,
                new NativeRuntimePackage("Angri450.Nong.OcrRuntime.LinuxX64", OcrRuntimeVersion.Current, "runtimes/linux-x64/native/"),
                new[]
                {
                    new NativeRuntimePackage("Sdcb.PaddleInference.runtime.linux-x64.openblas", "3.3.1.70", "runtimes/linux-x64/native/"),
                    new NativeRuntimePackage("OpenCvSharp4.runtime.ubuntu.18.04-x64", "4.6.0.20220608", "runtimes/ubuntu.18.04-x64/native/")
                }),
            "linux-arm64" => new NativeRuntimePlan(
                runtimeId,
                new NativeRuntimePackage("Angri450.Nong.OcrRuntime.LinuxArm64", OcrRuntimeVersion.Current, "runtimes/linux-arm64/native/"),
                new[]
                {
                    new NativeRuntimePackage("Sdcb.PaddleInference.runtime.linux-arm64", "3.3.1.70", "runtimes/linux-arm64/native/"),
                    new NativeRuntimePackage("OpenCvSharp4.runtime.linux-arm64", "4.13.0.20260602", "runtimes/linux-arm64/native/")
                }),
            "osx-x64" => new NativeRuntimePlan(
                runtimeId,
                new NativeRuntimePackage("Angri450.Nong.OcrRuntime.OsxX64", OcrRuntimeVersion.Current, "runtimes/osx-x64/native/"),
                new[]
                {
                    new NativeRuntimePackage("Sdcb.PaddleInference.runtime.osx-x64", "3.3.1.70", "runtimes/osx-x64/native/"),
                    new NativeRuntimePackage("OpenCvSharp4.runtime.osx.10.15-universal", "4.7.0.20230224", "runtimes/osx-x64/native/")
                }),
            "osx-arm64" => new NativeRuntimePlan(
                runtimeId,
                new NativeRuntimePackage("Angri450.Nong.OcrRuntime.OsxArm64", OcrRuntimeVersion.Current, "runtimes/osx-arm64/native/"),
                new[]
                {
                    new NativeRuntimePackage("Sdcb.PaddleInference.runtime.osx-arm64", "3.3.1.70", "runtimes/osx-arm64/native/"),
                    new NativeRuntimePackage("OpenCvSharp4.runtime.osx.10.15-universal", "4.7.0.20230224", "runtimes/osx-arm64/native/")
                }),
            _ => new NativeRuntimePlan(runtimeId, null, Array.Empty<NativeRuntimePackage>())
        };
    }

    static List<object> InstallNativeRuntime(
        NativeRuntimePlan plan,
        string runtimeCache,
        string source,
        bool allowUpstreamFallback)
    {
        Directory.CreateDirectory(runtimeCache);
        Exception? bundleError = null;
        if (plan.BundlePackage != null)
        {
            try
            {
                var bundleInstalled = InstallNativeRuntimePackage(plan.BundlePackage, runtimeCache, source, "nong-bundle");
                WriteNativeRuntimeManifest(runtimeCache, plan, new[] { bundleInstalled });
                return new List<object> { bundleInstalled };
            }
            catch (Exception ex)
            {
                bundleError = ex;
                if (!allowUpstreamFallback)
                {
                    throw new InvalidOperationException(
                        $"First-party Nong OCR runtime bundle {plan.BundlePackage.Id} {plan.BundlePackage.Version} is unavailable or invalid. Publish/sync Angri450.Nong.OcrRuntime.* to the NuGet source, or rerun with --allow-upstream-fallback to use upstream Sdcb/OpenCvSharp packages. Bundle error: {ex.Message}",
                        ex);
                }
            }
        }

        if (!allowUpstreamFallback)
            throw new InvalidOperationException($"No first-party Nong OCR runtime bundle is configured for {plan.RuntimeId}.");

        var installed = new List<object>();
        foreach (var package in plan.FallbackPackages)
            installed.Add(InstallNativeRuntimePackage(package, runtimeCache, source, "upstream-fallback"));

        if (installed.Count == 0)
            throw new InvalidOperationException($"No native runtime package is configured for {plan.RuntimeId}.{(bundleError == null ? "" : " Bundle error: " + bundleError.Message)}");

        WriteNativeRuntimeManifest(runtimeCache, plan, installed);
        return installed;
    }

    static object InstallNativeRuntimePackage(NativeRuntimePackage package, string runtimeCache, string source, string origin)
    {
        var nupkgPath = ResolvePackageFromDirectory(package.Id, package.Version, source)
            ?? ResolvePackageFromCache(package.Id, package.Version)
            ?? DownloadNuGetPackage(package.Id, package.Version, source, runtimeCache);
        var files = ExtractNativeFiles(nupkgPath, package.NativePrefix, runtimeCache);
        return new
        {
            origin,
            package = package.Id,
            version = package.Version,
            nupkg = nupkgPath,
            files
        };
    }

    static void WriteNativeRuntimeManifest(
        string runtimeCache,
        NativeRuntimePlan plan,
        IEnumerable<object> installed)
    {
        var manifestPath = Path.Combine(runtimeCache, "manifest.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(new
            {
            schemaVersion = "nong-ocr-native-runtime/v1",
            runtimeId = plan.RuntimeId,
            installedAt = DateTimeOffset.UtcNow,
            bundlePackage = plan.BundlePackage,
            fallbackPackages = plan.FallbackPackages,
            installed,
            noPython = true
        }, CliHelpers.JsonOpts));
    }

    static RuntimeDownloadCleanup CleanupRuntimeDownloads(string runtimeCache)
    {
        var downloads = Path.Combine(runtimeCache, "downloads");
        var downloadsFull = Path.GetFullPath(downloads);

        if (!Directory.Exists(downloadsFull))
            return new RuntimeDownloadCleanup(false, false, downloadsFull, null);

        try
        {
            var runtimeRoot = EnsureTrailingSeparator(Path.GetFullPath(runtimeCache));
            var downloadsRoot = EnsureTrailingSeparator(downloadsFull);
            if (!downloadsRoot.StartsWith(runtimeRoot, StringComparison.OrdinalIgnoreCase))
            {
                return new RuntimeDownloadCleanup(
                    true,
                    false,
                    downloadsFull,
                    $"Refused to clean download cache outside runtime cache: {downloadsFull}");
            }

            Directory.Delete(downloadsFull, recursive: true);
            return new RuntimeDownloadCleanup(true, true, downloadsFull, null);
        }
        catch (Exception ex)
        {
            return new RuntimeDownloadCleanup(
                true,
                false,
                downloadsFull,
                $"Native runtime download cache cleanup failed: {ex.Message}");
        }
    }

    static void AddCleanupWarning(JsonOutput output, RuntimeDownloadCleanup cleanup)
    {
        if (cleanup.Warning == null)
            return;

        output.Issues.Add(new Issue
        {
            Id = "runtime_download_cleanup_failed",
            Severity = "Warning",
            Message = cleanup.Warning
        });
    }

    static string EnsureTrailingSeparator(string path)
    {
        if (path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar))
            return path;
        return path + Path.DirectorySeparatorChar;
    }

    static string? ResolvePackageFromCache(string id, string version)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home)) return null;
        var lowerId = id.ToLowerInvariant();
        var path = Path.Combine(home, ".nuget", "packages", lowerId, version, $"{lowerId}.{version}.nupkg");
        return File.Exists(path) ? path : null;
    }

    static string DownloadNuGetPackage(string id, string version, string source, string runtimeCache)
    {
        var localPackage = ResolvePackageFromDirectory(id, version, source);
        if (localPackage != null)
            return localPackage;

        using var http = new HttpClient();
        using var indexDoc = JsonDocument.Parse(http.GetStringAsync(source).GetAwaiter().GetResult());
        var packageBase = indexDoc.RootElement.GetProperty("resources")
            .EnumerateArray()
            .Where(r => r.TryGetProperty("@type", out var t) &&
                        t.GetString()?.Contains("PackageBaseAddress", StringComparison.OrdinalIgnoreCase) == true)
            .Select(r => r.GetProperty("@id").GetString())
            .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s))
            ?? throw new InvalidOperationException($"NuGet source has no PackageBaseAddress resource: {source}");

        var lowerId = id.ToLowerInvariant();
        var url = $"{packageBase.TrimEnd('/')}/{lowerId}/{version}/{lowerId}.{version}.nupkg";
        var bytes = http.GetByteArrayAsync(url).GetAwaiter().GetResult();
        var downloadDir = Path.Combine(runtimeCache, "downloads");
        Directory.CreateDirectory(downloadDir);
        var outPath = Path.Combine(downloadDir, $"{lowerId}.{version}.nupkg");
        File.WriteAllBytes(outPath, bytes);
        return outPath;
    }

    static string? ResolvePackageFromDirectory(string id, string version, string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return null;

        string? path = null;
        if (Directory.Exists(source))
            path = source;
        else if (source.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(source);
                if (uri.IsFile && Directory.Exists(uri.LocalPath))
                    path = uri.LocalPath;
            }
            catch
            {
                return null;
            }
        }

        if (path == null)
            return null;

        var lowerId = id.ToLowerInvariant();
        var candidates = new[]
        {
            Path.Combine(path, $"{id}.{version}.nupkg"),
            Path.Combine(path, $"{lowerId}.{version}.nupkg"),
            Path.Combine(path, lowerId, version, $"{lowerId}.{version}.nupkg")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    static List<string> ExtractNativeFiles(string nupkgPath, string nativePrefix, string runtimeCache)
    {
        var files = new List<string>();
        using var archive = ZipFile.OpenRead(nupkgPath);
        foreach (var entry in archive.Entries)
        {
            var fullName = entry.FullName.Replace('\\', '/');
            if (!fullName.StartsWith(nativePrefix, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!IsNativeRuntimeFile(fullName))
                continue;

            var fileName = Path.GetFileName(fullName);
            if (string.IsNullOrWhiteSpace(fileName))
                continue;

            var outPath = Path.Combine(runtimeCache, fileName);
            entry.ExtractToFile(outPath, overwrite: true);
            files.Add(fileName);
        }

        if (files.Count == 0)
            throw new InvalidOperationException($"No native runtime files found under {nativePrefix} in {nupkgPath}");

        return files;
    }

    static bool IsNativeRuntimeFile(string path)
    {
        var fileName = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        var lower = fileName.ToLowerInvariant();
        return lower.EndsWith(".dll")
            || lower.EndsWith(".dylib")
            || lower.EndsWith(".so")
            || lower.Contains(".so.");
    }

    // ===== ocr batch =====

    static Command CreateBatch(Option<bool> jsonOpt)
    {
        var dirArg = new Argument<string>("dir", "Directory containing image files");
        var patternOpt = new Option<string>("--pattern", () => "*.png", "File pattern (e.g. *.jpg)");
        var recursiveOpt = new Option<bool>("--recursive", () => false, "Search subdirectories");
        var cmd = new Command("batch", "Batch OCR on all images in a directory") { dirArg, patternOpt, recursiveOpt };

        cmd.SetHandler((string dir, string pattern, bool recursive, bool json) =>
        {
            if (string.IsNullOrWhiteSpace(dir))
            {
                CliHelpers.WriteError("ocr batch", ErrorCodes.MissingArgument with { Message = "Directory path is required." }, json);
                return;
            }
            if (!Directory.Exists(dir))
            {
                CliHelpers.WriteError("ocr batch", ErrorCodes.FileNotFound with { Message = $"Directory not found: {dir}" }, json);
                return;
            }

            try
            {
                var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var files = Directory.EnumerateFiles(dir, pattern, searchOption)
                    .Where(f => IsImageExtension(f))
                    .OrderBy(f => f)
                    .ToList();

                if (files.Count == 0)
                {
                    var output = JsonOutput.Ok("ocr batch", "No matching image files found.",
                        new { dir = Path.GetFullPath(dir), pattern, fileCount = 0 });
                    Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                    return;
                }

                var (ocr, modelId) = CreateOcrClient();
                using var client = ocr;
                var results = new List<object>();
                var totalElapsed = 0L;

                foreach (var file in files)
                {
                    try
                    {
                        var (result, elapsed) = CliHelpers.Time(() =>
                            InvokeRecognize(client, file));
                        totalElapsed += elapsed;
                        var page = result.Pages.FirstOrDefault();

                        results.Add(new
                        {
                            file = Path.GetFullPath(file),
                            fileName = Path.GetFileName(file),
                            text = string.Join(" ", (page?.Blocks ?? new List<PpOcrV5Block>()).Select(b => b.Text)),
                            confidence = page?.Blocks.FirstOrDefault()?.Confidence,
                            blocks = page?.Blocks.Count ?? 0
                        });
                    }
                    catch (Exception ex)
                    {
                        results.Add(new
                        {
                            file = Path.GetFullPath(file),
                            fileName = Path.GetFileName(file),
                            error = ex.Message
                        });
                    }
                }

                var okCount = results.Count(r =>
                {
                    try { return ((dynamic)r).error == null; } catch { return true; }
                });

                if (json)
                {
                    var data = new
                    {
                        dir = Path.GetFullPath(dir),
                        pattern,
                        totalFiles = files.Count,
                        successCount = okCount,
                        results
                    };
                    var output = JsonOutput.Ok("ocr batch",
                        $"Batch OCR: {okCount}/{files.Count} files", data);
                    output.Metrics["totalFiles"] = files.Count;
                    output.Metrics["successCount"] = okCount;
                    output.Meta.DurationMs = totalElapsed;
                    Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"Batch: {okCount}/{files.Count}");
                    foreach (dynamic r in results)
                    {
                        if (r.error != null)
                            Console.WriteLine($"  FAIL {r.fileName}: {r.error}");
                        else
                            Console.WriteLine($"  OK   {r.fileName}: {r.text}");
                    }
                }
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("ocr batch", ErrorCodes.InternalError with { Message = ex.Message }, json);
            }
        }, dirArg, patternOpt, recursiveOpt, jsonOpt);

        return cmd;
    }

    // ===== ocr video =====

    static Command CreateVideo(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to video file");
        var outOpt = new Option<string>("-o", "Output directory (also writes .srt subtitle file)") { IsRequired = true };
        var fpsOpt = new Option<double>("--fps", () => 1, "Frames per second to sample");
        var thresholdOpt = new Option<int>("--dedup-threshold", () => 12, "dHash dedup threshold (0-64, lower=stricter)");
        var cmd = new Command("video", "Extract and OCR text from video frames") { fileArg, outOpt, fpsOpt, thresholdOpt };

        cmd.SetHandler((string file, string outputDir, double fps, int dedupThreshold, bool json) =>
        {
            if (string.IsNullOrWhiteSpace(file))
            {
                CliHelpers.WriteError("ocr video", ErrorCodes.MissingArgument with { Message = "Video path is required." }, json);
                return;
            }
            if (!File.Exists(file))
            {
                CliHelpers.WriteError("ocr video", ErrorCodes.FileNotFound with { Message = $"Video not found: {file}" }, json);
                return;
            }

            try
            {
                // Pre-load native runtime before VideoCapture (needs OpenCvSharpExtern + videoio DLLs)
                PpOcrV6Client.CheckEnvironment();
                using var cap = new OpenCvSharp.VideoCapture(file);
                if (!cap.IsOpened())
                {
                    CliHelpers.WriteError("ocr video",
                        ErrorCodes.DependencyMissing with { Message = $"Cannot open video: {file}. Ensure opencv_videoio_ffmpeg DLL is present in runtime directory." }, json);
                    return;
                }

                var sourceFps = cap.Fps > 0 ? cap.Fps : 30;
                var frameInterval = Math.Max(1, (int)(sourceFps / fps));

                CliHelpers.EnsureParentDir(Path.Combine(outputDir, ".keep"));
                var (ocr, modelId) = CreateOcrClient();
                using var client = ocr;
                using var frame = new OpenCvSharp.Mat();

                var allFrames = new List<FrameOcrResult>();
                var lastHash = 0UL;
                var frameIdx = 0;
                var processedIdx = 0;
                var totalElapsed = 0L;

                while (cap.Read(frame) && !frame.Empty())
                {
                    frameIdx++;
                    if (frameIdx % frameInterval != 0) continue;

                    var hash = ComputeDHash(frame);
                    var dist = HammingDistance(lastHash, hash);
                    if (processedIdx > 0 && dist < dedupThreshold)
                    {
                        lastHash = hash;
                        continue;
                    }
                    lastHash = hash;

                    var ocrResult = RecognizeOcrFrame(ocr, frame, out var elapsed);
                    totalElapsed += elapsed;

                    if (ocrResult != null)
                    {
                        processedIdx++;
                        var ts = TimeSpan.FromSeconds(frameIdx / sourceFps);
                        allFrames.Add(new FrameOcrResult
                        {
                            FrameIndex = frameIdx,
                            Timestamp = ts,
                            TimestampStr = $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2},{ts.Milliseconds:D3}",
                            Text = ocrResult.Value.Text.Trim(),
                            Confidence = ocrResult.Value.Confidence
                        });
                    }
                }

                var srtPath = Path.Combine(outputDir, "ocr.srt");
                WriteSrt(srtPath, allFrames);
                var framesPath = Path.Combine(outputDir, "frames.json");
                File.WriteAllText(framesPath, JsonSerializer.Serialize(allFrames, CliHelpers.JsonOpts));

                var textFrames = allFrames.Where(f => f.Text.Length > 0).ToList();

                if (json)
                {
                    var data = new
                    {
                        video = Path.GetFullPath(file),
                        sourceFps,
                        sampleFps = fps,
                        totalFrames = frameIdx,
                        sampledFrames = processedIdx,
                        textFrames = textFrames.Count,
                        textLines = textFrames.Select(f => f.Text).ToList(),
                        artifacts = new { srt = Path.GetFullPath(srtPath), framesJson = Path.GetFullPath(framesPath) }
                    };
                    var output = JsonOutput.Ok("ocr video",
                        $"Video OCR: {textFrames.Count} frames with text (from {processedIdx} sampled, {frameIdx} total)", data);
                    output.Artifacts["srt"] = Path.GetFullPath(srtPath);
                    output.Artifacts["framesJson"] = Path.GetFullPath(framesPath);
                    output.Metrics["totalFrames"] = frameIdx;
                    output.Metrics["sampledFrames"] = processedIdx;
                    output.Metrics["textFrames"] = textFrames.Count;
                    output.Meta.DurationMs = totalElapsed;
                    Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"Video: {textFrames.Count} unique text frames (from {processedIdx} sampled, {frameIdx} total)");
                    Console.WriteLine($"SRT: {srtPath}");
                    Console.WriteLine($"Frames JSON: {framesPath}");
                    foreach (var f in textFrames)
                        Console.WriteLine($"  {f.TimestampStr}  {f.Text}");
                }
            }
            catch (Exception ex) when (ex.Message.Contains("opencv_videoio") || ex is DllNotFoundException)
            {
                CliHelpers.WriteError("ocr video",
                    ErrorCodes.DependencyMissing with { Message = $"Video decoding requires opencv_videoio_ffmpeg DLL. {ex.Message}" }, json);
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("ocr video", ErrorCodes.InternalError with { Message = ex.Message }, json);
            }
        }, fileArg, outOpt, fpsOpt, thresholdOpt, jsonOpt);

        return cmd;
    }

    // ===== ocr screen =====

    static Command CreateScreen(Option<bool> jsonOpt)
    {
        var regionOpt = new Option<string>("--region", "Screen region x,y,w,h (e.g. 100,100,800,600)");
        var cmd = new Command("screen", "Capture and OCR a screen region (Windows)") { regionOpt };

        cmd.SetHandler((string? region, bool json) =>
        {
            if (!OperatingSystem.IsWindows())
            {
                CliHelpers.WriteError("ocr screen",
                    ErrorCodes.UnsupportedFormat with { Message = "ocr screen is Windows only." }, json);
                return;
            }

            try
            {
                Rectangle rect;
                if (!string.IsNullOrWhiteSpace(region))
                {
                    var parts = region.Split(',');
                    if (parts.Length != 4 ||
                        !int.TryParse(parts[0], out var x) || !int.TryParse(parts[1], out var y) ||
                        !int.TryParse(parts[2], out var w) || !int.TryParse(parts[3], out var h))
                    {
                        CliHelpers.WriteError("ocr screen",
                            ErrorCodes.ValidationFailed with { Message = $"Invalid region: {region}. Expected x,y,w,h (e.g. 100,100,800,600)" }, json);
                        return;
                    }
                    rect = new Rectangle(x, y, w, h);
                }
                else
                {
                    rect = GetPrimaryScreenBounds();
                }

                using var bmp = new Bitmap(rect.Width, rect.Height, PixelFormat.Format24bppRgb);
                using var g = Graphics.FromImage(bmp);
                g.CopyFromScreen(rect.X, rect.Y, 0, 0, rect.Size);

                var tmpPath = Path.Combine(Path.GetTempPath(), $"nong_screen_{Guid.NewGuid():N}.png");
                bmp.Save(tmpPath, ImageFormat.Png);

                var (ocr, _) = CreateOcrClient();
                using var client = ocr;
                var (result, elapsed) = CliHelpers.Time(() =>
                    InvokeRecognize(client, tmpPath));

                try { File.Delete(tmpPath); } catch { }

                var page = result.Pages.FirstOrDefault();
                var blocks = page?.Blocks ?? new List<PpOcrV5Block>();

                if (json)
                {
                    var data = new
                    {
                        region = new { x = rect.X, y = rect.Y, width = rect.Width, height = rect.Height },
                        text = string.Join("\n", blocks.Select(b => b.Text)),
                        blocks = blocks.Select(b => new
                        {
                            text = b.Text,
                            confidence = b.Confidence,
                            bbox = b.Bbox
                        })
                    };
                    var output = JsonOutput.Ok("ocr screen", $"Screen OCR: {blocks.Count} block(s)", data);
                    output.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                }
                else
                {
                    foreach (var b in blocks)
                        Console.WriteLine($"  {b.Text}\t{FormatConfidence(b.Confidence)}");
                }
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("ocr screen", ErrorCodes.InternalError with { Message = ex.Message }, json);
            }
        }, regionOpt, jsonOpt);

        return cmd;
    }

    // ===== ocr camera =====

    static Command CreateCamera(Option<bool> jsonOpt)
    {
        var deviceOpt = new Option<int>("--device", () => 0, "Camera device index");
        var intervalOpt = new Option<int>("--interval", () => 2000, "Capture interval in milliseconds");
        var countOpt = new Option<int>("--count", () => 5, "Number of captures (0 = unlimited)");
        var cmd = new Command("camera", "Capture and OCR frames from camera (requires opencv_videoio)") { deviceOpt, intervalOpt, countOpt };

        cmd.SetHandler((int device, int interval, int count, bool json) =>
        {
            // Pre-load native runtime before VideoCapture
            PpOcrV6Client.CheckEnvironment();
            using var cap = new OpenCvSharp.VideoCapture(device);
            if (!cap.IsOpened())
            {
                CliHelpers.WriteError("ocr camera",
                    ErrorCodes.DependencyMissing with { Message = $"Cannot open camera device {device}. Ensure a camera is connected and opencv_videoio_ffmpeg DLL is installed." }, json);
                return;
            }

            try
            {
                var (ocr, modelId) = CreateOcrClient();
                using var client = ocr;
                using var frame = new OpenCvSharp.Mat();
                var captures = new List<object>();
                var capIdx = 0;
                var totalElapsed = 0L;
                var maxCaptures = count <= 0 ? int.MaxValue : count;

                while (capIdx < maxCaptures)
                {
                    if (!cap.Read(frame) || frame.Empty())
                    {
                        if (capIdx == 0)
                        {
                            CliHelpers.WriteError("ocr camera",
                                ErrorCodes.ReadFailed with { Message = $"Cannot read frames from camera device {device}." }, json);
                            return;
                        }
                        break;
                    }

                    capIdx++;
                    var ocrResult = RecognizeOcrFrame(ocr, frame, out var elapsed);
                    totalElapsed += elapsed;
                    var text = ocrResult?.Text.Trim() ?? "";
                    var confidence = ocrResult?.Confidence;

                    captures.Add(new { capture = capIdx, timestamp = DateTimeOffset.UtcNow, text, confidence });
                    Console.Error.WriteLine($"[ocr camera] Capture {capIdx}: {Trunc(text, 50)}");

                    if (capIdx < maxCaptures)
                        Thread.Sleep(interval);
                }

                var allText = string.Join("\n", captures
                    .Select(c => (string)((dynamic)c).text)
                    .Where(t => t.Length > 0));

                if (json)
                {
                    var data = new { device, intervalMs = interval, captures, text = allText };
                    var output = JsonOutput.Ok("ocr camera", $"Camera OCR: {capIdx} capture(s)", data);
                    output.Meta.DurationMs = totalElapsed;
                    Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"Camera: {capIdx} capture(s)");
                    foreach (dynamic c in captures)
                    {
                        var label = c.confidence != null ? $"({c.confidence:F2}) " : "";
                        Console.WriteLine($"  [{c.capture}] {label}{c.text}");
                    }
                    if (allText.Length > 0)
                        Console.WriteLine($"\n---\n{allText}\n---");
                }
            }
            catch (Exception ex) when (ex.Message.Contains("opencv_videoio") || ex is DllNotFoundException)
            {
                CliHelpers.WriteError("ocr camera",
                    ErrorCodes.DependencyMissing with { Message = $"Camera capture requires opencv_videoio_ffmpeg DLL. {ex.Message}" }, json);
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("ocr camera", ErrorCodes.InternalError with { Message = ex.Message }, json);
            }
        }, deviceOpt, intervalOpt, countOpt, jsonOpt);

        return cmd;
    }

    // ===== shared OCR client helpers =====

    static (IDisposable Client, string ModelId) CreateOcrClient()
    {
        var (v6Avail, v6Sz, v6Path) = PpOcrV6ModelResolver.DetectInstalled();
        if (v6Avail && v6Sz != null && v6Path != null)
            return (new PpOcrV6Client(v6Sz, v6Path), $"pp-ocrv6-{v6Sz}");
        return (new PpOcrV6Client(), "pp-ocrv6-medium");
    }

    static PpOcrV5Result InvokeRecognize(IDisposable client, string imagePath)
    {
        return ((PpOcrV6Client)client).RecognizeAsync(imagePath).GetAwaiter().GetResult();
    }

    static (string Text, double? Confidence)? RecognizeOcrFrame(IDisposable ocr, OpenCvSharp.Mat frame, out long elapsedMs)
    {
        elapsedMs = 0;
        try
        {
            var tmp = Path.Combine(Path.GetTempPath(), $"nong_frame_{Guid.NewGuid():N}.png");
            OpenCvSharp.Cv2.ImWrite(tmp, frame);
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var result = InvokeRecognize(ocr, tmp);
                sw.Stop();
                elapsedMs = sw.ElapsedMilliseconds;
                var page = result.Pages.FirstOrDefault();
                var block = page?.Blocks.FirstOrDefault();
                return (block?.Text ?? "", block?.Confidence);
            }
            finally { try { File.Delete(tmp); } catch { } }
        }
        catch { return null; }
    }

    static ulong ComputeDHash(OpenCvSharp.Mat frame)
    {
        using var gray = new OpenCvSharp.Mat();
        OpenCvSharp.Cv2.CvtColor(frame, gray, OpenCvSharp.ColorConversionCodes.BGR2GRAY);
        using var small = new OpenCvSharp.Mat();
        OpenCvSharp.Cv2.Resize(gray, small, new OpenCvSharp.Size(9, 8));
        ulong hash = 0;
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                if (small.At<byte>(y, x) > small.At<byte>(y, x + 1))
                    hash |= 1UL << (y * 8 + x);
        return hash;
    }

    static int HammingDistance(ulong a, ulong b)
    {
        ulong x = a ^ b;
        int count = 0;
        while (x != 0) { count += (int)(x & 1); x >>= 1; }
        return count;
    }

    static void WriteSrt(string path, List<FrameOcrResult> frames)
    {
        using var w = new StreamWriter(path, false, System.Text.Encoding.UTF8);
        int idx = 1;
        foreach (var f in frames.Where(f => f.Text.Length > 0))
        {
            var end = f.Timestamp + TimeSpan.FromSeconds(3);
            var endStr = $"{(int)end.TotalHours:D2}:{end.Minutes:D2}:{end.Seconds:D2},{end.Milliseconds:D3}";
            w.WriteLine(idx);
            w.WriteLine($"{f.TimestampStr} --> {endStr}");
            w.WriteLine(f.Text);
            w.WriteLine();
            idx++;
        }
    }

    static bool IsImageExtension(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".tiff" or ".tif" or ".webp";
    }

    sealed record FrameOcrResult
    {
        public int FrameIndex { get; init; }
        public TimeSpan Timestamp { get; init; }
        public string TimestampStr { get; init; } = "";
        public string Text { get; init; } = "";
        public double? Confidence { get; init; }
    }

    static string Trunc(string s, int max) => s.Length <= max ? s : s[..max] + "...";

    static Rectangle GetPrimaryScreenBounds()
    {
        // Use User32 P/Invoke to avoid System.Windows.Forms dependency
        return new Rectangle(
            0, 0,
            GetSystemMetrics(0),  // SM_CXSCREEN
            GetSystemMetrics(1)); // SM_CYSCREEN
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern int GetSystemMetrics(int nIndex);
    }
}
