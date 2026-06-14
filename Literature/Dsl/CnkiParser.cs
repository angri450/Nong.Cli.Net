namespace Angri450.Nong.Literature.Dsl;

public sealed class CnkiParser
{
    readonly IReadOnlyList<CnkiToken> _tokens;
    readonly List<CnkiParseIssue> _issues = new();
    string _text = string.Empty;
    int _index;

    CnkiParser(IReadOnlyList<CnkiToken> tokens)
    {
        _tokens = tokens;
    }

    public static CnkiQuery Parse(string text)
    {
        var tokens = CnkiLexer.Tokenize(text ?? string.Empty);
        var parser = new CnkiParser(tokens);
        return parser.ParseInternal(text ?? string.Empty);
    }

    CnkiQuery ParseInternal(string text)
    {
        _text = text;
        foreach (var token in _tokens.Where(t => t.Kind == CnkiTokenKind.Unsupported))
        {
            _issues.Add(new CnkiParseIssue(
                "E006",
                "Error",
                $"Unsupported CNKI operator '{token.Text}' at position {token.Position}.",
                token.Position,
                Context(token.Position)));
        }

        CnkiAstNode? root = null;
        if (Current.Kind != CnkiTokenKind.End)
        {
            root = ParseOr(null);
        }

        if (Current.Kind != CnkiTokenKind.End)
        {
            _issues.Add(new CnkiParseIssue(
                "E006",
                "Error",
                $"Unexpected token '{Current.Text}' at position {Current.Position}.",
                Current.Position,
                Context(Current.Position)));
        }

        var terms = new List<CnkiTermNode>();
        CollectTerms(root, terms);
        return new CnkiQuery
        {
            Text = text,
            Root = root,
            Tokens = _tokens,
            Issues = _issues,
            Terms = terms
        };
    }

    CnkiToken Current => _tokens[Math.Min(_index, _tokens.Count - 1)];

    CnkiToken Peek(int offset = 1) => _tokens[Math.Min(_index + offset, _tokens.Count - 1)];

    CnkiToken Advance()
    {
        var current = Current;
        if (_index < _tokens.Count - 1)
            _index++;
        return current;
    }

    bool Match(CnkiTokenKind kind)
    {
        if (Current.Kind != kind)
            return false;
        Advance();
        return true;
    }

    CnkiAstNode ParseOr(string? fieldContext)
    {
        var node = ParseAnd(fieldContext);
        while (Current.Kind is CnkiTokenKind.Or or CnkiTokenKind.Plus)
        {
            var op = Advance();
            node = new CnkiBinaryNode(CnkiBooleanOperator.Or, node, ParseAnd(fieldContext), op.Position);
        }

        return node;
    }

    CnkiAstNode ParseAnd(string? fieldContext)
    {
        var node = ParseNot(fieldContext);
        while (Current.Kind is CnkiTokenKind.And or CnkiTokenKind.Star or CnkiTokenKind.Minus or CnkiTokenKind.Unsupported)
        {
            var op = Advance();
            SkipUnsupportedDistance(op);
            var right = ParseNot(fieldContext);
            node = op.Kind == CnkiTokenKind.Minus
                ? new CnkiBinaryNode(CnkiBooleanOperator.And, node, new CnkiNotNode(right, op.Position), op.Position)
                : new CnkiBinaryNode(CnkiBooleanOperator.And, node, right, op.Position);
        }

        return node;
    }

    CnkiAstNode ParseNot(string? fieldContext)
    {
        if (Current.Kind is CnkiTokenKind.Not or CnkiTokenKind.Minus)
        {
            var op = Advance();
            return new CnkiNotNode(ParsePrimary(fieldContext), op.Position);
        }

        return ParsePrimary(fieldContext);
    }

    CnkiAstNode ParsePrimary(string? fieldContext)
    {
        if (Match(CnkiTokenKind.LeftParen))
        {
            var node = ParseOr(fieldContext);
            if (!Match(CnkiTokenKind.RightParen))
            {
                _issues.Add(new CnkiParseIssue("E006", "Error", $"Missing ')' at position {Current.Position}.", Current.Position, Context(Current.Position)));
            }

            return node;
        }

        if (Current.Kind == CnkiTokenKind.Word && Peek().Kind == CnkiTokenKind.Between)
        {
            var field = Advance();
            Advance();
            return ParseBetween(field.Text, field.Position);
        }

        if (Current.Kind == CnkiTokenKind.Word && Peek().Kind == CnkiTokenKind.Equal)
        {
            var field = Advance();
            Advance();
            return ParseFieldClause(field.Text, field.Position);
        }

        return ParseTerm(fieldContext);
    }

    CnkiAstNode ParseFieldClause(string field, int fieldPosition)
    {
        var node = ParseFieldOperand(field, fieldPosition);
        while (Current.Kind is CnkiTokenKind.Plus or CnkiTokenKind.Star or CnkiTokenKind.Minus or CnkiTokenKind.Unsupported)
        {
            var op = Advance();
            SkipUnsupportedDistance(op);
            var right = ParseFieldOperand(field, fieldPosition);
            node = op.Kind switch
            {
                CnkiTokenKind.Plus => new CnkiBinaryNode(CnkiBooleanOperator.Or, node, right, op.Position),
                CnkiTokenKind.Star => new CnkiBinaryNode(CnkiBooleanOperator.And, node, right, op.Position),
                CnkiTokenKind.Minus => new CnkiBinaryNode(CnkiBooleanOperator.And, node, new CnkiNotNode(right, op.Position), op.Position),
                CnkiTokenKind.Unsupported => new CnkiBinaryNode(CnkiBooleanOperator.And, node, right, op.Position),
                _ => node
            };
        }

        return node;
    }

    CnkiAstNode ParseFieldOperand(string field, int fieldPosition)
    {
        if (Match(CnkiTokenKind.LeftParen))
        {
            var node = ParseOr(field);
            if (!Match(CnkiTokenKind.RightParen))
            {
                _issues.Add(new CnkiParseIssue("E006", "Error", $"Missing ')' at position {Current.Position}.", Current.Position, Context(Current.Position)));
            }

            return node;
        }

        return ParseTerm(field, fieldPosition);
    }

    CnkiAstNode ParseBetween(string field, int position)
    {
        Match(CnkiTokenKind.LeftParen);
        var start = ParseScalar();
        if (!Match(CnkiTokenKind.Comma))
        {
            _issues.Add(new CnkiParseIssue("E006", "Error", $"Expected ',' in BETWEEN expression at position {Current.Position}.", Current.Position, Context(Current.Position)));
        }

        var end = ParseScalar();
        if (!Match(CnkiTokenKind.RightParen))
        {
            _issues.Add(new CnkiParseIssue("E006", "Error", $"Missing ')' after BETWEEN range at position {Current.Position}.", Current.Position, Context(Current.Position)));
        }

        return new CnkiTermNode(field, $"{start}..{end}", true, position)
        {
            IsBetween = true,
            BetweenStart = start,
            BetweenEnd = end
        };
    }

    CnkiAstNode ParseTerm(string? field, int? fieldPosition = null)
    {
        var token = Current;
        if (token.Kind is not (CnkiTokenKind.Word or CnkiTokenKind.Quoted))
        {
            _issues.Add(new CnkiParseIssue("E006", "Error", $"Expected search term at position {token.Position}.", token.Position, Context(token.Position)));
            Advance();
            return new CnkiTermNode(field, string.Empty, false, token.Position, fieldPosition);
        }

        Advance();
        return new CnkiTermNode(field, token.Text, token.Kind == CnkiTokenKind.Quoted, token.Position, fieldPosition);
    }

    string ParseScalar()
    {
        if (Current.Kind is not (CnkiTokenKind.Word or CnkiTokenKind.Quoted))
        {
            _issues.Add(new CnkiParseIssue("E006", "Error", $"Expected scalar value at position {Current.Position}.", Current.Position, Context(Current.Position)));
            return string.Empty;
        }

        return Advance().Text;
    }

    void SkipUnsupportedDistance(CnkiToken op)
    {
        if (op.Kind != CnkiTokenKind.Unsupported
            || !op.Text.StartsWith("/", StringComparison.Ordinal)
            || Current.Kind != CnkiTokenKind.Word
            || !Current.Text.All(char.IsDigit))
        {
            return;
        }

        Advance();
    }

    string Context(int position)
    {
        if (string.IsNullOrEmpty(_text))
        {
            return string.Empty;
        }

        var start = Math.Max(0, position - 16);
        var end = Math.Min(_text.Length, position + 17);
        return _text[start..end];
    }

    static void CollectTerms(CnkiAstNode? node, List<CnkiTermNode> terms)
    {
        switch (node)
        {
            case null:
                return;
            case CnkiTermNode term:
                terms.Add(term);
                return;
            case CnkiBinaryNode binary:
                CollectTerms(binary.Left, terms);
                CollectTerms(binary.Right, terms);
                return;
            case CnkiNotNode not:
                CollectTerms(not.Operand, terms);
                return;
        }
    }
}
