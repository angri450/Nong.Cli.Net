using System.Text.Json;

namespace SkillManager.Cli.Tools;

public static class BlindAnonymizer
{
    private static readonly HashSet<string> LeakageFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "skill_path", "skill_version", "skill_name", "configuration",
        "with_skill", "without_skill", "old_skill", "new_skill",
        "baseline", "candidate", "version", "run_config"
    };

    private static readonly HashSet<string> LeakagePathSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "with_skill", "without_skill", "old_skill", "new_skill",
        "baseline", "candidate", "control", "experiment"
    };

    /// <summary>
    /// Check if a directory path contains leakage-identifying segments.
    /// </summary>
    public static bool PathContainsLeakage(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(s => LeakagePathSegments.Contains(s));
    }

    /// <summary>
    /// Check if artifact labels are properly anonymous (A, B, artifact-N, etc.).
    /// </summary>
    public static bool IsAnonymousLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label)) return false;

        // Single letter: A, B, C...
        if (label.Length == 1 && char.IsAsciiLetterUpper(label[0])) return true;

        // artifact-N format
        if (label.StartsWith("artifact-", StringComparison.OrdinalIgnoreCase))
            return true;

        // output-N format
        if (label.StartsWith("output-", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>
    /// Check if a label contains leakage (skill names, version indicators).
    /// </summary>
    public static bool LabelContainsLeakage(string label)
    {
        foreach (var field in LeakageFields)
        {
            if (label.Contains(field, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Strip identity fields from a metadata JSON object.
    /// </summary>
    public static string StripMetadataIdentity(string jsonContent)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;
            var dict = new Dictionary<string, JsonElement>();

            foreach (var prop in root.EnumerateObject())
            {
                if (!LeakageFields.Contains(prop.Name))
                    dict[prop.Name] = prop.Value;
            }

            // Add blind marker
            var result = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
            var parsed = JsonSerializer.Deserialize<Dictionary<string, object>>(result)!;
            parsed["_blind_anonymized"] = true;
            return JsonSerializer.Serialize(parsed, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return jsonContent;
        }
    }

    /// <summary>
    /// Validate that a comparison setup is properly blinded.
    /// Returns (isBlind, issues).
    /// </summary>
    public static (bool IsBlind, List<string> Issues) ValidateBlindSetup(
        string artifactALabel, string artifactBLabel,
        string? artifactAPath = null, string? artifactBPath = null,
        string? metadata = null)
    {
        var issues = new List<string>();

        if (!IsAnonymousLabel(artifactALabel))
            issues.Add($"Artifact A label '{artifactALabel}' is not anonymous");

        if (!IsAnonymousLabel(artifactBLabel))
            issues.Add($"Artifact B label '{artifactBLabel}' is not anonymous");

        if (LabelContainsLeakage(artifactALabel))
            issues.Add($"Artifact A label '{artifactALabel}' contains identity leakage");

        if (LabelContainsLeakage(artifactBLabel))
            issues.Add($"Artifact B label '{artifactBLabel}' contains identity leakage");

        if (artifactAPath != null && PathContainsLeakage(artifactAPath))
            issues.Add($"Artifact A path contains leakage: {artifactAPath}");

        if (artifactBPath != null && PathContainsLeakage(artifactBPath))
            issues.Add($"Artifact B path contains leakage: {artifactBPath}");

        if (metadata != null)
        {
            foreach (var field in LeakageFields)
            {
                if (metadata.Contains($"\"{field}\"", StringComparison.OrdinalIgnoreCase) ||
                    metadata.Contains($"'{field}'", StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add($"Metadata contains identity field: {field}");
                }
            }
        }

        return (!issues.Any(), issues);
    }
}
