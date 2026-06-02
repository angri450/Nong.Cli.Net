using System.Text.Json.Serialization;

namespace SkillManager.Cli.Models;

public class EvalItem
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("prompt")] public string Prompt { get; set; } = "";
    [JsonPropertyName("expected_output")] public string? ExpectedOutput { get; set; }
    [JsonPropertyName("files")] public List<string> Files { get; set; } = new();
    [JsonPropertyName("assertions")] public List<string> Assertions { get; set; } = new();
    [JsonPropertyName("expectations")] public List<string> Expectations { get; set; } = new();
}

public class EvalSet
{
    [JsonPropertyName("skill_name")] public string? SkillName { get; set; }
    [JsonPropertyName("evals")] public List<EvalItem> Evals { get; set; } = new();
}