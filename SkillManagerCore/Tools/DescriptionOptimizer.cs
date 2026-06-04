using System.Text.Json;
using System.Text.RegularExpressions;
using SkillManager.Cli.Models;
using YamlDotNet.Serialization;

namespace SkillManager.Cli.Tools;

public class DescriptionOptimizer
{
    private readonly string _skillDir;
    private readonly bool _apply;

    private static readonly IDeserializer _yamlDeserializer = new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .Build();

    public DescriptionOptimizer(string skillDir, bool apply = false)
    {
        _skillDir = Path.GetFullPath(skillDir);
        _apply = apply;
    }

    public async Task<OptimizationResult> OptimizeAsync()
    {
        var result = new OptimizationResult();

        var skillMdPath = Path.Combine(_skillDir, "SKILL.md");
        if (!File.Exists(skillMdPath))
        {
            result.Errors.Add("SKILL.md not found");
            return result;
        }

        var content = await File.ReadAllTextAsync(skillMdPath);

        // Parse frontmatter
        var fmMatch = System.Text.RegularExpressions.Regex.Match(content, @"^---\r?\n(.*?)\r?\n---", System.Text.RegularExpressions.RegexOptions.Singleline);
        if (!fmMatch.Success)
        {
            result.Errors.Add("No frontmatter found");
            return result;
        }

        SkillFrontmatter? frontmatter;
        try
        {
            frontmatter = _yamlDeserializer.Deserialize<SkillFrontmatter>(fmMatch.Groups[1].Value);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Invalid YAML frontmatter: {ex.Message}");
            return result;
        }

        if (frontmatter == null || frontmatter.Description is not string desc)
        {
            result.Errors.Add("No string description found");
            return result;
        }

        result.CurrentDescription = desc;
        result.SkillName = frontmatter.Name ?? Path.GetFileName(_skillDir);

        // Extract SKILL.md body (after frontmatter) — don't check description against itself
        var bodyContent = content.Substring(fmMatch.Index + fmMatch.Length).Trim();

        // Check honesty boundary: find claims in description
        var claimedAbilities = ExtractClaimedAbilities(desc);
        foreach (var claim in claimedAbilities)
        {
            var traceable = IsCapabilityTraceable(claim, bodyContent, _skillDir);
            result.ClaimChecks.Add(new ClaimCheck
            {
                Claim = claim,
                Traceable = traceable,
                Issue = traceable ? null : "Capability claimed in description but not found in SKILL.md body, references/, scripts/, or assets/"
            });
        }

        // Analyze risks
        AnalyzeRisks(result);

        // Suggest improvements
        GenerateSuggestions(result);

        // Apply if requested
        if (_apply && result.SuggestedDescription != null)
        {
            var updatedContent = content.Replace(
                $"description: {desc}",
                $"description: {result.SuggestedDescription}");
            await File.WriteAllTextAsync(skillMdPath, updatedContent);
            result.Applied = true;
            result.ApplyNote = "SKILL.md updated with new description.";
        }
        else
        {
            result.Applied = false;
            result.ApplyNote = "Dry-run mode. Use --apply to write changes.";
        }

        return result;
    }

    private List<string> ExtractClaimedAbilities(string description)
    {
        var claims = new List<string>();
        var desc = description.ToLowerInvariant();

        // Split by common separators
        var parts = desc.Split(new[] { '.', ',', ';', '(', ')', ':' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length >= 5);

        foreach (var part in parts)
        {
            // Skip tool/option listings — "X vs Y", "A/B/C", tool enumeration
            if (Regex.IsMatch(part, @"\b\w+\s+vs\s+\w+", RegexOptions.IgnoreCase))
                continue;
            if (Regex.IsMatch(part, @"^[\w/]+$") && part.Count(c => c == '/') >= 2)
                continue;

            // Check if it starts with an action verb
            var verbs = new[] { "create", "modify", "improve", "fix", "repair", "debug", "merge",
                "split", "deprecate", "audit", "package", "scan", "validate", "edit", "optimize",
                "benchmark", "eval", "test", "convert", "export", "extract", "analyze", "generate",
                "handle", "process", "support", "manage" };

            bool hasVerb = verbs.Any(v => part.Contains(v));
            // Also detect technical nouns that imply capabilities: OCR, PDF, XLSX, etc.
            bool hasTechnicalNoun = Regex.IsMatch(part,
                @"\b(ocr|pdf|docx|xlsx|csv|json|xml|html|api|sdk|cli|gui|database|auth|encrypt|compress|deploy)\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (hasVerb || hasTechnicalNoun)
                claims.Add(part);
        }

        return claims.Distinct().ToList();
    }

    private bool IsCapabilityTraceable(string claim, string skillMdContent, string skillDir)
    {
        // Check if the claim's action verb has corresponding content
        var lowerClaim = claim.ToLowerInvariant();
        var lowerSkill = skillMdContent.ToLowerInvariant();

        // Direct match in SKILL.md
        if (lowerSkill.Contains(lowerClaim))
            return true;

        // Check reference files
        var refsDir = Path.Combine(skillDir, "references");
        if (Directory.Exists(refsDir))
        {
            foreach (var refFile in Directory.GetFiles(refsDir, "*.md"))
            {
                var refContent = File.ReadAllText(refFile).ToLowerInvariant();
                if (refContent.Contains(lowerClaim))
                    return true;
            }
        }

        // Check scripts
        var scriptsDir = Path.Combine(skillDir, "scripts");
        if (Directory.Exists(scriptsDir))
        {
            if (Directory.GetFiles(scriptsDir, "*.*", SearchOption.TopDirectoryOnly).Any())
                return true; // Having scripts implies tool capabilities
        }

        // Check tools
        var toolsDir = Path.Combine(skillDir, "tools");
        if (Directory.Exists(toolsDir))
        {
            if (Directory.GetFiles(toolsDir, "*.csproj", SearchOption.AllDirectories).Any())
                return true; // Having .NET tools implies command capabilities
        }

        return false;
    }

    private void AnalyzeRisks(OptimizationResult result)
    {
        var desc = result.CurrentDescription ?? "";
        var risks = new List<string>();

        var descLower = desc.ToLowerInvariant();
        var hasNegativeTrigger = descLower.Contains("do not trigger") || descLower.Contains("not trigger when");

        // Under-trigger risk
        if (desc.Length < 50)
            risks.Add("UNDER_TRIGGER: Description is short (< 50 chars). May miss common user phrases.");
        if (!desc.Contains("when", StringComparison.OrdinalIgnoreCase))
            risks.Add("UNDER_TRIGGER: No 'when to use' guidance in description.");

        // Over-trigger risk
        // Long descriptions with explicit negative triggers are intentionally precise — don't flag
        if (desc.Length > 500 && !hasNegativeTrigger)
            risks.Add("OVER_TRIGGER: Description is long (> 500 chars). May match unrelated queries.");
        // "any" in generic qualification phrases (any task, any kind, any agent) is not universal claim
        if ((descLower.Contains(" all ") || descLower.Contains(" all,")) && !hasNegativeTrigger)
            risks.Add("OVER_TRIGGER: Universal qualifier 'all' may cause over-matching.");

        // False capability claim
        var falseClaims = result.ClaimChecks.Where(c => !c.Traceable).ToList();
        foreach (var fc in falseClaims)
            risks.Add($"FALSE_CAPABILITY: Claim '{fc.Claim}' not traceable to skill content.");

        // Overlap risk — skip if the mention is in a "do NOT trigger" context
        var overlapSkills = new[] { ("word","word"), ("excel","excel"), ("pptx","pptx"), ("email","email"), ("bash","bash"), ("powershell","powershell"), ("dotnet","dotnet"), ("github","github") };
        foreach (var (skill, keyword) in overlapSkills)
        {
            if (result.SkillName?.Contains(skill, StringComparison.OrdinalIgnoreCase) == true)
                continue; // skip self

            // Find the position of the keyword
            var idx = descLower.IndexOf(keyword, StringComparison.Ordinal);
            if (idx < 0) continue;

            // Check if it's in a negative trigger context: "do NOT trigger when ... Word document"
            var preceding = descLower.Substring(0, Math.Min(idx, descLower.Length));
            if (preceding.Contains("not trigger") || preceding.Contains("do not trigger"))
                continue;

            risks.Add($"OVERLAP: Description mentions '{keyword}' — may conflict with {keyword} skill.");
        }

        result.Risks = risks;
    }

    private void GenerateSuggestions(OptimizationResult result)
    {
        var suggestions = new List<string>();

        if (result.Risks.Any(r => r.StartsWith("UNDER_TRIGGER")))
            suggestions.Add("Add specific user trigger phrases: 'when user says X', 'when user needs Y'.");

        if (result.Risks.Any(r => r.StartsWith("FALSE_CAPABILITY")))
            suggestions.Add("Remove or narrow unsupported capabilities from description, or implement them.");

        if (result.Risks.Any(r => r.StartsWith("OVER_TRIGGER")))
            suggestions.Add("Narrow description with specific file types, tasks, or context triggers.");

        if (!suggestions.Any() && !result.Risks.Any())
            suggestions.Add("Description looks balanced. Consider A/B testing trigger rates.");

        result.Suggestions = suggestions;
        result.SuggestedDescription = result.CurrentDescription; // Keep current unless --apply with explicit new text
    }
}

public class ClaimCheck
{
    public string Claim { get; set; } = "";
    public bool Traceable { get; set; }
    public string? Issue { get; set; }
}

public class OptimizationResult
{
    public string? SkillName { get; set; }
    public string? CurrentDescription { get; set; }
    public string? SuggestedDescription { get; set; }
    public List<ClaimCheck> ClaimChecks { get; set; } = new();
    public List<string> Risks { get; set; } = new();
    public List<string> Suggestions { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public bool Applied { get; set; }
    public string? ApplyNote { get; set; }
}
