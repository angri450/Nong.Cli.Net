using System.CommandLine;
using System.Text.Json;
using DocxCore;
using Nong.Cli.Common;

namespace Nong.Cli.Commands;

/// <summary>
/// Word command group: read, preview, fill, rebuild, extract, dissect, stats,
/// fonts, styles, validate, merge.
/// </summary>
public static class WordCommands
{
    public static Command Create(Option<bool> jsonOpt)
    {
        var cmd = new Command("word", "Word document operations");

        // === Phase 2: read + preview ===
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
        cmd.AddCommand(CreateComments(jsonOpt));
        cmd.AddCommand(CreateRevisions(jsonOpt));
        cmd.AddCommand(CreateInferFormat(jsonOpt));

        // === Stage 15: modify commands ===
        cmd.AddCommand(CreateFixOrder(jsonOpt));
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

        return cmd;
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
                            Meta = new MetaInfo { Version = "3.1.0", DurationMs = elapsed }
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
                    var analyzer = new Nong.Cli.Adapters.WordImageAnalyzerAdapter();
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
        var outOpt = new Option<string>("-o", "Output directory for extracted images");
        var cmd = new Command("images", "List and optionally extract images") { fileArg, outOpt };
        cmd.SetHandler((string file, string? output, bool json) =>
        {
            var err = CliHelpers.ValidateDocxFile(file);
            if (err != null) { CliHelpers.WriteError("word images", err, json); return; }
            try
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
                    Console.WriteLine(JsonSerializer.Serialize(outputJson, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine(result.Summary);
                    foreach (var img in result.Images)
                        Console.WriteLine($"  {img.Id} {img.ContentType} {img.Width}x{img.Height} usedBy={string.Join(",", img.UsedBy ?? new())}");
                }
            }
            catch (Exception ex) { CliHelpers.WriteError("word images", ErrorCodes.InternalError with { Message = ex.Message }, json); }
        }, fileArg, outOpt, jsonOpt);
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
                        var errOut = new JsonOutput { Status = "error", Command = "word infer-format", Summary = "Could not parse format", Meta = new MetaInfo { Version = "3.1.0" } };
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
        var cmd = new Command("fix-order", "Fix OOXML element ordering") { fileArg, outOpt };
        cmd.SetHandler((string file, string output, bool json) =>
        {
            var err = CliHelpers.ValidateDocxFile(file);
            if (err != null) { CliHelpers.WriteError("word fix-order", err, json); return; }
            if (string.Equals(Path.GetFullPath(file), Path.GetFullPath(output), StringComparison.OrdinalIgnoreCase))
            { CliHelpers.WriteError("word fix-order", ErrorCodes.ValidationFailed with { Message = "Input and output paths must be different." }, json); return; }
            try { CliHelpers.EnsureParentDir(output); var (r, e) = CliHelpers.Time(() => WordEditOperations.FixOrder(file, output));
                var a = CliHelpers.CheckArtifact(output, "DOCX"); if (a != null) { CliHelpers.WriteError("word fix-order", a, json); return; }
                var o = JsonOutput.Ok("word fix-order", $"Fixed {r.FixedElements} elements", r); o.Artifacts["docx"] = Path.GetFullPath(output); o.Meta.DurationMs = e;
                Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts)); }
            catch (Exception ex) { CliHelpers.WriteError("word fix-order", ErrorCodes.InternalError with { Message = ex.Message }, json); }
        }, fileArg, outOpt, jsonOpt);
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
