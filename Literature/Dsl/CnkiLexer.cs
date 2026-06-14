using System.Text;

namespace Angri450.Nong.Literature.Dsl;

public sealed class CnkiLexer
{
    static readonly Dictionary<string, CnkiProximityKind> ProximityOps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/SEN"]  = CnkiProximityKind.Sen,
        ["/NEAR"] = CnkiProximityKind.Near,
        ["/PREV"] = CnkiProximityKind.Prev,
        ["/AFT"]  = CnkiProximityKind.Aft,
        ["/PRG"]  = CnkiProximityKind.Prg
    };

    static readonly HashSet<string> ProximityOpKeys = new(
        ProximityOps.Keys, StringComparer.OrdinalIgnoreCase);

    readonly string _text;
    int _index;

    public CnkiLexer(string text)
    {
        _text = text ?? string.Empty;
    }

    public static IReadOnlyList<CnkiToken> Tokenize(string text) => new CnkiLexer(text).Tokenize();

    public IReadOnlyList<CnkiToken> Tokenize()
    {
        var tokens = new List<CnkiToken>();
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
                    tokens.Add(new CnkiToken(CnkiTokenKind.LeftParen, "(", position));
                    _index++;
                    break;
                case ')':
                    tokens.Add(new CnkiToken(CnkiTokenKind.RightParen, ")", position));
                    _index++;
                    break;
                case '=':
                    tokens.Add(new CnkiToken(CnkiTokenKind.Equal, "=", position));
                    _index++;
                    break;
                case '+':
                    tokens.Add(new CnkiToken(CnkiTokenKind.Plus, "+", position));
                    _index++;
                    break;
                case '*':
                    tokens.Add(new CnkiToken(CnkiTokenKind.Star, "*", position));
                    _index++;
                    break;
                case '-':
                    tokens.Add(new CnkiToken(CnkiTokenKind.Minus, "-", position));
                    _index++;
                    break;
                case ',':
                    tokens.Add(new CnkiToken(CnkiTokenKind.Comma, ",", position));
                    _index++;
                    break;
                case '%':
                    tokens.Add(ReadFuzzy(position));
                    break;
                case '#':
                    tokens.Add(new CnkiToken(CnkiTokenKind.Hash, "#", position));
                    _index++;
                    break;
                case '>':
                    tokens.Add(new CnkiToken(CnkiTokenKind.Unsupported, ">", position));
                    _index++;
                    break;
                case '<':
                    tokens.Add(new CnkiToken(CnkiTokenKind.Unsupported, "<", position));
                    _index++;
                    break;
                case '\'':
                case '"':
                    tokens.Add(ReadQuoted(ch, position));
                    break;
                case '/':
                    tokens.Add(ReadSlash(position));
                    break;
                case '$':
                    tokens.Add(ReadDollar(position));
                    break;
                default:
                    tokens.Add(ReadWord(position));
                    break;
            }
        }

        tokens.Add(new CnkiToken(CnkiTokenKind.End, string.Empty, _text.Length));
        return tokens;
    }

    CnkiToken ReadFuzzy(int position)
    {
        _index++;
        SkipSpaces();

        if (_index < _text.Length && _text[_index] == '=')
        {
            _index++;
            return new CnkiToken(CnkiTokenKind.FuzzyOp, "%=", position);
        }

        if (_index < _text.Length && (_text[_index] == '\'' || _text[_index] == '"'))
        {
            var quote = _text[_index];
            var quoted = ReadQuoted(quote, position);
            return quoted with { Kind = CnkiTokenKind.FuzzyQuoted };
        }

        // Bare % not followed by a quote or = — treat as Unsupported
        return new CnkiToken(CnkiTokenKind.Unsupported, "%", position);
    }

    CnkiToken ReadQuoted(char quote, int position)
    {
        _index++;
        var builder = new StringBuilder();
        while (_index < _text.Length)
        {
            var ch = _text[_index++];
            if (ch == quote)
                return new CnkiToken(CnkiTokenKind.Quoted, builder.ToString(), position);

            if (ch == '\\' && _index < _text.Length)
            {
                builder.Append(_text[_index++]);
                continue;
            }

            builder.Append(ch);
        }

        return new CnkiToken(CnkiTokenKind.Unsupported, "unterminated quote", position);
    }

    CnkiToken ReadSlash(int position)
    {
        var word = ReadUntilBoundary();

        if (ProximityOpKeys.Contains(word))
            return new CnkiToken(CnkiTokenKind.ProximityOp, word, position);

        if (string.Equals(word, "/SUB", StringComparison.OrdinalIgnoreCase))
            return new CnkiToken(CnkiTokenKind.SubOp, word, position);

        return new CnkiToken(CnkiTokenKind.Unsupported, word, position);
    }

    CnkiToken ReadDollar(int position)
    {
        var word = ReadUntilBoundary();
        return word.Length > 1 && word[0] == '$' && word[1..].All(char.IsDigit)
            ? new CnkiToken(CnkiTokenKind.WordFreq, word, position)
            : ClassifyWord(word, position);
    }

    CnkiToken ReadWord(int position) => ClassifyWord(ReadUntilBoundary(), position);

    string ReadUntilBoundary()
    {
        var start = _index;
        while (_index < _text.Length)
        {
            var ch = _text[_index];
            if (char.IsWhiteSpace(ch) || ch is '(' or ')' or '=' or '+' or '*' or '-' or ',' or '\'' or '"' or '%' or '#' or '$' or '>' or '<')
                break;
            _index++;
        }

        if (_index == start)
            _index++;

        return _text[start.._index];
    }

    void SkipSpaces()
    {
        while (_index < _text.Length && char.IsWhiteSpace(_text[_index]))
            _index++;
    }

    static CnkiToken ClassifyWord(string word, int position)
    {
        return word.ToUpperInvariant() switch
        {
            "AND" => new CnkiToken(CnkiTokenKind.And, word, position),
            "OR"  => new CnkiToken(CnkiTokenKind.Or, word, position),
            "NOT" => new CnkiToken(CnkiTokenKind.Not, word, position),
            "BETWEEN" => new CnkiToken(CnkiTokenKind.Between, word, position),
            _ => new CnkiToken(CnkiTokenKind.Word, word, position)
        };
    }
}
