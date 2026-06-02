namespace SkillManager.Cli.Models;

public class AllowlistConfig
{
    public List<AllowlistEntry> Allowlist { get; set; } = new();
}

public class AllowlistEntry
{
    public string File { get; set; } = "";
    public List<string> Rules { get; set; } = new();
    public string? Reason { get; set; }
}
