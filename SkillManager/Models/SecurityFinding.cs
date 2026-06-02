namespace SkillManager.Cli.Models;

public enum Severity
{
    Critical,
    High,
    Medium,
    Low,
    Info
}

public class SecurityFinding
{
    public Severity Severity { get; set; }
    public string Rule { get; set; } = "";
    public string File { get; set; } = "";
    public int Line { get; set; }
    public string Detail { get; set; } = "";
}
