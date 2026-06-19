namespace Nong.Cli.Common;

/// <summary>
/// OpenAI-compatible tool schema for function-calling bridges (NanoBot, etc.).
/// V12.2: Added Examples, StreamOutput, ErrorRecoveryHints, streaming helpers.
/// </summary>
public sealed class OpenAiToolSchema
{
    public sealed record FunctionDef(
        string Name,
        string Description,
        ParameterDef Parameters
    );

    /// <summary>Example input/output pair for agent function-calling guidance.</summary>
    public sealed record Example(string Input, string Output);

    /// <summary>Error recovery hint for agent retry logic.</summary>
    public sealed record ErrorHint(string Code, string Description, string Recovery);

    public sealed record ParameterDef(
        string Type,
        Dictionary<string, PropertyDef>? Properties,
        string[]? Required
    );

    public sealed record PropertyDef(
        string Type,
        string Description
    );

    public string Type => "function";
    public FunctionDef Function { get; init; } = null!;
    public List<Example>? Examples { get; set; }
    public bool StreamOutput { get; set; }
    public List<ErrorHint>? ErrorRecovery { get; set; }

    // ── Streaming output helpers ──

    /// <summary>Serialize this tool as a single-line JSON (for streaming line-by-line).</summary>
    public string ToStreamLine() =>
        System.Text.Json.JsonSerializer.Serialize(this, CliHelpers.JsonOpts);

    /// <summary>Serialize a collection of tools as streaming JSON Lines.</summary>
    public static string ToStreamLines(IEnumerable<OpenAiToolSchema> tools)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var t in tools) sb.AppendLine(t.ToStreamLine());
        return sb.ToString();
    }

    // ── From manifest command ──

    public static OpenAiToolSchema FromCommand(Manifest.CommandInfo cmd)
    {
        var name = cmd.Name.Replace(' ', '_');
        var schema = new OpenAiToolSchema
        {
            Function = new FunctionDef(name, cmd.Description,
                new ParameterDef("object",
                    cmd.Parameters?.ToDictionary(p => p.Name, p => new PropertyDef(p.Type, p.Description)),
                    cmd.Parameters?.Where(p => p.Required).Select(p => p.Name).ToArray() ?? [])),
            StreamOutput = cmd.Group is "word" or "excel" or "pdf" or "pptx",
            ErrorRecovery = BuildErrorHints(cmd),
            Examples = BuildExamples(cmd)
        };
        return schema;
    }

    static List<ErrorHint>? BuildErrorHints(Manifest.CommandInfo cmd)
    {
        var hints = new List<ErrorHint>();
        if (!string.IsNullOrEmpty(cmd.Description))
        {
            hints.Add(new ErrorHint("E001", "Validation failed",
                $"Check that all required parameters for '{cmd.Name}' are provided. See the schema for required fields."));
            hints.Add(new ErrorHint("E009", "Not implemented",
                $"The command '{cmd.Name}' may have stub status. Check if it's fully implemented before use."));
        }
        return hints.Count > 0 ? hints : null;
    }

    static List<Example>? BuildExamples(Manifest.CommandInfo cmd)
    {
        if (cmd.Name == "word" || cmd.Description.Contains("legacy", StringComparison.OrdinalIgnoreCase)) return null;
        // Provide generic example based on command pattern
        if (cmd.Name.Contains("create") || cmd.Name.Contains("convert"))
            return new() { new Example($"{{\"file\": \"input.json\"}}", $"{{\"output\": \"result.docx\"}}") };
        if (cmd.Name.Contains("read") || cmd.Name.Contains("extract"))
            return new() { new Example($"{{\"file\": \"input.docx\"}}", $"{{\"text\": \"Extracted content...\"}}") };
        return null;
    }
}
