using System.Text.Json;
using SkillManager.Cli.Models;

namespace SkillManager.Cli.Tools;

public class EvalRunner
{
    private readonly string _skillDir;

    public EvalRunner(string skillDir)
    {
        _skillDir = Path.GetFullPath(skillDir);
    }

    public async Task<EvalRunResult> RunEvalsAsync(string evalSetPath)
    {
        var result = new EvalRunResult();
        var evalSetFullPath = Path.GetFullPath(evalSetPath);

        if (!File.Exists(evalSetFullPath))
        {
            result.Errors.Add($"Eval set file not found: {evalSetPath}");
            return result;
        }

        // Parse eval set
        EvalSet? evalSet;
        try
        {
            var json = await File.ReadAllTextAsync(evalSetFullPath);
            evalSet = JsonSerializer.Deserialize<EvalSet>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Failed to parse eval set: {ex.Message}");
            return result;
        }

        if (evalSet == null || evalSet.Evals == null || !evalSet.Evals.Any())
        {
            result.Errors.Add("No evals found in eval set");
            return result;
        }

        result.SkillName = evalSet.SkillName;
        result.TotalEvals = evalSet.Evals.Count;

        foreach (var eval in evalSet.Evals)
        {
            var evalResult = new EvalCheckResult
            {
                Id = eval.Id,
                Prompt = eval.Prompt
            };

            // Validate eval schema
            if (string.IsNullOrWhiteSpace(eval.Prompt))
            {
                evalResult.Errors.Add("Eval missing prompt");
                evalResult.Status = "invalid";
            }

            if (string.IsNullOrWhiteSpace(eval.ExpectedOutput))
            {
                evalResult.Warnings.Add("Eval has no expected_output — can only do smoke validation");
            }

            // Check for trigger-related expectations
            if (eval.Expectations.Any())
                evalResult.HasTriggerChecks = true;

            if (eval.Assertions.Any())
                evalResult.HasAssertions = true;

            // Check if referenced files exist
            foreach (var file in eval.Files)
            {
                var fullPath = Path.GetFullPath(Path.Combine(_skillDir, file));
                if (!File.Exists(fullPath))
                {
                    evalResult.Errors.Add($"Referenced file not found: {file}");
                }
            }

            if (!evalResult.Errors.Any())
            {
                evalResult.Status = "ready"; // Schema valid, needs model environment to actually execute
                evalResult.NeedsModelEnvironment = true;
            }

            result.EvalResults.Add(evalResult);
        }

        result.ReadyCount = result.EvalResults.Count(e => e.Status == "ready");
        result.InvalidCount = result.EvalResults.Count(e => e.Status == "invalid");
        result.NeedsModelCount = result.EvalResults.Count(e => e.NeedsModelEnvironment);

        return result;
    }
}

public class EvalRunResult
{
    public string? SkillName { get; set; }
    public int TotalEvals { get; set; }
    public int ReadyCount { get; set; }
    public int InvalidCount { get; set; }
    public int NeedsModelCount { get; set; }
    public List<EvalCheckResult> EvalResults { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

public class EvalCheckResult
{
    public int Id { get; set; }
    public string Prompt { get; set; } = "";
    public string Status { get; set; } = "unknown";
    public bool NeedsModelEnvironment { get; set; }
    public bool HasTriggerChecks { get; set; }
    public bool HasAssertions { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
