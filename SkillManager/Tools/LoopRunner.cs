using System.Text.Json;
using SkillManager.Cli.Models;

namespace SkillManager.Cli.Tools;

public class LoopRunner
{
    private readonly string _skillDir;
    private readonly string? _evalsPath;

    public LoopRunner(string skillDir, string? evalsPath = null)
    {
        _skillDir = Path.GetFullPath(skillDir);
        _evalsPath = evalsPath != null ? Path.GetFullPath(evalsPath) : null;
    }

    public async Task<LoopRunResult> RunLoopAsync()
    {
        var result = new LoopRunResult
        {
            SkillDir = _skillDir
        };

        // Step 1: Validate
        result.Steps.Add(new LoopStep { Name = "validate", Status = "running" });
        var validator = new SkillValidator(_skillDir);
        var valResult = validator.Validate();
        result.Steps.Last().Status = valResult.IsValid ? "passed" : "failed";
        result.Steps.Last().Output = valResult.IsValid ? "SKILL.md valid" : string.Join("; ", valResult.Issues.Select(i => i.Message));
        if (!valResult.IsValid)
        {
            result.NeedsFixing = true;
            result.Errors.Add("Validation failed. Fix before continuing.");
            return result;
        }

        // Step 2: Scan
        result.Steps.Add(new LoopStep { Name = "security-scan", Status = "running" });
        var scanner = new SecurityScanner(_skillDir);
        var findings = scanner.Scan();
        var highPlus = findings.Where(f => f.Severity == Severity.Critical || f.Severity == Severity.High).ToList();
        result.Steps.Last().Status = highPlus.Any() ? "failed" : "passed";
        result.Steps.Last().Output = $"{findings.Count} findings, {highPlus.Count} High+";
        if (highPlus.Any())
        {
            result.NeedsFixing = true;
            result.Errors.Add($"Security scan found {highPlus.Count} High+ findings.");
            return result;
        }

        // Step 3: Eval schema check
        if (_evalsPath != null && File.Exists(_evalsPath))
        {
            result.Steps.Add(new LoopStep { Name = "eval-schema", Status = "running" });
            var evalRunner = new EvalRunner(_skillDir);
            var evalResult = await evalRunner.RunEvalsAsync(_evalsPath);
            bool evalOk = evalResult.InvalidCount == 0 && !evalResult.Errors.Any();
            result.Steps.Last().Status = evalOk ? "passed" : "failed";
            result.Steps.Last().Output = $"{evalResult.ReadyCount} ready, {evalResult.InvalidCount} invalid";
            if (!evalOk)
            {
                result.Errors.Add("Eval schema validation failed.");
            }
        }
        else
        {
            result.Steps.Add(new LoopStep
            {
                Name = "eval-schema",
                Status = "skipped",
                Output = "No evals file found or specified."
            });
        }

        // Step 4: Blind comparison preparation
        result.Steps.Add(new LoopStep { Name = "blind-comparison", Status = "ready" });
        result.Steps.Last().Output = "Blind protocol documented in references/evaluation-workflow.md. " +
            "Actual comparison requires subagent environment or model evaluation, which is not available in dry-run mode.";
        result.NeedsModelEnvironment = true;

        // Step 5: Report readiness
        result.Steps.Add(new LoopStep { Name = "report", Status = "ready" });
        result.Steps.Last().Output = "Run evaluation in environment with subagent/model access to complete the loop.";

        return result;
    }
}

public class LoopRunResult
{
    public string SkillDir { get; set; } = "";
    public List<LoopStep> Steps { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public bool NeedsFixing { get; set; }
    public bool NeedsModelEnvironment { get; set; }
}

public class LoopStep
{
    public string Name { get; set; } = "";
    public string Status { get; set; } = "pending"; // pending, running, passed, failed, skipped, ready
    public string? Output { get; set; }
}
