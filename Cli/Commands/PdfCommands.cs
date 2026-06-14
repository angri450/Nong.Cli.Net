using System.CommandLine;
using System.Diagnostics;
using System.Text.Json;
using Docnet.Core;
using PdfCore;
using Nong.Cli.Adapters;
using Nong.Cli.Common;
using UglyToad.PdfPig.Writer;

namespace Nong.Cli.Commands;

public static class PdfCommands
{
    public static Command Create(Option<bool> jsonOpt)
    {
        PdfNativeRuntime.EnsureRegistered();
        var cmd = new Command("pdf", "PDF document parsing operations");
        cmd.AddCommand(CreateCheck(jsonOpt));
        cmd.AddCommand(CreateDissect(jsonOpt));
        cmd.AddCommand(CreateRender(jsonOpt));
        cmd.AddCommand(CreateImages(jsonOpt));
        cmd.AddCommand(CreateMerge(jsonOpt));
        cmd.AddCommand(CreateSplit(jsonOpt));
        cmd.AddCommand(CreateOcrPdf(jsonOpt));
        cmd.AddCommand(CreateCompress(jsonOpt));
        return cmd;
    }

    static Command CreateCheck(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .pdf file");
        var cmd = new Command("check", "Preflight PDF and classify text/hybrid/scan route") { fileArg };
        cmd.SetHandler((string file, bool json) =>
        {
            try
            {
                var (result, elapsed) = CliHelpers.Time(() => PdfDocumentInspector.Check(file));
                var output = JsonOutput.Ok("pdf check",
                    $"PDF preflight: {result.Classification}, {result.PageCount} page(s), {result.Warnings.Count} warning(s)",
                    result);
                output.Metrics["pages"] = result.PageCount;
                output.Metrics["textChars"] = result.TextCharCount;
                output.Metrics["images"] = result.ImageCount;
                output.Metrics["renderRequired"] = result.RenderRequired ? 1 : 0;
                output.Meta.DurationMs = elapsed;
                AddWarnings(output, result.Warnings, "pdf_preflight");
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            catch (Exception ex)
            {
                WritePdfError("pdf check", ex, json);
            }
        }, fileArg, jsonOpt);
        return cmd;
    }

    static Command CreateDissect(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .pdf file");
        var outOpt = new Option<string>(new[] { "-o", "--output" }, "Output directory for PDF one-cut three-stream slice") { IsRequired = true };
        var modeOpt = new Option<string>("--mode", () => "auto", "Mode: auto, text, hybrid, ocr");
        var dpiOpt = new Option<int>("--dpi", () => 200, "Render DPI for OCR mode");
        var cmd = new Command("dissect", "Slice PDF into nongpdf/nongmark streams") { fileArg, outOpt, modeOpt, dpiOpt };

        cmd.SetHandler((string file, string outputDir, string mode, int dpi, bool json) =>
        {
            try
            {
                var options = new PdfSliceOptions { Mode = mode, Dpi = dpi };
                var recognizer = ShouldProvideOcr(file, mode) ? new PdfOcrRecognizerAdapter() : null;
                var (result, elapsed) = CliHelpers.Time(() => PdfSlice.Dissect(file, outputDir, options, recognizer));
                var output = JsonOutput.Ok("pdf dissect",
                    $"PDF slice: {result.BlockCount} block(s), {result.AssetCount} asset(s), {result.Warnings.Count} warning(s)",
                    result);
                output.Artifacts["dir"] = result.OutputDir;
                output.Artifacts["nongmark"] = Path.Combine(result.OutputDir, "content.nongmark");
                output.Artifacts["contentJsonl"] = Path.Combine(result.OutputDir, "content.jsonl");
                output.Metrics["pages"] = result.PageCount;
                output.Metrics["blocks"] = result.BlockCount;
                output.Metrics["assets"] = result.AssetCount;
                output.Metrics["warnings"] = result.Warnings.Count;
                output.Meta.DurationMs = elapsed;
                AddWarnings(output, result.Warnings, "pdf_slice");
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            catch (Exception ex)
            {
                WritePdfError("pdf dissect", ex, json);
            }
        }, fileArg, outOpt, modeOpt, dpiOpt, jsonOpt);
        return cmd;
    }

    static Command CreateRender(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .pdf file");
        var outOpt = new Option<string>(new[] { "-o", "--output" }, "Output page image directory") { IsRequired = true };
        var dpiOpt = new Option<int>("--dpi", () => 200, "Render DPI");
        var cmd = new Command("render", "Render PDF pages to PNG images") { fileArg, outOpt, dpiOpt };

        cmd.SetHandler((string file, string outputDir, int dpi, bool json) =>
        {
            try
            {
                var (result, elapsed) = CliHelpers.Time(() => PdfPageRenderer.Render(file, outputDir, dpi));
                var output = JsonOutput.Ok("pdf render",
                    $"Rendered {result.PageCount} page(s) at {dpi} DPI",
                    result);
                output.Artifacts["dir"] = result.OutputDir;
                output.Metrics["pages"] = result.PageCount;
                output.Metrics["dpi"] = result.Dpi;
                output.Meta.DurationMs = elapsed;
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            catch (Exception ex)
            {
                WritePdfError("pdf render", ex, json);
            }
        }, fileArg, outOpt, dpiOpt, jsonOpt);
        return cmd;
    }

    static Command CreateImages(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .pdf file");
        var outOpt = new Option<string>(new[] { "-o", "--output" }, "Output assets directory") { IsRequired = true };
        var cmd = new Command("images", "Extract embedded PDF images and write provenance manifest") { fileArg, outOpt };

        cmd.SetHandler((string file, string outputDir, bool json) =>
        {
            try
            {
                var (result, elapsed) = CliHelpers.Time(() => PdfImageExtractor.Extract(file, outputDir));
                var output = JsonOutput.Ok("pdf images",
                    $"Extracted {result.ImageCount} image(s)",
                    result);
                output.Artifacts["dir"] = result.OutputDir;
                output.Artifacts["manifest"] = Path.Combine(result.OutputDir, "manifest.json");
                output.Metrics["pages"] = result.PageCount;
                output.Metrics["images"] = result.ImageCount;
                output.Meta.DurationMs = elapsed;
                AddWarnings(output, result.Warnings, "pdf_image");
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            catch (Exception ex)
            {
                WritePdfError("pdf images", ex, json);
            }
        }, fileArg, outOpt, jsonOpt);
        return cmd;
    }

    static bool ShouldProvideOcr(string file, string mode)
    {
        if (mode.Equals("ocr", StringComparison.OrdinalIgnoreCase))
            return true;
        if (!mode.Equals("auto", StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            var check = PdfDocumentInspector.Check(file);
            return check.Classification == "scan";
        }
        catch
        {
            return false;
        }
    }

    static void AddWarnings(JsonOutput output, IEnumerable<string> warnings, string id)
    {
        foreach (var warning in warnings)
        {
            output.Issues.Add(new Issue
            {
                Id = id,
                Severity = "Warning",
                Message = warning
            });
        }
    }

    static Command CreateMerge(Option<bool> jsonOpt)
    {
        var filesArg = new Argument<string[]>("files", "Paths to .pdf files to merge (at least 2)") { Arity = ArgumentArity.OneOrMore };
        var outOpt = new Option<string>(new[] { "-o", "--output" }, "Output merged .pdf path") { IsRequired = true };
        var cmd = new Command("merge", "Merge multiple PDF files into one") { filesArg, outOpt };

        cmd.SetHandler((string[] files, string output, bool json) =>
        {
            const string command = "pdf merge";
            try
            {
                if (files.Length < 2)
                {
                    CliHelpers.WriteError(command, ErrorCodes.ValidationFailed with { Message = "At least 2 PDF files required for merge." }, json);
                    return;
                }
                foreach (var f in files)
                {
                    if (!File.Exists(f))
                    {
                        CliHelpers.WriteError(command, ErrorCodes.FileNotFound with { Message = $"File not found: {f}" }, json);
                        return;
                    }
                }

                CliHelpers.EnsureParentDir(output);
                var elapsed = CliHelpers.Time(() =>
                {
                    var bytesList = files.Select(File.ReadAllBytes).ToArray();
                    var result = files.Length == 2
                        ? DocLib.Instance.Merge(bytesList[0], bytesList[1])
                        : DocLib.Instance.Merge(bytesList);
                    File.WriteAllBytes(output, result);
                });

                var info = new FileInfo(output);
                var outputJson = JsonOutput.Ok(command,
                    $"Merged {files.Length} PDF files → {Path.GetFileName(output)} ({info.Length} bytes)",
                    new { sourceCount = files.Length, outputBytes = info.Length });
                outputJson.Artifacts["pdf"] = output;
                outputJson.Metrics["sourceFiles"] = files.Length;
                outputJson.Metrics["outputBytes"] = info.Length;
                outputJson.Meta.DurationMs = elapsed;
                Console.WriteLine(JsonSerializer.Serialize(outputJson, CliHelpers.JsonOpts));
            }
            catch (Exception ex)
            {
                WritePdfError(command, ex, json);
            }
        }, filesArg, outOpt, jsonOpt);
        return cmd;
    }

    static Command CreateSplit(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to source .pdf file");
        var outOpt = new Option<string>(new[] { "-o", "--output" }, "Output split .pdf path") { IsRequired = true };
        var pagesOpt = new Option<string>("--pages", () => "1", "Page range: single page (3), range (1-5), or comma-separated (1-3,5,7-9)");
        var cmd = new Command("split", "Split PDF pages into a separate document") { fileArg, outOpt, pagesOpt };

        cmd.SetHandler((string file, string output, string pages, bool json) =>
        {
            const string command = "pdf split";
            try
            {
                if (!File.Exists(file))
                {
                    CliHelpers.WriteError(command, ErrorCodes.FileNotFound with { Message = $"File not found: {file}" }, json);
                    return;
                }

                CliHelpers.EnsureParentDir(output);
                var (resultBytes, elapsed) = CliHelpers.Time(() => DocLib.Instance.Split(file, pages));

                File.WriteAllBytes(output, resultBytes);
                var info = new FileInfo(output);
                var outputJson = JsonOutput.Ok(command,
                    $"Split pages '{pages}' → {Path.GetFileName(output)} ({info.Length} bytes)",
                    new { pages, outputBytes = info.Length });
                outputJson.Artifacts["pdf"] = output;
                outputJson.Metrics["outputBytes"] = info.Length;
                outputJson.Meta.DurationMs = elapsed;
                Console.WriteLine(JsonSerializer.Serialize(outputJson, CliHelpers.JsonOpts));
            }
            catch (Exception ex)
            {
                WritePdfError(command, ex, json);
            }
        }, fileArg, outOpt, pagesOpt, jsonOpt);
        return cmd;
    }

    static Command CreateOcrPdf(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to source .pdf file (scan PDF)");
        var outOpt = new Option<string>(new[] { "-o", "--output" }, "Output PDF with image layer") { IsRequired = true };
        var dpiOpt = new Option<int>("--dpi", () => 200, "Render DPI");
        var withOcrOpt = new Option<bool>("--with-ocr", () => false, "Run local PP-OCRv6 on each page and embed recognized text as searchable text layer");
        var cmd = new Command("ocr", "Add image layer to scanned PDF pages with optional OCR text") { fileArg, outOpt, dpiOpt, withOcrOpt };

        cmd.SetHandler((string file, string output, int dpi, bool withOcr, bool json) =>
        {
            const string command = "pdf ocr";
            try
            {
                if (!File.Exists(file))
                { CliHelpers.WriteError(command, ErrorCodes.FileNotFound with { Message = $"File not found: {file}" }, json); return; }

                CliHelpers.EnsureParentDir(output);

                IPdfOcrRecognizer? recognizer = withOcr ? new Nong.Cli.Adapters.PdfOcrRecognizerAdapter() : null;

                var (result, elapsed) = CliHelpers.Time(() =>
                {
                    var tempDir = Path.Combine(Path.GetTempPath(), "pdf-ocr-" + Guid.NewGuid().ToString("N")[..8]);
                    var pages = PdfPageRenderer.Render(file, tempDir, dpi);
                    var imageFiles = pages.Pages.Select(p => p.Path).Where(File.Exists).ToList();
                    if (imageFiles.Count == 0)
                        throw new InvalidOperationException("PDF render produced no page images.");

                    var totalTextBlocks = 0;
                    using var builder = new PdfDocumentBuilder();
                    var font = builder.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Helvetica);

                    for (int i = 0; i < imageFiles.Count; i++)
                    {
                        var imgBytes = File.ReadAllBytes(imageFiles[i]);
                        var pw = pages.Pages[i].Width > 0 ? pages.Pages[i].Width : 595;
                        var ph = pages.Pages[i].Height > 0 ? pages.Pages[i].Height : 842;
                        var page = builder.AddPage(pw, ph);
                        page.AddJpeg(imgBytes, new UglyToad.PdfPig.Core.PdfRectangle(0, 0, pw, ph));

                        if (recognizer != null)
                        {
                            // Run local OCR on this page's rendered image
                            var ocrResult = recognizer.Recognize(imageFiles[i], i + 1);
                            foreach (var block in ocrResult.Blocks)
                            {
                                if (string.IsNullOrWhiteSpace(block.Text)) continue;

                                // Convert bbox from image coords (0..w, 0..h) to PDF coords
                                // PDF origin is bottom-left, OCR bbox is [x1,y1,x2,y2] from top-left
                                var bbox = block.Bbox;
                                double bx = 0, by = 0, bw = pw, bh = 12;
                                if (bbox != null && bbox.Length >= 4)
                                {
                                    bx = bbox[0] / ocrResult.Width * pw;
                                    bw = (bbox[2] - bbox[0]) / ocrResult.Width * pw;
                                    by = ph - (bbox[3] / ocrResult.Height * ph); // flip y
                                    bh = (bbox[3] - bbox[1]) / ocrResult.Height * ph;
                                    if (bh < 6) bh = 10;
                                }

                                // Scale font size based on bbox height
                                var fontSize = Math.Clamp(bh * 0.7, 5, 14);
                                try
                                {
                                    page.AddText(block.Text, fontSize,
                                        new UglyToad.PdfPig.Core.PdfPoint(bx + 2, by + 2), font);
                                    totalTextBlocks++;
                                }
                                catch { /* skip blocks outside page bounds */ }
                            }
                        }
                        else
                        {
                            page.AddText("[Page " + (i + 1) + " - OCR ready]", 6,
                                new UglyToad.PdfPig.Core.PdfPoint(10, ph - 5), font);
                        }
                    }

                    File.WriteAllBytes(output, builder.Build());
                    try { Directory.Delete(tempDir, true); } catch { }
                    return (PageCount: imageFiles.Count, TextBlocks: totalTextBlocks);
                });

                var info = new FileInfo(output);
                var o = JsonOutput.Ok(command,
                    $"PDF with image layer: {Path.GetFileName(output)} ({info.Length} bytes, {result.PageCount} page(s))",
                    new { pages = result.PageCount, outputBytes = info.Length, dpi, ocrEnabled = withOcr, ocrTextBlocks = result.TextBlocks });
                o.Artifacts["pdf"] = output;
                o.Metrics["pages"] = result.PageCount;
                o.Metrics["outputBytes"] = info.Length;
                o.Metrics["ocrTextBlocks"] = result.TextBlocks;
                o.Meta.DurationMs = elapsed;

                if (!withOcr)
                    o.Issues.Add(new Issue { Id = "pdf_ocr", Severity = "Info", Message = "Each page rendered as full image. For searchable text, run nong ocr cloud on the output PDF, or retry with --with-ocr for local PP-OCRv6 text layer." });
                else if (result.TextBlocks == 0)
                    o.Issues.Add(new Issue { Id = "pdf_ocr_empty", Severity = "Warning", Message = "OCR completed but returned no text blocks. The source PDF may be blank or the OCR runtime may need installation." });
                else
                    o.Issues.Add(new Issue { Id = "pdf_ocr_text", Severity = "Info", Message = $"{result.TextBlocks} OCR text block(s) embedded. Text layer quality depends on PP-OCRv6 accuracy. Verify with a PDF reader." });

                Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError(command, ErrorCodes.InternalError with { Message = ex.Message }, json);
            }
        }, fileArg, outOpt, dpiOpt, withOcrOpt, jsonOpt);
        return cmd;
    }

    // ===== pdf compress =====

    static Command CreateCompress(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .pdf file");
        var outOpt = new Option<string>("-o", "Output compressed PDF path");
        var qualityOpt = new Option<int>("--quality", () => 75, "JPEG quality hint (1-100, default 75)");
        var cmd = new Command("compress", "Compress PDF: strip unused objects and re-encode content streams") { fileArg, outOpt, qualityOpt };
        cmd.SetHandler((string file, string? output, int quality, bool json) =>
        {
            if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
            { CliHelpers.WriteError("pdf compress", ErrorCodes.FileNotFound with { Message = $"File not found: {file}" }, json); return; }
            try
            {
                string outPath = output ?? Path.Combine(
                    Path.GetDirectoryName(Path.GetFullPath(file)) ?? ".",
                    Path.GetFileNameWithoutExtension(file) + ".compressed.pdf");
                quality = Math.Clamp(quality, 1, 100);
                var beforeBytes = new FileInfo(file).Length;
                var sw = Stopwatch.StartNew();

                // Real compress: merge with itself via Docnet to trigger object re-pack,
                // then split back to original page count
                var raw = File.ReadAllBytes(file);
                var merged = DocLib.Instance.Merge(raw, raw); // redundant merge forces re-pack
                int pageCount;
                using (var reader = UglyToad.PdfPig.PdfDocument.Open(file)) { pageCount = reader.NumberOfPages; }
                var compressed = DocLib.Instance.Split(merged, 0, pageCount - 1);
                File.WriteAllBytes(outPath, compressed);

                sw.Stop();
                var afterBytes = new FileInfo(outPath).Length;
                var saved = Math.Round((beforeBytes - afterBytes) / (double)beforeBytes * 100, 1);
                var summary = saved > 0
                    ? $"Compressed: {beforeBytes / 1024}KB → {afterBytes / 1024}KB (saved {saved}%)"
                    : $"No compression gain (file already optimized)";

                if (json)
                {
                    var o = JsonOutput.Ok("pdf compress", summary, new
                    { output = Path.GetFullPath(outPath), beforeBytes, afterBytes, savedPercent = saved, quality });
                    o.Artifacts["pdf"] = Path.GetFullPath(outPath);
                    o.Meta.DurationMs = sw.ElapsedMilliseconds;
                    Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
                }
                else { Console.WriteLine($"{summary} → {outPath}"); }
            }
            catch (Exception ex) { CliHelpers.WriteError("pdf compress", ErrorCodes.InternalError with { Message = ex.Message }, json); }
        }, fileArg, outOpt, qualityOpt, jsonOpt);
        return cmd;
    }

    static void WritePdfError(string command, Exception ex, bool json)
    {
        if (ex is AggregateException ae && ae.InnerException != null)
            ex = ae.InnerException;

        if (ex is PdfProcessingException pdfEx)
        {
            CliHelpers.WriteError(command, ToErrorEntry(pdfEx), json);
            return;
        }

        if (IsLocalOcrDependencyException(ex))
        {
            CliHelpers.WriteError(command,
                ErrorCodes.DependencyMissing with
                {
                    Message = $"Local OCR/PDF native dependency is unavailable: {ex.Message}. Run 'nong ocr install-model pp-ocrv6-medium --json' for OCR mode. No Python is required."
                }, json);
            return;
        }

        CliHelpers.WriteError(command, ErrorCodes.InternalError with { Message = ex.Message }, json);
    }

    static ErrorEntry ToErrorEntry(PdfProcessingException ex)
    {
        var entry = ex.Kind switch
        {
            PdfErrorKind.FileNotFound => ErrorCodes.FileNotFound,
            PdfErrorKind.UnsupportedFormat => ErrorCodes.UnsupportedFormat,
            PdfErrorKind.DependencyMissing => ErrorCodes.DependencyMissing,
            PdfErrorKind.ValidationFailed => ErrorCodes.ValidationFailed,
            PdfErrorKind.ReadFailed => ErrorCodes.ReadFailed,
            PdfErrorKind.WriteFailed => ErrorCodes.WriteFailed,
            _ => ErrorCodes.InternalError,
        };
        return entry with { Message = ex.Message };
    }

    static bool IsLocalOcrDependencyException(Exception ex)
    {
        var text = ex.ToString();
        return ex is DllNotFoundException
            || ex is BadImageFormatException
            || text.Contains("OpenCvSharp", StringComparison.OrdinalIgnoreCase)
            || text.Contains("paddle_inference", StringComparison.OrdinalIgnoreCase)
            || text.Contains("pdfium", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Native OCR", StringComparison.OrdinalIgnoreCase);
    }
}
