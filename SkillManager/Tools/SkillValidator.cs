using System.Text.RegularExpressions;
using SkillManager.Cli.Models;
using YamlDotNet.Serialization;

namespace SkillManager.Cli.Tools;

public class SkillValidator
{
    private readonly string _skillDir;
    private static readonly IDeserializer _yamlDeserializer = new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly HashSet<string> ResourceDirNames = new()
    {
        "scripts", "references", "assets", "templates", "agents", "workflows", "eval-viewer", "tools", "tests"
    };

    public SkillValidator(string skillDir)
    {
        _skillDir = Path.GetFullPath(skillDir);
    }

    public ValidationResult Validate()
    {
        var result = new ValidationResult();

        // 1. Verify SKILL.md exists
        var skillMdPath = Path.Combine(_skillDir, "SKILL.md");
        if (!File.Exists(skillMdPath))
        {
            result.Issues.Add(new ValidationIssue
            {
                File = "SKILL.md",
                Message = "SKILL.md not found in skill directory"
            });
            result.IsValid = false;
            return result;
        }

        // 2. Read SKILL.md and parse frontmatter
        var content = File.ReadAllText(skillMdPath);
        result.SkillMdLineCount = content.Count(c => c == '\n') + 1;

        if (!content.StartsWith("---"))
        {
            result.Issues.Add(new ValidationIssue
            {
                File = "SKILL.md",
                Message = "No YAML frontmatter found (file must start with ---)"
            });
            result.IsValid = false;
            return result;
        }

        var match = Regex.Match(content, @"^---\r?\n(.*?)\r?\n---", RegexOptions.Singleline);
        if (!match.Success)
        {
            result.Issues.Add(new ValidationIssue
            {
                File = "SKILL.md",
                Message = "Invalid frontmatter format"
            });
            result.IsValid = false;
            return result;
        }

        var frontmatterText = match.Groups[1].Value;
        SkillFrontmatter? frontmatter;
        try
        {
            frontmatter = _yamlDeserializer.Deserialize<SkillFrontmatter>(frontmatterText);
        }
        catch (Exception ex)
        {
            result.Issues.Add(new ValidationIssue
            {
                File = "SKILL.md",
                Message = $"Invalid YAML in frontmatter: {ex.Message}"
            });
            result.IsValid = false;
            return result;
        }

        if (frontmatter == null)
        {
            result.Issues.Add(new ValidationIssue
            {
                File = "SKILL.md",
                Message = "Empty frontmatter"
            });
            result.IsValid = false;
            return result;
        }

        // 3. Verify name exists
        if (string.IsNullOrWhiteSpace(frontmatter.Name))
        {
            result.Issues.Add(new ValidationIssue
            {
                File = "SKILL.md:frontmatter",
                Message = "Missing 'name' in frontmatter"
            });
        }
        else
        {
            result.SkillName = frontmatter.Name.Trim();
            // Validate naming
            if (!Regex.IsMatch(result.SkillName, @"^[a-z0-9-]+$"))
            {
                result.Issues.Add(new ValidationIssue
                {
                    File = "SKILL.md:frontmatter",
                    Level = "Warning",
                    Message = $"Name '{result.SkillName}' should be hyphen-case (lowercase, digits, hyphens only)"
                });
            }
            if (result.SkillName.Length > 64)
            {
                result.Issues.Add(new ValidationIssue
                {
                    File = "SKILL.md:frontmatter",
                    Message = $"Name too long ({result.SkillName.Length} chars, max 64)"
                });
            }
        }

        // 4. Verify description exists and is a string
        if (frontmatter.Description == null)
        {
            result.Issues.Add(new ValidationIssue
            {
                File = "SKILL.md:frontmatter",
                Message = "Missing 'description' in frontmatter"
            });
        }
        else if (frontmatter.Description is not string)
        {
            result.Issues.Add(new ValidationIssue
            {
                File = "SKILL.md:frontmatter",
                Message = $"Description must be a string, got {frontmatter.Description.GetType().Name}"
            });
        }
        else
        {
            var desc = (string)frontmatter.Description;
            result.Description = desc.Trim();
            if (string.IsNullOrWhiteSpace(desc))
            {
                result.Issues.Add(new ValidationIssue
                {
                    File = "SKILL.md:frontmatter",
                    Message = "Description is empty"
                });
            }
            if (desc.Contains('<') || desc.Contains('>'))
            {
                result.Issues.Add(new ValidationIssue
                {
                    File = "SKILL.md:frontmatter",
                    Message = "Description contains angle brackets"
                });
            }
            if (desc.Trim().Length > 1024)
            {
                result.Issues.Add(new ValidationIssue
                {
                    File = "SKILL.md:frontmatter",
                    Level = "Warning",
                    Message = $"Description is {desc.Trim().Length} chars (max 1024)"
                });
            }
        }

        // 5. Check for forbiddens
        if (content.Contains("## Version") || content.Contains("## Changelog"))
        {
            result.Issues.Add(new ValidationIssue
            {
                File = "SKILL.md",
                Level = "Warning",
                Message = "Skill contains version/changelog section (should be in marketplace.json)"
            });
        }

        // 6. Validate referenced files
        ValidateReferencedFiles(content, result);
        ValidateReferencedPaths(content, result);

        // 7. Warn on large SKILL.md
        if (result.SkillMdLineCount > 700)
        {
            result.Issues.Add(new ValidationIssue
            {
                File = "SKILL.md",
                Level = "Warning",
                Message = $"SKILL.md is {result.SkillMdLineCount} lines. Consider moving content to references/ (target: < 700 lines)"
            });
        }
        else if (result.SkillMdLineCount > 500)
        {
            result.Issues.Add(new ValidationIssue
            {
                File = "SKILL.md",
                Level = "Info",
                Message = $"SKILL.md is {result.SkillMdLineCount} lines. Ideal is < 500 lines."
            });
        }

        // 8. Check for empty content after frontmatter
        var bodyContent = content.Substring(match.Index + match.Length).Trim();
        if (string.IsNullOrWhiteSpace(bodyContent))
        {
            result.Issues.Add(new ValidationIssue
            {
                File = "SKILL.md",
                Level = "Warning",
                Message = "No content in SKILL.md body after frontmatter"
            });
        }

        result.IsValid = !result.Issues.Any(i => i.Level == "Error");
        return result;
    }

    private void ValidateReferencedFiles(string content, ValidationResult result)
    {
        var baseDir = _skillDir;
        // Find Markdown links: [text](path) or `path`
        var linkPatterns = new[] { @"\[([^\]]+)\]\(([^)]+)\)", @"`(scripts/[^`]+)`", @"`(references/[^`]+)`", @"`(assets/[^`]+)`" };

        foreach (var pattern in linkPatterns)
        {
            var matches = Regex.Matches(content, pattern);
            foreach (Match m in matches)
            {
                var path = m.Groups.Count >= 3 ? m.Groups[2].Value : m.Groups[1].Value;
                // Skip URLs and absolute paths
                if (path.StartsWith("http://") || path.StartsWith("https://") || Path.IsPathRooted(path))
                    continue;
                // Skip paths in example/code blocks (rough heuristic)
                if (path.Contains("example") && path.EndsWith(".py"))
                    continue;

                var fullPath = Path.GetFullPath(Path.Combine(baseDir, path));
                if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
                {
                    // Check if it's a directory reference
                    var dirOnly = path.Split('/').FirstOrDefault();
                    if (dirOnly != null && ResourceDirNames.Contains(dirOnly) && path == dirOnly)
                        continue;

                    result.BrokenReferences.Add(path);
                }
            }
        }

        if (result.BrokenReferences.Any())
        {
            foreach (var br in result.BrokenReferences.Distinct())
            {
                result.Issues.Add(new ValidationIssue
                {
                    File = "SKILL.md",
                    Level = "Warning",
                    Message = $"Referenced file not found: {br}"
                });
            }
        }
    }

    private void ValidateReferencedPaths(string content, ValidationResult result)
    {
        // Check if referenced resource directories actually exist
        var baseDir = _skillDir;
        foreach (var dir in ResourceDirNames)
        {
            if (content.Contains(dir + "/") || content.Contains(dir + "\\"))
            {
                var fullDir = Path.Combine(baseDir, dir);
                if (!Directory.Exists(fullDir))
                {
                    result.MissingResourceDirs.Add(dir);
                    result.Issues.Add(new ValidationIssue
                    {
                        File = "SKILL.md",
                        Level = "Warning",
                        Message = $"SKILL.md references '{dir}' directory but it does not exist"
                    });
                }
            }
        }
    }
}
