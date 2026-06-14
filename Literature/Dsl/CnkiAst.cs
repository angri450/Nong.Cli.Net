namespace Angri450.Nong.Literature.Dsl;

public enum CnkiBooleanOperator
{
    And,
    Or
}

public abstract class CnkiAstNode
{
    protected CnkiAstNode(int position)
    {
        Position = position;
    }

    public int Position { get; }
}

public sealed class CnkiTermNode : CnkiAstNode
{
    public CnkiTermNode(string? field, string value, bool isPhrase, int position, int? fieldPosition = null)
        : base(position)
    {
        Field = string.IsNullOrWhiteSpace(field) ? null : CnkiDslFields.Normalize(field);
        Value = value;
        IsPhrase = isPhrase;
        FieldPosition = fieldPosition;
    }

    public string? Field { get; }
    public int? FieldPosition { get; }
    public string Value { get; }
    public bool IsPhrase { get; }
    public bool IsBetween { get; init; }
    public string? BetweenStart { get; init; }
    public string? BetweenEnd { get; init; }
    public string EffectiveField => string.IsNullOrWhiteSpace(Field) ? "SU" : Field!;
}

public sealed class CnkiBinaryNode : CnkiAstNode
{
    public CnkiBinaryNode(CnkiBooleanOperator op, CnkiAstNode left, CnkiAstNode right, int position)
        : base(position)
    {
        Operator = op;
        Left = left;
        Right = right;
    }

    public CnkiBooleanOperator Operator { get; }
    public CnkiAstNode Left { get; }
    public CnkiAstNode Right { get; }
}

public sealed class CnkiNotNode : CnkiAstNode
{
    public CnkiNotNode(CnkiAstNode operand, int position)
        : base(position)
    {
        Operand = operand;
    }

    public CnkiAstNode Operand { get; }
}

public sealed class CnkiQuery
{
    public string Text { get; init; } = "";
    public string Source => Text;
    public CnkiAstNode? Root { get; init; }
    public IReadOnlyList<CnkiToken> Tokens { get; init; } = Array.Empty<CnkiToken>();
    public IReadOnlyList<CnkiParseIssue> Issues { get; init; } = Array.Empty<CnkiParseIssue>();
    public IReadOnlyList<CnkiParseIssue> Diagnostics => Issues;
    public IReadOnlyList<CnkiTermNode> Terms { get; init; } = Array.Empty<CnkiTermNode>();
    public bool IsValid => Root is not null && Issues.Count == 0;
    public bool HasDiagnostics => Issues.Count > 0;

    public override string ToString() => Text;
}

public sealed class CnkiValidationResult
{
    public bool IsValid => Issues.Count == 0 && Query.Root is not null;
    public CnkiQuery Query { get; init; } = new();
    public IReadOnlyList<CnkiParseIssue> Issues { get; init; } = Array.Empty<CnkiParseIssue>();
}

public sealed class CnkiNormalizedQuery
{
    public string Text { get; init; } = "";
    public string NormalizedExpression => Text;
    public IReadOnlyList<string> Fields { get; init; } = Array.Empty<string>();
    public IReadOnlyList<CnkiParsedField> ParsedFields { get; init; } = Array.Empty<CnkiParsedField>();
    public IReadOnlyList<string> Concepts { get; init; } = Array.Empty<string>();
    public IReadOnlyList<CnkiConceptGroup> ConceptGroups { get; init; } = Array.Empty<CnkiConceptGroup>();
    public IReadOnlyList<CnkiTermNode> Terms { get; init; } = Array.Empty<CnkiTermNode>();
    public IReadOnlyList<CnkiYearRange> YearRanges { get; init; } = Array.Empty<CnkiYearRange>();
}

public sealed record CnkiParsedField(
    string Field,
    string NormalizedField,
    IReadOnlyList<string> Terms,
    int Position);

public sealed record CnkiConceptGroup(
    string? Field,
    IReadOnlyList<string> Alternatives,
    int Position);

public sealed record CnkiYearRange(
    int? StartYear,
    int? EndYear,
    int Position);
