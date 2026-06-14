using System.CommandLine;
using System.Text.Json;
using Nong.Cli.Common;
using Nong.Inspect;

namespace Nong.Cli.Commands;

/// <summary>
/// Inspect command group: paper diagnostics, references, structure, evidence, and writing.
/// </summary>
public static class InspectCommands
{
    public static Command Create(Option<bool> jsonOpt)
    {
        var cmd = new Command("inspect", "Content inspection and document writing");

        cmd.AddCommand(CreateDiagnose(jsonOpt));
        cmd.AddCommand(CreateRefsCheck(jsonOpt));
        cmd.AddCommand(CreateWritePaper(jsonOpt));
        cmd.AddCommand(CreateWriteOfficial(jsonOpt));
        cmd.AddCommand(CreateOfficialCheck(jsonOpt));
        cmd.AddCommand(CreateClassify(jsonOpt));
        cmd.AddCommand(CreateStructure(jsonOpt));
        cmd.AddCommand(CreateVarplan(jsonOpt));
        cmd.AddCommand(CreateEvidence(jsonOpt));
        cmd.AddCommand(CreateDataReq(jsonOpt));
        cmd.AddCommand(CreateGap(jsonOpt));
        cmd.AddCommand(CreateSemantics(jsonOpt));

        return cmd;
    }

    // ===== inspect diagnose =====

    static Command CreateDiagnose(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to paper text file (.txt)");
        var cmd = new Command("diagnose", "Full paper quality diagnosis pipeline") { fileArg };

        cmd.SetHandler((string file, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(file);
            if (err != null)
            {
                CliHelpers.WriteError("inspect diagnose", err, json);
                return;
            }

            var (result, elapsed) = CliHelpers.Time(() => RunDiagnosis(file));

            if (json)
            {
                var data = new
                {
                    paperType = result.PaperType,
                    typeMatch = result.TypeMatchPercent,
                    recommendedData = result.RecommendedData,
                    recommendedMethods = result.RecommendedMethods,
                    structure = new
                    {
                        title = result.Structure?.Title ?? "",
                        keywords = result.Structure?.Keywords ?? new List<string>(),
                        sectionCount = result.Structure?.Sections?.Count ?? 0,
                        hasReferences = result.Structure?.ReferenceStartLine != null,
                        hasAppendix = result.Structure?.AppendixStartLine != null
                    },
                    evidence = result.Evidence.Select(e => new
                    {
                        item = e.诊断项目, adequate = e.是否充分 == "是",
                        issue = e.主要问题, suggestion = e.修改建议, priority = e.优先级
                    }),
                    dataRequirements = result.DataReqs.Select(d => new
                    {
                        item = d.项目, adequate = d.是否充分 == "是",
                        gap = d.缺口说明, requirement = d.最低补充要求
                    }),
                    gapGrade = result.GapGrade.等级,
                    gapDescription = result.GapGrade.判断标准,
                    canContinue = result.GapGrade.是否可继续分析,
                    quality = result.QualityIssues.Select(q => new
                    {
                        category = q.类别, issue = q.具体问题,
                        fixRequirement = q.最低修改要求, priority = q.优先级
                    }),
                    references = new
                    {
                        count = result.ReferenceCount,
                        riskCount = result.ReferenceRisks.Count,
                        risks = result.ReferenceRisks.Select(r => new
                        {
                            problem = r.文献问题, description = r.风险说明, fix = r.修改建议
                        })
                    }
                };

                var metrics = new Dictionary<string, object>
                {
                    ["textLength"] = result.TextLength,
                    ["paperTypeMatch"] = result.TypeMatchPercent,
                    ["evidenceAdequate"] = result.Evidence.Count(e => e.是否充分 == "是"),
                    ["evidenceTotal"] = result.Evidence.Count,
                    ["dataReqsAdequate"] = result.DataReqs.Count(d => d.是否充分 == "是"),
                    ["dataReqsTotal"] = result.DataReqs.Count,
                    ["gapLevel"] = result.GapGrade.等级,
                    ["referenceCount"] = result.ReferenceCount,
                    ["referenceRiskCount"] = result.ReferenceRisks.Count,
                    ["qualityIssueCount"] = result.QualityIssues.Count
                };

                var output = JsonOutput.Ok("inspect diagnose",
                    $"Type: {result.PaperType} ({result.TypeMatchPercent}%), Gap: {result.GapGrade.等级}, Evidences: {result.Evidence.Count(e => e.是否充分 == "是")}/{result.Evidence.Count}",
                    data);
                foreach (var kv in metrics) output.Metrics[kv.Key] = kv.Value;
                output.Meta.DurationMs = elapsed;
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            else
            {
                Console.WriteLine($"=== Paper Diagnosis ===");
                Console.WriteLine($"Type: {result.PaperType} (match: {result.TypeMatchPercent}%)");
                Console.WriteLine($"Recommended data: {result.RecommendedData}");
                Console.WriteLine($"Recommended methods: {result.RecommendedMethods}");
                Console.WriteLine();

                Console.WriteLine($"--- Evidence Chain ({result.Evidence.Count(e => e.是否充分 == "是")}/{result.Evidence.Count} adequate) ---");
                foreach (var e in result.Evidence)
                {
                    var status = e.是否充分 == "是" ? "[OK]" : "[!!]";
                    Console.WriteLine($"{status} {e.诊断项目}: {e.修改建议}");
                }
                Console.WriteLine();

                Console.WriteLine($"--- Data Requirements ({result.DataReqs.Count(d => d.是否充分 == "是")}/{result.DataReqs.Count} adequate) ---");
                foreach (var d in result.DataReqs)
                {
                    var status = d.是否充分 == "是" ? "[OK]" : "[!!]";
                    Console.WriteLine($"{status} {d.项目}: {d.最低补充要求}");
                }
                Console.WriteLine();

                Console.WriteLine($"--- Gap Grade: {result.GapGrade.等级} ---");
                Console.WriteLine(result.GapGrade.判断标准);
                Console.WriteLine($"Can continue: {result.GapGrade.是否可继续分析}");
                Console.WriteLine();

                if (result.QualityIssues.Count > 0)
                {
                    Console.WriteLine($"--- Quality Issues ({result.QualityIssues.Count}) ---");
                    foreach (var q in result.QualityIssues)
                        Console.WriteLine($"[{q.类别}] {q.具体问题} → {q.最低修改要求}");
                    Console.WriteLine();
                }

                if (result.ReferenceRisks.Count > 0)
                {
                    Console.WriteLine($"--- Reference Risks ({result.ReferenceRisks.Count}) ---");
                    foreach (var r in result.ReferenceRisks)
                        Console.WriteLine($"  [{r.文献问题}] {r.修改建议}");
                }
            }


        }, fileArg, jsonOpt);

        return cmd;
    }

    // ===== refs check =====

    static Command CreateRefsCheck(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to paper text file (.txt)");
        var cmd = new Command("refs", "Reference analysis and risk check") { fileArg };
        cmd.AddAlias("references");

        cmd.SetHandler((string file, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(file);
            if (err != null)
            {
                CliHelpers.WriteError("inspect refs", err, json);
                return;
            }

            var text = File.ReadAllText(file);
            var (result, elapsed) = CliHelpers.Time(() =>
            {
                var refs = ReferenceAnalyzer.ExtractReferences(text);
                var risks = ReferenceAnalyzer.CheckReferenceRisks(text, refs);
                var strategy = ReferenceAnalyzer.BuildLiteratureSearchStrategy(
                    PaperStructureExtractor.BuildPaperStructure(text).Keywords,
                    PaperTypeClassifier.TopType(text));
                return (refs, risks, strategy);
            });

            if (json)
            {
                var data = new
                {
                    count = result.refs.Count,
                    entries = result.refs.Select(r => new
                    {
                        number = r.序号, raw = r.原文,
                        year = r.年份, formatRisk = r.格式风险
                    }),
                    risks = result.risks.Select(r => new
                    {
                        problem = r.文献问题, location = r.位置,
                        detail = r.风险说明, fix = r.修改建议
                    }),
                    searchStrategy = result.strategy
                };

                var output = JsonOutput.Ok("inspect refs",
                    $"{result.refs.Count} references, {result.risks.Count} risks",
                    data);
                output.Metrics["referenceCount"] = result.refs.Count;
                output.Metrics["riskCount"] = result.risks.Count;
                output.Meta.DurationMs = elapsed;
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            else
            {
                Console.WriteLine($"=== References ({result.refs.Count}) ===");
                foreach (var r in result.refs)
                {
                    var risk = string.IsNullOrEmpty(r.格式风险) ? "" : $" [RISK: {r.格式风险}]";
                    Console.WriteLine($"[{r.序号}] {r.原文}{risk}");
                }

                if (result.risks.Count > 0)
                {
                    Console.WriteLine($"\n=== Risks ({result.risks.Count}) ===");
                    foreach (var r in result.risks)
                        Console.WriteLine($"  [{r.文献问题}] {r.修改建议}");
                }

                Console.WriteLine($"\n=== Search Strategy ===");
                Console.WriteLine(result.strategy);
            }


        }, fileArg, jsonOpt);

        return cmd;
    }

    // ===== helpers =====

    record DiagnosisResult(
        string PaperType, int TypeMatchPercent, string RecommendedData, string RecommendedMethods,
        PaperStructure? Structure,
        List<EvidenceChainItem> Evidence, List<DataRequirementItem> DataReqs,
        GapGrade GapGrade, List<QualityIssue> QualityIssues,
        int ReferenceCount, List<ReferenceRisk> ReferenceRisks,
        int TextLength
    );

    static DiagnosisResult RunDiagnosis(string filePath)
    {
        var text = File.ReadAllText(filePath);
        var types = PaperTypeClassifier.Classify(text);
        var topType = types[0];
        var structure = PaperStructureExtractor.BuildPaperStructure(text);
        var evidence = PaperDiagnostics.DiagnoseEvidenceChain(text, structure.Sections);
        var dataReqs = PaperDiagnostics.DiagnoseDataRequirements(text);
        var gap = PaperDiagnostics.DiagnoseGapGrade(evidence, dataReqs, types);
        var quality = PaperDiagnostics.DiagnosePaperQuality(text, evidence, dataReqs, gap, structure.Sections);
        var refs = ReferenceAnalyzer.ExtractReferences(text);
        var refRisks = ReferenceAnalyzer.CheckReferenceRisks(text, refs);

        return new DiagnosisResult(
            topType.论文类型, topType.当前匹配度,
            topType.推荐数据, topType.推荐方法,
            structure,
            evidence, dataReqs,
            gap, quality.问题表,
            refs.Count, refRisks,
            text.Length
        );
    }

    // ===== inspect write paper (phase 6) =====

    static Command CreateWritePaper(Option<bool> jsonOpt)
    {
        var specArg = new Argument<string>("spec", "Path to paper spec JSON");
        var outOpt = new Option<string>("-o", "Output docx path") { IsRequired = true };
        var cmd = new Command("write-paper", "Generate paper docx from JSON spec") { specArg, outOpt };

        cmd.SetHandler((string spec, string output, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(spec);
            if (err != null) { CliHelpers.WriteError("inspect write-paper", err, json); return; }

            try
            {
                var specJson = File.ReadAllText(spec);
                var verr = ValidatePaperSpec(specJson);
                if (verr != null) { CliHelpers.WriteError("inspect write-paper", verr, json); return; }

                CliHelpers.EnsureParentDir(output);
                var elapsed = CliHelpers.Time(() =>
                {
                    using var docEl = JsonDocument.Parse(specJson);
                    var root = docEl.RootElement;

                    using var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(output,
                        DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
                    var main = doc.AddMainDocumentPart();
                    main.Document = new DocumentFormat.OpenXml.Wordprocessing.Document(
                        new DocumentFormat.OpenXml.Wordprocessing.Body());
                    var body = main.Document.Body!;

                    var sp = main.AddNewPart<DocumentFormat.OpenXml.Packaging.StyleDefinitionsPart>();
                    sp.Styles = new DocumentFormat.OpenXml.Wordprocessing.Styles();
                    Gbt7714Style.BuildAll(sp.Styles);

                    var np = main.AddNewPart<DocumentFormat.OpenXml.Packaging.NumberingDefinitionsPart>();
                    np.Numbering = new DocumentFormat.OpenXml.Wordprocessing.Numbering();
                    Gbt7714Style.BuildNumbering(np.Numbering);

                    var w = new PaperWriter(body, doc);
                    if (root.TryGetProperty("title", out var t)) w.Title(t.GetString()!);
                    if (root.TryGetProperty("abstract", out var a)) { w.AbstractTitle(); w.Abstract(a.GetString()!); }
                    if (root.TryGetProperty("keywords", out var kw))
                    {
                        if (kw.ValueKind == JsonValueKind.Array)
                            w.Keywords(string.Join("; ", kw.EnumerateArray().Select(e => e.GetString() ?? "")));
                        else
                            w.Keywords(kw.GetString()!);
                    }

                    if (root.TryGetProperty("sections", out var secs))
                    {
                        foreach (var sec in secs.EnumerateArray())
                        {
                            var heading = sec.GetProperty("heading").GetString()!;
                            var level = sec.TryGetProperty("level", out var l) ? l.GetInt32() : 1;
                            w.Heading(heading, level);
                            if (sec.TryGetProperty("body", out var bodyArr) || sec.TryGetProperty("content", out bodyArr))
                            {
                                foreach (var bp in bodyArr.EnumerateArray())
                                    w.Body(bp.GetString()!);
                            }
                        }
                    }

                    if (root.TryGetProperty("references", out var refs))
                    {
                        w.BibHeading();
                        w.References(refs.EnumerateArray().Select(r => r.GetString()!).ToArray());
                    }

                    main.Document.Save();
                });

                if (json)
                {
                    var outputJson = JsonOutput.Ok("inspect write-paper", $"Paper saved: {output}");
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
                try { if (File.Exists(output)) File.Delete(output); } catch { }
                CliHelpers.WriteError("inspect write-paper",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
            }


        }, specArg, outOpt, jsonOpt);

        return cmd;
    }

    static ErrorEntry? ValidatePaperSpec(string json)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException) { return ErrorCodes.ValidationFailed with { Message = "Spec is not valid JSON." }; }
        using (doc)
        {
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            return ErrorCodes.ValidationFailed with { Message = "Spec must be a JSON object." };

        if (root.TryGetProperty("title", out var t) && t.ValueKind != JsonValueKind.String)
            return ErrorCodes.ValidationFailed with { Message = "title must be a string." };
        if (root.TryGetProperty("abstract", out var a) && a.ValueKind != JsonValueKind.String)
            return ErrorCodes.ValidationFailed with { Message = "abstract must be a string." };
        if (root.TryGetProperty("keywords", out var kw))
        {
            if (kw.ValueKind == JsonValueKind.Array)
            {
                // Accept string[] — will be joined later
            }
            else if (kw.ValueKind != JsonValueKind.String)
                return ErrorCodes.ValidationFailed with { Message = "keywords must be a string or array of strings." };
        }

        if (root.TryGetProperty("sections", out var secs))
        {
            if (secs.ValueKind != JsonValueKind.Array)
                return ErrorCodes.ValidationFailed with { Message = "sections must be an array." };
            int i = 0;
            foreach (var sec in secs.EnumerateArray())
            {
                if (!sec.TryGetProperty("heading", out var h) || h.ValueKind != JsonValueKind.String)
                    return ErrorCodes.ValidationFailed with { Message = $"sections[{i}].heading is required and must be a string." };
                if (sec.TryGetProperty("level", out var lv) && lv.ValueKind != JsonValueKind.Number)
                    return ErrorCodes.ValidationFailed with { Message = $"sections[{i}].level must be a number (1-3)." };
                if (sec.TryGetProperty("level", out var lv2) && lv2.GetInt32() is var lvi && (lvi < 1 || lvi > 3))
                    return ErrorCodes.ValidationFailed with { Message = $"sections[{i}].level must be 1-3, got {lvi}." };
                if (sec.TryGetProperty("body", out var bd) || sec.TryGetProperty("content", out bd))
                {
                    if (bd.ValueKind != JsonValueKind.Array)
                        return ErrorCodes.ValidationFailed with { Message = $"sections[{i}].body must be an array." };
                    int j = 0;
                    foreach (var bp in bd.EnumerateArray())
                    {
                        if (bp.ValueKind != JsonValueKind.String)
                            return ErrorCodes.ValidationFailed with { Message = $"sections[{i}].body[{j}] must be a string." };
                        j++;
                    }
                }
                i++;
            }
        }

        if (root.TryGetProperty("references", out var refs))
        {
            if (refs.ValueKind != JsonValueKind.Array)
                return ErrorCodes.ValidationFailed with { Message = "references must be an array." };
            int k = 0;
            foreach (var r in refs.EnumerateArray())
            {
                if (r.ValueKind != JsonValueKind.String)
                    return ErrorCodes.ValidationFailed with { Message = $"references[{k}] must be a string." };
                k++;
            }
        }

        return null;
        }
    }

    // ===== inspect write official =====

    static Command CreateWriteOfficial(Option<bool> jsonOpt)
    {
        var specArg = new Argument<string>("spec", "Path to official-document spec JSON");
        var outOpt = new Option<string>("-o", "Output docx path") { IsRequired = true };
        var cmd = new Command("write-official", "Generate official-document docx from JSON spec") { specArg, outOpt };

        cmd.SetHandler((string spec, string output, bool json) =>
        {
            const string command = "inspect write-official";
            var err = CliHelpers.ValidateTextFile(spec);
            if (err != null) { CliHelpers.WriteError(command, err, json); return; }
            if (!string.Equals(Path.GetExtension(output), ".docx", StringComparison.OrdinalIgnoreCase))
            {
                CliHelpers.WriteError(command,
                    ErrorCodes.ValidationFailed with { Message = "Output path must end with .docx." }, json);
                return;
            }

            try
            {
                var specJson = File.ReadAllText(spec);
                var verr = ValidateOfficialSpec(specJson);
                if (verr != null) { CliHelpers.WriteError(command, verr, json); return; }

                var (data, elapsed) = CliHelpers.Time(() =>
                {
                    using var docEl = JsonDocument.Parse(specJson);
                    var root = docEl.RootElement;
                    var paragraphs = ReadOfficialParagraphs(root).ToList();

                    CliHelpers.EnsureParentDir(output);
                    using var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(output,
                        DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
                    var main = doc.AddMainDocumentPart();
                    main.Document = new DocumentFormat.OpenXml.Wordprocessing.Document(
                        new DocumentFormat.OpenXml.Wordprocessing.Body());
                    var body = main.Document.Body!;

                    var w = new OfficialDocWriter(body, doc);
                    WriteOfficialPart(root, "redHeader", w.RedHeader);
                    WriteOfficialPart(root, "docNumber", w.DocNumber);
                    w.Title(root.GetProperty("title").GetString()!);
                    WriteOfficialPart(root, "recipient", w.Recipient);
                    foreach (var paragraph in paragraphs)
                        w.Body(paragraph);
                    WriteOfficialPart(root, "closing", w.Closing);
                    WriteOfficialPart(root, "signature", w.Signature);
                    WriteOfficialPart(root, "date", w.Date);

                    main.Document.Save();
                    return new
                    {
                        spec = Path.GetFullPath(spec),
                        output = Path.GetFullPath(output),
                        title = root.GetProperty("title").GetString(),
                        bodyParagraphs = paragraphs.Count,
                    };
                });

                var a = CliHelpers.CheckArtifact(output, "DOCX");
                if (a != null) { CliHelpers.WriteError(command, a, json); return; }

                if (json)
                {
                    var outputJson = JsonOutput.Ok(command, $"Official document saved: {output}", data);
                    outputJson.Artifacts["docx"] = Path.GetFullPath(output);
                    outputJson.Metrics["bodyParagraphs"] = data.bodyParagraphs;
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
                try { if (File.Exists(output)) File.Delete(output); } catch { }
                CliHelpers.WriteError(command, ErrorCodes.InternalError with { Message = ex.Message }, json);
            }
        }, specArg, outOpt, jsonOpt);

        return cmd;
    }

    static ErrorEntry? ValidateOfficialSpec(string json)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException) { return ErrorCodes.ValidationFailed with { Message = "Spec is not valid JSON." }; }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return ErrorCodes.ValidationFailed with { Message = "Spec must be a JSON object." };

            if (!root.TryGetProperty("title", out var title) || title.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(title.GetString()))
                return ErrorCodes.ValidationFailed with { Message = "title is required and must be a non-empty string." };

            foreach (var name in new[] { "redHeader", "docNumber", "recipient", "closing", "signature", "date" })
            {
                if (root.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.String)
                    return ErrorCodes.ValidationFailed with { Message = $"{name} must be a string." };
            }

            if (root.TryGetProperty("body", out var body))
            {
                var err = ValidateOfficialParagraphValue(body, "body");
                if (err != null) return err;
            }

            if (root.TryGetProperty("paragraphs", out var paragraphs))
            {
                if (paragraphs.ValueKind != JsonValueKind.Array)
                    return ErrorCodes.ValidationFailed with { Message = "paragraphs must be an array." };
                var err = ValidateOfficialParagraphArray(paragraphs, "paragraphs");
                if (err != null) return err;
            }

            return null;
        }
    }

    static ErrorEntry? ValidateOfficialParagraphValue(JsonElement value, string name)
    {
        if (value.ValueKind == JsonValueKind.String) return null;
        if (value.ValueKind != JsonValueKind.Array)
            return ErrorCodes.ValidationFailed with { Message = $"{name} must be a string or array of strings." };
        return ValidateOfficialParagraphArray(value, name);
    }

    static ErrorEntry? ValidateOfficialParagraphArray(JsonElement array, string name)
    {
        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                return ErrorCodes.ValidationFailed with { Message = $"{name}[{index}] must be a string." };
            index++;
        }
        return null;
    }

    static IEnumerable<string> ReadOfficialParagraphs(JsonElement root)
    {
        if (root.TryGetProperty("body", out var body))
        {
            if (body.ValueKind == JsonValueKind.String)
            {
                var value = body.GetString();
                if (!string.IsNullOrWhiteSpace(value)) yield return value;
            }
            else if (body.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in body.EnumerateArray())
                {
                    var value = item.GetString();
                    if (!string.IsNullOrWhiteSpace(value)) yield return value;
                }
            }
        }

        if (root.TryGetProperty("paragraphs", out var paragraphs))
        {
            foreach (var item in paragraphs.EnumerateArray())
            {
                var value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value)) yield return value;
            }
        }
    }

    static void WriteOfficialPart(JsonElement root, string propertyName, Func<string, OfficialDocWriter> write)
    {
        if (root.TryGetProperty(propertyName, out var value))
        {
            var text = value.GetString();
            if (!string.IsNullOrWhiteSpace(text))
                write(text);
        }
    }

    // ===== official-check =====

    static Command CreateOfficialCheck(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .docx file to audit");
        var cmd = new Command("official-check", "Audit official-document DOCX format compliance") { fileArg };

        cmd.SetHandler((string file, bool json) =>
        {
            var err = CliHelpers.ValidateDocxFile(file);
            if (err != null) { CliHelpers.WriteError("inspect official-check", err, json); return; }

            try
            {
                var (result, elapsed) = CliHelpers.Time(() =>
                {
                    using var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(file, false);
                    var body = doc.MainDocumentPart?.Document?.Body;
                    if (body == null) return new OfficialCheckResult { Status = "error", Issues = { "Document has no body." } };

                    var paras = body.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>().ToList();
                    var result = new OfficialCheckResult { Status = "ok" };
                    var texts = paras.Select(p => p.InnerText.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToList();

                    // Check for gongwen structural elements
                    bool hasRedHeader = false, hasDocNumber = false, hasTitle = false;
                    bool hasRecipient = false, hasBody = false, hasClosing = false;
                    bool hasSignature = false, hasDate = false;

                    foreach (var p in paras)
                    {
                        var t = p.InnerText.Trim();
                        if (string.IsNullOrEmpty(t)) continue;

                        var runs = p.Elements<DocumentFormat.OpenXml.Wordprocessing.Run>();
                        bool isRed = runs.Any(r => r.RunProperties?.Color?.Val?.Value == "FF0000"
                            || r.RunProperties?.Color?.Val?.Value == "C00000");
                        bool isBold = runs.Any(r => r.RunProperties?.Bold?.Val?.Value == true);
                        bool isCenter = p.ParagraphProperties?.Justification?.Val?.Value
                            == DocumentFormat.OpenXml.Wordprocessing.JustificationValues.Center;
                        bool isRight = p.ParagraphProperties?.Justification?.Val?.Value
                            == DocumentFormat.OpenXml.Wordprocessing.JustificationValues.Right;
                        double? fontSize = runs.Select(r =>
                            r.RunProperties?.FontSize?.Val?.Value is string v && double.TryParse(v, out var d) ? d : (double?)null)
                            .FirstOrDefault(f => f.HasValue);

                        if (!hasRedHeader && isRed && isCenter)
                        { hasRedHeader = true; result.RedHeaderText = t; continue; }
                        if (!hasDocNumber && isCenter && fontSize < 18 && t.Length > 4 && t.Contains("〔"))
                        { hasDocNumber = true; result.DocNumber = t; continue; }
                        if (!hasTitle && isBold && isCenter && fontSize >= 16)
                        { hasTitle = true; result.TitleText = t; continue; }
                        if (!hasRecipient && t.EndsWith("：") && t.Length < 40)
                        { hasRecipient = true; result.RecipientText = t; continue; }
                        if (!hasClosing && (t.StartsWith("特此") || t.StartsWith("此致") || t.StartsWith("以上")))
                        { hasClosing = true; result.ClosingText = t; continue; }
                        if (!hasSignature && isRight && (t.Length < 30) && paras.IndexOf(p) > paras.Count * 0.7)
                        { hasSignature = true; result.SignatureText = t; continue; }
                        if (!hasDate && isRight && System.Text.RegularExpressions.Regex.IsMatch(t, @"\d{4}年\d{1,2}月\d{1,2}日"))
                        { hasDate = true; result.DateText = t; continue; }

                        if (hasTitle && !hasBody)
                        { hasBody = true; }
                    }

                    if (!hasRedHeader) result.Issues.Add("缺少红头标题（红色居中文字）");
                    if (!hasDocNumber) result.Issues.Add("缺少发文字号（居中、含〔〕的文字）");
                    if (!hasTitle) result.Issues.Add("缺少公文标题（粗体居中、字号>=16pt）");
                    if (!hasRecipient) result.Issues.Add("缺少主送机关（以：结尾的短文字）");
                    if (!hasBody) result.Issues.Add("缺少正文内容");
                    if (!hasClosing) result.Issues.Add("缺少结束语（特此xx/此致等）");
                    if (!hasSignature) result.Issues.Add("缺少落款（靠右侧的机关署名）");
                    if (!hasDate) result.Issues.Add("缺少成文日期（XXXX年X月X日格式）");

                    result.PassCount = 8 - result.Issues.Count;
                    return result;
                });

                if (json)
                {
                    var output = JsonOutput.Ok("inspect official-check",
                        $"{result.PassCount}/8 gongwen checks passed, {result.Issues.Count} issue(s)",
                        result);
                    output.Metrics["passCount"] = result.PassCount;
                    output.Metrics["issueCount"] = result.Issues.Count;
                    output.Meta.DurationMs = elapsed;
                    foreach (var issue in result.Issues)
                        output.Issues.Add(new Issue { Id = "gongwen_check", Severity = "Warning", Message = issue });
                    Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"=== 公文格式检查 ({result.PassCount}/8) ===");
                    foreach (var issue in result.Issues)
                        Console.WriteLine($"  [!] {issue}");
                    if (result.Issues.Count == 0)
                        Console.WriteLine("  全部通过。");
                }
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("inspect official-check",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
            }
        }, fileArg, jsonOpt);
        return cmd;
    }

    public class OfficialCheckResult
    {
        public string Status { get; set; } = "ok";
        public int PassCount { get; set; }
        public string? RedHeaderText { get; set; }
        public string? DocNumber { get; set; }
        public string? TitleText { get; set; }
        public string? RecipientText { get; set; }
        public string? ClosingText { get; set; }
        public string? SignatureText { get; set; }
        public string? DateText { get; set; }
        public List<string> Issues { get; set; } = new();
    }

    // ===== classify =====

    static Command CreateClassify(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to paper text file (.txt)");
        var cmd = new Command("classify", "Classify paper type (16 types)") { fileArg };

        cmd.SetHandler((string file, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(file);
            if (err != null) { CliHelpers.WriteError("inspect classify", err, json); return; }

            var text = File.ReadAllText(file);
            var (types, elapsed) = CliHelpers.Time(() => PaperTypeClassifier.Classify(text));

            if (json)
            {
                var topType = types.Count > 0 ? types[0] : null;
                var data = new
                {
                    topType = topType?.论文类型 ?? "未知",
                    match = topType?.当前匹配度 ?? 0,
                    candidates = types.Select(t => new
                    {
                        type = t.论文类型,
                        match = t.当前匹配度,
                        recommendedData = t.推荐数据,
                        recommendedMethods = t.推荐方法
                    })
                };
                var output = JsonOutput.Ok("inspect classify",
                    $"Top: {data.topType} ({data.match}%)", data);
                output.Metrics["candidateCount"] = types.Count;
                output.Metrics["topMatch"] = data.match;
                output.Meta.DurationMs = elapsed;
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            else
            {
                Console.WriteLine("=== Paper Type Classification ===");
                foreach (var t in types.Take(5))
                {
                    Console.WriteLine($"[{t.当前匹配度}%] {t.论文类型}");
                    if (!string.IsNullOrEmpty(t.推荐数据))
                        Console.WriteLine($"  Data: {t.推荐数据}");
                    if (!string.IsNullOrEmpty(t.推荐方法))
                        Console.WriteLine($"  Methods: {t.推荐方法}");
                    Console.WriteLine();
                }
            }
        }, fileArg, jsonOpt);

        return cmd;
    }

    // ===== structure =====

    static Command CreateStructure(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to paper text file (.txt)");
        var cmd = new Command("structure", "Extract paper structure") { fileArg };

        cmd.SetHandler((string file, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(file);
            if (err != null) { CliHelpers.WriteError("inspect structure", err, json); return; }

            var text = File.ReadAllText(file);
            var (structure, elapsed) = CliHelpers.Time(() => PaperStructureExtractor.BuildPaperStructure(text));

            if (json)
            {
                var data = new
                {
                    title = structure.Title,
                    keywords = structure.Keywords,
                    sections = structure.Sections.Select(s => new
                    {
                        level = s.Level,
                        heading = s.Title,
                        startLine = s.StartLine,
                        endLine = s.EndLine
                    }),
                    hasReferences = structure.ReferenceStartLine != null,
                    referenceStartLine = structure.ReferenceStartLine,
                    hasAppendix = structure.AppendixStartLine != null
                };
                var metrics = new Dictionary<string, object>
                {
                    ["sectionCount"] = structure.Sections.Count
                };
                var output = JsonOutput.Ok("inspect structure",
                    $"{structure.Sections.Count} sections, title: {(structure.Title.Length > 60 ? structure.Title[..60] : structure.Title)}", data);
                foreach (var kv in metrics) output.Metrics[kv.Key] = kv.Value;
                output.Meta.DurationMs = elapsed;
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            else
            {
                Console.WriteLine("=== Paper Structure ===");
                Console.WriteLine($"Title: {structure.Title}");
                if (structure.Keywords.Count > 0)
                    Console.WriteLine($"Keywords: {string.Join(", ", structure.Keywords)}");
                Console.WriteLine($"Sections ({structure.Sections.Count}):");
                foreach (var s in structure.Sections)
                    Console.WriteLine($"  L{s.Level} {s.Title} (lines {s.StartLine}-{s.EndLine})");
                Console.WriteLine($"Has References: {(structure.ReferenceStartLine != null ? $"yes (line {structure.ReferenceStartLine})" : "no")}");
                Console.WriteLine($"Has Appendix: {(structure.AppendixStartLine != null ? "yes" : "no")}");
            }
        }, fileArg, jsonOpt);

        return cmd;
    }

    // ===== evidence =====

    static Command CreateEvidence(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to paper text file (.txt)");
        var cmd = new Command("evidence", "Evidence chain diagnosis") { fileArg };

        cmd.SetHandler((string file, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(file);
            if (err != null) { CliHelpers.WriteError("inspect evidence", err, json); return; }

            var text = File.ReadAllText(file);
            var (evidence, elapsed) = CliHelpers.Time(() =>
            {
                var structure = PaperStructureExtractor.BuildPaperStructure(text);
                return PaperDiagnostics.DiagnoseEvidenceChain(text, structure.Sections);
            });

            if (json)
            {
                var adequate = evidence.Count(e => e.是否充分 == "是");
                var data = new
                {
                    items = evidence.Select(e => new
                    {
                        item = e.诊断项目,
                        adequate = e.是否充分 == "是",
                        issue = e.主要问题,
                        suggestion = e.修改建议,
                        priority = e.优先级
                    })
                };
                var metrics = new Dictionary<string, object>
                {
                    ["total"] = evidence.Count,
                    ["adequate"] = adequate
                };
                var output = JsonOutput.Ok("inspect evidence",
                    $"{adequate}/{evidence.Count} adequate", data);
                foreach (var kv in metrics) output.Metrics[kv.Key] = kv.Value;
                output.Meta.DurationMs = elapsed;
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            else
            {
                Console.WriteLine($"=== Evidence Chain ({evidence.Count(e => e.是否充分 == "是")}/{evidence.Count} adequate) ===");
                foreach (var e in evidence)
                {
                    var status = e.是否充分 == "是" ? "[OK]" : "[!!]";
                    Console.WriteLine($"{status} {e.诊断项目}");
                    if (!string.IsNullOrEmpty(e.修改建议))
                        Console.WriteLine($"    -> {e.修改建议}");
                }
            }
        }, fileArg, jsonOpt);

        return cmd;
    }

    // ===== data-req =====

    static Command CreateDataReq(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to paper text file (.txt)");
        var cmd = new Command("data-req", "Data requirements diagnosis") { fileArg };
        cmd.AddAlias("data-requirements");

        cmd.SetHandler((string file, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(file);
            if (err != null) { CliHelpers.WriteError("inspect data-req", err, json); return; }

            var text = File.ReadAllText(file);
            var (dataReqs, elapsed) = CliHelpers.Time(() => PaperDiagnostics.DiagnoseDataRequirements(text));

            if (json)
            {
                var adequate = dataReqs.Count(d => d.是否充分 == "是");
                var data = new
                {
                    items = dataReqs.Select(d => new
                    {
                        item = d.项目,
                        adequate = d.是否充分 == "是",
                        gap = d.缺口说明,
                        requirement = d.最低补充要求
                    })
                };
                var metrics = new Dictionary<string, object>
                {
                    ["total"] = dataReqs.Count,
                    ["adequate"] = adequate
                };
                var output = JsonOutput.Ok("inspect data-req",
                    $"{adequate}/{dataReqs.Count} adequate", data);
                foreach (var kv in metrics) output.Metrics[kv.Key] = kv.Value;
                output.Meta.DurationMs = elapsed;
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            else
            {
                Console.WriteLine($"=== Data Requirements ({dataReqs.Count(d => d.是否充分 == "是")}/{dataReqs.Count} adequate) ===");
                foreach (var d in dataReqs)
                {
                    var status = d.是否充分 == "是" ? "[OK]" : "[!!]";
                    Console.WriteLine($"{status} {d.项目}");
                    if (!string.IsNullOrEmpty(d.最低补充要求))
                        Console.WriteLine($"    -> {d.最低补充要求}");
                }
            }
        }, fileArg, jsonOpt);

        return cmd;
    }

    // ===== gap =====

    static Command CreateGap(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to paper text file (.txt)");
        var cmd = new Command("gap", "Gap grade assessment") { fileArg };

        cmd.SetHandler((string file, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(file);
            if (err != null) { CliHelpers.WriteError("inspect gap", err, json); return; }

            var text = File.ReadAllText(file);
            var (gapGrade, elapsed) = CliHelpers.Time(() =>
            {
                var types = PaperTypeClassifier.Classify(text);
                var structure = PaperStructureExtractor.BuildPaperStructure(text);
                var evidence = PaperDiagnostics.DiagnoseEvidenceChain(text, structure.Sections);
                var dataReqs = PaperDiagnostics.DiagnoseDataRequirements(text);
                return PaperDiagnostics.DiagnoseGapGrade(evidence, dataReqs, types);
            });

            if (json)
            {
                var data = new
                {
                    grade = gapGrade.等级,
                    description = gapGrade.判断标准,
                    canContinue = gapGrade.是否可继续分析
                };
                var metrics = new Dictionary<string, object>
                {
                    ["gapCount"] = gapGrade.缺口数量,
                    ["topMatch"] = gapGrade.最高论文类型匹配度
                };
                var output = JsonOutput.Ok("inspect gap",
                    $"Grade: {gapGrade.等级}, {gapGrade.是否可继续分析}", data);
                foreach (var kv in metrics) output.Metrics[kv.Key] = kv.Value;
                output.Meta.DurationMs = elapsed;
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            else
            {
                Console.WriteLine($"=== Gap Grade: {gapGrade.等级} ===");
                Console.WriteLine(gapGrade.判断标准);
                Console.WriteLine($"Can continue: {gapGrade.是否可继续分析}");
                Console.WriteLine($"Gap count: {gapGrade.缺口数量}");
                Console.WriteLine($"Top paper type match: {gapGrade.最高论文类型匹配度}%");
                if (!string.IsNullOrEmpty(gapGrade.修改建议))
                    Console.WriteLine($"Suggestion: {gapGrade.修改建议}");
            }
        }, fileArg, jsonOpt);

        return cmd;
    }

    // ===== varplan =====

    static Command CreateVarplan(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to paper text file (.txt)");
        var cmd = new Command("varplan", "Variable operationalization plan") { fileArg };
        cmd.AddAlias("variables");

        cmd.SetHandler((string file, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(file);
            if (err != null) { CliHelpers.WriteError("inspect varplan", err, json); return; }

            var text = File.ReadAllText(file);
            var (result, elapsed) = CliHelpers.Time(() =>
            {
                var types = PaperTypeClassifier.Classify(text);
                var topType = types.Count > 0 ? types[0] : null;
                var paperType = topType?.论文类型 ?? "问卷调查型论文";
                var vars = VariablePlanGenerator.GenerateVariablePlan(text, paperType);
                return (vars, topType, paperType);
            });

            if (json)
            {
                var variables = result.vars.Select(v => new
                {
                    name = v.变量名称,
                    label = v.中文标签,
                    role = v.变量角色,
                    meaning = v.理论含义,
                    dataType = v.数据类型,
                    required = v.是否必须,
                    usage = v.分析用途
                }).ToList();
                var measurement = result.vars.Select(v => new
                {
                    variable = v.变量名称,
                    method = v.操作化方式,
                    measure = v.测量题项指标,
                    range = v.取值范围
                }).ToList();
                var dataNeeded = result.vars.Select(v => v.数据来源).Distinct().ToList();
                var methods = result.topType?.推荐方法?
                    .Split(new[] { '、', ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(m => m.Trim())
                    .Where(m => m.Length > 0)
                    .ToList() ?? new List<string>();
                var raw = $"Paper type: {result.paperType}. {result.vars.Count} variables identified. " +
                    $"Data sources: {string.Join("; ", dataNeeded)}. " +
                    $"Methods: {string.Join(", ", methods)}.";

                var data = new
                {
                    variables,
                    measurement,
                    dataNeeded,
                    methods,
                    raw
                };
                var output = JsonOutput.Ok("inspect varplan",
                    $"{result.vars.Count} variables for {result.paperType}", data);
                output.Metrics["variableCount"] = result.vars.Count;
                output.Meta.DurationMs = elapsed;
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            else
            {
                Console.WriteLine($"=== Variable Plan ({result.paperType}) ===");
                Console.WriteLine($"Recommended data: {result.topType?.推荐数据 ?? "N/A"}");
                Console.WriteLine($"Recommended methods: {result.topType?.推荐方法 ?? "N/A"}");
                Console.WriteLine();
                Console.WriteLine($"{result.vars.Count} variables:");
                foreach (var v in result.vars)
                {
                    Console.WriteLine($"  [{v.变量角色}] {v.变量名称} ({v.中文标签})");
                    Console.WriteLine($"    Data: {v.数据类型}, Range: {v.取值范围}, Source: {v.数据来源}");
                    Console.WriteLine($"    Measure: {v.测量题项指标}");
                    Console.WriteLine($"    Required: {v.是否必须}, Usage: {v.分析用途}");
                }
            }
        }, fileArg, jsonOpt);

        return cmd;
    }

    // ===== semantics =====

    static Command CreateSemantics(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to paper text file (.txt)");
        var cmd = new Command("semantics", "Semantic diagnosis") { fileArg };

        cmd.SetHandler((string file, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(file);
            if (err != null) { CliHelpers.WriteError("inspect semantics", err, json); return; }

            var text = File.ReadAllText(file);
            var (qualityIssues, elapsed) = CliHelpers.Time(() =>
            {
                var types = PaperTypeClassifier.Classify(text);
                var structure = PaperStructureExtractor.BuildPaperStructure(text);
                var evidence = PaperDiagnostics.DiagnoseEvidenceChain(text, structure.Sections);
                var dataReqs = PaperDiagnostics.DiagnoseDataRequirements(text);
                var gap = PaperDiagnostics.DiagnoseGapGrade(evidence, dataReqs, types);
                var quality = PaperDiagnostics.DiagnosePaperQuality(text, evidence, dataReqs, gap, structure.Sections);
                return quality.问题表;
            });

            if (json)
            {
                var data = new
                {
                    issues = qualityIssues.Select(q => new
                    {
                        category = q.类别,
                        issue = q.具体问题,
                        fixRequirement = q.最低修改要求,
                        priority = q.优先级
                    })
                };
                var metrics = new Dictionary<string, object>
                {
                    ["issueCount"] = qualityIssues.Count
                };
                var output = JsonOutput.Ok("inspect semantics",
                    $"{qualityIssues.Count} quality issues", data);
                foreach (var kv in metrics) output.Metrics[kv.Key] = kv.Value;
                output.Meta.DurationMs = elapsed;
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            else
            {
                Console.WriteLine($"=== Quality Issues ({qualityIssues.Count}) ===");
                if (qualityIssues.Count == 0)
                {
                    Console.WriteLine("No quality issues detected.");
                }
                else
                {
                    foreach (var q in qualityIssues)
                    {
                        Console.WriteLine($"[{q.类别}] {q.具体问题}");
                        Console.WriteLine($"  Fix: {q.最低修改要求}");
                        Console.WriteLine($"  Priority: {q.优先级}");
                        Console.WriteLine();
                    }
                }
            }
        }, fileArg, jsonOpt);

        return cmd;
    }
}
