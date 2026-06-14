namespace Angri450.Nong.Literature.Dsl;

public enum CnkiTokenKind
{
    End,
    Word,
    Quoted,
    LeftParen,
    RightParen,
    Equal,
    Plus,
    Star,
    Minus,
    Comma,
    And,
    Or,
    Not,
    Between,
    Unsupported
}

public sealed record CnkiToken(CnkiTokenKind Kind, string Text, int Position)
{
    public string RawText => Text;
}

public sealed record CnkiParseIssue(
    string Id,
    string Severity,
    string Message,
    int? Position = null,
    string? Context = null);

public static class CnkiDslFields
{
    static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        "SU", "TI", "KY", "AB", "FT", "AU", "FI", "F", "AF", "JN", "RF", "YE",
        "FU", "CLC", "SN", "CN", "IB", "CF", "DOI"
    };

    public static IReadOnlyCollection<string> SupportedFields => Supported;

    public static bool TryNormalize(string field, out string normalized)
    {
        normalized = Normalize(field);
        return Supported.Contains(field);
    }

    public static string Normalize(string field)
    {
        if (string.Equals(field, "F", StringComparison.OrdinalIgnoreCase))
        {
            return "FI";
        }

        return string.IsNullOrWhiteSpace(field) ? string.Empty : field.ToUpperInvariant();
    }
}
