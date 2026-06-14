using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocxCore;
using Nong.Cli.Common;
using Nong.Inspect;
using A = DocumentFormat.OpenXml.Drawing;

namespace Nong.Cli.Commands;

/// <summary>
/// Word command group: preflight, conversion, reading, slicing, validation, and editing.
/// </summary>
public static class WordCommands
{
    public static Command Create(Option<bool> jsonOpt)
    {
        var cmd = new Command("word", "Word document operations");

        // === Phase 2: read + preview ===
        cmd.AddCommand(CreateCheck(jsonOpt));
        cmd.AddCommand(CreateConvert(jsonOpt));
        cmd.AddCommand(CreateCreate(jsonOpt));
        cmd.AddCommand(CreateRead(jsonOpt));
        cmd.AddCommand(CreatePreview(jsonOpt));

        // === Phase 6: fill + rebuild ===
        cmd.AddCommand(CreateFill(jsonOpt));
        cmd.AddCommand(CreateRebuild(jsonOpt));

        // === Phase 7: extract, dissect, stats, fonts, styles, validate, merge ===
        cmd.AddCommand(CreateExtract(jsonOpt));
        cmd.AddCommand(CreateDissect(jsonOpt));
        cmd.AddCommand(CreateStats(jsonOpt));
        cmd.AddCommand(CreateFonts(jsonOpt));
        cmd.AddCommand(CreateStyles(jsonOpt));
        cmd.AddCommand(CreateValidate(jsonOpt));
        cmd.AddCommand(CreateMerge(jsonOpt));

        // === Stage 15: read commands ===
        cmd.AddCommand(CreateOutline(jsonOpt));
        cmd.AddCommand(CreateImages(jsonOpt));
        cmd.AddCommand(CreateCrop(jsonOpt));
        cmd.AddCommand(CreateFitImages(jsonOpt));
        cmd.AddCommand(CreateCompactTables(jsonOpt));
        cmd.AddCommand(CreateRegroupImages(jsonOpt));
        cmd.AddCommand(CreateEstimate(jsonOpt));
        cmd.AddCommand(CreatePageSetup(jsonOpt));
        cmd.AddCommand(CreateIndent(jsonOpt));
        cmd.AddCommand(CreateParagraphControl(jsonOpt));
        cmd.AddCommand(CreateImageWrap(jsonOpt));
        cmd.AddCommand(CreateCellFormat(jsonOpt));
        cmd.AddCommand(CreateRunFormat(jsonOpt));
        cmd.AddCommand(CreateComments(jsonOpt));
        cmd.AddCommand(CreateRevisions(jsonOpt));
        cmd.AddCommand(CreateInferFormat(jsonOpt));

        // === Stage 15: modify commands ===
        cmd.AddCommand(CreateFixOrder(jsonOpt));
        cmd.AddCommand(CreateAcademicFormat(jsonOpt));
        cmd.AddCommand(CreateFormatGongwen(jsonOpt));
        cmd.AddCommand(CreateFormatAudit(jsonOpt));
        cmd.AddCommand(CreateRepairPlan(jsonOpt));
        cmd.AddCommand(CreateTableReflow(jsonOpt));
        cmd.AddCommand(CreateProtect(jsonOpt));
        cmd.AddCommand(CreateEmbedFont(jsonOpt));

        // === Stage 15: add commands ===
        cmd.AddCommand(CreateAddGroup(jsonOpt));
        cmd.AddCommand(CreateAddParagraph(jsonOpt));
        cmd.AddCommand(CreateAddTable(jsonOpt));
        cmd.AddCommand(CreateAddFootnote(jsonOpt));
        cmd.AddCommand(CreateAddEndnote(jsonOpt));
        cmd.AddCommand(CreateAddImage(jsonOpt));
        cmd.AddCommand(CreateAddToc(jsonOpt));
        cmd.AddCommand(CreateAddXref(jsonOpt));
        cmd.AddCommand(CreateAddLink(jsonOpt));
        cmd.AddCommand(CreateAddBookmark(jsonOpt));
        cmd.AddCommand(CreateAddComment(jsonOpt));
        cmd.AddCommand(CreateAddMath(jsonOpt));
        cmd.AddCommand(CreateCompare(jsonOpt));
        cmd.AddCommand(CreateRenderPreview(jsonOpt));

        return cmd;
    }

    // ===== word check =====

    static Command CreateCheck(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .doc or .docx file");
        var cmd = new Command("check", "Preflight a Word document before editing") { fileArg };

        cmd.SetHandler((string file, bool json) =>
        {
            if (string.IsNullOrWhiteSpace(file))
            {
                CliHelpers.WriteError("word check", ErrorCodes.MissingArgument with { Message = "File path is required." }, json);
                return;
            }
            if (!File.Exists(file))
            {
                CliHelpers.WriteError("word check", ErrorCodes.FileNotFound with { Message = $"File not found: {file}" }, json);
                return;
            }

            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext == ".doc")
            {
                var data = new WordCheckResult(
                    InputFormat: "doc",
                    CanProcessDirectly: false,
                    Paragraphs: null,
                    Tables: null,
                    DrawingImages: null,
                    VmlImages: null,
                    ImageParts: null,
                    BlockIdStatus: "unavailable_until_conversion",
                    NextSteps:
                    [
                        "Run: nong word convert <file.doc> -o <file.docx> --json",
                        "Then run: nong word check <file.docx> --json",
                        "Then use dissect/fix-order/validate on the converted .docx"
                    ],
                    Warnings: ["Legacy binary .doc is outside OpenXML and must be converted before nong word inspection or editing."]
                );
                WriteCheckOutput(data, json);
                return;
            }

            if (ext != ".docx")
            {
                CliHelpers.WriteError("word check",
                    ErrorCodes.UnsupportedFormat with { Message = $"Expected .doc or .docx file, got: {ext}" }, json);
                return;
            }

            try
            {
                var (data, elapsed) = CliHelpers.Time(() => CheckDocx(file));
                WriteCheckOutput(data, json, elapsed);
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("word check",
                    ErrorCodes.ReadFailed with { Message = $"Cannot inspect DOCX: {ex.Message}" }, json);
            }
        }, fileArg, jsonOpt);

        return cmd;
    }

    static void WriteCheckOutput(WordCheckResult data, bool json, long elapsed = 0)
    {
        if (json)
        {
            var output = JsonOutput.Ok("word check",
                data.CanProcessDirectly
                    ? $"DOCX preflight: {data.Warnings.Count} warning(s)"
                    : "Word preflight: conversion required",
                data);
            output.Meta.DurationMs = elapsed;
            output.Metrics["warnings"] = data.Warnings.Count;
            output.Metrics["canProcessDirectly"] = data.CanProcessDirectly ? 1 : 0;
            foreach (var warning in data.Warnings)
            {
                output.Issues.Add(new Issue
                {
                    Id = warning.Contains("VML", StringComparison.OrdinalIgnoreCase)
                        ? "vml_picture"
                        : "word_preflight",
                    Severity = "warning",
                    Message = warning
                });
            }
            Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
        }
        else
        {
            Console.WriteLine($"Format: {data.InputFormat}");
            Console.WriteLine($"Direct OpenXML processing: {data.CanProcessDirectly}");
            Console.WriteLine($"Block IDs: {data.BlockIdStatus}");
            foreach (var warning in data.Warnings)
                Console.Error.WriteLine($"[WARN] {warning}");
            foreach (var step in data.NextSteps)
                Console.WriteLine($"- {step}");
        }
    }

    static WordCheckResult CheckDocx(string file)
    {
        using var doc = WordprocessingDocument.Open(file, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        var paragraphs = body?.Elements<Paragraph>().Count() ?? 0;
        var tables = body?.Elements<Table>().Count() ?? 0;
        var drawingImages = body?.Descendants<A.Blip>().Count() ?? 0;
        var vmlImages = body?.Descendants()
            .Count(e => e.LocalName.Equals("imagedata", StringComparison.OrdinalIgnoreCase)) ?? 0;
        var imageParts = doc.MainDocumentPart?.ImageParts.Count() ?? 0;

        var warnings = new List<string>();
        var next = new List<string>
        {
            "Run: nong word dissect <file.docx> --output <slice-dir> --json",
            "Review content.jsonl, structure.json, format.json, and assets/manifest.json"
        };

        if (vmlImages > 0)
        {
            warnings.Add($"{vmlImages} VML image reference(s) found. These are legacy picture/formula images; Nong surfaces them as image blocks/assets, not editable text.");
            next.Add("If formulas must become editable equations, extract the image assets and OCR/retype them separately.");
        }

        if (paragraphs == 0 && tables == 0)
            warnings.Add("No body paragraphs or tables were found.");

        next.Add("Use --after with block IDs from content.jsonl or structure.json only after slicing.");

        return new WordCheckResult(
            InputFormat: "docx",
            CanProcessDirectly: true,
            Paragraphs: paragraphs,
            Tables: tables,
            DrawingImages: drawingImages,
            VmlImages: vmlImages,
            ImageParts: imageParts,
            BlockIdStatus: "generated_by_dissect",
            NextSteps: next,
            Warnings: warnings
        );
    }

    sealed record WordCheckResult(
        string InputFormat,
        bool CanProcessDirectly,
        int? Paragraphs,
        int? Tables,
        int? DrawingImages,
        int? VmlImages,
        int? ImageParts,
        string BlockIdStatus,
        List<string> NextSteps,
        List<string> Warnings
    );

    // ===== word convert =====

    static Command CreateConvert(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .doc or .docx file");
        var outOpt = new Option<string>("-o", "Output .docx path") { IsRequired = true };
        var engineOpt = new Option<string>("--engine", () => "auto", "Conversion engine: auto, libreoffice, word");
        var cmd = new Command("convert", "Convert legacy .doc to .docx as a boundary step") { fileArg, outOpt, engineOpt };

        cmd.SetHandler((string file, string output, string engine, bool json) =>
        {
            if (string.IsNullOrWhiteSpace(file))
            {
                CliHelpers.WriteError("word convert", ErrorCodes.MissingArgument with { Message = "File path is required." }, json);
                return;
            }
            if (!File.Exists(file))
            {
                CliHelpers.WriteError("word convert", ErrorCodes.FileNotFound with { Message = $"File not found: {file}" }, json);
                return;
            }
            if (!string.Equals(Path.GetExtension(output), ".docx", StringComparison.OrdinalIgnoreCase))
            {
                CliHelpers.WriteError("word convert", ErrorCodes.ValidationFailed with { Message = "Output path must end with .docx." }, json);
                return;
            }

            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext is not ".doc" and not ".docx")
            {
                CliHelpers.WriteError("word convert",
                    ErrorCodes.UnsupportedFormat with { Message = $"Expected .doc or .docx file, got: {ext}" }, json);
                return;
            }

            try
            {
                CliHelpers.EnsureParentDir(output);
                var (result, elapsed) = CliHelpers.Time(() => ConvertWord(file, output, engine));
                var aerr = CliHelpers.CheckArtifact(output, "DOCX");
                if (aerr != null) { CliHelpers.WriteError("word convert", aerr, json); return; }

                if (json)
                {
                    var outputJson = JsonOutput.Ok("word convert", $"Converted with {result.Engine}: {output}", result);
                    outputJson.Artifacts["docx"] = Path.GetFullPath(output);
                    outputJson.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(outputJson, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"OK: {Path.GetFullPath(output)} ({result.Engine})");
                }
            }
            catch (InvalidOperationException ex)
            {
                CliHelpers.WriteError("word convert", ErrorCodes.DependencyMissing with { Message = ex.Message }, json);
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("word convert", ErrorCodes.InternalError with { Message = $"Conversion failed: {ex.Message}" }, json);
            }
        }, fileArg, outOpt, engineOpt, jsonOpt);

        return cmd;
    }

    static WordConvertResult ConvertWord(string file, string output, string engine)
    {
        var inputFull = Path.GetFullPath(file);
        var outputFull = Path.GetFullPath(output);
        if (string.Equals(inputFull, outputFull, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Input and output paths must be different.");

        var ext = Path.GetExtension(file).ToLowerInvariant();
        if (ext == ".docx")
        {
            File.Copy(inputFull, outputFull, true);
            return new WordConvertResult(inputFull, outputFull, "copy", []);
        }

        engine = engine.ToLowerInvariant();
        if (engine is not "auto" and not "libreoffice" and not "word")
            throw new InvalidOperationException("Unknown --engine. Supported: auto, libreoffice, word.");

        var errors = new List<string>();
        if (engine is "auto" or "libreoffice")
        {
            try
            {
                if (TryConvertWithLibreOffice(inputFull, outputFull, out var detail))
                    return new WordConvertResult(inputFull, outputFull, "libreoffice", detail);
                errors.Add("LibreOffice was not found on PATH or common install paths.");
            }
            catch (Exception ex)
            {
                errors.Add($"LibreOffice failed: {ex.Message}");
                if (engine == "libreoffice") throw new InvalidOperationException(errors[^1]);
            }
        }

        if (engine is "auto" or "word")
        {
            try
            {
                if (TryConvertWithWordCom(inputFull, outputFull, out var detail))
                    return new WordConvertResult(inputFull, outputFull, "word-com", detail);
                errors.Add("Microsoft Word COM automation is unavailable.");
            }
            catch (Exception ex)
            {
                errors.Add($"Word COM failed: {ex.Message}");
                if (engine == "word") throw new InvalidOperationException(errors[^1]);
            }
        }

        throw new InvalidOperationException("No .doc converter is available. Install LibreOffice or Microsoft Word, then rerun word convert. Details: " + string.Join(" | ", errors));
    }

    static bool TryConvertWithLibreOffice(string inputFull, string outputFull, out List<string> detail)
    {
        detail = new List<string>();
        var soffice = FindExecutable("soffice") ?? FindLibreOfficeOnWindows();
        if (soffice == null) return false;

        var tempDir = Path.Combine(Path.GetTempPath(), "nong-word-convert-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = soffice,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var arg in new[] { "--headless", "--convert-to", "docx", "--outdir", tempDir, inputFull })
                psi.ArgumentList.Add(arg);

            using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Cannot start LibreOffice.");
            var stdout = proc.StandardOutput.ReadToEnd();
            var stderr = proc.StandardError.ReadToEnd();
            if (!proc.WaitForExit(120000))
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                throw new TimeoutException("LibreOffice conversion timed out.");
            }
            if (proc.ExitCode != 0)
                throw new InvalidOperationException($"LibreOffice exit code {proc.ExitCode}: {stderr}");

            var converted = Directory.GetFiles(tempDir, "*.docx").FirstOrDefault();
            if (converted == null)
                throw new InvalidOperationException($"LibreOffice did not produce a .docx file. stdout: {stdout} stderr: {stderr}");

            File.Copy(converted, outputFull, true);
            detail.Add($"soffice={soffice}");
            return true;
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    static string? FindExecutable(string name)
    {
        var paths = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        var extensions = OperatingSystem.IsWindows()
            ? new[] { ".exe", ".cmd", ".bat", "" }
            : new[] { "" };
        foreach (var dir in paths)
        foreach (var ext in extensions)
        {
            var candidate = Path.Combine(dir, name + ext);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    static string? FindLibreOfficeOnWindows()
    {
        if (!OperatingSystem.IsWindows()) return null;
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LibreOffice", "program", "soffice.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "LibreOffice", "program", "soffice.exe"),
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    static bool TryConvertWithWordCom(string inputFull, string outputFull, out List<string> detail)
    {
        detail = new List<string>();
        if (!OperatingSystem.IsWindows()) return false;

        var wordType = Type.GetTypeFromProgID("Word.Application");
        if (wordType == null) return false;

        object? word = null;
        object? documents = null;
        object? document = null;
        try
        {
            word = Activator.CreateInstance(wordType);
            if (word == null) return false;
            dynamic dword = word;
            dword.Visible = false;
            dword.DisplayAlerts = 0;
            documents = dword.Documents;
            dynamic ddocs = documents;
            document = ddocs.Open(inputFull, false, true, false);
            dynamic ddoc = document;
            ddoc.SaveAs2(outputFull, 16);
            ddoc.Close(false);
            document = null;
            dword.Quit(false);
            word = null;
            detail.Add("Word COM SaveAs2 format=16");
            return true;
        }
        finally
        {
            ReleaseWordComObject(document, closeDocument: true);
            if (word != null)
            {
                try { ((dynamic)word).Quit(false); } catch { }
            }
            ReleaseWordComObject(documents);
            ReleaseWordComObject(word);
        }
    }

    static void ReleaseWordComObject(object? value, bool closeDocument = false)
    {
        if (value == null) return;
        try
        {
            if (closeDocument) ((dynamic)value).Close(false);
        }
        catch { }
        try
        {
            if (Marshal.IsComObject(value))
                Marshal.FinalReleaseComObject(value);
        }
        catch { }
    }

    sealed record WordConvertResult(
        string Input,
        string Output,
        string Engine,
        List<string> Details
    );

    // ===== word create =====

    static Command CreateCreate(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to authored .nongmark or .nmk source");
        var outOpt = new Option<string>("-o", "Output .docx path") { IsRequired = true };
        var cmd = new Command("create", "Create a DOCX directly from NongMark") { fileArg, outOpt };

        cmd.SetHandler((string file, string output, bool json) =>
        {
            const string command = "word create";
            var err = ValidateNongMarkFile(file);
            if (err != null) { CliHelpers.WriteError(command, err, json); return; }

            if (!string.Equals(Path.GetExtension(output), ".docx", StringComparison.OrdinalIgnoreCase))
            {
                CliHelpers.WriteError(command,
                    ErrorCodes.ValidationFailed with { Message = "Output path must end with .docx." }, json);
                return;
            }

            if (string.Equals(Path.GetFullPath(file), Path.GetFullPath(output), StringComparison.OrdinalIgnoreCase))
            {
                CliHelpers.WriteError(command,
                    ErrorCodes.ValidationFailed with { Message = "Input and output paths must be different." }, json);
                return;
            }

            try
            {
                CliHelpers.EnsureParentDir(output);
                var (result, elapsed) = CliHelpers.Time(() =>
                {
                    var built = NongMarkDocumentBuilder.Build(file, output);
                    FixOrderInPlace(output);
                    return built;
                });
                var aerr = CliHelpers.CheckArtifact(output, "DOCX");
                if (aerr != null) { CliHelpers.WriteError(command, aerr, json); return; }

                var o = JsonOutput.Ok(command,
                    $"Created DOCX from NongMark: {result.Blocks} blocks",
                    result);
                o.Artifacts["docx"] = Path.GetFullPath(output);
                o.Metrics["blocks"] = result.Blocks;
                o.Metrics["paragraphs"] = result.Paragraphs;
                o.Metrics["headings"] = result.Headings;
                o.Metrics["tables"] = result.Tables;
                o.Metrics["images"] = result.Images;
                o.Metrics["references"] = result.References;
                o.Meta.DurationMs = elapsed;
                Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
            }
            catch (FileNotFoundException ex)
            {
                CliHelpers.WriteError(command, ErrorCodes.FileNotFound with { Message = ex.Message }, json);
            }
            catch (InvalidDataException ex)
            {
                CliHelpers.WriteError(command, ErrorCodes.ValidationFailed with { Message = ex.Message }, json);
            }
            catch (ArgumentException ex)
            {
                CliHelpers.WriteError(command, ErrorCodes.ValidationFailed with { Message = ex.Message }, json);
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError(command, ErrorCodes.InternalError with { Message = ex.Message }, json);
            }
        }, fileArg, outOpt, jsonOpt);

        return cmd;
    }

    static ErrorEntry? ValidateNongMarkFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return ErrorCodes.MissingArgument with { Message = "File path is required." };
        if (!File.Exists(path))
            return ErrorCodes.FileNotFound with { Message = $"File not found: {path}" };
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is not ".nongmark" and not ".nmk")
            return ErrorCodes.UnsupportedFormat with { Message = $"Expected .nongmark or .nmk file, got: {ext}" };
        return null;
    }

    static void FixOrderInPlace(string output)
    {
        var fixedTmp = Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(output)) ?? Directory.GetCurrentDirectory(),
            Path.GetFileNameWithoutExtension(output) + ".fixed-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            WordEditOperations.FixOrder(output, fixedTmp);
            File.Copy(fixedTmp, output, true);
        }
        finally
        {
            try { File.Delete(fixedTmp); } catch { }
        }
    }

    // ===== word read =====

    static Command CreateRead(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var cmd = new Command("read", "Extract plain text from a .docx file") { fileArg };

        cmd.SetHandler((string file, bool json) =>
        {
            var err = CliHelpers.ValidateDocxFile(file);
            if (err != null)
            {
                CliHelpers.WriteError("word read", err, json);
                return;
            }

            var (result, elapsed) = CliHelpers.Time(() => WordTextReader.Read(file));

            if (json)
            {
                var data = new
                {
                    text = result.Text,
                    paragraphs = result.Paragraphs,
                    tables = result.Tables,
                    footnotes = result.Footnotes,
                    endnotes = result.Endnotes
                };
                var metrics = new Dictionary<string, object>
                {
                    ["characters"] = result.Text.Length,
                    ["paragraphs"] = result.Paragraphs.Count,
                    ["tables"] = result.Tables.Count,
                    ["footnotes"] = result.Footnotes.Count,
                    ["endnotes"] = result.Endnotes.Count
                };
                var output = JsonOutput.Ok("word read", $"Extracted {result.Paragraphs.Count} paragraphs, {result.Tables.Count} tables", data);
                foreach (var kv in metrics) output.Metrics[kv.Key] = kv.Value;
                output.Meta.DurationMs = elapsed;
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            else
            {
                Console.Write(result.Text);
            }


        }, fileArg, jsonOpt);

        return cmd;
    }

    // ===== word preview =====

    static Command CreatePreview(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var cmd = new Command("preview", "7-step document structure diagnostic") { fileArg };
        cmd.AddAlias("diagnose");

        cmd.SetHandler((string file, bool json) =>
        {
            var err = CliHelpers.ValidateDocxFile(file);
            if (err != null)
            {
                CliHelpers.WriteError("word preview", err, json);
                return;
            }

            var (pr, elapsed) = CliHelpers.Time(() => WordPreview.Preview(file));

            if (json)
            {
                var data = new
                {
                    text = pr.Text,
                    warnings = pr.Warnings,
                    errors = pr.Errors,
                    info = pr.Info,
                    statistics = new
                    {
                        paragraphs = pr.Statistics.Paragraphs,
                        tables = pr.Statistics.Tables,
                        images = pr.Statistics.Images,
                        ooxmlErrors = pr.Statistics.OoxmlErrors,
                        ooxmlWarnings = pr.Statistics.OoxmlWarnings
                    }
                };
                var metrics = new Dictionary<string, object>
                {
                    ["paragraphs"] = pr.Statistics.Paragraphs,
                    ["tables"] = pr.Statistics.Tables,
                    ["images"] = pr.Statistics.Images,
                    ["ooxml_errors"] = pr.Statistics.OoxmlErrors,
                    ["ooxml_warnings"] = pr.Statistics.OoxmlWarnings
                };
                var output = JsonOutput.Ok("word preview", $"Diagnosed: {pr.Warnings.Count} warnings, {pr.Errors.Count} errors", data);
                foreach (var kv in metrics) output.Metrics[kv.Key] = kv.Value;
                output.Meta.DurationMs = elapsed;
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            else
            {
                Console.WriteLine(pr.Text);
                if (pr.Errors.Count > 0)
                {
                    Console.Error.WriteLine($"=== Errors ({pr.Errors.Count}) ===");
                    foreach (var e in pr.Errors) Console.Error.WriteLine($"  [ERR] {e}");
                }
                if (pr.Warnings.Count > 0)
                {
                    Console.Error.WriteLine($"=== Warnings ({pr.Warnings.Count}) ===");
                    foreach (var w in pr.Warnings) Console.Error.WriteLine($"  [WARN] {w}");
                }
                if (pr.Info.Count > 0)
                {
                    Console.Error.WriteLine($"=== Info ({pr.Info.Count}) ===");
                    foreach (var i in pr.Info) Console.Error.WriteLine($"  [INFO] {i}");
                }
                Console.Error.WriteLine($"Stats: {pr.Statistics.Paragraphs}p {pr.Statistics.Tables}t {pr.Statistics.Images}i | OOXML errors={pr.Statistics.OoxmlErrors} warnings={pr.Statistics.OoxmlWarnings}");
            }


        }, fileArg, jsonOpt);

        return cmd;
    }

    // ===== word fill (phase 6) =====

    static Command CreateFill(Option<bool> jsonOpt)
    {
        var tmplArg = new Argument<string>("template", "Path to template .docx");
        var dataArg = new Argument<string>("data", "Path to data .json");
        var outOpt = new Option<string>("-o", "Output path") { IsRequired = true };
        var cmd = new Command("fill", "Template fill from JSON data") { tmplArg, dataArg, outOpt };

        cmd.SetHandler((string template, string data, string output, bool json) =>
        {
            var err = CliHelpers.ValidateDocxFile(template) ?? CliHelpers.ValidateTextFile(data);
            if (err != null) { CliHelpers.WriteError("word fill", err, json); return; }

            try
            {
                CliHelpers.EnsureParentDir(output);
                var dataObj = JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(data));
                var elapsed = CliHelpers.Time(() =>
                    DocxCore.DocxTemplate.Fill(template, output, dataObj!));

                if (json)
                {
                    var aerr = CliHelpers.CheckArtifact(output, "DOCX");
                    if (aerr != null) { CliHelpers.WriteError("word fill", aerr, json); return; }

                    var outputJson = JsonOutput.Ok("word fill", $"Filled template: {output}");
                    outputJson.Artifacts["docx"] = Path.GetFullPath(output);
                    outputJson.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(outputJson, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"OK: {Path.GetFullPath(output)}");
                }
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("word fill",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
            }


        }, tmplArg, dataArg, outOpt, jsonOpt);

        return cmd;
    }

    // ===== word rebuild (phase 6) =====

    static Command CreateRebuild(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var outOpt = new Option<string>("-o", "Output path") { IsRequired = true };
        var cmd = new Command("rebuild", "Clean OOXML style pollution") { fileArg, outOpt };
        cmd.AddAlias("clean-styles");

        cmd.SetHandler((string file, string output, bool json) =>
        {
            var err = CliHelpers.ValidateDocxFile(file);
            if (err != null) { CliHelpers.WriteError("word rebuild", err, json); return; }
            if (string.Equals(Path.GetFullPath(file), Path.GetFullPath(output), StringComparison.OrdinalIgnoreCase))
            {
                CliHelpers.WriteError("word rebuild",
                    ErrorCodes.ValidationFailed with { Message = "Input and output paths must be different." }, json);
                return;
            }

            try
            {
                CliHelpers.EnsureParentDir(output);
                File.Copy(file, output, true);
                var elapsed = CliHelpers.Time(() =>
                {
                    using var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(output, true);
                    DocxCore.StyleRebuilder.RebuildAllParagraphs(doc);
                });

                if (json)
                {
                    var aerr = CliHelpers.CheckArtifact(output, "DOCX");
                    if (aerr != null) { CliHelpers.WriteError("word rebuild", aerr, json); return; }

                    var outputJson = JsonOutput.Ok("word rebuild", $"Rebuilt: {output}");
                    outputJson.Artifacts["docx"] = Path.GetFullPath(output);
                    outputJson.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(outputJson, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"OK: {Path.GetFullPath(output)}");
                }
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("word rebuild",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
            }


        }, fileArg, outOpt, jsonOpt);

        return cmd;
    }

    // ===== word stats =====

    static Command CreateStats(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var cmd = new Command("stats", "Document statistics") { fileArg };

        cmd.SetHandler((string file, bool json) =>
        {
            var err = CliHelpers.ValidateDocxFile(file);
            if (err != null) { CliHelpers.WriteError("word stats", err, json); return; }

            try
            {
                var (result, elapsed) = CliHelpers.Time(() => DocxAnalysis.GetStats(file));

                if (json)
                {
                    var data = new
                    {
                        paragraphs = result.Paragraphs,
                        tables = result.Tables,
                        images = result.Images,
                        footnotes = result.Footnotes,
                        endnotes = result.Endnotes,
                        characters = result.Characters,
                        wordsApprox = result.WordsApprox,
                        sections = result.Sections
                    };
                    var metrics = new Dictionary<string, object>
                    {
                        ["paragraphs"] = result.Paragraphs,
                        ["tables"] = result.Tables,
                        ["images"] = result.Images
                    };
                    var output = JsonOutput.Ok("word stats",
                        $"{result.Paragraphs} paragraphs, {result.Tables} tables, {result.Images} images", data);
                    foreach (var kv in metrics) output.Metrics[kv.Key] = kv.Value;
                    output.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"Paragraphs: {result.Paragraphs}");
                    Console.WriteLine($"Tables:     {result.Tables}");
                    Console.WriteLine($"Images:     {result.Images}");
                    Console.WriteLine($"Footnotes:  {result.Footnotes}");
                    Console.WriteLine($"Endnotes:   {result.Endnotes}");
                    Console.WriteLine($"Characters: {result.Characters}");
                    Console.WriteLine($"Words (est):{result.WordsApprox}");
                    Console.WriteLine($"Sections:   {result.Sections}");
                }
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("word stats",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
            }

        }, fileArg, jsonOpt);

        return cmd;
    }

    // ===== word fonts =====

    static Command CreateFonts(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var cmd = new Command("fonts", "List all fonts") { fileArg };

        cmd.SetHandler((string file, bool json) =>
        {
            var err = CliHelpers.ValidateDocxFile(file);
            if (err != null) { CliHelpers.WriteError("word fonts", err, json); return; }

            try
            {
                var (result, elapsed) = CliHelpers.Time(() => DocxAnalysis.GetFonts(file));

                if (json)
                {
                    var data = new
                    {
                        fonts = result.Fonts,
                        eastAsiaFonts = result.EastAsiaFonts,
                        asciiFonts = result.AsciiFonts,
                        warnings = result.Warnings
                    };
                    var output = JsonOutput.Ok("word fonts",
                        $"{result.Fonts.Count} font families found", data);
                    output.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"Fonts ({result.Fonts.Count}):");
                    foreach (var f in result.Fonts)
                        Console.WriteLine($"  {f.Name} x{f.Count} [{f.Source}]");
                    if (result.EastAsiaFonts.Count > 0)
                    {
                        Console.WriteLine($"East Asian fonts:");
                        foreach (var f in result.EastAsiaFonts)
                            Console.WriteLine($"  {f.Name} x{f.Count}");
                    }
                    if (result.Warnings.Count > 0)
                    {
                        foreach (var w in result.Warnings)
                            Console.Error.WriteLine($"[WARN] {w}");
                    }
                }
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("word fonts",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
            }

        }, fileArg, jsonOpt);

        return cmd;
    }

    // ===== word styles =====

    static Command CreateStyles(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var cmd = new Command("styles", "List all style definitions") { fileArg };

        cmd.SetHandler((string file, bool json) =>
        {
            var err = CliHelpers.ValidateDocxFile(file);
            if (err != null) { CliHelpers.WriteError("word styles", err, json); return; }

            try
            {
                var (result, elapsed) = CliHelpers.Time(() => DocxAnalysis.GetStyles(file));

                if (json)
                {
                    var data = new
                    {
                        styles = result.Styles,
                        count = result.Count
                    };
                    var output = JsonOutput.Ok("word styles",
                        $"{result.Count} styles defined", data);
                    output.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"Styles ({result.Count}):");
                    foreach (var s in result.Styles)
                    {
                        var def = s.IsDefault ? " [default]" : "";
                        Console.WriteLine($"  {s.Id} ({s.Name}) type={s.Type} basedOn={s.BasedOn ?? "N/A"}{def}");
                    }
                }
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("word styles",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
            }

        }, fileArg, jsonOpt);

        return cmd;
    }

    // ===== word validate =====

    static Command CreateValidate(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var cmd = new Command("validate", "OOXML schema validation") { fileArg };

        cmd.SetHandler((string file, bool json) =>
        {
            var err = CliHelpers.ValidateDocxFile(file);
            if (err != null) { CliHelpers.WriteError("word validate", err, json); return; }

            try
            {
                var (result, elapsed) = CliHelpers.Time(() => DocxAnalysis.Validate(file));

                if (json)
                {
                    if (result.Valid)
                    {
                        // Clean: no errors, optional warnings
                        var data = new
                        {
                            valid = true,
                            errors = result.Errors,
                            warnings = result.Warnings
                        };
                        var issues = result.Warnings.Select(w => new Issue
                        {
                            Id = "OOXML-W",
                            Severity = "warning",
                            Message = w
                        }).ToList();

                        var output = JsonOutput.Ok("word validate",
                            result.Warnings.Count > 0
                                ? $"{result.Warnings.Count} warnings (valid)"
                                : "Document is valid",
                            data);
                        output.Issues = issues;
                        output.Meta.DurationMs = elapsed;
                        Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                    }
                    else
                    {
                        // Errors found: status=error with E006
                        Environment.ExitCode = 1;
                        var data = new
                        {
                            valid = false,
                            errors = result.Errors,
                            warnings = result.Warnings
                        };
                        var issues = result.Warnings.Select(w => new Issue
                        {
                            Id = "OOXML-W",
                            Severity = "warning",
                            Message = w
                        }).ToList();

                        var output = new JsonOutput
                        {
                            Status = "error",
                            Command = "word validate",
                            Summary = $"{result.Errors.Count} validation errors, {result.Warnings.Count} warnings",
                            Data = data,
                            Issues = issues,
                            Errors = new List<ErrorEntry>
                            {
                                ErrorCodes.ValidationFailed with
                                {
                                    Message = $"{result.Errors.Count} OOXML validation errors found"
                                }
                            },
                            Meta = new MetaInfo { Version = CliVersion.Current, DurationMs = elapsed }
                        };
                        Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                    }
                }
                else
                {
                    if (result.Errors.Count > 0)
                    {
                        Console.WriteLine($"FAIL: {result.Errors.Count} validation errors, {result.Warnings.Count} warnings");
                        foreach (var e in result.Errors)
                            Console.Error.WriteLine($"  [ERR] {e}");
                        foreach (var w in result.Warnings)
                            Console.Error.WriteLine($"  [WARN] {w}");
                        Environment.ExitCode = 1;
                    }
                    else if (result.Warnings.Count > 0)
                    {
                        Console.WriteLine($"OK: {result.Warnings.Count} warnings (valid)");
                        foreach (var w in result.Warnings)
                            Console.Error.WriteLine($"  [WARN] {w}");
                    }
                    else
                    {
                        Console.WriteLine("OK: Document is valid");
                    }
                }
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("word validate",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
            }

        }, fileArg, jsonOpt);

        return cmd;
    }

    // ===== word extract =====

    static Command CreateExtract(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var outOpt = new Option<string>("-o", "Output directory for extracted images") { IsRequired = true };
        var cmd = new Command("extract", "Extract embedded images") { fileArg, outOpt };

        cmd.SetHandler((string file, string output, bool json) =>
        {
            var err = CliHelpers.ValidateDocxFile(file);
            if (err != null) { CliHelpers.WriteError("word extract", err, json); return; }

            try
            {
                CliHelpers.EnsureParentDir(Path.Combine(output, ".keep"));
                var (result, elapsed) = CliHelpers.Time(() => DocxAnalysis.ExtractImages(file, output));

                if (json)
                {
                    if (result.Images.Count == 0)
                    {
                        var outputJson = JsonOutput.Ok("word extract", "0 images", new { dir = result.Dir, images = result.Images });
                        outputJson.Meta.DurationMs = elapsed;
                        outputJson.Metrics["images"] = 0;
                        Console.WriteLine(JsonSerializer.Serialize(outputJson, CliHelpers.JsonOpts));
                    }
                    else
                    {
                        foreach (var img in result.Images)
                        {
                            var aerr = CliHelpers.CheckArtifact(img, "Image");
                            if (aerr != null) { CliHelpers.WriteError("word extract", aerr, json); return; }
                        }

                        var outputJson = JsonOutput.Ok("word extract",
                            $"Extracted {result.Images.Count} images",
                            new
                            {
                                dir = result.Dir,
                                images = result.Images.Select(Path.GetFullPath).ToList()
                            });
                        outputJson.Artifacts["dir"] = result.Dir;
                        outputJson.Metrics["images"] = result.Images.Count;
                        outputJson.Meta.DurationMs = elapsed;
                        Console.WriteLine(JsonSerializer.Serialize(outputJson, CliHelpers.JsonOpts));
                    }
                }
                else
                {
                    if (result.Images.Count == 0)
                        Console.WriteLine("0 images found in document.");
                    else
                    {
                        Console.WriteLine($"Extracted {result.Images.Count} images to {result.Dir}:");
                        foreach (var img in result.Images)
                            Console.WriteLine($"  {Path.GetFileName(img)}");
                    }
                }
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("word extract",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
            }

        }, fileArg, outOpt, jsonOpt);

        return cmd;
    }

    // ===== word dissect =====

    static Command CreateDissect(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var outOpt = new Option<string>(new[] { "-o", "--output" }, "Output directory for full one-cut three-stream slice");
        var cmd = new Command("dissect", "Format fingerprint or full nongmark slice") { fileArg, outOpt };

        cmd.SetHandler((string file, string? output, bool json) =>
        {
            var err = CliHelpers.ValidateDocxFile(file);
            if (err != null) { CliHelpers.WriteError("word dissect", err, json); return; }

            try
            {
                if (output != null)
                {
                    // Full one-cut three-stream mode
                    CliHelpers.EnsureParentDir(Path.Combine(output, ".keep"));
                    IImageAnalyzer? analyzer = null;
                    var (result, elapsed) = CliHelpers.Time(() => WordSlice.Slice(file, output, analyzer));
                    if (json)
                    {
                        var o = JsonOutput.Ok("word dissect",
                            $"Sliced: {result.BlockCount} blocks, {result.Warnings.Count} warnings",
                            new { outputDir = result.OutputDir, blockCount = result.BlockCount, warnings = result.Warnings });
                        o.Artifacts["dir"] = Path.GetFullPath(output);
                        o.Metrics["blocks"] = result.BlockCount;
                        o.Metrics["warnings"] = result.Warnings.Count;
                        o.Meta.DurationMs = elapsed;
                        Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
                    }
                    else
                    {
                        Console.WriteLine($"Sliced to {Path.GetFullPath(output)}: {result.BlockCount} blocks");
                        if (result.Warnings.Count > 0)
                            foreach (var w in result.Warnings) Console.Error.WriteLine($"[WARN] {w}");
                    }
                    return;
                }

                // Legacy format fingerprint mode (no -o)
                var (dissectResult, dissectElapsed) = CliHelpers.Time(() => DocxAnalysis.Dissect(file));

                if (json)
                {
                    var data = new
                    {
                        stats = new
                        {
                            paragraphs = dissectResult.Stats.Paragraphs,
                            tables = dissectResult.Stats.Tables,
                            images = dissectResult.Stats.Images,
                            footnotes = dissectResult.Stats.Footnotes,
                            endnotes = dissectResult.Stats.Endnotes,
                            characters = dissectResult.Stats.Characters,
                            wordsApprox = dissectResult.Stats.WordsApprox,
                            sections = dissectResult.Stats.Sections
                        },
                        fonts = new
                        {
                            fonts = dissectResult.Fonts.Fonts,
                            eastAsiaFonts = dissectResult.Fonts.EastAsiaFonts,
                            asciiFonts = dissectResult.Fonts.AsciiFonts,
                            warnings = dissectResult.Fonts.Warnings
                        },
                        styles = new
                        {
                            styles = dissectResult.Styles.Styles,
                            count = dissectResult.Styles.Count
                        },
                        tables = dissectResult.Tables,
                        numbering = new
                        {
                            abstractNums = dissectResult.Numbering.AbstractNums,
                            instances = dissectResult.Numbering.Instances
                        },
                        sections = new
                        {
                            count = dissectResult.Sections.Count,
                            pageSizes = dissectResult.Sections.PageSizes
                        },
                        warnings = dissectResult.Warnings
                    };
                    var output2 = JsonOutput.Ok("word dissect",
                        $"Format fingerprint: {dissectResult.Stats.Paragraphs}p, {dissectResult.Tables.Count}t",
                        data);
                    output2.Meta.DurationMs = dissectElapsed;
                    Console.WriteLine(JsonSerializer.Serialize(output2, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine("=== Format Fingerprint ===");
                    Console.WriteLine($"Stats:    {dissectResult.Stats.Paragraphs}p {dissectResult.Stats.Tables}t {dissectResult.Stats.Images}img");
                    Console.WriteLine($"Fonts:    {dissectResult.Fonts.Fonts.Count} families");
                    Console.WriteLine($"Styles:   {dissectResult.Styles.Count} definitions");
                    Console.WriteLine($"Tables:   {dissectResult.Tables.Count} ({string.Join(", ", dissectResult.Tables.Select(t => $"{t.RowCount}r x {t.ColCount}c"))})");
                    Console.WriteLine($"Numbering: {dissectResult.Numbering.AbstractNums} abstract, {dissectResult.Numbering.Instances} instances");
                    Console.WriteLine($"Sections: {dissectResult.Sections.Count}");
                    if (dissectResult.Warnings.Count > 0)
                        foreach (var w in dissectResult.Warnings)
                            Console.Error.WriteLine($"[WARN] {w}");
                }
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("word dissect",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
            }

        }, fileArg, outOpt, jsonOpt);

        return cmd;
    }

    // ===== word merge =====

    static Command CreateMerge(Option<bool> jsonOpt)
    {
        var filesArg = new Argument<string[]>("files", "Paths to .docx files (2+)") { Arity = ArgumentArity.OneOrMore };
        var outOpt = new Option<string>("-o", "Output .docx path") { IsRequired = true };
        var cmd = new Command("merge", "Merge two or more docx files") { filesArg, outOpt };

        cmd.SetHandler((string[] files, string output, bool json) =>
        {
            if (files.Length < 2)
            {
                CliHelpers.WriteError("word merge",
                    ErrorCodes.MissingArgument with { Message = "At least 2 input files are required." }, json);
                return;
            }

            foreach (var f in files)
            {
                var err = CliHelpers.ValidateDocxFile(f);
                if (err != null) { CliHelpers.WriteError("word merge", err, json); return; }
            }

            // Guard: input == output
            var outputFull = Path.GetFullPath(output);
            foreach (var f in files)
            {
                if (string.Equals(Path.GetFullPath(f), outputFull, StringComparison.OrdinalIgnoreCase))
                {
                    CliHelpers.WriteError("word merge",
                        ErrorCodes.ValidationFailed with { Message = "Input and output paths must be different." }, json);
                    return;
                }
            }

            try
            {
                CliHelpers.EnsureParentDir(output);
                WordEditOperations.MergeResult? mergeResult = null;
                var elapsed = CliHelpers.Time(() => { mergeResult = DocxAnalysis.MergeDocx(files, output); });

                if (json)
                {
                    var aerr = CliHelpers.CheckArtifact(output, "DOCX");
                    if (aerr != null) { CliHelpers.WriteError("word merge", aerr, json); return; }

                    var outputJson = JsonOutput.Ok("word merge", $"Merged {files.Length} files into {output}", mergeResult);
                    outputJson.Artifacts["docx"] = outputFull;
                    outputJson.Metrics["sourceFiles"] = files.Length;
                    if (mergeResult != null)
                    {
                        foreach (var warning in mergeResult.Warnings)
                        {
                            outputJson.Issues.Add(new Issue
                            {
                                Id = "merge_warning",
                                Severity = "warning",
                                Message = warning
                            });
                        }
                    }
                    outputJson.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(outputJson, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"OK: {outputFull} ({files.Length} files merged)");
                }
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("word merge",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
            }

        }, filesArg, outOpt, jsonOpt);

        return cmd;
    }

    // ===== Stage 15: word outline =====

    static Command CreateOutline(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var cmd = new Command("outline", "Extract document outline") { fileArg };
        cmd.SetHandler((string file, bool json) =>
        {
            var err = CliHelpers.ValidateDocxFile(file);
            if (err != null) { CliHelpers.WriteError("word outline", err, json); return; }
            try
            {
                var (result, elapsed) = CliHelpers.Time(() => OutlineReader.ReadOutline(file));
                if (json)
                {
                    var output = JsonOutput.Ok("word outline", $"{result.Count} headings", result);
                    output.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                }
                else
                {
                    foreach (var h in result.Items)
                        Console.WriteLine($"{new string(' ', (h.Level - 1) * 2)}{h.Level}. {h.Text} [{h.StyleId}]");
                }
            }
            catch (Exception ex) { CliHelpers.WriteError("word outline", ErrorCodes.InternalError with { Message = ex.Message }, json); }
        }, fileArg, jsonOpt);
        return cmd;
    }

    // ===== Stage 15: word images =====

    static Command CreateImages(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var outOpt = new Option<string>("-o", "Output directory for extracted images, or output DOCX path for --crop");
        var analyzeOpt = new Option<bool>("--analyze", "Analyze images for content-aware crop margins without modifying the file");
        var cropOpt = new Option<bool>("--crop", "Auto-crop blank margins from all images and write a new DOCX");
        var cmd = new Command("images", "List, extract, analyze, and auto-crop images") { fileArg, outOpt, analyzeOpt, cropOpt };
        cmd.SetHandler((string file, string? output, bool analyze, bool crop, bool json) =>
        {
            var err = CliHelpers.ValidateDocxFile(file);
            if (err != null) { CliHelpers.WriteError("word images", err, json); return; }
            try
            {
                if (analyze)
                {
                    Environment.ExitCode = RunImagingImages(file, analyze: true, crop: false, output: null, json);
                }
                else if (crop)
                {
                    Environment.ExitCode = RunImagingImages(file, analyze: false, crop: true, output, json);
                }
                else
                {
                    RunImageList(file, output, json);
                }
            }
            catch (Exception ex) { CliHelpers.WriteError("word images", ErrorCodes.InternalError with { Message = ex.Message }, json); }
        }, fileArg, outOpt, analyzeOpt, cropOpt, jsonOpt);
        return cmd;
    }

    static void RunImageList(string file, string? output, bool json)
    {
        var (result, elapsed) = CliHelpers.Time(() =>
        {
            if (output != null) { CliHelpers.EnsureParentDir(Path.Combine(output, ".keep")); }
            return ImageLister.ListImages(file, output);
        });
        if (json)
        {
            var outputJson = JsonOutput.Ok("word images", result.Summary, result);
            outputJson.Meta.DurationMs = elapsed;
            if (output != null) outputJson.Artifacts["dir"] = Path.GetFullPath(output);
            foreach (var warning in result.Warnings)
                outputJson.Issues.Add(new Issue { Id = "vml_image_reference", Severity = "warning", Message = warning });
            Console.WriteLine(JsonSerializer.Serialize(outputJson, CliHelpers.JsonOpts));
        }
        else
        {
            Console.WriteLine(result.Summary);
            foreach (var img in result.Images)
                Console.WriteLine($"  {img.Id} {img.ContentType} {img.Width}x{img.Height} usedBy={string.Join(",", img.UsedBy ?? new())}");
            foreach (var warning in result.Warnings)
                Console.Error.WriteLine($"[WARN] {warning}");
        }
    }

    static int RunImagingImages(string file, bool analyze, bool crop, string? output, bool json)
    {
        var args = new List<string> { "images", file };
        if (analyze) args.Add("--analyze");
        if (crop) args.Add("--crop");
        if (crop && output != null)
        {
            args.Add("-o");
            args.Add(output);
        }
        if (json) args.Add("--json");
        return CliHelpers.RunTool("nong-imaging", ToolPackages.Imaging, args.ToArray());
    }

    // ===== word crop (external imaging tool) =====

    static Command CreateCrop(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var outOpt = new Option<string>("-o", "Output DOCX path (default: <input>.cropped.docx)");
        var cmd = new Command("crop", "Auto-crop blank margins from all images using content-aware border detection") { fileArg, outOpt };
        cmd.AddAlias("images-crop");
        cmd.SetHandler((string file, string? output, bool json) =>
        {
            var err = CliHelpers.ValidateDocxFile(file);
            if (err != null) { CliHelpers.WriteError("word crop", err, json); return; }
            Environment.ExitCode = RunImagingImages(file, analyze: false, crop: true, output, json);
        }, fileArg, outOpt, jsonOpt);
        return cmd;
    }

    // ===== word fit-images (scale inline multi-image paragraphs to side-by-side) =====

    static Command CreateFitImages(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var outOpt = new Option<string>("-o", "Output DOCX path (default: <input>.fit.docx)");
        var gapOpt = new Option<double>("--gap", () => 2.0, "Gap between images in mm (default: 2)");
        var cmd = new Command("fit-images", "Scale multi-image paragraphs so inline images fit side-by-side within page width") { fileArg, outOpt, gapOpt };
        cmd.AddAlias("compact-images");
        cmd.SetHandler((string file, string? output, double gap, bool json) =>
        {
            var err = CliHelpers.ValidateDocxFile(file);
            if (err != null) { CliHelpers.WriteError("word fit-images", err, json); return; }
            try
            {
                string outPath = output ?? Path.Combine(
                    Path.GetDirectoryName(Path.GetFullPath(file)) ?? ".",
                    Path.GetFileNameWithoutExtension(file) + ".fit.docx");

                var result = DocxImageFitter.FitImages(file, outPath, gap);

                string summary = (result.ParagraphsModified, result.ImagesScaled) switch
                {
                    (0, _) => "No multi-image paragraphs found that need scaling",
                    var (p, i) => $"Scaled {i} images across {p} paragraphs → {outPath}"
                };

                if (json)
                {
                    var outputJson = JsonOutput.Ok("word fit-images", summary, new
                    {
                        output = Path.GetFullPath(outPath),
                        paragraphsModified = result.ParagraphsModified,
                        imagesScaled = result.ImagesScaled,
                        details = result.Modified.Select(m => new
                        {
                            paragraphIndex = m.ParagraphIndex,
                            imageCount = m.ImageCount,
                            originalTotalEmu = m.OriginalTotalEmu,
                            pageTextEmu = m.PageTextEmu,
                            scaleFactor = m.ScaleFactor,
                            dimensions = m.Dimensions.Select(d => new
                            {
                                oldWidth = d.OldWidth, oldHeight = d.OldHeight,
                                newWidth = d.NewWidth, newHeight = d.NewHeight
                            })
                        })
                    });
                    outputJson.Artifacts["docx"] = Path.GetFullPath(outPath);
                    Console.WriteLine(JsonSerializer.Serialize(outputJson, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine(summary);
                    foreach (var m in result.Modified)
                    {
                        Console.WriteLine($"  para[{m.ParagraphIndex}]: {m.ImageCount} images, scale={m.ScaleFactor:P1}, {m.OriginalTotalEmu}→{m.PageTextEmu} EMU");
                    }
                }
            }
            catch (Exception ex) { CliHelpers.WriteError("word fit-images", ErrorCodes.InternalError with { Message = ex.Message }, json); }
        }, fileArg, outOpt, gapOpt, jsonOpt);
        return cmd;
    }

    // ===== word compact-tables (remove fixed row heights, equalize columns, center) =====

    static Command CreateCompactTables(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var outOpt = new Option<string>("-o", "Output DOCX path (default: <input>.compact.docx)");
        var autoHOpt = new Option<bool>("--auto-height", "Remove all row height constraints (auto: let content dictate)");
        var cmd = new Command("compact-tables", "Compact tables: remove fixed row heights, equalize column widths, center on page") { fileArg, outOpt, autoHOpt };
        cmd.AddAlias("tables");
        cmd.SetHandler((string file, string? output, bool autoHeight, bool json) =>
        {
            var err = CliHelpers.ValidateDocxFile(file);
            if (err != null) { CliHelpers.WriteError("word compact-tables", err, json); return; }
            try
            {
                string outPath = output ?? Path.Combine(
                    Path.GetDirectoryName(Path.GetFullPath(file)) ?? ".",
                    Path.GetFileNameWithoutExtension(file) + ".compact.docx");

                var result = DocxTableCompactor.Compact(file, outPath, autoHeight);
                string summary = $"{result.TablesModified} tables compacted (fixed rows freed: {result.FixedRowsTotal}){(autoHeight ? " → auto-height" : "")}";

                if (json)
                {
                    var outputJson = JsonOutput.Ok("word compact-tables", summary, new
                    {
                        output = Path.GetFullPath(outPath),
                        tablesModified = result.TablesModified,
                        fixedRowsTotal = result.FixedRowsTotal,
                        tables = result.Tables.Select(t => new
                        {
                            index = t.TableIndex,
                            rows = t.RowsBefore,
                            changes = t.Changes
                        })
                    });
                    outputJson.Artifacts["docx"] = Path.GetFullPath(outPath);
                    Console.WriteLine(JsonSerializer.Serialize(outputJson, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"{summary} → {outPath}");
                    foreach (var t in result.Tables.Where(t => t.Changes.Count > 0))
                        Console.WriteLine($"  table[{t.TableIndex}]: {string.Join(", ", t.Changes)}");
                }
            }
            catch (Exception ex) { CliHelpers.WriteError("word compact-tables", ErrorCodes.InternalError with { Message = ex.Message }, json); }
        }, fileArg, outOpt, autoHOpt, jsonOpt);
        return cmd;
    }

    // ===== word regroup-images (cross-paragraph image pairing) =====

    static Command CreateRegroupImages(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var outOpt = new Option<string>("-o", "Output DOCX path (default: <input>.regroup.docx)");
        var cmd = new Command("regroup-images", "Merge orphan images across paragraphs for side-by-side layout, then scale to fit") { fileArg, outOpt };
        cmd.AddAlias("pair-images");
        cmd.SetHandler((string file, string? output, bool json) =>
        {
            var err = CliHelpers.ValidateDocxFile(file);
            if (err != null) { CliHelpers.WriteError("word regroup-images", err, json); return; }
            try
            {
                string outPath = output ?? Path.Combine(
                    Path.GetDirectoryName(Path.GetFullPath(file)) ?? ".",
                    Path.GetFileNameWithoutExtension(file) + ".regroup.docx");

                // Use FitImages with a wider scan window (10 paragraphs instead of 3)
                var result = DocxImageFitter.FitImagesWide(file, outPath, 2.0, maxGap: 10);

                string summary = result.ParagraphsModified switch
                {
                    0 => "No orphan images found for regrouping",
                    var p => $"Regrouped {result.ImagesScaled} images across {p} paragraphs → {outPath}"
                };

                if (json)
                {
                    var outputJson = JsonOutput.Ok("word regroup-images", summary, new
                    {
                        output = Path.GetFullPath(outPath),
                        paragraphsModified = result.ParagraphsModified,
                        imagesScaled = result.ImagesScaled
                    });
                    outputJson.Artifacts["docx"] = Path.GetFullPath(outPath);
                    Console.WriteLine(JsonSerializer.Serialize(outputJson, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine(summary);
                }
            }
            catch (Exception ex) { CliHelpers.WriteError("word regroup-images", ErrorCodes.InternalError with { Message = ex.Message }, json); }
        }, fileArg, outOpt, jsonOpt);
        return cmd;
    }

    // ===== word estimate (page-break estimation + blank-space detection) =====

    static Command CreateEstimate(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var cmd = new Command("estimate", "Estimate page breaks and measure blank space on each page") { fileArg };
        cmd.AddAlias("pages");
        cmd.SetHandler((string file, bool json) =>
        {
            var err = CliHelpers.ValidateDocxFile(file);
            if (err != null) { CliHelpers.WriteError("word estimate", err, json); return; }
            try
            {
                var result = DocxPageEstimator.Estimate(file);
                string summary = $"{result.PageCount} pages, {result.ProblemPages} with >30% blank (waste total {result.WasteTotalMm}mm)";

                if (json)
                {
                    var outputJson = JsonOutput.Ok("word estimate", summary, new
                    {
                        pageCount = result.PageCount,
                        textAreaMm = result.TextAreaMm,
                        problemPages = result.ProblemPages,
                        wasteTotalMm = result.WasteTotalMm,
                        pages = result.Pages.Select(p => new
                        {
                            page = p.PageNumber,
                            items = p.ItemCount,
                            contentMm = p.ContentMm,
                            wasteMm = p.WasteMm,
                            wastePct = p.WastePercent,
                            hasImage = p.HasImage,
                            hasTable = p.HasTable,
                            isProblem = p.IsProblem
                        })
                    });
                    Console.WriteLine(JsonSerializer.Serialize(outputJson, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine(summary);
                    Console.WriteLine($"  Text area: {result.TextAreaMm}mm, estimated {result.PageCount} pages");
                    Console.WriteLine();
                    foreach (var p in result.Pages)
                    {
                        var flag = p.IsProblem ? " ** WASTE **" : "";
                        var kind = (p.HasImage, p.HasTable) switch
                        {
                            (true, true) => "[img+tbl]",
                            (true, false) => "[img]",
                            (false, true) => "[tbl]",
                            _ => ""
                        };
                        Console.WriteLine($"  p{p.PageNumber:D2} {kind}: content={p.ContentMm}mm, waste={p.WasteMm}mm ({p.WastePercent}%){flag}");
                    }
                }
            }
            catch (Exception ex) { CliHelpers.WriteError("word estimate", ErrorCodes.InternalError with { Message = ex.Message }, json); }
        }, fileArg, jsonOpt);
        return cmd;
    }

    // ===== Stage 15: word comments =====

    // ===== word page-setup =====

    static Command CreatePageSetup(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var sizeOpt = new Option<string>("--size", "Page size: A4, A3, B5, Letter, or WxH mm");
        var orientOpt = new Option<string>("--orient", "Orientation: portrait or landscape");
        var marginTopOpt = new Option<double?>("--margin-top", "Top margin in mm");
        var marginBottomOpt = new Option<double?>("--margin-bottom", "Bottom margin in mm");
        var marginLeftOpt = new Option<double?>("--margin-left", "Left margin in mm");
        var marginRightOpt = new Option<double?>("--margin-right", "Right margin in mm");
        var columnsOpt = new Option<int?>("--columns", "Number of columns (>1 for multi-column)");
        var columnGapOpt = new Option<double?>("--column-gap", "Gap between columns in mm");
        var firstPageOpt = new Option<bool?>("--first-page-different", "Different first page header/footer");
        var pageNumOpt = new Option<string>("--page-number", "Page number format: decimal, roman, romanUpper");
        var sectionOpt = new Option<int?>("--section", "Target section index (default: all)");
        var outOpt = new Option<string>("-o", "Output DOCX path");
        var cmd = new Command("page-setup", "Set page size, orientation, margins, columns, different first page") {
            fileArg, sizeOpt, orientOpt, marginTopOpt, marginBottomOpt, marginLeftOpt, marginRightOpt,
            columnsOpt, columnGapOpt, firstPageOpt, pageNumOpt, sectionOpt, outOpt };
        cmd.AddAlias("layout");
        cmd.SetHandler((InvocationContext ctx) =>
        {
            var parseResult = ctx.ParseResult;
            var file = parseResult.GetValueForArgument(fileArg);
            var size = parseResult.GetValueForOption(sizeOpt);
            var orient = parseResult.GetValueForOption(orientOpt);
            var marginTop = parseResult.GetValueForOption(marginTopOpt);
            var marginBottom = parseResult.GetValueForOption(marginBottomOpt);
            var marginLeft = parseResult.GetValueForOption(marginLeftOpt);
            var marginRight = parseResult.GetValueForOption(marginRightOpt);
            var columns = parseResult.GetValueForOption(columnsOpt);
            var columnGap = parseResult.GetValueForOption(columnGapOpt);
            var firstPageDiff = parseResult.GetValueForOption(firstPageOpt);
            var pageNum = parseResult.GetValueForOption(pageNumOpt);
            var section = parseResult.GetValueForOption(sectionOpt);
            var output = parseResult.GetValueForOption(outOpt);
            var json = parseResult.GetValueForOption(jsonOpt);
            var err = CliHelpers.ValidateDocxFile(file);
            if (err != null) { CliHelpers.WriteError("word page-setup", err, json); return; }
            try
            {
                string outPath = output ?? Path.Combine(
                    Path.GetDirectoryName(Path.GetFullPath(file)) ?? ".",
                    Path.GetFileNameWithoutExtension(file) + ".layout.docx");
                var opts = new DocxPageSetup.PageSetupOptions { PageSize = size, Orient = orient, MarginTopMm = marginTop, MarginBottomMm = marginBottom, MarginLeftMm = marginLeft, MarginRightMm = marginRight, Columns = columns, ColumnGapMm = columnGap, DifferentFirstPage = firstPageDiff, PageNumberFormat = pageNum, SectionIndex = section };
                var result = DocxPageSetup.Apply(file, outPath, opts);
                var summary = $"{result.SectionsApplied} section(s) updated: {string.Join("; ", result.Changes)}";
                if (json) { var o = JsonOutput.Ok("word page-setup", summary, new { output = Path.GetFullPath(outPath), sections = result.SectionsApplied, changes = result.Changes }); o.Artifacts["docx"] = Path.GetFullPath(outPath); Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts)); }
                else Console.WriteLine($"{summary} → {outPath}");
            }
            catch (Exception ex) { CliHelpers.WriteError("word page-setup", ErrorCodes.InternalError with { Message = ex.Message }, json); }
        });
        return cmd;
    }

    // ===== word indent =====

    static Command CreateIndent(Option<bool> jsonOpt)
    {
        var fa = new Argument<string>("file", "Path to .docx file");
        var fl = new Option<double?>("--first-line", "First-line indent in mm");
        var hg = new Option<double?>("--hanging", "Hanging indent in mm");
        var lf = new Option<double?>("--left", "Left indent in mm");
        var rt = new Option<double?>("--right", "Right indent in mm");
        var ol = new Option<int?>("--outline-level", "Outline level (0-9)");
        var rl = new Option<string>("--role", "Target role: heading, body, or all (default)");
        var ot = new Option<string>("-o", "Output DOCX path");
        var cmd = new Command("indent", "Set paragraph indentation: first-line, hanging, left, right, outline level") {
            fa, fl, hg, lf, rt, ol, rl, ot };
        cmd.SetHandler((InvocationContext ctx) =>
        {
            var r = ctx.ParseResult; var f = r.GetValueForArgument(fa); var j = r.GetValueForOption(jsonOpt);
            var er = CliHelpers.ValidateDocxFile(f);
            if (er != null) { CliHelpers.WriteError("word indent", er, j); return; }
            try {
                var o = r.GetValueForOption(ot) ?? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(f))??".",Path.GetFileNameWithoutExtension(f)+".indent.docx");
                var opt = new DocxIndenter.IndentOptions{FirstLineMm=r.GetValueForOption(fl),HangingMm=r.GetValueForOption(hg),LeftMm=r.GetValueForOption(lf),RightMm=r.GetValueForOption(rt),OutlineLevel=r.GetValueForOption(ol),Role=r.GetValueForOption(rl)??"all"};
                var res = DocxIndenter.Apply(f,o,opt);
                var s = $"{res.ParagraphsChanged} paragraphs: {string.Join("; ",res.Changes)}";
                if (j) { var x=JsonOutput.Ok("word indent",s,new{output=Path.GetFullPath(o),pc=res.ParagraphsChanged,changes=res.Changes}); x.Artifacts["docx"]=Path.GetFullPath(o); Console.WriteLine(JsonSerializer.Serialize(x,CliHelpers.JsonOpts)); }
                else Console.WriteLine($"{s} → {o}");
            } catch (Exception ex) { CliHelpers.WriteError("word indent", ErrorCodes.InternalError with{Message=ex.Message}, j); }
        });
        return cmd;
    }

    // ===== word paragraph-control =====

    static Command CreateParagraphControl(Option<bool> jsonOpt)
    {
        var fa = new Argument<string>("file", "Path to .docx file");
        var kn = new Option<bool?>("--keep-next", "Keep with next paragraph");
        var kl = new Option<bool?>("--keeplines", "Keep lines together");
        var pb = new Option<bool?>("--page-break-before", "Force page break before");
        var wc = new Option<bool?>("--widow-control", "Widow/orphan control");
        var rl = new Option<string>("--role", "Target role: heading, body, or all");
        var ot = new Option<string>("-o", "Output DOCX path");
        var cmd = new Command("paragraph-control", "Set pagination controls: keepNext, keepLines, pageBreakBefore, widowControl") {
            fa, kn, kl, pb, wc, rl, ot };
        cmd.SetHandler((InvocationContext ctx) =>
        {
            var r = ctx.ParseResult; var f = r.GetValueForArgument(fa); var j = r.GetValueForOption(jsonOpt);
            var er = CliHelpers.ValidateDocxFile(f);
            if (er != null) { CliHelpers.WriteError("word paragraph-control", er, j); return; }
            try {
                var o = r.GetValueForOption(ot) ?? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(f))??".",Path.GetFileNameWithoutExtension(f)+".paginate.docx");
                var opt = new DocxParagraphControl.PaginationOptions{KeepNext=r.GetValueForOption(kn),KeepLines=r.GetValueForOption(kl),PageBreakBefore=r.GetValueForOption(pb),WidowControl=r.GetValueForOption(wc),Role=r.GetValueForOption(rl)??"all"};
                var res = DocxParagraphControl.Apply(f,o,opt);
                var s = $"{res.ParagraphsChanged} paragraphs: {string.Join("; ",res.Changes)}";
                if (j) { var x=JsonOutput.Ok("word paragraph-control",s,new{output=Path.GetFullPath(o),pc=res.ParagraphsChanged,changes=res.Changes}); x.Artifacts["docx"]=Path.GetFullPath(o); Console.WriteLine(JsonSerializer.Serialize(x,CliHelpers.JsonOpts)); }
                else Console.WriteLine($"{s} → {o}");
            } catch (Exception ex) { CliHelpers.WriteError("word paragraph-control", ErrorCodes.InternalError with{Message=ex.Message}, j); }
        });
        return cmd;
    }

    // ===== word image-wrap (inline→floating anchor + wrap modes) =====

    static Command CreateImageWrap(Option<bool> jsonOpt)
    {
        var fa = new Argument<string>("file", "Path to .docx file");
        var md = new Option<string>("--mode", "Wrap mode: square, topAndBottom, tight, through, behind, inFront, inline");
        var off = new Option<double?>("--offset", "Distance from text in mm (default 3)");
        var ah = new Option<string>("--align-h", "Horizontal alignment: left, center, right");
        var av = new Option<string>("--align-v", "Vertical alignment: top, center, bottom");
        var ot = new Option<string>("-o", "Output DOCX path");
        var cmd = new Command("image-wrap", "Convert inline images to floating with configurable text wrap modes") { fa, md, off, ah, av, ot };
        cmd.AddAlias("wrap");
        cmd.SetHandler((InvocationContext ctx) =>
        {
            var r = ctx.ParseResult; var f = r.GetValueForArgument(fa); var j = r.GetValueForOption(jsonOpt);
            var er = CliHelpers.ValidateDocxFile(f);
            if (er != null) { CliHelpers.WriteError("word image-wrap", er, j); return; }
            try {
                var o = r.GetValueForOption(ot) ?? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(f))??".",Path.GetFileNameWithoutExtension(f)+".wrap.docx");
                var opt = new DocxImageWrap.WrapOptions{Mode=r.GetValueForOption(md),OffsetMm=r.GetValueForOption(off),AlignH=r.GetValueForOption(ah),AlignV=r.GetValueForOption(av)};
                var res = DocxImageWrap.Apply(f,o,opt);
                var s = string.Join("; ",res.Changes);
                if (j) { var x=JsonOutput.Ok("word image-wrap",s,new{output=Path.GetFullPath(o),converted=res.ImagesConverted,total=res.ImagesTotal,changes=res.Changes}); x.Artifacts["docx"]=Path.GetFullPath(o); Console.WriteLine(JsonSerializer.Serialize(x,CliHelpers.JsonOpts)); }
                else Console.WriteLine($"{s} → {o}");
            } catch (Exception ex) { CliHelpers.WriteError("word image-wrap", ErrorCodes.InternalError with{Message=ex.Message}, j); }
        });
        return cmd;
    }

    // ===== word cell-format (table cell borders/shading/alignment) =====

    static Command CreateCellFormat(Option<bool> jsonOpt)
    {
        var fa = new Argument<string>("file", "Path to .docx file");
        var ti = new Option<int?>("--table", "Table index (0-based, default all tables)");
        var ri = new Option<int?>("--row", "Row index (0-based)");
        var ci = new Option<int?>("--col", "Column index (0-based)");
        var sh = new Option<string>("--shading", "Cell background color hex or 'none' to remove");
        var bt = new Option<double?>("--border-top", "Top border width in mm");
        var bb = new Option<double?>("--border-bottom", "Bottom border width in mm");
        var bl = new Option<double?>("--border-left", "Left border width in mm");
        var br = new Option<double?>("--border-right", "Right border width in mm");
        var bc = new Option<string>("--border-color", "Border color hex (default 2A7A65)");
        var va = new Option<string>("--valign", "Vertical alignment: top, center, bottom");
        var pt = new Option<double?>("--pad-top", "Top padding in mm");
        var pl = new Option<double?>("--pad-left", "Left padding in mm");
        var pb = new Option<double?>("--pad-bottom", "Bottom padding in mm");
        var pr = new Option<double?>("--pad-right", "Right padding in mm");
        var ot = new Option<string>("-o", "Output DOCX path");
        var cmd = new Command("cell-format", "Format table cells: borders, shading, alignment, padding") { fa, ti, ri, ci, sh, bt, bb, bl, br, bc, va, pt, pl, pb, pr, ot };
        cmd.SetHandler((InvocationContext ctx) =>
        {
            var r = ctx.ParseResult; var f = r.GetValueForArgument(fa); var j = r.GetValueForOption(jsonOpt);
            var er = CliHelpers.ValidateDocxFile(f);
            if (er != null) { CliHelpers.WriteError("word cell-format", er, j); return; }
            try {
                var o = r.GetValueForOption(ot) ?? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(f))??".",Path.GetFileNameWithoutExtension(f)+".cells.docx");
                var opt = new DocxCellFormatter.CellFormatOptions{TableIndex=r.GetValueForOption(ti),RowIndex=r.GetValueForOption(ri),ColIndex=r.GetValueForOption(ci),Shading=r.GetValueForOption(sh),BorderTopMm=r.GetValueForOption(bt),BorderBottomMm=r.GetValueForOption(bb),BorderLeftMm=r.GetValueForOption(bl),BorderRightMm=r.GetValueForOption(br),BorderColor=r.GetValueForOption(bc),VAlign=r.GetValueForOption(va),PaddingTopMm=r.GetValueForOption(pt),PaddingLeftMm=r.GetValueForOption(pl),PaddingBottomMm=r.GetValueForOption(pb),PaddingRightMm=r.GetValueForOption(pr)};
                var res = DocxCellFormatter.Apply(f,o,opt);
                var s = $"{res.CellsChanged} cells: {string.Join("; ",res.Changes)}";
                if (j) { var x=JsonOutput.Ok("word cell-format",s,new{output=Path.GetFullPath(o),cellsChanged=res.CellsChanged,changes=res.Changes}); x.Artifacts["docx"]=Path.GetFullPath(o); Console.WriteLine(JsonSerializer.Serialize(x,CliHelpers.JsonOpts)); }
                else Console.WriteLine($"{s} → {o}");
            } catch (Exception ex) { CliHelpers.WriteError("word cell-format", ErrorCodes.InternalError with{Message=ex.Message}, j); }
        });
        return cmd;
    }

    // ===== word run-format (character-level formatting) =====

    static Command CreateRunFormat(Option<bool> jsonOpt)
    {
        var fa = new Argument<string>("file", "Path to .docx file");
        var ul = new Option<string>("--underline", "Underline: single, double, or none");
        var uc = new Option<string>("--underline-color", "Underline color hex");
        var sk = new Option<bool?>("--strikethrough", "Strikethrough text");
        var cl = new Option<string>("--color", "Font color hex or 'none'");
        var hl = new Option<string>("--highlight", "Highlight color: yellow, cyan, none, etc.");
        var sp = new Option<double?>("--spacing", "Character spacing in mm");
        var su = new Option<bool?>("--superscript", "Superscript");
        var sb = new Option<bool?>("--subscript", "Subscript");
        var pt = new Option<string>("--pattern", "Regex pattern to match text content");
        var rl = new Option<string>("--role", "Target role: heading, body, or all");
        var ot = new Option<string>("-o", "Output DOCX path");
        var cmd = new Command("run-format", "Character-level formatting: underline, strikethrough, color, highlight, spacing, superscript") { fa, ul, uc, sk, cl, hl, sp, su, sb, pt, rl, ot };
        cmd.AddAlias("char-format");
        cmd.SetHandler((InvocationContext ctx) =>
        {
            var r = ctx.ParseResult; var f = r.GetValueForArgument(fa); var j = r.GetValueForOption(jsonOpt);
            var er = CliHelpers.ValidateDocxFile(f);
            if (er != null) { CliHelpers.WriteError("word run-format", er, j); return; }
            try {
                var o = r.GetValueForOption(ot) ?? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(f))??".",Path.GetFileNameWithoutExtension(f)+".runs.docx");
                var opt = new DocxRunFormatter.RunFormatOptions{Underline=r.GetValueForOption(ul),UnderlineColor=r.GetValueForOption(uc),Strikethrough=r.GetValueForOption(sk),Color=r.GetValueForOption(cl),Highlight=r.GetValueForOption(hl),SpacingMm=r.GetValueForOption(sp),Superscript=r.GetValueForOption(su),Subscript=r.GetValueForOption(sb),Pattern=r.GetValueForOption(pt),Role=r.GetValueForOption(rl)??"all"};
                var res = DocxRunFormatter.Apply(f,o,opt);
                var s = $"{res.RunsChanged} runs: {string.Join("; ",res.Changes)}";
                if (j) { var x=JsonOutput.Ok("word run-format",s,new{output=Path.GetFullPath(o),runsChanged=res.RunsChanged,changes=res.Changes}); x.Artifacts["docx"]=Path.GetFullPath(o); Console.WriteLine(JsonSerializer.Serialize(x,CliHelpers.JsonOpts)); }
                else Console.WriteLine($"{s} → {o}");
            } catch (Exception ex) { CliHelpers.WriteError("word run-format", ErrorCodes.InternalError with{Message=ex.Message}, j); }
        });
        return cmd;
    }

    // ===== Stage 15: word comments =====

    static Command CreateComments(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var cmd = new Command("comments", "Read document comments") { fileArg };
        cmd.SetHandler((string file, bool json) =>
        {
            var err = CliHelpers.ValidateDocxFile(file);
            if (err != null) { CliHelpers.WriteError("word comments", err, json); return; }
            try
            {
                var (result, elapsed) = CliHelpers.Time(() => CommentReader.ReadComments(file));
                if (json)
                {
                    var output = JsonOutput.Ok("word comments", $"{result.Comments.Count} comments", result);
                    output.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                }
                else
                {
                    foreach (var c in result.Comments)
                        Console.WriteLine($"[{c.Author}] {c.Text}");
                }
            }
            catch (Exception ex) { CliHelpers.WriteError("word comments", ErrorCodes.InternalError with { Message = ex.Message }, json); }
        }, fileArg, jsonOpt);
        return cmd;
    }

    // ===== Stage 15: word revisions =====

    static Command CreateRevisions(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var cmd = new Command("revisions", "List tracked changes") { fileArg };
        cmd.SetHandler((string file, bool json) =>
        {
            var err = CliHelpers.ValidateDocxFile(file);
            if (err != null) { CliHelpers.WriteError("word revisions", err, json); return; }
            try
            {
                var (result, elapsed) = CliHelpers.Time(() => RevisionReader.ReadRevisions(file));
                if (json)
                {
                    var output = JsonOutput.Ok("word revisions", $"{result.TotalRevisions} revisions", result);
                    output.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"Insertions: {result.Insertions}, Deletions: {result.Deletions}, Moves: {result.Moves}");
                    foreach (var s in result.Snippets)
                        Console.WriteLine($"  [{s.Type}] {s.Snippet}");
                }
            }
            catch (Exception ex) { CliHelpers.WriteError("word revisions", ErrorCodes.InternalError with { Message = ex.Message }, json); }
        }, fileArg, jsonOpt);
        return cmd;
    }

    // ===== Stage 15: word infer-format =====

    static Command CreateInferFormat(Option<bool> jsonOpt)
    {
        var textArg = new Argument<string>("text", "Chinese format description, e.g. '黑体 四号 居中'");
        var cmd = new Command("infer-format", "Infer OpenXML format from Chinese description") { textArg };
        cmd.SetHandler((string text, bool json) =>
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                CliHelpers.WriteError("word infer-format", ErrorCodes.ValidationFailed with { Message = "Format description is empty." }, json);
                return;
            }
            try
            {
                var (result, elapsed) = CliHelpers.Time(() => FormatInferrer.Infer(text));
                if (json)
                {
                    if (string.IsNullOrEmpty(result.FontFamily) && string.IsNullOrEmpty(result.FontSize) && result.Warnings.Count > 0)
                    {
                        var errOut = new JsonOutput { Status = "error", Command = "word infer-format", Summary = "Could not parse format", Meta = new MetaInfo { Version = CliVersion.Current } };
                        errOut.Errors.Add(ErrorCodes.ValidationFailed with { Message = "No known format patterns detected." });
                        foreach (var w in result.Warnings) errOut.Issues.Add(new Issue { Id = "parse_warning", Severity = "Warning", Message = w });
                        Console.WriteLine(JsonSerializer.Serialize(errOut, CliHelpers.JsonOpts));
                        Environment.ExitCode = 1;
                        return;
                    }
                    var output = JsonOutput.Ok("word infer-format", $"Parsed: {result.FontFamily ?? "?"} {result.FontSize ?? "?"}", result);
                    foreach (var w in result.Warnings) output.Issues.Add(new Issue { Id = "parse_warning", Severity = "Warning", Message = w });
                    output.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"Font: {result.FontFamily ?? "N/A"} {result.FontSize ?? "N/A"}");
                    Console.WriteLine($"Alignment: {result.Alignment ?? "N/A"}");
                    Console.WriteLine($"Line spacing: {result.LineSpacing ?? "N/A"} ({result.LineRule ?? "N/A"})");
                    if (result.FirstLineIndent != null) Console.WriteLine($"First-line indent: {result.FirstLineIndent}");
                }
            }
            catch (Exception ex) { CliHelpers.WriteError("word infer-format", ErrorCodes.InternalError with { Message = ex.Message }, json); }
        }, textArg, jsonOpt);
        return cmd;
    }

    // ===== Stage 15: modify commands =====

    static Command CreateFixOrder(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var outOpt = new Option<string>("-o", "Output .docx path") { IsRequired = true };
        var cmd = new Command("fix-order", "Internal OOXML/structure repair only; does not promise visible Word formatting improvements") { fileArg, outOpt };
        cmd.SetHandler((string file, string output, bool json) =>
        {
            var err = CliHelpers.ValidateDocxFile(file);
            if (err != null) { CliHelpers.WriteError("word fix-order", err, json); return; }
            if (string.Equals(Path.GetFullPath(file), Path.GetFullPath(output), StringComparison.OrdinalIgnoreCase))
            { CliHelpers.WriteError("word fix-order", ErrorCodes.ValidationFailed with { Message = "Input and output paths must be different." }, json); return; }
            try { CliHelpers.EnsureParentDir(output); var (r, e) = CliHelpers.Time(() => WordEditOperations.FixOrder(file, output));
                var a = CliHelpers.CheckArtifact(output, "DOCX"); if (a != null) { CliHelpers.WriteError("word fix-order", a, json); return; }
                var data = new
                {
                    Input = Path.GetFullPath(file),
                    Output = Path.GetFullPath(output),
                    r.FixedElements,
                    repairKind = "internal-ooxml-structure",
                    visibleFormattingChanged = false,
                    nextForVisibleFormatting = "word academic-format",
                };
                var o = JsonOutput.Ok("word fix-order", $"Fixed {r.FixedElements} internal OOXML element(s); visible formatting was not the goal", data); o.Artifacts["docx"] = Path.GetFullPath(output); o.Meta.DurationMs = e;
                Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts)); }
            catch (Exception ex) { CliHelpers.WriteError("word fix-order", ErrorCodes.InternalError with { Message = ex.Message }, json); }
        }, fileArg, outOpt, jsonOpt);
        return cmd;
    }

    static Command CreateAcademicFormat(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var outOpt = new Option<string>("-o", "Output .docx path") { IsRequired = true };
        var cmd = new Command("academic-format", "Visible academic Word formatting repair for headings, body text, tables, fonts, and spacing") { fileArg, outOpt };
        cmd.SetHandler((string file, string output, bool json) =>
        {
            const string command = "word academic-format";
            var err = CliHelpers.ValidateDocxFile(file);
            if (err != null) { CliHelpers.WriteError(command, err, json); return; }
            if (!string.Equals(Path.GetExtension(output), ".docx", StringComparison.OrdinalIgnoreCase))
            {
                CliHelpers.WriteError(command,
                    ErrorCodes.ValidationFailed with { Message = "Output path must end with .docx." }, json);
                return;
            }
            if (string.Equals(Path.GetFullPath(file), Path.GetFullPath(output), StringComparison.OrdinalIgnoreCase))
            {
                CliHelpers.WriteError(command,
                    ErrorCodes.ValidationFailed with { Message = "Input and output paths must be different." }, json);
                return;
            }

            try
            {
                CliHelpers.EnsureParentDir(output);
                var (r, e) = CliHelpers.Time(() =>
                {
                    var formatted = WordAcademicFormatter.Apply(file, output);
                    FixOrderInPlace(output);
                    return formatted;
                });
                var a = CliHelpers.CheckArtifact(output, "DOCX");
                if (a != null) { CliHelpers.WriteError(command, a, json); return; }
                var o = JsonOutput.Ok(command,
                    $"Applied academic formatting: {r.ParagraphsFormatted} paragraphs, {r.TablesFormatted} tables",
                    r);
                o.Artifacts["docx"] = Path.GetFullPath(output);
                o.Metrics["paragraphs"] = r.ParagraphsFormatted;
                o.Metrics["runs"] = r.RunsFormatted;
                o.Metrics["tables"] = r.TablesFormatted;
                o.Metrics["latinParenthesesItalicized"] = r.LatinParentheticalRunsItalicized;
                o.Meta.DurationMs = e;
                Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
            }
            catch (ArgumentException ex)
            {
                CliHelpers.WriteError(command, ErrorCodes.ValidationFailed with { Message = ex.Message }, json);
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError(command, ErrorCodes.InternalError with { Message = ex.Message }, json);
            }
        }, fileArg, outOpt, jsonOpt);
        return cmd;
    }

    static Command CreateFormatAudit(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var profileOpt = new Option<string>("--profile", () => "academic", "Audit profile: academic");
        var failOnWarningOpt = new Option<bool>("--fail-on-warning", () => false, "Return E006 when the audit reports warning or fail status.");
        var minScoreOpt = new Option<int?>("--min-score", "Return E006 when the audit score is lower than this threshold.");
        var cmd = new Command("format-audit", "Audit visible Word formatting evidence for headings, body text, tables, fonts, and spacing")
        {
            fileArg,
            profileOpt,
            failOnWarningOpt,
            minScoreOpt,
        };
        cmd.SetHandler((string file, string profile, bool failOnWarning, int? minScore, bool json) =>
        {
            const string command = "word format-audit";
            var err = CliHelpers.ValidateDocxFile(file);
            if (err != null) { CliHelpers.WriteError(command, err, json); return; }
            if (!string.Equals(profile, "academic", StringComparison.OrdinalIgnoreCase))
            {
                CliHelpers.WriteError(command,
                    ErrorCodes.ValidationFailed with { Message = "Unsupported --profile. Supported: academic." }, json);
                return;
            }
            if (minScore is < 0 or > 100)
            {
                CliHelpers.WriteError(command,
                    ErrorCodes.ValidationFailed with { Message = "--min-score must be between 0 and 100." }, json);
                return;
            }

            try
            {
                var (result, elapsed) = CliHelpers.Time(() => WordFormatAuditor.Audit(file, profile));
                var gateFailures = GetFormatAuditGateFailures(result, failOnWarning, minScore);
                var gateFailed = gateFailures.Count > 0;
                if (json)
                {
                    var output = gateFailed
                        ? new JsonOutput
                        {
                            Status = "error",
                            Command = command,
                            Summary = $"Format audit gate failed: {string.Join("; ", gateFailures)}",
                            Data = result,
                            Errors = new List<ErrorEntry>
                            {
                                ErrorCodes.ValidationFailed with
                                {
                                    Message = $"Format audit gate failed: {string.Join("; ", gateFailures)}",
                                },
                            },
                            Meta = new MetaInfo { Version = CliVersion.Current },
                        }
                        : JsonOutput.Ok(command,
                            $"Format audit {result.StatusLevel}: {result.Summary.Issues} issue(s), score {result.Score}",
                            result);
                    output.Metrics["score"] = result.Score;
                    output.Metrics["issues"] = result.Summary.Issues;
                    output.Metrics["headings"] = result.Summary.Headings;
                    output.Metrics["bodyParagraphs"] = result.Summary.BodyParagraphs;
                    output.Metrics["tables"] = result.Summary.Tables;
                    output.Metrics["threeLineTables"] = result.Tables.ThreeLineLike;
                    output.Metrics["gateFailed"] = gateFailed;
                    output.Meta.DurationMs = elapsed;
                    foreach (var issue in result.Issues)
                    {
                        output.Issues.Add(new Issue
                        {
                            Id = issue.Id,
                            Severity = issue.Severity,
                            Message = issue.BlockId == null
                                ? issue.Message
                                : $"{issue.BlockId}: {issue.Message}",
                        });
                    }
                    foreach (var failure in gateFailures)
                    {
                        output.Issues.Add(new Issue
                        {
                            Id = "format_audit_gate",
                            Severity = "error",
                            Message = failure,
                        });
                    }
                    Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"Status: {result.StatusLevel}");
                    Console.WriteLine($"Score: {result.Score}");
                    Console.WriteLine($"Headings: {result.Summary.Headings}");
                    Console.WriteLine($"Body paragraphs: {result.Summary.BodyParagraphs}");
                    Console.WriteLine($"Tables: {result.Tables.ThreeLineLike}/{result.Tables.Total} three-line-like");
                    foreach (var issue in result.Issues.Take(20))
                        Console.WriteLine($"[{issue.Severity}] {issue.BlockId ?? "-"} {issue.Id}: {issue.Message}");
                    foreach (var failure in gateFailures)
                        Console.Error.WriteLine($"[error] format_audit_gate: {failure}");
                }

                if (gateFailed)
                    Environment.ExitCode = 1;
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError(command, ErrorCodes.InternalError with { Message = ex.Message }, json);
            }
        }, fileArg, profileOpt, failOnWarningOpt, minScoreOpt, jsonOpt);
        return cmd;
    }

    static List<string> GetFormatAuditGateFailures(WordFormatAuditResult result, bool failOnWarning, int? minScore)
    {
        var failures = new List<string>();
        if (result.StatusLevel.Equals("fail", StringComparison.OrdinalIgnoreCase))
            failures.Add("statusLevel is fail");
        if (failOnWarning && result.StatusLevel.Equals("warn", StringComparison.OrdinalIgnoreCase))
            failures.Add("statusLevel is warn and --fail-on-warning was set");
        if (minScore.HasValue && result.Score < minScore.Value)
            failures.Add($"score {result.Score} is lower than --min-score {minScore.Value}");
        return failures;
    }

    static Command CreateFormatGongwen(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var outOpt = new Option<string>("-o", "Output .docx path") { IsRequired = true };
        var configOpt = new Option<string?>("--config", "Optional gongwen style JSON config");
        var cmd = new Command("format-gongwen", "Apply Chinese official-document formatting to an existing DOCX")
        {
            fileArg,
            outOpt,
            configOpt,
        };

        cmd.SetHandler((string file, string output, string? config, bool json) =>
        {
            const string command = "word format-gongwen";
            var err = CliHelpers.ValidateDocxFile(file);
            if (err != null) { CliHelpers.WriteError(command, err, json); return; }
            if (!string.Equals(Path.GetExtension(output), ".docx", StringComparison.OrdinalIgnoreCase))
            {
                CliHelpers.WriteError(command,
                    ErrorCodes.ValidationFailed with { Message = "Output path must end with .docx." }, json);
                return;
            }
            if (string.Equals(Path.GetFullPath(file), Path.GetFullPath(output), StringComparison.OrdinalIgnoreCase))
            {
                CliHelpers.WriteError(command,
                    ErrorCodes.ValidationFailed with { Message = "Input and output paths must be different." }, json);
                return;
            }
            if (!string.IsNullOrWhiteSpace(config) && !File.Exists(config))
            {
                CliHelpers.WriteError(command,
                    ErrorCodes.FileNotFound with { Message = $"Config file not found: {config}" }, json);
                return;
            }

            try
            {
                CliHelpers.EnsureParentDir(output);
                var (data, elapsed) = CliHelpers.Time(() =>
                {
                    var fullInput = Path.GetFullPath(file);
                    var fullOutput = Path.GetFullPath(output);
                    var options = string.IsNullOrWhiteSpace(config)
                        ? new FormatOptions()
                        : FormatOptions.FromJsonFile(config);

                    File.Copy(fullInput, fullOutput, overwrite: true);
                    using var doc = WordprocessingDocument.Open(fullOutput, true);
                    new GongWenFormatter().FormatDocument(doc, options);

                    return new
                    {
                        input = fullInput,
                        output = fullOutput,
                        config = string.IsNullOrWhiteSpace(config) ? null : Path.GetFullPath(config),
                        formatProfile = "gongwen",
                        source = options.Source,
                    };
                });
                var a = CliHelpers.CheckArtifact(output, "DOCX");
                if (a != null) { CliHelpers.WriteError(command, a, json); return; }

                var o = JsonOutput.Ok(command, $"Applied gongwen formatting: {output}", data);
                o.Artifacts["docx"] = Path.GetFullPath(output);
                o.Meta.DurationMs = elapsed;
                Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
            }
            catch (JsonException ex)
            {
                CliHelpers.WriteError(command,
                    ErrorCodes.ValidationFailed with { Message = $"Config is not valid JSON: {ex.Message}" }, json);
            }
            catch (Exception ex)
            {
                try { if (File.Exists(output)) File.Delete(output); } catch { }
                CliHelpers.WriteError(command, ErrorCodes.InternalError with { Message = ex.Message }, json);
            }
        }, fileArg, outOpt, configOpt, jsonOpt);
        return cmd;
    }

    static Command CreateRepairPlan(Option<bool> jsonOpt)
    {
        var cmd = new Command("repair-plan", "Explain which Word repair command to use; prevents confusing internal OOXML repair with visible formatting repair");
        cmd.SetHandler((bool json) =>
        {
            var plan = new
            {
                schemaVersion = "nong-word/repair-plan/v1",
                rules = new[]
                {
                    new
                    {
                        userGoal = "Open Word and make the document visibly better.",
                        command = "word academic-format",
                        outputNameHint = "*.academic-fixed.docx",
                        completionEvidence = new[] { "word validate", "word format-audit", "word dissect", "slice inspect --strict", "format.json.visualEvidence" },
                        note = "This is the current visible formatting path for academic-style documents."
                    },
                    new
                    {
                        userGoal = "Prove whether a Word document is visibly formatted well enough.",
                        command = "word format-audit",
                        outputNameHint = "*.format-audit.json",
                        completionEvidence = new[] { "data.statusLevel", "data.headings", "data.body", "data.tables", "issues" },
                        note = "This is a read-only visible-format evidence audit. It does not modify the document."
                    },
                    new
                    {
                        userGoal = "Fix invalid OOXML element order or table compatibility warnings.",
                        command = "word fix-order",
                        outputNameHint = "*.ooxml-fixed.docx",
                        completionEvidence = new[] { "word validate", "word preview" },
                        note = "This is an internal structure repair. Do not call the user-facing document visually fixed just because this passes."
                    },
                    new
                    {
                        userGoal = "Split long or wide tables into continuation tables.",
                        command = "word table-reflow",
                        outputNameHint = "*.table-reflowed.docx",
                        completionEvidence = new[] { "word validate", "word dissect", "format.json.visualEvidence.tables" },
                        note = "Use after academic-format when table layout still needs explicit reflow."
                    },
                },
                plannedCommands = new[]
                {
                    "word repair",
                    "word compare-format"
                },
                forbiddenCompletionClaim = "Do not claim visible Word repair is complete from word validate, word preview, word outline, word dissect, or word fix-order alone. Use word format-audit for visible formatting evidence.",
            };

            if (json)
            {
                var output = JsonOutput.Ok("word repair-plan", "Word repair command routing", plan);
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            else
            {
                Console.WriteLine(JsonSerializer.Serialize(plan, CliHelpers.JsonOpts));
            }
        }, jsonOpt);
        return cmd;
    }

    static Command CreateTableReflow(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var outOpt = new Option<string>("-o", "Output .docx path") { IsRequired = true };
        var maxRowsOpt = new Option<int>("--max-rows", () => 0, "Split table body rows into continuation tables after this many rows. 0 disables row splitting.");
        var maxColsOpt = new Option<int>("--max-cols", () => 0, "Split wide tables into column groups after this many columns. 0 disables column splitting.");
        var repeatLeftColsOpt = new Option<int>("--repeat-left-cols", () => 0, "Repeat this many left-most columns in later wide-table parts.");
        var continuationLabelOpt = new Option<string>("--continuation-label", () => "续表", "Continuation table label prefix.");
        var cmd = new Command("table-reflow", "Explicitly split long or wide tables into continuation tables") { fileArg, outOpt, maxRowsOpt, maxColsOpt, repeatLeftColsOpt, continuationLabelOpt };
        cmd.SetHandler((string file, string output, int maxRows, int maxCols, int repeatLeftCols, string continuationLabel, bool json) =>
        {
            const string command = "word table-reflow";
            var err = CliHelpers.ValidateDocxFile(file);
            if (err != null) { CliHelpers.WriteError(command, err, json); return; }
            if (!string.Equals(Path.GetExtension(output), ".docx", StringComparison.OrdinalIgnoreCase))
            {
                CliHelpers.WriteError(command,
                    ErrorCodes.ValidationFailed with { Message = "Output path must end with .docx." }, json);
                return;
            }
            if (maxRows < 0 || maxCols < 0 || repeatLeftCols < 0)
            {
                CliHelpers.WriteError(command,
                    ErrorCodes.ValidationFailed with { Message = "--max-rows, --max-cols, and --repeat-left-cols must be non-negative." }, json);
                return;
            }
            if (string.Equals(Path.GetFullPath(file), Path.GetFullPath(output), StringComparison.OrdinalIgnoreCase))
            {
                CliHelpers.WriteError(command,
                    ErrorCodes.ValidationFailed with { Message = "Input and output paths must be different." }, json);
                return;
            }

            try
            {
                CliHelpers.EnsureParentDir(output);
                var options = new WordTableReflow.TableReflowOptions(maxRows, maxCols, repeatLeftCols, continuationLabel);
                var (r, e) = CliHelpers.Time(() =>
                {
                    var reflowed = WordTableReflow.Apply(file, output, options);
                    FixOrderInPlace(output);
                    return reflowed;
                });
                var a = CliHelpers.CheckArtifact(output, "DOCX");
                if (a != null) { CliHelpers.WriteError(command, a, json); return; }
                var o = JsonOutput.Ok(command,
                    $"Reflowed {r.TablesReflowed} table(s), produced {r.OutputTables} table part(s)",
                    r);
                o.Artifacts["docx"] = Path.GetFullPath(output);
                o.Metrics["tablesVisited"] = r.TablesVisited;
                o.Metrics["tablesReflowed"] = r.TablesReflowed;
                o.Metrics["longTablesSplit"] = r.LongTablesSplit;
                o.Metrics["wideTablesSplit"] = r.WideTablesSplit;
                o.Metrics["outputTables"] = r.OutputTables;
                o.Meta.DurationMs = e;
                foreach (var warning in r.Warnings)
                {
                    o.Issues.Add(new Issue
                    {
                        Id = "table_reflow",
                        Severity = "warning",
                        Message = warning,
                    });
                }
                Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
            }
            catch (ArgumentException ex)
            {
                CliHelpers.WriteError(command, ErrorCodes.ValidationFailed with { Message = ex.Message }, json);
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError(command, ErrorCodes.InternalError with { Message = ex.Message }, json);
            }
        }, fileArg, outOpt, maxRowsOpt, maxColsOpt, repeatLeftColsOpt, continuationLabelOpt, jsonOpt);
        return cmd;
    }

    static Command CreateProtect(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var outOpt = new Option<string>("-o", "Output .docx path") { IsRequired = true };
        var modeOpt = new Option<string>("--mode", () => "readonly", "readonly, comments, tracked, forms");
        var pwdOpt = new Option<string>("-p", "Password");
        var cmd = new Command("protect", "Apply document protection") { fileArg, outOpt, modeOpt, pwdOpt };
        cmd.SetHandler((string file, string output, string mode, string? pwd, bool json) =>
        {
            var err = CliHelpers.ValidateDocxFile(file);
            if (err != null) { CliHelpers.WriteError("word protect", err, json); return; }
            if (string.Equals(Path.GetFullPath(file), Path.GetFullPath(output), StringComparison.OrdinalIgnoreCase))
            { CliHelpers.WriteError("word protect", ErrorCodes.ValidationFailed with { Message = "Input and output paths must be different." }, json); return; }
            try { CliHelpers.EnsureParentDir(output); var (r, e) = CliHelpers.Time(() => WordEditOperations.Protect(file, output, mode, pwd));
                var a = CliHelpers.CheckArtifact(output, "DOCX"); if (a != null) { CliHelpers.WriteError("word protect", a, json); return; }
                var o = JsonOutput.Ok("word protect", $"Protected: {r.ProtectionMode}", r); o.Artifacts["docx"] = Path.GetFullPath(output); o.Meta.DurationMs = e;
                Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts)); }
            catch (ArgumentException ex) { CliHelpers.WriteError("word protect", ErrorCodes.ValidationFailed with { Message = ex.Message }, json); }
            catch (Exception ex) { CliHelpers.WriteError("word protect", ErrorCodes.InternalError with { Message = ex.Message }, json); }
        }, fileArg, outOpt, modeOpt, pwdOpt, jsonOpt);
        return cmd;
    }

    static Command CreateEmbedFont(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var fontArg = new Argument<string>("font-file", "Path to .ttf/.otf font");
        var outOpt = new Option<string>("-o", "Output .docx path") { IsRequired = true };
        var nameOpt = new Option<string>("--name", "Font name override");
        var cmd = new Command("embed-font", "Embed font into document") { fileArg, fontArg, outOpt, nameOpt };
        cmd.SetHandler((string file, string fontFile, string output, string? name, bool json) =>
        {
            var err = CliHelpers.ValidateDocxFile(file);
            if (err != null) { CliHelpers.WriteError("word embed-font", err, json); return; }
            if (!File.Exists(fontFile)) { CliHelpers.WriteError("word embed-font", ErrorCodes.FileNotFound with { Message = $"Font not found: {fontFile}" }, json); return; }
            if (string.Equals(Path.GetFullPath(file), Path.GetFullPath(output), StringComparison.OrdinalIgnoreCase))
            { CliHelpers.WriteError("word embed-font", ErrorCodes.ValidationFailed with { Message = "Input and output paths must be different." }, json); return; }
            try { CliHelpers.EnsureParentDir(output); var (r, e) = CliHelpers.Time(() => WordEditOperations.EmbedFont(file, output, fontFile, name));
                var a = CliHelpers.CheckArtifact(output, "DOCX"); if (a != null) { CliHelpers.WriteError("word embed-font", a, json); return; }
                var o = JsonOutput.Ok("word embed-font", $"Embedded: {r.FontName}", r); o.Artifacts["docx"] = Path.GetFullPath(output); o.Meta.DurationMs = e;
                Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts)); }
            catch (FileNotFoundException ex) { CliHelpers.WriteError("word embed-font", ErrorCodes.FileNotFound with { Message = ex.Message }, json); }
            catch (ArgumentException ex) when (ex.Message.Contains("Unsupported", StringComparison.OrdinalIgnoreCase))
            { CliHelpers.WriteError("word embed-font", ErrorCodes.UnsupportedFormat with { Message = ex.Message }, json); }
            catch (ArgumentException ex) { CliHelpers.WriteError("word embed-font", ErrorCodes.ValidationFailed with { Message = ex.Message }, json); }
            catch (Exception ex) { CliHelpers.WriteError("word embed-font", ErrorCodes.InternalError with { Message = ex.Message }, json); }
        }, fileArg, fontArg, outOpt, nameOpt, jsonOpt);
        return cmd;
    }

    // ===== word compare =====

    static Command CreateCompare(Option<bool> jsonOpt)
    {
        var file1Arg = new Argument<string>("file1", "First .docx file");
        var file2Arg = new Argument<string>("file2", "Second .docx file");
        var cmd = new Command("compare", "Compare two DOCX files and report differences") { file1Arg, file2Arg };

        cmd.SetHandler((string file1, string file2, bool json) =>
        {
            const string command = "word compare";
            var e1 = CliHelpers.ValidateDocxFile(file1);
            if (e1 != null) { CliHelpers.WriteError(command, e1, json); return; }
            var e2 = CliHelpers.ValidateDocxFile(file2);
            if (e2 != null) { CliHelpers.WriteError(command, e2, json); return; }

            try
            {
                var (result, elapsed) = CliHelpers.Time(() =>
                {
                    var ps1 = ExtractParagraphs(file1);
                    var ps2 = ExtractParagraphs(file2);
                    var diffs = DiffParagraphs(ps1, ps2);
                    return new { file1, file2, changes = diffs, paragraphCount1 = ps1.Count, paragraphCount2 = ps2.Count };
                });

                var output = JsonOutput.Ok(command,
                    $"Compared: {result.paragraphCount1} vs {result.paragraphCount2} paragraphs, {result.changes.Count} change(s)",
                    result);
                output.Metrics["paragraphsA"] = result.paragraphCount1;
                output.Metrics["paragraphsB"] = result.paragraphCount2;
                output.Metrics["changes"] = result.changes.Count;
                output.Metrics["added"] = result.changes.Count(c => c.Kind == "added");
                output.Metrics["removed"] = result.changes.Count(c => c.Kind == "removed");
                output.Metrics["modified"] = result.changes.Count(c => c.Kind == "modified");
                output.Meta.DurationMs = elapsed;
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            catch (Exception ex) { CliHelpers.WriteError(command, ErrorCodes.InternalError with { Message = ex.Message }, json); }
        }, file1Arg, file2Arg, jsonOpt);
        return cmd;
    }

    static List<ParagraphSnapshot> ExtractParagraphs(string docxPath)
    {
        var paragraphs = new List<ParagraphSnapshot>();
        using var doc = WordprocessingDocument.Open(docxPath, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body == null) return paragraphs;

        int idx = 0;
        foreach (var p in body.Elements<Paragraph>())
        {
            var text = p.InnerText;
            var styleId = p.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
            paragraphs.Add(new ParagraphSnapshot(idx, text, styleId));
            idx++;
        }
        return paragraphs;
    }

    static List<CompareChange> DiffParagraphs(List<ParagraphSnapshot> a, List<ParagraphSnapshot> b)
    {
        var changes = new List<CompareChange>();
        int maxLen = Math.Max(a.Count, b.Count);
        // Simple line-by-line comparison with text normalization
        for (int i = 0; i < maxLen; i++)
        {
            var ta = i < a.Count ? NormalizeText(a[i].Text) : null;
            var tb = i < b.Count ? NormalizeText(b[i].Text) : null;

            if (i >= a.Count)
            {
                changes.Add(new CompareChange(i, "added", b[i].Text, b[i].StyleId));
            }
            else if (i >= b.Count)
            {
                changes.Add(new CompareChange(i, "removed", a[i].Text, a[i].StyleId));
            }
            else if (ta != tb)
            {
                // Try to find if this paragraph moved by searching in the other doc
                changes.Add(new CompareChange(i, "modified", b[i].Text, b[i].StyleId,
                    previousText: a[i].Text, previousStyleId: a[i].StyleId));
            }
        }
        return changes;
    }

    static string NormalizeText(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        // Collapse whitespace to single spaces
        var result = System.Text.RegularExpressions.Regex.Replace(text.Trim(), @"\s+", " ");
        return result;
    }

    sealed record ParagraphSnapshot(int Index, string Text, string? StyleId);
    sealed record CompareChange(int Index, string Kind, string Text, string? StyleId,
        string? previousText = null, string? previousStyleId = null);

    // ===== word render-preview =====

    static Command CreateRenderPreview(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var outOpt = new Option<string>("-o", "Output directory for rendered page PNGs") { IsRequired = true };
        var dpiOpt = new Option<int>("--dpi", () => 150, "Render DPI");
        var cmd = new Command("render-preview", "Render DOCX pages as PNGs via LibreOffice headless PDF conversion") { fileArg, outOpt, dpiOpt };

        cmd.SetHandler((string file, string output, int dpi, bool json) =>
        {
            const string command = "word render-preview";
            try
            {
                if (!File.Exists(file))
                { CliHelpers.WriteError(command, ErrorCodes.FileNotFound with { Message = $"File not found: {file}" }, json); return; }

                // Step 1: DOCX → PDF via LibreOffice
                var soffice = FindExecutable("soffice") ?? FindLibreOfficeOnWindows();
                if (soffice == null)
                {
                    CliHelpers.WriteError(command,
                        ErrorCodes.DependencyMissing with { Message = "LibreOffice is required for render-preview. Install LibreOffice (libreoffice.org) and ensure soffice is on PATH." }, json);
                    return;
                }

                var tempDir = Path.Combine(Path.GetTempPath(), "nong-render-preview-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);
                var pdfPath = Path.Combine(tempDir, "preview.pdf");

                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = soffice,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    foreach (var arg in new[] { "--headless", "--convert-to", "pdf", "--outdir", tempDir, file })
                        psi.ArgumentList.Add(arg);

                    using var proc = Process.Start(psi)!;
                    var soStdout = proc.StandardOutput.ReadToEnd();
                    var soStderr = proc.StandardError.ReadToEnd();
                    if (!proc.WaitForExit(120000))
                    {
                        try { proc.Kill(entireProcessTree: true); } catch { }
                        throw new TimeoutException("LibreOffice conversion timed out (120s).");
                    }
                    if (proc.ExitCode != 0)
                        throw new InvalidOperationException($"LibreOffice exit code {proc.ExitCode}: {soStderr}");

                    // LibreOffice names output by replacing extension: paper.docx → paper.pdf
                    var expectedName = Path.GetFileNameWithoutExtension(file) + ".pdf";
                    var produced = Directory.GetFiles(tempDir, "*.pdf").FirstOrDefault();
                    if (produced == null)
                        throw new InvalidOperationException($"LibreOffice did not produce PDF. stdout: {soStdout}");

                    // Step 2: PDF → PNG via nong-pdf render subprocess
                    Directory.CreateDirectory(output);
                    var (renderExit, renderOut, renderErr) = CliHelpers.RunToolCapture(
                        "nong-pdf", ToolPackages.Pdf,
                        new[] { "pdf", "render", produced, "-o", output, "--dpi", dpi.ToString(), "--json" });

                    if (renderExit != 0)
                        throw new InvalidOperationException($"nong-pdf render failed (exit {renderExit}): {renderErr}");

                    // Parse render output for page count
                    int pageCount = 0;
                    var pageFiles = new List<string>();
                    try
                    {
                        using var renderDoc = JsonDocument.Parse(renderOut);
                        var rd = renderDoc.RootElement;
                        if (rd.TryGetProperty("data", out var rdata))
                        {
                            if (rdata.TryGetProperty("pages", out var p) && p.TryGetInt32(out var pc))
                                pageCount = pc;
                        }
                        // Enumerate produced PNGs
                        pageFiles = Directory.GetFiles(output, "*.png").OrderBy(f => f).Select(Path.GetFullPath).ToList();
                        if (pageFiles.Count == 0)
                            pageFiles = Directory.GetFiles(output, "*.jpg").OrderBy(f => f).Select(Path.GetFullPath).ToList();
                    }
                    catch { /* best-effort parse */ }

                    if (json)
                    {
                        var o = JsonOutput.Ok(command,
                            $"Rendered {pageCount} page(s) at {dpi} DPI",
                            new { pages = pageCount, dpi, pageFiles });
                        o.Artifacts["dir"] = Path.GetFullPath(output);
                        o.Metrics["pages"] = pageCount;
                        o.Metrics["dpi"] = dpi;
                        o.Issues.Add(new Issue { Id = "render_engine", Severity = "Info", Message = $"Rendered via LibreOffice (soffice) at {dpi} DPI. Layout fidelity depends on LibreOffice's DOCX import." });
                        Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
                    }
                    else
                    {
                        Console.WriteLine($"Rendered {pageCount} page(s) to {Path.GetFullPath(output)}");
                        foreach (var pf in pageFiles)
                            Console.WriteLine($"  {pf}");
                    }
                }
                finally
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
            catch (TimeoutException ex)
            { CliHelpers.WriteError(command, ErrorCodes.InternalError with { Message = ex.Message }, json); }
            catch (InvalidOperationException ex)
            { CliHelpers.WriteError(command, ErrorCodes.DependencyMissing with { Message = ex.Message }, json); }
            catch (Exception ex)
            { CliHelpers.WriteError(command, ErrorCodes.InternalError with { Message = ex.Message }, json); }
        }, fileArg, outOpt, dpiOpt, jsonOpt);
        return cmd;
    }

    // ===== Stage 15: add commands =====

    static readonly JsonSerializerOptions SpecJsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    static Command CreateAddGroup(Option<bool> jsonOpt)
    {
        var cmd = new Command("add", "Add content to a Word document");
        cmd.AddCommand(CreateAddParagraphCommand("paragraph", jsonOpt));
        cmd.AddCommand(CreateAddTableCommand("table", jsonOpt));
        cmd.AddCommand(CreateAddFootnoteCommand("footnote", jsonOpt));
        cmd.AddCommand(CreateAddEndnoteCommand("endnote", jsonOpt));
        cmd.AddCommand(CreateAddImageCommand("image", jsonOpt));
        cmd.AddCommand(CreateAddTocCommand("toc", jsonOpt));
        cmd.AddCommand(CreateAddXrefCommand("xref", jsonOpt));
        cmd.AddCommand(CreateAddLinkCommand("link", jsonOpt));
        cmd.AddCommand(CreateAddBookmarkCommand("bookmark", jsonOpt));
        cmd.AddCommand(CreateAddCommentCommand("comment", jsonOpt));
        cmd.AddCommand(CreateAddMathCommand("math", jsonOpt));
        return cmd;
    }

    static Command CreateAddParagraph(Option<bool> jsonOpt) => CreateAddParagraphCommand("add-paragraph", jsonOpt);

    static Command CreateAddParagraphCommand(string commandName, Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var specOpt = new Option<string>("--spec", "Spec JSON file path or inline JSON: {text, style, bold, italic}") { IsRequired = true };
        var outOpt = new Option<string>("-o", "Output .docx path") { IsRequired = true };
        var afterOpt = new Option<string>("--after", "Insert after blockId, e.g. p0001");
        var cmd = new Command(commandName, "Append paragraph") { fileArg, specOpt, outOpt, afterOpt };
        cmd.SetHandler(HandleAddParagraph, fileArg, specOpt, outOpt, afterOpt, jsonOpt);
        return cmd;
    }

    static void HandleAddParagraph(string file, string spec, string output, string? after, bool json)
    {
        const string command = "word add paragraph";
        if (!ValidateAddPaths(command, file, output, json)) return;
        if (!TryLoadSpec<WordAddOperations.ParagraphSpec>(command, spec, json, out var ps)) return;
        try
        {
            CliHelpers.EnsureParentDir(output);
            var (r, e) = CliHelpers.Time(() => WordAddOperations.AddParagraph(file, output, ps!, after));
            WriteDocxAddOk(command, $"Added: {r.TextPreview}", r, output, e, json);
        }
        catch (Exception ex) { WriteAddException(command, ex, json); }
    }

    static Command CreateAddTable(Option<bool> jsonOpt) => CreateAddTableCommand("add-table", jsonOpt);

    static Command CreateAddTableCommand(string commandName, Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var specOpt = new Option<string>("--spec", "Spec JSON file path or inline JSON: {caption, headers, rows}") { IsRequired = true };
        var outOpt = new Option<string>("-o", "Output .docx path") { IsRequired = true };
        var afterOpt = new Option<string>("--after", "Insert after blockId, e.g. p0001");
        var cmd = new Command(commandName, "Append table") { fileArg, specOpt, outOpt, afterOpt };
        cmd.SetHandler(HandleAddTable, fileArg, specOpt, outOpt, afterOpt, jsonOpt);
        return cmd;
    }

    static void HandleAddTable(string file, string spec, string output, string? after, bool json)
    {
        const string command = "word add table";
        if (!ValidateAddPaths(command, file, output, json)) return;
        if (!TryLoadSpec<WordAddOperations.TableSpec>(command, spec, json, out var ts)) return;
        try
        {
            CliHelpers.EnsureParentDir(output);
            var (r, e) = CliHelpers.Time(() => WordAddOperations.AddTable(file, output, ts!, after));
            WriteDocxAddOk(command, $"Added: {r.Rows}x{r.Cols}", r, output, e, json);
        }
        catch (Exception ex) { WriteAddException(command, ex, json); }
    }

    static Command CreateAddMath(Option<bool> jsonOpt) => CreateAddMathCommand("add-math", jsonOpt);

    static Command CreateAddMathCommand(string commandName, Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var latexOpt = new Option<string>("--latex", "LaTeX formula") { IsRequired = true };
        var displayOpt = new Option<bool>("--display", () => false, "Display mode");
        var outOpt = new Option<string>("-o", "Output .docx path") { IsRequired = true };
        var afterOpt = new Option<string>("--after", "Insert after blockId, e.g. p0001");
        var cmd = new Command(commandName, "Append math equation") { fileArg, latexOpt, displayOpt, outOpt, afterOpt };
        cmd.SetHandler(HandleAddMath, fileArg, latexOpt, displayOpt, outOpt, afterOpt, jsonOpt);
        return cmd;
    }

    static void HandleAddMath(string file, string latex, bool display, string output, string? after, bool json)
    {
        const string command = "word add math";
        if (!ValidateAddPaths(command, file, output, json)) return;
        if (string.IsNullOrWhiteSpace(latex)) { CliHelpers.WriteError(command, ErrorCodes.ValidationFailed with { Message = "LaTeX required." }, json); return; }
        try
        {
            CliHelpers.EnsureParentDir(output);
            var ms = new WordAddOperations.MathSpec(latex, display);
            var (r, e) = CliHelpers.Time(() => WordAddOperations.AddMath(file, output, ms, after));
            WriteDocxAddOk(command, $"Added: {r.Latex}", r, output, e, json);
        }
        catch (Exception ex) { WriteAddException(command, ex, json); }
    }

    static Command CreateAddImage(Option<bool> jsonOpt) => CreateAddImageCommand("add-image", jsonOpt);

    static Command CreateAddImageCommand(string commandName, Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var srcOpt = new Option<string>("--src", "Path to image") { IsRequired = true };
        var capOpt = new Option<string>("--caption", "Image caption");
        var outOpt = new Option<string>("-o", "Output .docx path") { IsRequired = true };
        var afterOpt = new Option<string>("--after", "Insert after blockId, e.g. p0001");
        var cmd = new Command(commandName, "Append image") { fileArg, srcOpt, capOpt, outOpt, afterOpt };
        cmd.SetHandler(HandleAddImage, fileArg, srcOpt, capOpt, outOpt, afterOpt, jsonOpt);
        return cmd;
    }

    static void HandleAddImage(string file, string src, string? caption, string output, string? after, bool json)
    {
        const string command = "word add image";
        if (!ValidateAddPaths(command, file, output, json)) return;
        if (!File.Exists(src)) { CliHelpers.WriteError(command, ErrorCodes.FileNotFound with { Message = $"Image not found: {src}" }, json); return; }
        try
        {
            CliHelpers.EnsureParentDir(output);
            var isp = new WordAddOperations.ImageSpec(src, caption);
            var (r, e) = CliHelpers.Time(() => WordAddOperations.AddImage(file, output, isp, after));
            WriteDocxAddOk(command, $"Added: {r.Width}x{r.Height}", r, output, e, json);
        }
        catch (Exception ex) { WriteAddException(command, ex, json); }
    }

    // ===== Simple add commands (footnote, endnote, toc, bookmark, comment, xref, link) =====

    static Command CreateAddFootnote(Option<bool> j) => CreateAddFootnoteCommand("add-footnote", j);
    static Command CreateAddEndnote(Option<bool> j) => CreateAddEndnoteCommand("add-endnote", j);
    static Command CreateAddToc(Option<bool> j) => CreateAddTocCommand("add-toc", j);
    static Command CreateAddBookmark(Option<bool> j) => CreateAddBookmarkCommand("add-bookmark", j);
    static Command CreateAddComment(Option<bool> j) => CreateAddCommentCommand("add-comment", j);
    static Command CreateAddXref(Option<bool> j) => CreateAddXrefCommand("add-xref", j);
    static Command CreateAddLink(Option<bool> j) => CreateAddLinkCommand("add-link", j);

    static Command CreateAddFootnoteCommand(string commandName, Option<bool> jsonOpt) =>
        TextAdd(commandName, "Append footnote", "word add footnote",
            (f, o, text, after) => WordAddOperations.AddFootnote(f, o, text, after), jsonOpt);

    static Command CreateAddEndnoteCommand(string commandName, Option<bool> jsonOpt) =>
        TextAdd(commandName, "Append endnote", "word add endnote",
            (f, o, text, after) => WordAddOperations.AddEndnote(f, o, text, after), jsonOpt);

    static Command CreateAddTocCommand(string commandName, Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var titleOpt = new Option<string>("--title", () => "目录", "TOC title");
        var outOpt = new Option<string>("-o", "Output .docx path") { IsRequired = true };
        var afterOpt = new Option<string>("--after", "Insert after blockId, e.g. p0001");
        var cmd = new Command(commandName, "Append table of contents") { fileArg, titleOpt, outOpt, afterOpt };
        cmd.SetHandler((string file, string title, string output, string? after, bool json) =>
        {
            const string command = "word add toc";
            if (!ValidateAddPaths(command, file, output, json)) return;
            try
            {
                CliHelpers.EnsureParentDir(output);
                var (r, e) = CliHelpers.Time(() => WordAddOperations.AddTableOfContents(file, output, title, after));
                WriteDocxAddOk(command, $"Added TOC: {r.Title}", r, output, e, json);
            }
            catch (Exception ex) { WriteAddException(command, ex, json); }
        }, fileArg, titleOpt, outOpt, afterOpt, jsonOpt);
        return cmd;
    }

    static Command CreateAddBookmarkCommand(string commandName, Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var nameOpt = new Option<string>("--name", "Bookmark name") { IsRequired = true };
        var outOpt = new Option<string>("-o", "Output .docx path") { IsRequired = true };
        var afterOpt = new Option<string>("--after", "Insert after blockId, e.g. p0001");
        var cmd = new Command(commandName, "Append bookmark") { fileArg, nameOpt, outOpt, afterOpt };
        cmd.SetHandler((string file, string name, string output, string? after, bool json) =>
        {
            const string command = "word add bookmark";
            if (!ValidateAddPaths(command, file, output, json)) return;
            try
            {
                CliHelpers.EnsureParentDir(output);
                var (r, e) = CliHelpers.Time(() => WordAddOperations.AddBookmark(file, output, name, after));
                WriteDocxAddOk(command, $"Added bookmark: {r.Name}", r, output, e, json);
            }
            catch (Exception ex) { WriteAddException(command, ex, json); }
        }, fileArg, nameOpt, outOpt, afterOpt, jsonOpt);
        return cmd;
    }

    static Command CreateAddCommentCommand(string commandName, Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var textOpt = new Option<string>("--text", "Comment text") { IsRequired = true };
        var authorOpt = new Option<string>("--author", () => "nong", "Comment author");
        var outOpt = new Option<string>("-o", "Output .docx path") { IsRequired = true };
        var afterOpt = new Option<string>("--after", "Insert after blockId, e.g. p0001");
        var cmd = new Command(commandName, "Append comment") { fileArg, textOpt, authorOpt, outOpt, afterOpt };
        cmd.SetHandler((string file, string text, string author, string output, string? after, bool json) =>
        {
            const string command = "word add comment";
            if (!ValidateAddPaths(command, file, output, json)) return;
            try
            {
                CliHelpers.EnsureParentDir(output);
                var (r, e) = CliHelpers.Time(() => WordAddOperations.AddComment(file, output, author, text, after));
                WriteDocxAddOk(command, $"Added comment: {r.TextPreview}", r, output, e, json);
            }
            catch (Exception ex) { WriteAddException(command, ex, json); }
        }, fileArg, textOpt, authorOpt, outOpt, afterOpt, jsonOpt);
        return cmd;
    }

    static Command CreateAddXrefCommand(string commandName, Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var toOpt = new Option<string>("--to", "Target bookmark") { IsRequired = true };
        var textOpt = new Option<string>("--text", "Display text") { IsRequired = true };
        var outOpt = new Option<string>("-o", "Output .docx path") { IsRequired = true };
        var afterOpt = new Option<string>("--after", "Insert after blockId, e.g. p0001");
        var cmd = new Command(commandName, "Append cross-reference") { fileArg, toOpt, textOpt, outOpt, afterOpt };
        cmd.SetHandler((string file, string to, string text, string output, string? after, bool json) =>
        {
            const string command = "word add xref";
            if (!ValidateAddPaths(command, file, output, json)) return;
            try
            {
                CliHelpers.EnsureParentDir(output);
                var (r, e) = CliHelpers.Time(() => WordAddOperations.AddCrossReference(file, output, to, text, after));
                WriteDocxAddOk(command, $"Added xref: {r.DisplayText}", r, output, e, json);
            }
            catch (Exception ex) { WriteAddException(command, ex, json); }
        }, fileArg, toOpt, textOpt, outOpt, afterOpt, jsonOpt);
        return cmd;
    }

    static Command CreateAddLinkCommand(string commandName, Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var urlOpt = new Option<string>("--url", "Target URL") { IsRequired = true };
        var textOpt = new Option<string>("--text", "Display text") { IsRequired = true };
        var outOpt = new Option<string>("-o", "Output .docx path") { IsRequired = true };
        var afterOpt = new Option<string>("--after", "Insert after blockId, e.g. p0001");
        var cmd = new Command(commandName, "Append hyperlink") { fileArg, urlOpt, textOpt, outOpt, afterOpt };
        cmd.SetHandler((string file, string url, string text, string output, string? after, bool json) =>
        {
            const string command = "word add link";
            if (!ValidateAddPaths(command, file, output, json)) return;
            try
            {
                CliHelpers.EnsureParentDir(output);
                var (r, e) = CliHelpers.Time(() => WordAddOperations.AddHyperlink(file, output, url, text, after));
                WriteDocxAddOk(command, $"Added link: {r.DisplayText}", r, output, e, json);
            }
            catch (Exception ex) { WriteAddException(command, ex, json); }
        }, fileArg, urlOpt, textOpt, outOpt, afterOpt, jsonOpt);
        return cmd;
    }

    static Command TextAdd(string commandName, string description, string jsonCommand,
        Func<string, string, string, string?, object> action, Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file");
        var textOpt = new Option<string>("--text", "Content") { IsRequired = true };
        var outOpt = new Option<string>("-o", "Output .docx path") { IsRequired = true };
        var afterOpt = new Option<string>("--after", "Insert after blockId, e.g. p0001");
        var cmd = new Command(commandName, description) { fileArg, textOpt, outOpt, afterOpt };
        cmd.SetHandler((string file, string text, string output, string? after, bool json) =>
        {
            if (!ValidateAddPaths(jsonCommand, file, output, json)) return;
            try
            {
                CliHelpers.EnsureParentDir(output);
                var (r, e) = CliHelpers.Time(() => action(file, output, text, after));
                WriteDocxAddOk(jsonCommand, $"Added {commandName}", r, output, e, json);
            }
            catch (Exception ex) { WriteAddException(jsonCommand, ex, json); }
        }, fileArg, textOpt, outOpt, afterOpt, jsonOpt);
        return cmd;
    }

    static bool ValidateAddPaths(string command, string file, string output, bool json)
    {
        var err = CliHelpers.ValidateDocxFile(file);
        if (err != null) { CliHelpers.WriteError(command, err, json); return false; }

        if (string.Equals(Path.GetFullPath(file), Path.GetFullPath(output), StringComparison.OrdinalIgnoreCase))
        {
            CliHelpers.WriteError(command,
                ErrorCodes.ValidationFailed with { Message = "Input and output paths must be different." }, json);
            return false;
        }

        return true;
    }

    static bool TryLoadSpec<T>(string command, string spec, bool json, out T? value)
    {
        value = default;
        string raw;
        try
        {
            if (File.Exists(spec))
            {
                raw = File.ReadAllText(spec);
            }
            else if (LooksLikeSpecPath(spec))
            {
                CliHelpers.WriteError(command,
                    ErrorCodes.FileNotFound with { Message = $"Spec file not found: {spec}" }, json);
                return false;
            }
            else
            {
                raw = spec;
            }

            value = JsonSerializer.Deserialize<T>(raw, SpecJsonOpts);
            if (value == null)
            {
                CliHelpers.WriteError(command,
                    ErrorCodes.ValidationFailed with { Message = "Invalid JSON spec." }, json);
                return false;
            }

            return true;
        }
        catch (JsonException)
        {
            CliHelpers.WriteError(command,
                ErrorCodes.ValidationFailed with { Message = "Invalid JSON spec." }, json);
            return false;
        }
    }

    static bool LooksLikeSpecPath(string spec)
    {
        var trimmed = spec.TrimStart();
        if (trimmed.StartsWith("{") || trimmed.StartsWith("[")) return false;
        return spec.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            || spec.Contains('\\')
            || spec.Contains('/');
    }

    static void WriteDocxAddOk(string command, string summary, object data, string output, long elapsed, bool json)
    {
        var a = CliHelpers.CheckArtifact(output, "DOCX");
        if (a != null) { CliHelpers.WriteError(command, a, json); return; }
        var o = JsonOutput.Ok(command, summary, data);
        o.Artifacts["docx"] = Path.GetFullPath(output);
        o.Meta.DurationMs = elapsed;
        Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
    }

    static void WriteAddException(string command, Exception ex, bool json)
    {
        switch (ex)
        {
            case FileNotFoundException:
                CliHelpers.WriteError(command, ErrorCodes.FileNotFound with { Message = ex.Message }, json);
                break;
            case NotSupportedException:
                CliHelpers.WriteError(command, ErrorCodes.UnsupportedFormat with { Message = ex.Message }, json);
                break;
            case ArgumentException when ex.Message.Contains("Unsupported", StringComparison.OrdinalIgnoreCase)
                                     && ex.Message.Contains("format", StringComparison.OrdinalIgnoreCase):
                CliHelpers.WriteError(command, ErrorCodes.UnsupportedFormat with { Message = ex.Message }, json);
                break;
            case ArgumentException:
                CliHelpers.WriteError(command, ErrorCodes.ValidationFailed with { Message = ex.Message }, json);
                break;
            default:
                CliHelpers.WriteError(command, ErrorCodes.InternalError with { Message = ex.Message }, json);
                break;
        }
    }
}
