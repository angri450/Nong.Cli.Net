using System.Globalization;
using System.Text;

namespace Angri450.Nong.Literature.Pipeline;

internal sealed class CnkiBooleanParseResult
{
    public CnkiBooleanNode? Root { get; init; }

    public IReadOnlyList<LiteratureQueryTerm> Terms { get; init; } = Array.Empty<LiteratureQueryTerm>();

    public IReadOnlyList<string> Fields { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Concepts { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public bool IsValid => Errors.Count == 0 && Root is not null;
}

internal sealed class LiteratureQueryTerm
{
    public string? Field { get; init; }

    public string Value { get; init; } = string.Empty;

    public string? UpperValue { get; init; }

    public bool IsPhrase { get; init; }

    public bool IsBetween { get; init; }

    public string? BetweenStart { get; init; }

    public string? BetweenEnd { get; init; }

    public string DisplayField => string.IsNullOrWhiteSpace(Field) ? "SU" : Field!;

    public bool IsPositiveConcept =>
        !IsBetween &&
        !string.Equals(DisplayField, "YE", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(DisplayField, "CF", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(DisplayField, "DOI", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(Value);
}

internal enum CnkiBooleanNodeKind
{
    Term,
    And,
    Or,
    Not
}

internal sealed class CnkiBooleanNode
{
    private CnkiBooleanNode(CnkiBooleanNodeKind kind)
    {
        Kind = kind;
    }

    public CnkiBooleanNodeKind Kind { get; }

    public LiteratureQueryTerm? Term { get; private init; }

    public CnkiBooleanNode? Left { get; private init; }

    public CnkiBooleanNode? Right { get; private init; }

    public static CnkiBooleanNode FromTerm(LiteratureQueryTerm term) => new(CnkiBooleanNodeKind.Term)
    {
        Term = term
    };

    public static CnkiBooleanNode And(CnkiBooleanNode left, CnkiBooleanNode right) => new(CnkiBooleanNodeKind.And)
    {
        Left = left,
        Right = right
    };

    public static CnkiBooleanNode Or(CnkiBooleanNode left, CnkiBooleanNode right) => new(CnkiBooleanNodeKind.Or)
    {
        Left = left,
        Right = right
    };

    public static CnkiBooleanNode Not(CnkiBooleanNode node) => new(CnkiBooleanNodeKind.Not)
    {
        Left = node
    };
}

internal static class CnkiBooleanExpression
{
    public static CnkiBooleanParseResult Parse(string query)
    {
        var lexer = new Lexer(query);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        return parser.Parse();
    }

    public static IReadOnlyList<IReadOnlyList<LiteratureQueryTerm>> ToPositiveConjunctions(CnkiBooleanNode? node)
    {
        if (node is null)
        {
            return Array.Empty<IReadOnlyList<LiteratureQueryTerm>>();
        }

        var result = ToDnf(node)
            .Select(group => (IReadOnlyList<LiteratureQueryTerm>)group
                .Where(term => term.IsPositiveConcept || string.Equals(term.DisplayField, "DOI", StringComparison.OrdinalIgnoreCase))
                .ToArray())
            .Where(group => group.Count > 0)
            .ToArray();

        return result.Length == 0 ? new[] { Array.Empty<LiteratureQueryTerm>() } : result;
    }

    public static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var rune in value.Normalize(NormalizationForm.FormKC))
        {
            if (char.IsControl(rune))
            {
                continue;
            }

            builder.Append(char.IsWhiteSpace(rune) ? ' ' : char.ToLowerInvariant(rune));
        }

        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static IReadOnlyList<List<LiteratureQueryTerm>> ToDnf(CnkiBooleanNode node)
    {
        switch (node.Kind)
        {
            case CnkiBooleanNodeKind.Term:
                return new[] { new List<LiteratureQueryTerm> { node.Term! } };

            case CnkiBooleanNodeKind.Not:
                return new[] { new List<LiteratureQueryTerm>() };

            case CnkiBooleanNodeKind.Or:
                return ToDnf(node.Left!)
                    .Concat(ToDnf(node.Right!))
                    .Select(group => group.ToList())
                    .ToArray();

            case CnkiBooleanNodeKind.And:
                var left = ToDnf(node.Left!);
                var right = ToDnf(node.Right!);
                var combined = new List<List<LiteratureQueryTerm>>();
                foreach (var leftGroup in left)
                {
                    foreach (var rightGroup in right)
                    {
                        combined.Add(leftGroup.Concat(rightGroup).ToList());
                    }
                }

                return combined;

            default:
                return Array.Empty<List<LiteratureQueryTerm>>();
        }
    }

    private enum TokenKind
    {
        End,
        Word,
        Quoted,
        LParen,
        RParen,
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

    private readonly record struct Token(TokenKind Kind, string Text, int Position);

    private sealed class Lexer
    {
        private static readonly HashSet<string> UnsupportedSlashOperators = new(StringComparer.OrdinalIgnoreCase)
        {
            "/SEN",
            "/NEAR",
            "/PREV",
            "/AFT",
            "/PRG"
        };

        private readonly string _text;
        private int _index;

        public Lexer(string text)
        {
            _text = text ?? string.Empty;
        }

        public IReadOnlyList<Token> Tokenize()
        {
            var tokens = new List<Token>();
            while (_index < _text.Length)
            {
                var ch = _text[_index];
                if (char.IsWhiteSpace(ch))
                {
                    _index++;
                    continue;
                }

                var position = _index;
                switch (ch)
                {
                    case '(':
                        tokens.Add(new Token(TokenKind.LParen, "(", position));
                        _index++;
                        break;
                    case ')':
                        tokens.Add(new Token(TokenKind.RParen, ")", position));
                        _index++;
                        break;
                    case '=':
                        tokens.Add(new Token(TokenKind.Equal, "=", position));
                        _index++;
                        break;
                    case '+':
                        tokens.Add(new Token(TokenKind.Plus, "+", position));
                        _index++;
                        break;
                    case '*':
                        tokens.Add(new Token(TokenKind.Star, "*", position));
                        _index++;
                        break;
                    case '-':
                        tokens.Add(new Token(TokenKind.Minus, "-", position));
                        _index++;
                        break;
                    case ',':
                        tokens.Add(new Token(TokenKind.Comma, ",", position));
                        _index++;
                        break;
                    case '\'':
                    case '"':
                        tokens.Add(ReadQuoted(ch, position));
                        break;
                    case '%':
                        tokens.Add(new Token(TokenKind.Unsupported, "%", position));
                        _index++;
                        break;
                    case '/':
                        tokens.Add(ReadSlashOrWord(position));
                        break;
                    case '$':
                        tokens.Add(ReadDollar(position));
                        break;
                    default:
                        tokens.Add(ReadWord(position));
                        break;
                }
            }

            tokens.Add(new Token(TokenKind.End, string.Empty, _text.Length));
            return tokens;
        }

        private Token ReadQuoted(char quote, int position)
        {
            _index++;
            var builder = new StringBuilder();
            while (_index < _text.Length)
            {
                var ch = _text[_index++];
                if (ch == quote)
                {
                    return new Token(TokenKind.Quoted, builder.ToString(), position);
                }

                if (ch == '\\' && _index < _text.Length)
                {
                    builder.Append(_text[_index++]);
                    continue;
                }

                builder.Append(ch);
            }

            return new Token(TokenKind.Unsupported, "unterminated quote", position);
        }

        private Token ReadSlashOrWord(int position)
        {
            var word = ReadUntilBoundary();
            return UnsupportedSlashOperators.Any(op => word.StartsWith(op, StringComparison.OrdinalIgnoreCase))
                ? new Token(TokenKind.Unsupported, word, position)
                : ClassifyWord(word, position);
        }

        private Token ReadDollar(int position)
        {
            var word = ReadUntilBoundary();
            return word.Length > 1 && word.Skip(1).All(char.IsDigit)
                ? new Token(TokenKind.Unsupported, word, position)
                : ClassifyWord(word, position);
        }

        private Token ReadWord(int position)
        {
            return ClassifyWord(ReadUntilBoundary(), position);
        }

        private string ReadUntilBoundary()
        {
            var start = _index;
            while (_index < _text.Length)
            {
                var ch = _text[_index];
                if (char.IsWhiteSpace(ch) || ch is '(' or ')' or '=' or '+' or '*' or ',' or '\'' or '"')
                {
                    break;
                }

                if (ch == '-' && _index == start)
                {
                    break;
                }

                _index++;
            }

            if (_index == start)
            {
                _index++;
            }

            return _text[start.._index];
        }

        private static Token ClassifyWord(string word, int position)
        {
            return word.ToUpperInvariant() switch
            {
                "AND" => new Token(TokenKind.And, word, position),
                "OR" => new Token(TokenKind.Or, word, position),
                "NOT" => new Token(TokenKind.Not, word, position),
                "BETWEEN" => new Token(TokenKind.Between, word, position),
                _ => new Token(TokenKind.Word, word, position)
            };
        }
    }

    private sealed class Parser
    {
        private readonly IReadOnlyList<Token> _tokens;
        private readonly List<string> _errors = new();
        private int _index;

        public Parser(IReadOnlyList<Token> tokens)
        {
            _tokens = tokens;
        }

        public CnkiBooleanParseResult Parse()
        {
            if (_tokens.Any(token => token.Kind == TokenKind.Unsupported))
            {
                foreach (var unsupported in _tokens.Where(token => token.Kind == TokenKind.Unsupported))
                {
                    _errors.Add($"Unsupported CNKI DSL operator or token '{unsupported.Text}' at {unsupported.Position}.");
                }
            }

            var root = Current.Kind == TokenKind.End ? null : ParseOr(null);
            if (Current.Kind != TokenKind.End)
            {
                _errors.Add($"Unexpected token '{Current.Text}' at {Current.Position}.");
            }

            var terms = new List<LiteratureQueryTerm>();
            CollectTerms(root, terms);
            var fields = terms
                .Select(term => term.DisplayField.ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(field => field, StringComparer.Ordinal)
                .ToArray();
            var concepts = terms
                .Where(term => term.IsPositiveConcept)
                .Select(term => term.Value)
                .Where(term => !string.IsNullOrWhiteSpace(term))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(term => term, StringComparer.Ordinal)
                .ToArray();

            return new CnkiBooleanParseResult
            {
                Root = root,
                Terms = terms,
                Fields = fields,
                Concepts = concepts,
                Errors = _errors
            };
        }

        private Token Current => _tokens[Math.Min(_index, _tokens.Count - 1)];

        private Token Peek(int offset = 1) => _tokens[Math.Min(_index + offset, _tokens.Count - 1)];

        private Token Advance()
        {
            var current = Current;
            if (_index < _tokens.Count - 1)
            {
                _index++;
            }

            return current;
        }

        private bool Match(TokenKind kind)
        {
            if (Current.Kind != kind)
            {
                return false;
            }

            Advance();
            return true;
        }

        private CnkiBooleanNode ParseOr(string? fieldContext)
        {
            var node = ParseAnd(fieldContext);
            while (Current.Kind is TokenKind.Or or TokenKind.Plus)
            {
                Advance();
                node = CnkiBooleanNode.Or(node, ParseAnd(fieldContext));
            }

            return node;
        }

        private CnkiBooleanNode ParseAnd(string? fieldContext)
        {
            var node = ParseNot(fieldContext);
            while (Current.Kind is TokenKind.And or TokenKind.Star)
            {
                Advance();
                node = CnkiBooleanNode.And(node, ParseNot(fieldContext));
            }

            return node;
        }

        private CnkiBooleanNode ParseNot(string? fieldContext)
        {
            if (Current.Kind is TokenKind.Not or TokenKind.Minus)
            {
                Advance();
                return CnkiBooleanNode.Not(ParsePrimary(fieldContext));
            }

            return ParsePrimary(fieldContext);
        }

        private CnkiBooleanNode ParsePrimary(string? fieldContext)
        {
            if (Match(TokenKind.LParen))
            {
                var node = ParseOr(fieldContext);
                if (!Match(TokenKind.RParen))
                {
                    _errors.Add($"Expected ')' at {Current.Position}.");
                }

                return node;
            }

            if (Current.Kind == TokenKind.Word && Peek().Kind == TokenKind.Between)
            {
                var field = Advance().Text;
                Advance();
                return ParseBetween(field);
            }

            if (Current.Kind == TokenKind.Word && Peek().Kind == TokenKind.Equal)
            {
                var field = Advance().Text;
                Advance();
                return ParseFieldClause(field);
            }

            return ParseTerm(fieldContext);
        }

        private CnkiBooleanNode ParseFieldClause(string field)
        {
            var node = ParseFieldOperand(field);
            while (Current.Kind is TokenKind.Plus or TokenKind.Star or TokenKind.Minus)
            {
                var op = Advance().Kind;
                var right = ParseFieldOperand(field);
                node = op switch
                {
                    TokenKind.Plus => CnkiBooleanNode.Or(node, right),
                    TokenKind.Star => CnkiBooleanNode.And(node, right),
                    TokenKind.Minus => CnkiBooleanNode.And(node, CnkiBooleanNode.Not(right)),
                    _ => node
                };
            }

            return node;
        }

        private CnkiBooleanNode ParseFieldOperand(string field)
        {
            if (Match(TokenKind.LParen))
            {
                var node = ParseOr(field);
                if (!Match(TokenKind.RParen))
                {
                    _errors.Add($"Expected ')' at {Current.Position}.");
                }

                return node;
            }

            return ParseTerm(field);
        }

        private CnkiBooleanNode ParseBetween(string field)
        {
            Match(TokenKind.LParen);
            var start = ParseScalar();
            if (!Match(TokenKind.Comma))
            {
                _errors.Add($"Expected ',' in BETWEEN at {Current.Position}.");
            }

            var end = ParseScalar();
            Match(TokenKind.RParen);
            return CnkiBooleanNode.FromTerm(new LiteratureQueryTerm
            {
                Field = field.ToUpperInvariant(),
                Value = $"{start}..{end}",
                IsBetween = true,
                BetweenStart = start,
                BetweenEnd = end
            });
        }

        private CnkiBooleanNode ParseTerm(string? field)
        {
            var token = Current;
            if (token.Kind is not (TokenKind.Word or TokenKind.Quoted))
            {
                _errors.Add($"Expected search term at {token.Position}.");
                Advance();
                return CnkiBooleanNode.FromTerm(new LiteratureQueryTerm
                {
                    Field = field,
                    Value = string.Empty
                });
            }

            Advance();
            return CnkiBooleanNode.FromTerm(new LiteratureQueryTerm
            {
                Field = field?.ToUpperInvariant(),
                Value = token.Text,
                UpperValue = token.Text.ToUpper(CultureInfo.InvariantCulture),
                IsPhrase = token.Kind == TokenKind.Quoted
            });
        }

        private string ParseScalar()
        {
            if (Current.Kind is not (TokenKind.Word or TokenKind.Quoted))
            {
                _errors.Add($"Expected scalar value at {Current.Position}.");
                return string.Empty;
            }

            return Advance().Text;
        }

        private static void CollectTerms(CnkiBooleanNode? node, List<LiteratureQueryTerm> terms)
        {
            if (node is null)
            {
                return;
            }

            if (node.Kind == CnkiBooleanNodeKind.Term && node.Term is not null)
            {
                terms.Add(node.Term);
            }

            CollectTerms(node.Left, terms);
            CollectTerms(node.Right, terms);
        }
    }
}
