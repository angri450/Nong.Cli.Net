namespace Nong.Cli.Common;

/// <summary>
/// OpenAI-compatible tool schema for function-calling bridges (NanoBot, etc.).
/// Generated from Manifest command metadata.
/// </summary>
public sealed class OpenAiToolSchema
{
    public sealed record FunctionDef(
        string Name,
        string Description,
        ParameterDef Parameters
    );

    /// <summary>Example input/output pair for agent function-calling guidance. V10.</summary>
    public sealed record Example(string Input, string Output);

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

    public static OpenAiToolSchema FromCommand(Manifest.CommandInfo cmd)
    {
        var name = cmd.Name.Replace(' ', '_');
        return new OpenAiToolSchema
        {
            Function = new FunctionDef(name, cmd.Description,
                new ParameterDef("object",
                    cmd.Parameters?.ToDictionary(p => p.Name, p => new PropertyDef(p.Type, p.Description)),
                    cmd.Parameters?.Where(p => p.Required).Select(p => p.Name).ToArray() ?? []))
        };
    }
}
