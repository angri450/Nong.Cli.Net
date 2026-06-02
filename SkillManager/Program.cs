using System.Text.Json;
using SkillManager.Cli.Models;
using SkillManager.Cli.Tools;

var skillDir = Directory.GetCurrentDirectory();

var command = args.Length > 0 ? args[0].ToLowerInvariant() : "";
var commandArg = args.Length > 1 ? args[1] : ".";

switch (command)
{
    case "validate": case "v": await RunValidate(commandArg); break;
    case "scan": case "s": await RunScan(commandArg); break;
    case "package": case "pack": case "p": await RunPackage(commandArg); break;
    case "eval": case "e":
        if (commandArg == "serve" || commandArg == "s")
            await RunEvalServe();
        else
            await RunEval(commandArg);
        break;
    case "scaffold": RunScaffold(args.Skip(1).ToArray()); break;
    case "inventory": case "inspect": case "i": RunInventory(commandArg); break;
    case "optimize-description": case "od": await RunOptimizeDescription(commandArg); break;
    case "run-loop": case "rl": await RunLoopCommand(commandArg); break;
    default: PrintUsage(); break;
}

void PrintUsage()
{
    Console.WriteLine("skill-manager — CLI for the skill lifecycle\n");
    Console.WriteLine("Commands:");
    Console.WriteLine("  validate <path>       Validate SKILL.md structure and references");
    Console.WriteLine("  scan <path>           Security scan (always-on)");
    Console.WriteLine("  scan --audit-allowlist Audit scan allowlist");
    Console.WriteLine("  package <path>        Validate + scan + create .zip");
    Console.WriteLine("  eval <file>           Load and validate eval schema");
    Console.WriteLine("  eval serve            Start interactive eval viewer (browser)");
    Console.WriteLine("  scaffold <name>       Scaffold wrapper skill skeleton");
    Console.WriteLine("    --tool <name>         Display name of upstream tool");
    Console.WriteLine("    --target-dir <path>   Parent directory (default: .)");
    Console.WriteLine("  inventory <path>      List all components");
    Console.WriteLine("\nInstall: dotnet tool install --global Angri450.Nong.Skill.Manager");
}

// ---- validate ----
async Task RunValidate(string path)
{
    var targetDir = ResolveSkillDir(path);
    Console.WriteLine($"[VALIDATE] Target: {targetDir}\n");
    var validator = new SkillValidator(targetDir);
    var result = validator.Validate();
    Console.WriteLine($"Skill: {result.SkillName ?? "(unknown)"}");
    Console.WriteLine($"SKILL.md: {result.SkillMdLineCount} lines");
    Console.WriteLine($"Valid: {(result.IsValid ? "YES" : "NO")}\n");
    foreach (var issue in result.Issues.OrderByDescending(i => i.Level))
    {
        var prefix = issue.Level switch { "Error" => "[ERR] ", "Warning" => "[WARN]", "Info" => "[INFO]", _ => "[??]  " };
        Console.WriteLine($"{prefix} {issue.File}: {issue.Message}");
    }
    if (result.BrokenReferences.Any())
    {
        Console.WriteLine($"\nBroken references ({result.BrokenReferences.Distinct().Count()}):");
        foreach (var br in result.BrokenReferences.Distinct()) Console.WriteLine($"  - {br}");
    }
    Environment.Exit(result.IsValid ? 0 : 1);
}

// ---- scan ----
async Task RunScan(string path)
{
    var targetDir = ResolveSkillDir(path);
    var verbose = args.Length > 2 && args[2].Equals("--verbose", StringComparison.OrdinalIgnoreCase);
    var auditAllowlist = args.Length > 2 && args[2].Equals("--audit-allowlist", StringComparison.OrdinalIgnoreCase);

    if (auditAllowlist) { await RunAuditAllowlist(targetDir); return; }

    Console.WriteLine($"[SCAN] Target: {targetDir}" + (verbose ? " (verbose)" : "") + "\n");
    var scanner = new SecurityScanner(targetDir, verbose);
    var findings = scanner.Scan();
    PrintFindings(findings);
    var hasHighOrAbove = findings.Any(f => f.Severity == Severity.Critical || f.Severity == Severity.High);
    Environment.Exit(hasHighOrAbove ? 1 : 0);
}

async Task RunAuditAllowlist(string targetDir)
{
    Console.WriteLine($"[ALLOWLIST AUDIT] Target: {targetDir}\n");
    var allowlistPath = Path.Combine(targetDir, ".scan-allowlist.json");
    if (!File.Exists(allowlistPath)) { Console.WriteLine("[INFO] No .scan-allowlist.json found."); Environment.Exit(0); return; }

    var json = await File.ReadAllTextAsync(allowlistPath);
    AllowlistConfig? config;
    try { config = JsonSerializer.Deserialize<AllowlistConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
    catch (Exception ex) { Console.WriteLine($"[FAIL] Invalid allowlist JSON: {ex.Message}"); Environment.Exit(1); return; }

    var issues = new List<string>();
    var today = DateOnly.FromDateTime(DateTime.Today);

    foreach (var entry in config?.Allowlist ?? new())
    {
        if (string.IsNullOrWhiteSpace(entry.File)) issues.Add($"Entry missing 'file': {entry.Reason}");
        if (entry.Rules == null || !entry.Rules.Any()) issues.Add($"Entry '{entry.File}' has no rules — OVERBROAD");
        if (string.IsNullOrWhiteSpace(entry.Reason)) issues.Add($"Entry '{entry.File}' missing 'reason' — REJECTED");
        if (entry.Rules?.Contains("*") == true && !entry.File.Contains("tests/", StringComparison.OrdinalIgnoreCase))
            issues.Add($"Entry '{entry.File}' wildcard rules not in tests/ — OVERBROAD");
    }

    if (issues.Any())
    {
        Console.WriteLine($"[FAIL] {issues.Count} allowlist issues found:");
        foreach (var i in issues) Console.WriteLine($"  - {i}");
        Environment.Exit(1);
    }
    else
    {
        Console.WriteLine("[OK] Allowlist audit passed.");
        Console.WriteLine($"  Entries: {config?.Allowlist.Count ?? 0}");
    }
    Environment.Exit(0);
}

void PrintFindings(List<SecurityFinding> findings)
{
    var bySeverity = findings.GroupBy(f => f.Severity).ToDictionary(g => g.Key, g => g.ToList());
    Console.WriteLine($"Findings: {findings.Count}");
    foreach (var sev in new[] { Severity.Critical, Severity.High, Severity.Medium, Severity.Low, Severity.Info })
        if (bySeverity.TryGetValue(sev, out var list) && list.Any()) Console.WriteLine($"  {sev}: {list.Count}");
    Console.WriteLine();
    foreach (var finding in findings)
    {
        var prefix = finding.Severity switch { Severity.Critical => "[CRITICAL]", Severity.High => "[HIGH]    ", Severity.Medium => "[MEDIUM]  ", Severity.Low => "[LOW]     ", _ => "[INFO]    " };
        Console.WriteLine($"{prefix} {finding.Rule} | {finding.File}:{finding.Line}");
        Console.WriteLine($"           {finding.Detail}\n");
    }
}

// ---- package ----
async Task RunPackage(string path)
{
    var targetDir = ResolveSkillDir(path);
    Console.WriteLine($"[PACKAGE] Target: {targetDir}\n");

    Console.WriteLine("--- Step 1: Validate ---");
    var validator = new SkillValidator(targetDir);
    var valResult = validator.Validate();
    Console.WriteLine($"Valid: {(valResult.IsValid ? "YES" : "NO")}");
    foreach (var issue in valResult.Issues.Where(i => i.Level == "Error")) Console.WriteLine($"  [ERR] {issue.Message}");
    if (!valResult.IsValid) { Console.WriteLine("[FAIL] Validation failed."); Environment.Exit(1); return; }

    Console.WriteLine("\n--- Step 2: Security Scan ---");
    var scanner = new SecurityScanner(targetDir);
    var findings = scanner.Scan();
    var highPlus = findings.Where(f => f.Severity == Severity.Critical || f.Severity == Severity.High).ToList();
    Console.WriteLine($"Findings: {findings.Count} total, {highPlus.Count} High+");
    foreach (var f in highPlus) Console.WriteLine($"  [{f.Severity.ToString().ToUpper()}] {f.Rule}: {f.Detail}");
    if (highPlus.Any()) { Console.WriteLine("[FAIL] High+ findings block packaging."); Environment.Exit(2); return; }

    Console.WriteLine("\n--- Step 3: Package ---");
    var packager = new Packager(targetDir);
    try
    {
        var outputPath = await packager.PackageAsync();
        Console.WriteLine($"[OK] Packaged to: {outputPath}");
        Console.WriteLine($"[OK] Size: {new FileInfo(outputPath).Length / (1024.0 * 1024.0):F2} MB");
        await packager.VerifyArchiveAsync(outputPath);
        Console.WriteLine("[OK] Archive integrity verified");
        Console.WriteLine("\nPackage contents:");
        using var zip = System.IO.Compression.ZipFile.OpenRead(outputPath);
        foreach (var entry in zip.Entries.OrderBy(e => e.FullName)) Console.WriteLine($"  {entry.FullName} ({entry.Length} bytes)");
    }
    catch (Exception ex) { Console.WriteLine($"[FAIL] {ex.Message}"); Environment.Exit(3); }
    Console.WriteLine("\n[OK] Package complete!");
    Environment.Exit(0);
}

// ---- eval ----
async Task RunEval(string path)
{
    var evalPath = Path.GetFullPath(path);
    Console.WriteLine($"[EVAL] Eval set: {evalPath}\n");
    var runner = new EvalRunner(skillDir);
    var result = await runner.RunEvalsAsync(evalPath);
    Console.WriteLine($"Skill: {result.SkillName ?? "(unknown)"}");
    Console.WriteLine($"Total evals: {result.TotalEvals} | Ready: {result.ReadyCount} | Invalid: {result.InvalidCount} | Need model: {result.NeedsModelCount}\n");
    foreach (var eval in result.EvalResults)
    {
        Console.WriteLine($"--- Eval #{eval.Id}: {eval.Status} ---");
        Console.WriteLine($"  Prompt: {Truncate(eval.Prompt, 120)}");
        foreach (var err in eval.Errors) Console.WriteLine($"  [ERR] {err}");
        foreach (var warn in eval.Warnings) Console.WriteLine($"  [WARN] {warn}");
        Console.WriteLine();
    }
    foreach (var err in result.Errors) Console.WriteLine($"[ERR] {err}");
    Environment.Exit(result.InvalidCount > 0 ? 1 : 0);
}

// ---- eval serve ----
async Task RunEvalServe()
{
    var viewer = new EvalViewer(skillDir);
    Console.WriteLine("[EVAL SERVE]\n");
    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = $"http://localhost:{viewer.Port}", UseShellExecute = true }); } catch { }
    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
    await viewer.ServeAsync(cts.Token);
}

// ---- scaffold ----
void RunScaffold(string[] scaffoldArgs)
{
    var name = scaffoldArgs.FirstOrDefault() ?? "";
    var tool = ""; var targetDir = "."; var noConfig = false; var force = false;
    for (int i = 1; i < scaffoldArgs.Length; i++)
    {
        switch (scaffoldArgs[i])
        {
            case "--tool": if (i + 1 < scaffoldArgs.Length) tool = scaffoldArgs[++i]; break;
            case "--target-dir": if (i + 1 < scaffoldArgs.Length) targetDir = scaffoldArgs[++i]; break;
            case "--no-config-template": noConfig = true; break;
            case "--force": force = true; break;
        }
    }
    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(tool))
    {
        Console.Error.WriteLine("Usage: skill-manager scaffold <name> --tool <display-name> [--target-dir <path>]");
        Environment.Exit(1); return;
    }
    Environment.Exit(Scaffolder.Run(name, tool, targetDir, noConfig, force));
}

// ---- inventory ----
void RunInventory(string path)
{
    var targetDir = ResolveSkillDir(path);
    Console.WriteLine($"[INVENTORY] Target: {targetDir}\n");
    var runner = new InventoryRunner(targetDir);
    var result = runner.Run();
    Console.WriteLine($"Skill: {result.SkillName}\n");
    Console.WriteLine($"SKILL.md: {(result.HasSkillMd ? $"{result.SkillMdLineCount} lines ({result.SkillMdSizeBytes} bytes)" : "MISSING")}");
    Console.WriteLine($"References: {result.References.Count} files");
    Console.WriteLine($"Scripts: {result.Scripts.Count} files");
    Console.WriteLine($"Agents: {result.Agents.Count} files");
    Console.WriteLine($"Workflows: {result.Workflows.Count} files");
    Console.WriteLine($"Evals: {(result.HasEvals ? $"{result.EvalsFiles.Count} files" : "None")}");
    Console.WriteLine($".NET Tools: {result.DotNetTools.Count}");
    Console.WriteLine($"Tests: {result.TestFiles.Count} source files");
    Console.WriteLine($"Total: {result.TotalFileCount} files");
    Environment.Exit(0);
}

// ---- optimize-description ----
async Task RunOptimizeDescription(string path)
{
    var targetDir = ResolveSkillDir(path);
    var apply = args.Any(a => a.Equals("--apply", StringComparison.OrdinalIgnoreCase));
    Console.WriteLine($"[OPTIMIZE-DESC] Target: {targetDir} (mode: {(apply ? "apply" : "dry-run")})\n");

    var optimizer = new DescriptionOptimizer(targetDir, apply);
    var result = await optimizer.OptimizeAsync();

    if (result.Errors.Any()) { foreach (var e in result.Errors) Console.WriteLine($"[ERR] {e}"); Environment.Exit(1); return; }

    Console.WriteLine($"Skill: {result.SkillName}");
    Console.WriteLine($"Current description: {Truncate(result.CurrentDescription ?? "(none)", 200)}\n");

    Console.WriteLine($"Claim checks ({result.ClaimChecks.Count}):");
    foreach (var cc in result.ClaimChecks) Console.WriteLine($"  [{(cc.Traceable ? "OK" : "FAIL")}] {Truncate(cc.Claim, 100)}{(cc.Issue != null ? " — " + cc.Issue : "")}");

    Console.WriteLine($"\nRisks ({result.Risks.Count}):");
    foreach (var r in result.Risks) Console.WriteLine($"  - {r}");

    Console.WriteLine($"\nSuggestions ({result.Suggestions.Count}):");
    foreach (var s in result.Suggestions) Console.WriteLine($"  - {s}");

    Console.WriteLine($"\n{result.ApplyNote ?? "Dry-run complete. Use --apply to write changes."}");
    Environment.Exit(0);
}

// ---- run-loop ----
async Task RunLoopCommand(string path)
{
    var targetDir = ResolveSkillDir(path);
    var evalsFile = args.FirstOrDefault(a => a.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
    Console.WriteLine($"[RUN-LOOP] Target: {targetDir} Evals: {evalsFile ?? "none"}\n");

    var looper = new LoopRunner(targetDir, evalsFile);
    var result = await looper.RunLoopAsync();

    foreach (var step in result.Steps)
    {
        var icon = step.Status switch { "passed" => "[PASS]", "failed" => "[FAIL]", "skipped" => "[SKIP]", "ready" => "[READY]", _ => "[....]" };
        Console.WriteLine($"{icon} {step.Name}: {step.Output}");
    }

    if (result.Errors.Any()) { Console.WriteLine($"\nErrors ({result.Errors.Count}):"); foreach (var e in result.Errors) Console.WriteLine($"  - {e}"); }
    if (result.NeedsModelEnvironment) Console.WriteLine("\n[NOTE] Model environment needed for full eval execution. Dry-run completed schema checks.");
    if (result.NeedsFixing) Console.WriteLine("\n[NOTE] Issues need fixing before continuing.");

    Environment.Exit(result.Errors.Any() ? 1 : 0);
}

// ---- helpers ----
string ResolveSkillDir(string path) => path == "." || string.IsNullOrWhiteSpace(path) ? skillDir : Directory.Exists(Path.GetFullPath(path)) ? Path.GetFullPath(path) : skillDir;

string Truncate(string text, int maxLen) => text.Length <= maxLen ? text : text[..(maxLen - 3)] + "...";
