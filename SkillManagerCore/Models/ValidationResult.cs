namespace SkillManager.Cli.Models;

public class ValidationIssue
{
    public string Level { get; set; } = "Error"; // Error, Warning, Info
    public string File { get; set; } = "";
    public string Message { get; set; } = "";
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<ValidationIssue> Issues { get; set; } = new();
    public int SkillMdLineCount { get; set; }
    public string? SkillName { get; set; }
    public string? Description { get; set; }
    public List<string> BrokenReferences { get; set; } = new();
    public List<string> MissingResourceDirs { get; set; } = new();
}
