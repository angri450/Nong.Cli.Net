using System.Text;

namespace Angri450.Nong.Literature.Dsl;

public sealed class CnkiParser
{
    static readonly Dictionary<string, CnkiProximityKind> ProximityOps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/SEN"]  = CnkiProximityKind.Sen,
        ["/NEAR"] = CnkiProximityKind.Near,
        ["/PREV"] = CnkiProximityKind.Prev,
        ["/AFT"]  = CnkiProximityKind.Aft,
        ["/PRG"]  = CnkiProximityKind.Prg
    };

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

    bool IsOrOp(CnkiTokenKind kind) => kind is CnkiTokenKind.Or or CnkiTokenKind.Plus;

    bool IsAndOp(CnkiTokenKind kind) => kind is CnkiTokenKind.And or CnkiTokenKind.Star or CnkiTokenKind.Minus;

    bool IsNotOp(CnkiTokenKind kind) => kind is CnkiTokenKind.Not or CnkiTokenKind.Minus;

    CnkiAstNode ParseOr(string? fieldContext)
    {
        var node = ParseAnd(fieldContext);
        while (IsOrOp(Current.Kind))
        {
            var op = Advance();
            node = new CnkiBinaryNode(CnkiBooleanOperator.Or, node, ParseAnd(fieldContext), op.Position);
        }
        return node;
    }

    CnkiAstNode ParseAnd(string? fieldContext)
    {
        var node = ParseNot(fieldContext);
        while (IsAndOp(Current.Kind) || IsNotOp(Current.Kind))
        {
            var op = Advance();
            var right = ParseNot(fieldContext);
            node = op.Kind == CnkiTokenKind.Minus || op.Kind == CnkiTokenKind.Not
                ? new CnkiBinaryNode(CnkiBooleanOperator.And, node, new CnkiNotNode(right, op.Position), op.Position)
                : new CnkiBinaryNode(CnkiBooleanOperator.And, node, right, op.Position);
        }
        return node;
    }

    CnkiAstNode ParseNot(string? fieldContext)
    {
        if (IsNotOp(Current.Kind))
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
                _issues.Add(new CnkiParseIssue("E006", "Error",
                    $"Missing ')' at position {Current.Position}.", Current.Position, Context(Current.Position)));
            }
            return node;
        }

        // FUZZY: field % 'str'  (e.g. TI%'转基因')
        if (Current.Kind == CnkiTokenKind.Word && Peek().Kind == CnkiTokenKind.FuzzyQuoted)
        {
            var field = Advance();
            var fuzzy = Advance();
            return new CnkiTermNode(field.Text, fuzzy.Text, true, field.Position, field.Position)
            { IsFuzzy = true };
        }

        // RELEVANCE: field %= 'str'  (e.g. SU %= '大数据')
        if (Current.Kind == CnkiTokenKind.Word && Peek().Kind == CnkiTokenKind.FuzzyOp)
        {
            var field = Advance();
            Advance(); // consume %=
            var operand = ParseFieldOperand(field.Text, field.Position);
            if (operand is CnkiTermNode t)
                return new CnkiTermNode(t.Field, t.Value, t.IsPhrase, t.Position, t.FieldPosition)
                { IsBetween = t.IsBetween, BetweenStart = t.BetweenStart, BetweenEnd = t.BetweenEnd,
                  IsFuzzy = true, MinFrequency = t.MinFrequency };
            return operand;
        }

        // BETWEEN: field BETWEEN('start','end')
        if (Current.Kind == CnkiTokenKind.Word && Peek().Kind == CnkiTokenKind.Between)
        {
            var field = Advance();
            Advance();
            return ParseBetween(field.Text, field.Position);
        }

        // field = clause
        if (Current.Kind == CnkiTokenKind.Word && Peek().Kind == CnkiTokenKind.Equal)
        {
            var field = Advance();
            Advance();
            return ParseFieldClause(field.Text, field.Position);
        }

        // Standalone fuzzy quoted term: %'str' without field prefix
        if (Current.Kind == CnkiTokenKind.FuzzyQuoted)
        {
            var token = Advance();
            return new CnkiTermNode(fieldContext, token.Text, true, token.Position) { IsFuzzy = true };
        }

        return ParseTerm(fieldContext);
    }

    CnkiAstNode ParseFieldClause(string field, int fieldPosition)
    {
        var node = ParseFieldOperand(field, fieldPosition);

        while (true)
        {
            // Peek ahead: if the next operator is followed by a field assignment (field=, field%, field BETWEEN),
            // break out and let ParseOr/ParseAnd handle it at the query level.
            var p1 = Peek(1);
            var nextIsFieldAssign = p1.Kind == CnkiTokenKind.Word
                && (Peek(2).Kind is CnkiTokenKind.Equal or CnkiTokenKind.FuzzyQuoted or CnkiTokenKind.FuzzyOp or CnkiTokenKind.Between);

            // Boolean operators: + * - AND OR NOT
            if (IsOrOp(Current.Kind) && !nextIsFieldAssign)
            {
                var op = Advance();
                var right = ParseFieldOperand(field, fieldPosition);
                node = new CnkiBinaryNode(CnkiBooleanOperator.Or, node, right, op.Position);
                continue;
            }

            if ((IsAndOp(Current.Kind) || IsNotOp(Current.Kind)) && !nextIsFieldAssign)
            {
                var op = Advance();
                var right = ParseFieldOperand(field, fieldPosition);
                node = op.Kind == CnkiTokenKind.Minus || op.Kind == CnkiTokenKind.Not
                    ? new CnkiBinaryNode(CnkiBooleanOperator.And, node, new CnkiNotNode(right, op.Position), op.Position)
                    : new CnkiBinaryNode(CnkiBooleanOperator.And, node, right, op.Position);
                continue;
            }

            // Proximity operator outside quotes: 'str1'/NEAR N 'str2'
            if (Current.Kind == CnkiTokenKind.ProximityOp
                && ProximityOps.TryGetValue(Current.Text, out var pk))
            {
                var op = Advance();
                var distance = ParseProximityDistance();
                var right = ParseFieldOperand(field, fieldPosition);

                // node must be a term (left side of proximity)
                var leftTerm = AsTerm(node, field, fieldPosition, op.Position);
                var rightTerm = AsTerm(right, field, fieldPosition, op.Position);
                if (leftTerm != null && rightTerm != null)
                {
                    node = new CnkiProximityNode(pk, leftTerm, rightTerm, distance, op.Position)
                    { Field = field };
                }
                else
                {
                    _issues.Add(new CnkiParseIssue("E006", "Error",
                        $"Proximity operator '{op.Text}' requires term operands at position {op.Position}.",
                        op.Position, Context(op.Position)));
                }
                continue;
            }

            // Sub-position operator: 'str'/SUB N
            if (Current.Kind == CnkiTokenKind.SubOp)
            {
                var op = Advance();
                var distance = ParseProximityDistance();
                if (node is CnkiTermNode subTerm)
                {
                    subTerm = new CnkiTermNode(subTerm.Field, subTerm.Value, subTerm.IsPhrase, subTerm.Position, subTerm.FieldPosition)
                    { IsBetween = false, IsFuzzy = subTerm.IsFuzzy, MinFrequency = distance };
                }
                else
                {
                    _issues.Add(new CnkiParseIssue("E006", "Error",
                        $"/SUB requires a term on the left at position {op.Position}.",
                        op.Position, Context(op.Position)));
                }
                continue;
            }

            // Word frequency suffix at field-clause level: 'str' $ N
            if (Current.Kind == CnkiTokenKind.WordFreq)
            {
                var wf = Advance();
                if (int.TryParse(wf.Text.TrimStart('$'), out var freq) && node is CnkiTermNode freqTerm)
                {
                    node = new CnkiTermNode(freqTerm.Field, freqTerm.Value, freqTerm.IsPhrase, freqTerm.Position, freqTerm.FieldPosition)
                    { IsBetween = freqTerm.IsBetween, IsFuzzy = freqTerm.IsFuzzy, MinFrequency = freq };
                }
                continue;
            }

            break;
        }

        return node;
    }

    int ParseProximityDistance()
    {
        if (Current.Kind != CnkiTokenKind.Word || !int.TryParse(Current.Text, out var n))
        {
            _issues.Add(new CnkiParseIssue("E006", "Error",
                $"Expected numeric distance after proximity operator at position {Current.Position}.",
                Current.Position, Context(Current.Position)));
            return 0;
        }
        Advance();
        return n;
    }

    CnkiAstNode ParseFieldOperand(string field, int fieldPosition)
    {
        if (Match(CnkiTokenKind.LeftParen))
        {
            var node = ParseOr(field);
            if (!Match(CnkiTokenKind.RightParen))
            {
                _issues.Add(new CnkiParseIssue("E006", "Error",
                    $"Missing ')' at position {Current.Position}.", Current.Position, Context(Current.Position)));
            }
            return node;
        }

        // Fuzzy quoted in field context
        if (Current.Kind == CnkiTokenKind.FuzzyQuoted)
        {
            var token = Advance();
            return new CnkiTermNode(field, token.Text, true, token.Position, fieldPosition) { IsFuzzy = true };
        }

        return ParseTerm(field, fieldPosition);
    }

    CnkiAstNode ParseBetween(string field, int position)
    {
        Match(CnkiTokenKind.LeftParen);
        var start = ParseScalar();
        if (!Match(CnkiTokenKind.Comma))
        {
            _issues.Add(new CnkiParseIssue("E006", "Error",
                $"Expected ',' in BETWEEN expression at position {Current.Position}.",
                Current.Position, Context(Current.Position)));
        }

        var end = ParseScalar();
        if (!Match(CnkiTokenKind.RightParen))
        {
            _issues.Add(new CnkiParseIssue("E006", "Error",
                $"Missing ')' after BETWEEN range at position {Current.Position}.",
                Current.Position, Context(Current.Position)));
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
        if (token.Kind is not (CnkiTokenKind.Word or CnkiTokenKind.Quoted or CnkiTokenKind.FuzzyQuoted))
        {
            _issues.Add(new CnkiParseIssue("E006", "Error",
                $"Expected search term at position {token.Position}.", token.Position, Context(token.Position)));
            Advance();
            return new CnkiTermNode(field, string.Empty, false, token.Position, fieldPosition);
        }

        Advance();
        var text = token.Text;
        var isPhrase = token.Kind is CnkiTokenKind.Quoted or CnkiTokenKind.FuzzyQuoted;

        // Detect internal proximity operators within quoted strings: 'str1/NEAR N str2'
        if (isPhrase && TryParseQuotedProximity(text, out var left, out var pk, out var dist, out var right))
        {
            var leftTerm = new CnkiTermNode(field, left, true, token.Position, fieldPosition);
            var rightTerm = new CnkiTermNode(field, right, true, token.Position, fieldPosition);
            return new CnkiProximityNode(pk, leftTerm, rightTerm, dist, token.Position) { Field = field };
        }

        // Detect word frequency within quoted strings: 'str $ N'
        if (isPhrase && TryParseWordFreq(text, out var wfWord, out var wfFreq))
        {
            return new CnkiTermNode(field, wfWord, true, token.Position, fieldPosition)
            { MinFrequency = wfFreq, IsFuzzy = token.Kind == CnkiTokenKind.FuzzyQuoted };
        }

        return new CnkiTermNode(field, text, isPhrase, token.Position, fieldPosition)
        {
            IsFuzzy = token.Kind == CnkiTokenKind.FuzzyQuoted
        };
    }

    string ParseScalar()
    {
        if (Current.Kind is not (CnkiTokenKind.Word or CnkiTokenKind.Quoted))
        {
            _issues.Add(new CnkiParseIssue("E006", "Error",
                $"Expected scalar value at position {Current.Position}.", Current.Position, Context(Current.Position)));
            return string.Empty;
        }
        return Advance().Text;
    }

    string Context(int position)
    {
        if (string.IsNullOrEmpty(_text)) return string.Empty;
        var start = Math.Max(0, position - 16);
        var end = Math.Min(_text.Length, position + 17);
        return _text[start..end];
    }

    // ── helpers ──────────────────────────────────────────────

    static CnkiTermNode? AsTerm(CnkiAstNode node, string? field, int fieldPosition, int position)
    {
        return node switch
        {
            CnkiTermNode t => t,
            _ => null
        };
    }

    static bool TryParseQuotedProximity(string text, out string left, out CnkiProximityKind kind, out int distance, out string right)
    {
        // Check for # operator inside quotes: 'STR1 # STR2' (same sentence, unordered)
        var hashIdx = text.IndexOf(" # ", StringComparison.Ordinal);
        if (hashIdx >= 0)
        {
            left = text[..hashIdx].Trim();
            right = text[(hashIdx + 3)..].Trim();
            if (!string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right))
            {
                kind = CnkiProximityKind.SameSentence;
                distance = 0;
                return true;
            }
        }

        // Check for % operator inside quotes: 'STR1 % STR2' (same sentence, ordered)
        // Must check BEFORE slash operators to avoid false matches with e.g. /NEAR
        var pctIdx = text.IndexOf(" % ", StringComparison.Ordinal);
        if (pctIdx >= 0)
        {
            left = text[..pctIdx].Trim();
            right = text[(pctIdx + 3)..].Trim();
            if (!string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right))
            {
                kind = CnkiProximityKind.SameSentenceOrdered;
                distance = 0;
                return true;
            }
        }

        // Check for slash proximity operators: 'str1/NEAR N str2' etc.
        foreach (var kv in ProximityOps)
        {
            var idx = text.IndexOf(kv.Key, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) continue;

            var leftPart = text[..idx].Trim();
            var afterOp = text[(idx + kv.Key.Length)..].TrimStart();

            if (string.IsNullOrWhiteSpace(afterOp)) continue;

            // Parse distance N and right term.
            // CNKI canonical form: "N str2" (space-separated).
            // Lenient form: "Nstr2" (no space, e.g. "5水稻").
            var spaceIdx = afterOp.IndexOf(' ');
            string distStr, rightPart;
            if (spaceIdx >= 0)
            {
                distStr = afterOp[..spaceIdx];
                rightPart = afterOp[(spaceIdx + 1)..].Trim();
            }
            else
            {
                // No space — split at the first non-digit character
                var split = 0;
                while (split < afterOp.Length && char.IsDigit(afterOp[split]))
                    split++;
                if (split == 0) continue;
                distStr = afterOp[..split];
                rightPart = afterOp[split..].TrimStart();
            }

            if (!int.TryParse(distStr, out var n)) continue;
            if (string.IsNullOrWhiteSpace(leftPart) || string.IsNullOrWhiteSpace(rightPart))
                continue;

            left = leftPart;
            kind = kv.Value;
            distance = n;
            right = rightPart;
            return true;
        }

        left = right = "";
        kind = default;
        distance = 0;
        return false;
    }

    static bool TryParseWordFreq(string text, out string word, out int freq)
    {
        // Pattern: "word $ N"
        var idx = text.IndexOf(" $ ", StringComparison.Ordinal);
        if (idx < 0) { word = ""; freq = 0; return false; }

        word = text[..idx].Trim();
        var after = text[(idx + 3)..].Trim();

        if (string.IsNullOrWhiteSpace(word) || !int.TryParse(after, out freq))
        { word = ""; freq = 0; return false; }

        return true;
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
            case CnkiProximityNode prox:
                terms.Add(prox.Left);
                terms.Add(prox.Right);
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
