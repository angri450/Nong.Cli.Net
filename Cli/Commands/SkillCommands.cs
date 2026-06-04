using System.CommandLine;
using System.Text.Json;
using Nong.Cli.Common;
using SkillManager.Cli.Models;
using SkillManager.Cli.Tools;

namespace Nong.Cli.Commands;

public static class SkillCommands
{
    public static Command Create(Option<bool> jsonOpt)
    {
        var cmd = new Command("skill", "Skill lifecycle management (validate, scan, inventory, package)");

        cmd.AddCommand(CreateValidate(jsonOpt));
        cmd.AddCommand(CreateScan(jsonOpt));
        cmd.AddCommand(CreateInventory(jsonOpt));
        cmd.AddCommand(CreatePackage(jsonOpt));

        return cmd;
    }

    // ===== shared helpers =====

    static void GuardEmptyPath(string dir, string command, bool json)
    {
        if (string.IsNullOrWhiteSpace(dir))
        {
            CliHelpers.WriteError(command,
                ErrorCodes.MissingArgument with { Message = "Directory path is required." }, json);
        }
    }

    static bool TryResolveDir(string dir, string command, bool json, out string fullDir)
    {
        fullDir = "";
        if (string.IsNullOrWhiteSpace(dir))
        {
            CliHelpers.WriteError(command,
                ErrorCodes.MissingArgument with { Message = "Directory path is required." }, json);
            return false;
        }
        fullDir = Path.GetFullPath(dir);
        if (!Directory.Exists(fullDir))
        {
            CliHelpers.WriteError(command,
                ErrorCodes.FileNotFound with { Message = $"Directory not found: {fullDir}" }, json);
            return false;
        }
        return true;
    }

    // ===== skill validate =====

    static Command CreateValidate(Option<bool> jsonOpt)
    {
        var dirArg = new Argument<string>("dir", "Path to skill directory containing SKILL.md");
        var cmd = new Command("validate", "Validate SKILL.md structure and references") { dirArg };

        cmd.SetHandler((string dir, bool json) =>
        {
            if (!TryResolveDir(dir, "skill validate", json, out var fullDir)) return;

            try
            {
                var (result, elapsed) = CliHelpers.Time(() =>
                {
                    var validator = new SkillValidator(fullDir);
                    return validator.Validate();
                });

                var skillName = result.SkillName ?? Path.GetFileName(fullDir);
                var errorIssues = result.Issues.Where(i => i.Level == "Error").ToList();

                if (json)
                {
                    var data = new
                    {
                        valid = result.IsValid,
                        skill = skillName,
                        lines = result.SkillMdLineCount,
                        errors = errorIssues.Select(i => new { file = i.File, message = i.Message }),
                        warnings = result.Issues.Where(i => i.Level != "Error").Select(i => new
                        {
                            level = i.Level, file = i.File, message = i.Message
                        })
                    };

                    var output = new JsonOutput
                    {
                        Status = result.IsValid ? "ok" : "error",
                        Command = "skill validate",
                        Summary = result.IsValid
                            ? $"Skill valid: {skillName}"
                            : $"Validation failed: {errorIssues.Count} errors",
                        Data = data,
                        Meta = new MetaInfo { Version = "3.1.0" }
                    };

                    if (!result.IsValid)
                    {
                        Environment.ExitCode = 1;
                        output.Errors = errorIssues.Select(i => new ErrorEntry(
                            ErrorCodes.ValidationFailed.Code,
                            ErrorCodes.ValidationFailed.Name,
                            $"{i.File}: {i.Message}"
                        )).ToList();
                    }

                    output.Issues = result.Issues.Where(i => i.Level != "Error").Select(i =>
                        new Issue { Id = i.Level, Severity = i.Level, Message = $"{i.File}: {i.Message}" }
                    ).ToList();

                    output.Metrics["issueCount"] = result.Issues.Count;
                    output.Metrics["errorCount"] = errorIssues.Count;
                    output.Metrics["warningCount"] = result.Issues.Count(i => i.Level == "Warning");
                    output.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"Skill: {skillName}");
                    Console.WriteLine($"SKILL.md: {result.SkillMdLineCount} lines");
                    Console.WriteLine($"Valid: {(result.IsValid ? "YES" : "NO")}");
                    Console.WriteLine();
                    foreach (var issue in result.Issues.OrderByDescending(i => i.Level))
                    {
                        var prefix = issue.Level switch
                        {
                            "Error" => "[ERR] ",
                            "Warning" => "[WARN]",
                            "Info" => "[INFO]",
                            _ => "[??]  "
                        };
                        Console.WriteLine($"{prefix} {issue.File}: {issue.Message}");
                    }
                    if (result.BrokenReferences.Any())
                    {
                        Console.WriteLine($"\nBroken references ({result.BrokenReferences.Distinct().Count()}):");
                        foreach (var br in result.BrokenReferences.Distinct())
                            Console.WriteLine($"  - {br}");
                    }

                    if (!result.IsValid) Environment.ExitCode = 1;
                }
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("skill validate",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
            }

        }, dirArg, jsonOpt);

        return cmd;
    }

    // ===== skill scan =====

    static Command CreateScan(Option<bool> jsonOpt)
    {
        var dirArg = new Argument<string>("dir", "Path to skill or plugin directory");
        var cmd = new Command("scan", "Security scan for skill directories") { dirArg };

        cmd.SetHandler((string dir, bool json) =>
        {
            if (!TryResolveDir(dir, "skill scan", json, out var fullDir)) return;

            try
            {
                var (findings, elapsed) = CliHelpers.Time(() =>
                {
                    var scanner = new SecurityScanner(fullDir);
                    return scanner.Scan();
                });

                var critical = findings.Count(f => f.Severity == Severity.Critical);
                var high = findings.Count(f => f.Severity == Severity.High);
                var medium = findings.Count(f => f.Severity == Severity.Medium);
                var low = findings.Count(f => f.Severity == Severity.Low);
                var info = findings.Count(f => f.Severity == Severity.Info);
                var hasHighOrAbove = critical + high > 0;

                if (json)
                {
                    var data = new
                    {
                        critical, high, medium, low, info,
                        findings = findings.Select(f => new
                        {
                            severity = f.Severity.ToString(),
                            rule = f.Rule,
                            file = f.File,
                            line = f.Line,
                            detail = f.Detail
                        })
                    };

                    var output = new JsonOutput
                    {
                        Status = hasHighOrAbove ? "error" : "ok",
                        Command = "skill scan",
                        Summary = hasHighOrAbove
                            ? $"{critical + high} High+ findings"
                            : $"{findings.Count} findings, 0 High+",
                        Data = data,
                        Meta = new MetaInfo { Version = "3.1.0" }
                    };

                    if (hasHighOrAbove)
                    {
                        Environment.ExitCode = 1;
                        output.Errors = findings
                            .Where(f => f.Severity == Severity.Critical || f.Severity == Severity.High)
                            .Select(f => new ErrorEntry(
                                ErrorCodes.ValidationFailed.Code,
                                ErrorCodes.ValidationFailed.Name,
                                $"[{f.Severity}] {f.Rule}: {f.File}:{f.Line} — {f.Detail}"
                            )).ToList();
                    }

                    output.Issues = findings
                        .Where(f => f.Severity == Severity.Medium || f.Severity == Severity.Low || f.Severity == Severity.Info)
                        .Select(f => new Issue
                        {
                            Id = f.Rule,
                            Severity = f.Severity.ToString(),
                            Message = $"{f.File}:{f.Line} — {f.Detail}"
                        }).ToList();

                    output.Metrics["totalFindings"] = findings.Count;
                    output.Metrics["critical"] = critical;
                    output.Metrics["high"] = high;
                    output.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"Findings: {findings.Count}");
                    Console.WriteLine($"  Critical: {critical}");
                    Console.WriteLine($"  High: {high}");
                    Console.WriteLine($"  Medium: {medium}");
                    Console.WriteLine($"  Low: {low}");
                    Console.WriteLine($"  Info: {info}");
                    Console.WriteLine();

                    foreach (var finding in findings)
                    {
                        var prefix = finding.Severity switch
                        {
                            Severity.Critical => "[CRITICAL]",
                            Severity.High => "[HIGH]    ",
                            Severity.Medium => "[MEDIUM]  ",
                            Severity.Low => "[LOW]     ",
                            _ => "[INFO]    "
                        };
                        Console.WriteLine($"{prefix} {finding.Rule} | {finding.File}:{finding.Line}");
                        Console.WriteLine($"           {finding.Detail}");
                        Console.WriteLine();
                    }

                    if (hasHighOrAbove) Environment.ExitCode = 1;
                }
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("skill scan",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
            }

        }, dirArg, jsonOpt);

        return cmd;
    }

    // ===== skill inventory =====

    static Command CreateInventory(Option<bool> jsonOpt)
    {
        var dirArg = new Argument<string>("dir", "Path to skill or plugin root directory");
        var cmd = new Command("inventory", "List skill directory contents") { dirArg };

        cmd.SetHandler((string dir, bool json) =>
        {
            if (!TryResolveDir(dir, "skill inventory", json, out var fullDir)) return;

            try
            {
                var rootInfo = SkillRootClassifier.Classify(fullDir);
                var (result, elapsed) = CliHelpers.Time(() => RunInventory(fullDir, rootInfo));

                if (json)
                {
                    var data = new
                    {
                        root = fullDir,
                        rootType = rootInfo.Kind.ToString().ToLowerInvariant(),
                        skills = result.skills,
                        skillCount = result.skillCount,
                        totalFiles = result.totalFiles,
                        hasPluginManifest = rootInfo.HasPluginManifest,
                        hasMarketplaceManifest = rootInfo.HasMarketplaceManifest,
                        hasSkillsManifest = rootInfo.HasSkillsManifest
                    };

                    var summary = result.skillCount switch
                    {
                        0 => "0 skills found",
                        1 => $"Skill: {result.skills[0].name}",
                        _ => $"{result.skillCount} skills found"
                    };

                    var output = JsonOutput.Ok("skill inventory", summary, data);
                    output.Metrics["skillCount"] = result.skillCount;
                    output.Metrics["totalFiles"] = result.totalFiles;
                    output.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"Root type: {rootInfo.Kind}");
                    Console.WriteLine($"Plugin manifest: {rootInfo.HasPluginManifest}");
                    Console.WriteLine($"Marketplace manifest: {rootInfo.HasMarketplaceManifest}");
                    Console.WriteLine($"Skills manifest: {rootInfo.HasSkillsManifest}");
                    Console.WriteLine();

                    if (result.skillCount == 1)
                    {
                        var skill = result.skills[0];
                        Console.WriteLine($"Skill: {skill.name}");
                        Console.WriteLine($"Path: {skill.path}");
                        Console.WriteLine($"SKILL.md: {(skill.hasSkillMd ? $"{skill.skillMdLines} lines ({skill.skillMdSizeBytes} bytes)" : "MISSING")}");
                        Console.WriteLine($"References: {skill.references} files");
                        Console.WriteLine($"Scripts: {skill.scripts} files");
                        Console.WriteLine($"Agents: {skill.agents} files");
                        Console.WriteLine($"Workflows: {skill.workflows} files");
                        Console.WriteLine($"Evals: {(skill.hasEvals ? $"{skill.evalsFiles} files" : "None")}");
                        Console.WriteLine($".NET Tools: {skill.dotNetTools}");
                        Console.WriteLine($"Tests: {skill.testFiles}");
                        Console.WriteLine($"Total: {skill.totalFiles} files");
                    }
                    else if (result.skillCount > 1)
                    {
                        Console.WriteLine($"Skills found: {result.skillCount}");
                        Console.WriteLine($"Total files: {result.totalFiles}");
                        foreach (var skill in result.skills)
                            Console.WriteLine($"  {skill.name} — SKILL.md {(skill.hasSkillMd ? "OK" : "MISSING")}");
                    }
                    else
                    {
                        Console.WriteLine("No skills found.");
                    }
                }
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("skill inventory",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
            }

        }, dirArg, jsonOpt);

        return cmd;
    }

    static (List<SkillInventoryEntry> skills, int skillCount, int totalFiles) RunInventory(string fullDir, SkillRootInfo rootInfo)
    {
        if (rootInfo.Kind == SkillRootKind.Skill)
        {
            var runner = new InventoryRunner(fullDir);
            var r = runner.Run();
            var entry = ToEntry(r);
            return (new() { entry }, 1, r.TotalFileCount);
        }

        if (rootInfo.Kind == SkillRootKind.Plugin)
        {
            var skills = new List<SkillInventoryEntry>();
            foreach (var skillDir in rootInfo.SkillDirectories)
            {
                var runner = new InventoryRunner(skillDir);
                var r = runner.Run();
                skills.Add(ToEntry(r));
            }
            var totalFiles = skills.Sum(s => s.totalFiles);
            return (skills, skills.Count, totalFiles);
        }

        return (new(), 0, 0);
    }

    static SkillInventoryEntry ToEntry(InventoryResult r) => new()
    {
        name = r.SkillName ?? Path.GetFileName(r.SkillPath),
        path = r.SkillPath,
        hasSkillMd = r.HasSkillMd,
        skillMdLines = r.SkillMdLineCount,
        skillMdSizeBytes = r.SkillMdSizeBytes,
        hasEvals = r.HasEvals,
        evalsFiles = r.EvalsFiles.Count,
        references = r.References.Count,
        scripts = r.Scripts.Count,
        agents = r.Agents.Count,
        workflows = r.Workflows.Count,
        dotNetTools = r.DotNetTools.Count,
        testFiles = r.TestFiles.Count,
        totalFiles = r.TotalFileCount
    };

    sealed record SkillInventoryEntry
    {
        public string name { get; set; } = "";
        public string path { get; set; } = "";
        public bool hasSkillMd { get; set; }
        public int skillMdLines { get; set; }
        public long skillMdSizeBytes { get; set; }
        public bool hasEvals { get; set; }
        public int evalsFiles { get; set; }
        public int references { get; set; }
        public int scripts { get; set; }
        public int agents { get; set; }
        public int workflows { get; set; }
        public int dotNetTools { get; set; }
        public int testFiles { get; set; }
        public int totalFiles { get; set; }
    }

    // ===== skill package =====

    static Command CreatePackage(Option<bool> jsonOpt)
    {
        var dirArg = new Argument<string>("dir", "Path to skill or plugin root directory");
        var cmd = new Command("package", "Validate + scan + package skill or plugin into .zip") { dirArg };

        cmd.SetHandler((string dir, bool json) =>
        {
            if (!TryResolveDir(dir, "skill package", json, out var fullDir)) return;

            try
            {
                var rootInfo = SkillRootClassifier.Classify(fullDir);

                if (rootInfo.Kind == SkillRootKind.Directory)
                {
                    if (json)
                    {
                        var errOut = new JsonOutput
                        {
                            Status = "error",
                            Command = "skill package",
                            Summary = "Directory is neither a skill nor a plugin root",
                            Meta = new MetaInfo { Version = "3.1.0" }
                        };
                        errOut.Errors.Add(new ErrorEntry(
                            ErrorCodes.ValidationFailed.Code,
                            ErrorCodes.ValidationFailed.Name,
                            "Directory contains no SKILL.md and no plugin/skills manifest."
                        ));
                        Console.WriteLine(JsonSerializer.Serialize(errOut, CliHelpers.JsonOpts));
                    }
                    else
                    {
                        Console.Error.WriteLine("[FAIL] Directory is neither a skill nor a plugin root.");
                    }
                    Environment.ExitCode = 1;
                    return;
                }

                if (rootInfo.Kind == SkillRootKind.Skill)
                {
                    // Step 1: Validate single skill
                    var validator = new SkillValidator(fullDir);
                    var valResult = validator.Validate();
                    if (!valResult.IsValid)
                    {
                        EmitValidationError("skill package", valResult, json);
                        return;
                    }

                    // Step 2: Security Scan
                    var scanner = new SecurityScanner(fullDir);
                    var findings = scanner.Scan();
                    var highPlus = findings.Where(f => f.Severity == Severity.Critical || f.Severity == Severity.High).ToList();
                    if (highPlus.Any())
                    {
                        EmitScanBlockError("skill package", highPlus, json);
                        return;
                    }

                    // Step 3: Package
                    var (outputPath, elapsed) = CliHelpers.Time(() =>
                    {
                        var packager = new Packager(fullDir);
                        return packager.PackageAsync().GetAwaiter().GetResult();
                    });

                    EmitPackageSuccess("skill package", outputPath, elapsed, SkillRootKind.Skill);
                }
                else // Plugin
                {
                    // Step 1: Validate all child skills
                    var allErrors = new List<string>();
                    foreach (var skillDir in rootInfo.SkillDirectories)
                    {
                        var validator = new SkillValidator(skillDir);
                        var vr = validator.Validate();
                        if (!vr.IsValid)
                        {
                            var skillName = vr.SkillName ?? Path.GetFileName(skillDir);
                            allErrors.Add($"{skillName}: {vr.Issues.Count(i => i.Level == "Error")} validation errors");
                        }
                    }
                    if (allErrors.Any())
                    {
                        if (json)
                        {
                            var errOut = new JsonOutput
                            {
                                Status = "error",
                                Command = "skill package",
                                Summary = $"Plugin validation failed: {allErrors.Count} skill(s)",
                                Meta = new MetaInfo { Version = "3.1.0" }
                            };
                            foreach (var e in allErrors)
                                errOut.Errors.Add(new ErrorEntry(
                                    ErrorCodes.ValidationFailed.Code,
                                    ErrorCodes.ValidationFailed.Name, e));
                            Console.WriteLine(JsonSerializer.Serialize(errOut, CliHelpers.JsonOpts));
                        }
                        else
                        {
                            Console.Error.WriteLine("[FAIL] Plugin validation failed:");
                            foreach (var e in allErrors) Console.Error.WriteLine($"  - {e}");
                        }
                        Environment.ExitCode = 1;
                        return;
                    }

                    // Step 2: Security Scan the full plugin root
                    var scanner = new SecurityScanner(fullDir);
                    var findings = scanner.Scan();
                    var highPlus = findings.Where(f => f.Severity == Severity.Critical || f.Severity == Severity.High).ToList();
                    if (highPlus.Any())
                    {
                        EmitScanBlockError("skill package", highPlus, json);
                        return;
                    }

                    // Step 3: Package as plugin
                    var (outputPath, elapsed) = CliHelpers.Time(() =>
                    {
                        var packager = new Packager(fullDir);
                        return packager.PackageAsync().GetAwaiter().GetResult();
                    });

                    EmitPackageSuccess("skill package", outputPath, elapsed, SkillRootKind.Plugin, rootInfo.SkillDirectories.Count);
                }
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("skill package",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
            }

        }, dirArg, jsonOpt);

        return cmd;
    }

    static void EmitValidationError(string command, ValidationResult valResult, bool json)
    {
        if (json)
        {
            var errOut = new JsonOutput
            {
                Status = "error",
                Command = command,
                Summary = $"Validation failed: {valResult.Issues.Count(i => i.Level == "Error")} errors",
                Meta = new MetaInfo { Version = "3.1.0" }
            };
            errOut.Errors = valResult.Issues.Where(i => i.Level == "Error").Select(i =>
                new ErrorEntry(ErrorCodes.ValidationFailed.Code, ErrorCodes.ValidationFailed.Name,
                    $"{i.File}: {i.Message}")
            ).ToList();
            Console.WriteLine(JsonSerializer.Serialize(errOut, CliHelpers.JsonOpts));
        }
        else
        {
            Console.Error.WriteLine("[FAIL] Validation failed:");
            foreach (var issue in valResult.Issues.Where(i => i.Level == "Error"))
                Console.Error.WriteLine($"  [ERR] {issue.File}: {issue.Message}");
        }
        Environment.ExitCode = 1;
    }

    static void EmitScanBlockError(string command, List<SecurityFinding> highPlus, bool json)
    {
        if (json)
        {
            var errOut = new JsonOutput
            {
                Status = "error",
                Command = command,
                Summary = $"{highPlus.Count} High+ findings block packaging",
                Meta = new MetaInfo { Version = "3.1.0" }
            };
            errOut.Errors = highPlus.Select(f =>
                new ErrorEntry(ErrorCodes.ValidationFailed.Code, ErrorCodes.ValidationFailed.Name,
                    $"[{f.Severity}] {f.Rule}: {f.File}:{f.Line} — {f.Detail}")
            ).ToList();
            Console.WriteLine(JsonSerializer.Serialize(errOut, CliHelpers.JsonOpts));
        }
        else
        {
            Console.Error.WriteLine("[FAIL] High+ findings block packaging:");
            foreach (var f in highPlus)
                Console.Error.WriteLine($"  [{f.Severity.ToString().ToUpper()}] {f.Rule}: {f.File}:{f.Line} — {f.Detail}");
        }
        Environment.ExitCode = 1;
    }

    static void EmitPackageSuccess(string command, string outputPath, long elapsed, SkillRootKind kind, int skillCount = 1)
    {
        var aerr = CliHelpers.CheckArtifact(outputPath, "ZIP");
        if (aerr != null)
        {
            CliHelpers.WriteError(command, aerr, true);
            return;
        }

        // Verify archive contents
        try
        {
            var packager = new Packager(Path.GetDirectoryName(outputPath)!);
            packager.VerifyArchiveAsync(outputPath, kind).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            CliHelpers.WriteError(command,
                ErrorCodes.WriteFailed with { Message = $"Archive verification failed: {ex.Message}" }, true);
            return;
        }

        var fileInfo = new FileInfo(outputPath);
        var packageType = kind == SkillRootKind.Plugin ? "plugin" : "skill";

        var output = JsonOutput.Ok(command, $"Package created: {fileInfo.Name}");
        output.Data = kind == SkillRootKind.Plugin
            ? new { packageType, skillCount }
            : new { packageType };
        output.Artifacts["zip"] = outputPath;
        output.Metrics["sizeBytes"] = fileInfo.Length;
        if (kind == SkillRootKind.Plugin)
            output.Metrics["skillCount"] = skillCount;
        output.Meta.DurationMs = elapsed;
        Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
    }
}
