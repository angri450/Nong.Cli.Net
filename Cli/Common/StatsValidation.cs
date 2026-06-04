using System.Globalization;

namespace Nong.Cli.Common;

/// <summary>
/// Pre-flight validation for statistical data inputs.
/// </summary>
public static class StatsValidation
{
    /// <summary>Validate grouped data. Returns null on success, error on failure.</summary>
    public static ErrorEntry? Validate(Dictionary<string, List<double>> groups, string commandName)
    {
        if (groups.Count < 2)
            return ErrorCodes.ValidationFailed with { Message = $"[{commandName}] Need at least 2 groups, got {groups.Count}." };

        foreach (var (name, values) in groups)
        {
            if (values.Count == 0)
                return ErrorCodes.ValidationFailed with { Message = $"[{commandName}] Group '{name}' is empty." };
            if (values.Count == 1)
                return ErrorCodes.ValidationFailed with { Message = $"[{commandName}] Group '{name}' has only 1 observation. Need ≥2 for variance estimation." };
            if (values.Any(v => double.IsNaN(v) || double.IsInfinity(v)))
                return ErrorCodes.ValidationFailed with { Message = $"[{commandName}] Group '{name}' contains NaN or Infinity values." };
        }
        return null;
    }

    /// <summary>Format a method note about Duncan approximation.</summary>
    public const string DuncanMethodNote = "Duncan MRT uses simplified Q-value approximation. For formal publication, verify with SPSS/SAS/R.";
}
