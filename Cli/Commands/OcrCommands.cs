using System.CommandLine;
using System.Text.Json;
using System.IO.Compression;
using System.Globalization;
using Angri450.Nong.Data;
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
        var ingestOpt = new Option<bool>("--ingest", () => false, "Ingest OCR text into NongDb for semantic search");
        var cmd = new Command("cloud", "Cloud PaddleOCR-VL-1.6 via PADDLEOCR_ACCESS_TOKEN") { fileArg, outOpt, ingestOpt };

        cmd.SetHandler((string file, string outputDir, bool ingest, bool json) =>
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

                // Bug 15: .jfif files are functionally JPEG; rename to .jpg for API compatibility
                string uploadFile = file;
                string? tempJfif = null;
                if (Path.GetExtension(file).Equals(".jfif", StringComparison.OrdinalIgnoreCase))
                {
                    tempJfif = Path.Combine(Path.GetTempPath(), $"nong_ocr_{Guid.NewGuid():N}.jpg");
                    File.Copy(file, tempJfif, overwrite: true);
                    uploadFile = tempJfif;
                }

                using var client = new PaddleOcrVlClient(token);

                var (ocrResult, elapsed) = CliHelpers.Time(() =>
                {
                        // Use structured download for richer output
                        var task = ProcessStructuredAsync(client, uploadFile, outputDir);
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

                if (ingest && ocrResult.Pages.Count > 0)
                {
                    try
                    {
                        var texts = ocrResult.Pages.SelectMany(p => p.Blocks).Select(b => b.BlockContent ?? "").Where(t => !string.IsNullOrWhiteSpace(t));
                        var count = IngestHelper.IngestTexts(texts, file, "ocr", "cloud");
                        if (!json) Console.Error.WriteLine($"[ingest] {count} OCR blocks saved to nong.db");
                    }
                    catch (Exception ex) { if (!json) Console.Error.WriteLine($"[ingest] warning: {ex.Message}"); }
                }

                // Clean up temp .jfif→.jpg conversion
                if (tempJfif != null) { try { File.Delete(tempJfif); } catch { } }
            }
            catch (AggregateException ae) when (ae.InnerException != null)
            {
                WriteCloudError(ae.InnerException, json);
            }
            catch (Exception ex)
            {
                WriteCloudError(ex, json);
            }

        }, fileArg, outOpt, ingestOpt, jsonOpt);

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
        var ingestOpt = new Option<bool>("--ingest", () => false, "Ingest OCR text into NongDb for semantic search");
        var cmd = new Command("local", "Local PP-OCRv6 recognition with pure .NET runtime (no Python)") { imageArg, forceOpt, ingestOpt };

        cmd.SetHandler((string image, bool force, bool ingest, bool json) =>
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

                var (client, modelId) = CreateOcrClient();
                using var clientDispose = client;
                var (result, elapsed) = CliHelpers.Time(() =>
                    ((OcrOnnxEngine)client).Recognize(image));
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

                if (ingest && blocks.Count > 0)
                {
                    try
                    {
                        var texts = blocks.Select(b => b.Text ?? "").Where(t => !string.IsNullOrWhiteSpace(t));
                        var count = IngestHelper.IngestTexts(texts, image, "ocr", "local");
                        if (!json) Console.Error.WriteLine($"[ingest] {count} OCR text blocks saved to nong.db");
                    }
                    catch (Exception ex) { if (!json) Console.Error.WriteLine($"[ingest] warning: {ex.Message}"); }
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
        }, imageArg, forceOpt, ingestOpt, jsonOpt);

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

            // v6 ONNX auto-detection (no native runtime needed)
            var (v6Available, v6Size, v6CachePath) = PpOcrV6ModelResolver.DetectInstalled();
            var v6Status = v6Available ? "ok" : "missing";

            if (json)
            {
                var data = new
                {
                    imageAnalyzer = imageAnalyzerOk ? "ok" : "error",
                    cloudToken = tokenStatus,
                    ocrV6Onnx = new
                    {
                        status = v6Status,
                        engine = "pp-ocrv6-onnx",
                        runtime = "onnxruntime",
                        modelId = v6Available ? $"pp-ocrv6-{v6Size}" : "pp-ocrv6-medium",
                        modelSize = v6Available ? v6Size : null,
                        modelCachePath = v6Available ? v6CachePath : null,
                        noPython = true,
                        isDefault = true,
                        installCommand = "nong ocr install-model pp-ocrv6-medium --json",
                        message = v6Available
                            ? $"PP-OCRv6 {v6Size} ONNX model installed."
                            : "PP-OCRv6 ONNX model not installed. Run: nong ocr install-model pp-ocrv6-medium --json"
                    }
                };
                var output = JsonOutput.Ok("ocr check-env",
                    $"imageAnalyzer={data.imageAnalyzer}, token={tokenStatus}, v6={v6Status}",
                    data);
                output.Metrics["imageAnalyzer"] = imageAnalyzerOk ? 1 : 0;
                output.Metrics["cloudToken"] = tokenStatus == "missing" ? 0 : 1;
                output.Metrics["ocrV6Onnx"] = v6Available ? 1 : 0;

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
                Console.WriteLine($"PP-OCRv6 ONNX: {v6Status}" + (v6Available ? $" ({v6Size})" : " (not installed, run: nong ocr install-model pp-ocrv6-medium)"));
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

            // List v6 ONNX models
            foreach (var size in PpOcrV6ModelResolver.SupportedSizes)
            {
                var dir = PpOcrV6ModelResolver.GetModelCachePath(size);
                var avail = PpOcrV6ModelResolver.ValidateModelCache(dir);
                models.Add(new
                {
                    id = $"pp-ocrv6-{size}",
                    engine = "pp-ocrv6-onnx",
                    runtime = "onnxruntime",
                    modelSize = size,
                    deployment = "modelscope-onnx-git-clone",
                    available = avail,
                    isDefault = size == "medium",
                    noPython = true,
                    installCommand = $"nong ocr install-model pp-ocrv6-{size} --json",
                    modelCachePath = dir,
                    message = avail
                        ? $"PP-OCRv6 {size} ONNX model installed."
                        : $"Not installed. Run: nong ocr install-model pp-ocrv6-{size} --json"
                });
            }

            if (json)
            {
                var data = new { models, defaultModel = "pp-ocrv6-medium" };
                var output = JsonOutput.Ok("ocr models", $"Found {models.Count(m => ((dynamic)m).available)}/{models.Count} model(s)", data);
                output.Metrics["modelCount"] = models.Count;
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            else
            {
                foreach (dynamic m in models)
                    Console.WriteLine(JsonSerializer.Serialize(m, CliHelpers.JsonOpts));
            }
        }, jsonOpt);

        return cmd;
    }

    // ===== ocr install-model =====

    static Command CreateInstallModel(Option<bool> jsonOpt)
    {
        var modelIdArg = new Argument<string>("model-id", "Model ID: pp-ocrv6 (default=medium), pp-ocrv6-medium, pp-ocrv6-small, pp-ocrv6-tiny");
        var dryRunOpt = new Option<bool>("--dry-run", () => false, "Print download plan without executing");
        var cmd = new Command("install-model", "Install PP-OCRv6 ONNX model from ModelScope (no runtime needed)")
        {
            modelIdArg, dryRunOpt
        };

        cmd.SetHandler((string modelId, bool dryRun, bool json) =>
        {
            if (!PpOcrV6ModelResolver.AllModelIds.Contains(modelId))
            {
                CliHelpers.WriteError("ocr install-model",
                    ErrorCodes.ValidationFailed with { Message = $"Unknown model ID: {modelId}. Supported: {string.Join(", ", PpOcrV6ModelResolver.AllModelIds)}" }, json);
                return;
            }

            var (family, size) = PpOcrV6ModelResolver.ParseModelId(modelId);
            var modelDir = PpOcrV6ModelResolver.GetModelCachePath(size);
            var detUrl = string.Format(PpOcrV6ModelResolver.DetRepoBase, size);
            var recUrl = string.Format(PpOcrV6ModelResolver.RecRepoBase, size);

            if (dryRun)
            {
                var plan = new
                {
                    modelId = $"pp-ocrv6-{size}",
                    modelDir = modelDir.Replace('\\', '/'),
                    det = new { source = $"ModelScope: PaddlePaddle/PP-OCRv6_{size}_det_onnx", url = detUrl, file = "det.onnx" },
                    rec = new { source = $"ModelScope: PaddlePaddle/PP-OCRv6_{size}_rec_onnx", url = recUrl, file = "rec.onnx", dict = "dict.txt" },
                    runtime = "ONNX Runtime (Microsoft.ML.OnnxRuntime, already included in nong CLI)"
                };
                var output = JsonOutput.Ok("ocr install-model", $"Dry-run: pp-ocrv6-{size}", plan);
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                return;
            }

            try
            {
                Directory.CreateDirectory(modelDir);
                CloneModelFile(detUrl, modelDir, "det.onnx");
                CloneModelFile(recUrl, modelDir, "rec.onnx");

                // Extract dict from embedded resource (same as before)
                var dictPath = PpOcrV6ModelResolver.GetDictPath(modelDir);
                if (!File.Exists(dictPath))
                    PpOcrV6ModelResolver.ExtractDict(size, dictPath);

                // Write manifest
                var manifest = new
                {
                    schemaVersion = "pp-ocr-v6-onnx/1",
                    modelId = $"pp-ocrv6-{size}",
                    engine = "onnxruntime",
                    installedAt = DateTime.UtcNow
                };
                File.WriteAllText(Path.Combine(modelDir, "manifest.json"),
                    JsonSerializer.Serialize(manifest, CliHelpers.JsonOpts));

                var result = JsonOutput.Ok("ocr install-model",
                    $"ONNX model installed: pp-ocrv6-{size} → {modelDir.Replace('\\', '/')}",
                    new { modelId = $"pp-ocrv6-{size}", path = modelDir.Replace('\\', '/'), engine = "onnxruntime" });
                Console.WriteLine(JsonSerializer.Serialize(result, CliHelpers.JsonOpts));
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("ocr install-model",
                    ErrorCodes.DependencyMissing with { Message = $"Model install failed: {ex.Message}" }, json);
            }
        }, modelIdArg, dryRunOpt, jsonOpt);
        return cmd;
    }

    /// <summary>git clone --depth 1 a single ONNX file, rename to target name.</summary>
    static void CloneModelFile(string repoUrl, string destDir, string targetName)
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"nong-ocr-{Guid.NewGuid():N}");
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("git", $"clone --depth 1 --filter=blob:none --sparse {repoUrl} {tmpDir}")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            using var proc = System.Diagnostics.Process.Start(psi)!;
            proc.WaitForExit();
            if (proc.ExitCode != 0)
            {
                var err = proc.StandardError.ReadToEnd();
                throw new InvalidOperationException($"git clone failed (exit {proc.ExitCode}): {err}");
            }

            // sparse-checkout the onnx file
            psi = new System.Diagnostics.ProcessStartInfo("git", "sparse-checkout set inference.onnx")
            {
                UseShellExecute = false, CreateNoWindow = true,
                WorkingDirectory = tmpDir,
                RedirectStandardError = true, RedirectStandardOutput = true
            };
            using var proc2 = System.Diagnostics.Process.Start(psi)!;
            proc2.WaitForExit();
            // checkout to actually materialize the sparse file
            psi = new System.Diagnostics.ProcessStartInfo("git", "checkout")
            {
                UseShellExecute = false, CreateNoWindow = true,
                WorkingDirectory = tmpDir,
                RedirectStandardError = true, RedirectStandardOutput = true
            };
            using var proc3 = System.Diagnostics.Process.Start(psi)!;
            proc3.WaitForExit();

            var src = Path.Combine(tmpDir, "inference.onnx");
            if (!File.Exists(src))
                throw new FileNotFoundException($"inference.onnx not found after clone: {repoUrl}");
            File.Move(src, Path.Combine(destDir, targetName), overwrite: true);
        }
        finally
        {
            try { Directory.Delete(tmpDir, recursive: true); } catch { }
        }
    }

    // ===== ocr batch =====
    // ===== ocr batch =====

    static Command CreateBatch(Option<bool> jsonOpt)
    {
        var dirArg = new Argument<string>("dir", "Directory containing image files");
        var patternOpt = new Option<string>("--pattern", () => "*.png", "File pattern (e.g. *.jpg)");
        var recursiveOpt = new Option<bool>("--recursive", () => false, "Search subdirectories");
        var forceOpt = new Option<bool>("--force", () => false, "Run local OCR even if image preflight flags QR/code/graphic-heavy input");
        var cmd = new Command("batch", "Batch OCR on all images in a directory") { dirArg, patternOpt, recursiveOpt, forceOpt };

        cmd.SetHandler((string dir, string pattern, bool recursive, bool force, bool json) =>
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
                int preflightSkipped = 0;

                foreach (var file in files)
                {
                    try
                    {
                        // Preflight check (Bug 6: skip if preflight says so and --force not set)
                        if (!force)
                        {
                            try
                            {
                                var preflight = LocalOcrInputPreflight.Analyze(file);
                                if (preflight.ShouldSkip)
                                {
                                    preflightSkipped++;
                                    results.Add(new
                                    {
                                        file = Path.GetFullPath(file),
                                        fileName = Path.GetFileName(file),
                                        preflightSkipped = true,
                                        preflightClassification = preflight.Classification,
                                        preflightReason = preflight.Reason
                                    });
                                    continue;
                                }
                            }
                            catch
                            {
                                // Preflight analysis failure is non-fatal; proceed with OCR
                            }
                        }

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
                    try { return ((dynamic)r).error == null && ((dynamic)r).preflightSkipped == null; } catch { return true; }
                });

                if (json)
                {
                    var data = new
                    {
                        dir = Path.GetFullPath(dir),
                        pattern,
                        totalFiles = files.Count,
                        successCount = okCount,
                        preflightSkipped,
                        force,
                        results
                    };
                    var output = JsonOutput.Ok("ocr batch",
                        $"Batch OCR: {okCount}/{files.Count} files", data);
                    output.Metrics["totalFiles"] = files.Count;
                    output.Metrics["successCount"] = okCount;
                    output.Metrics["preflightSkipped"] = preflightSkipped;
                    output.Meta.DurationMs = totalElapsed;
                    Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"Batch: {okCount}/{files.Count}" + (preflightSkipped > 0 ? $" (preflight skipped {preflightSkipped})" : ""));
                    foreach (dynamic r in results)
                    {
                        if (r.error != null)
                            Console.WriteLine($"  FAIL {r.fileName}: {r.error}");
                        else if (r.preflightSkipped == true)
                            Console.WriteLine($"  SKIP {r.fileName}: [{r.preflightClassification}] {r.preflightReason}");
                        else
                            Console.WriteLine($"  OK   {r.fileName}: {r.text}");
                    }
                }
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("ocr batch", ErrorCodes.InternalError with { Message = ex.Message }, json);
            }
        }, dirArg, patternOpt, recursiveOpt, forceOpt, jsonOpt);

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
            return (new OcrOnnxEngine(v6Path, $"pp-ocrv6-{v6Sz}"), $"pp-ocrv6-{v6Sz}");
        // Fallback — try medium default path
        var defaultDir = PpOcrV6ModelResolver.GetModelCachePath("medium");
        if (!Directory.Exists(defaultDir)) Directory.CreateDirectory(defaultDir);
        return (new OcrOnnxEngine(defaultDir), "pp-ocrv6-medium");
    }

    static PpOcrV5Result InvokeRecognize(IDisposable client, string imagePath)
    {
        return ((OcrOnnxEngine)client).Recognize(imagePath);
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
        return ext is ".png" or ".jpg" or ".jpeg" or ".jfif" or ".bmp" or ".tiff" or ".tif" or ".webp";
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

    static bool IsImageAnalyzerAvailable()
    {
        try
        {
            var testPng = Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
            using var bmp = SkiaSharp.SKBitmap.Decode(testPng);
            return bmp != null;
        }
        catch { return false; }
    }
    }
}
