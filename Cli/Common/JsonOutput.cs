namespace Nong.Cli.Common;

/// <summary>
/// Structured JSON output model for all CLI commands.
/// </summary>
public sealed class JsonOutput
{
    public string Status { get; set; } = "ok";
    public string Command { get; set; } = "";
    public string Summary { get; set; } = "";
    public object? Data { get; set; }
    public List<Issue> Issues { get; set; } = new();
    public Dictionary<string, string> Artifacts { get; set; } = new();
    public Dictionary<string, object> Metrics { get; set; } = new();
    public List<ErrorEntry> Errors { get; set; } = new();
    public MetaInfo Meta { get; set; } = new();

    public static JsonOutput Ok(string command, string summary, object? data = null) => new()
    {
        Status = "ok",
        Command = command,
        Summary = summary,
        Data = data,
        Meta = new MetaInfo { Version = CliVersion.Current }
    };

    public static JsonOutput Fail(string command, List<ErrorEntry> errors) => new()
    {
        Status = "error",
        Command = command,
        Errors = errors,
        Meta = new MetaInfo { Version = CliVersion.Current }
    };
}

public sealed class Issue
{
    public string Id { get; set; } = "";
    public string Severity { get; set; } = "";
    public string Message { get; set; } = "";
}

public sealed class MetaInfo
{
    public long DurationMs { get; set; }
    public string Version { get; set; } = "";
}
