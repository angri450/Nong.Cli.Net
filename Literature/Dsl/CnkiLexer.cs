using System.Text;

namespace Angri450.Nong.Literature.Dsl;

public sealed class CnkiLexer
{
    static readonly HashSet<string> UnsupportedSlashOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "/SEN",
        "/NEAR",
        "/PREV",
        "/AFT",
        "/PRG"
    };

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
                case '\'':
                case '"':
                    tokens.Add(ReadQuoted(ch, position));
                    break;
                case '%':
                    tokens.Add(new CnkiToken(CnkiTokenKind.Unsupported, "%", position));
                    _index++;
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
        foreach (var op in UnsupportedSlashOperators)
        {
            if (!word.StartsWith(op, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var suffix = word[op.Length..];
            if (suffix.Length > 0)
            {
                _index -= suffix.Length;
            }

            return new CnkiToken(CnkiTokenKind.Unsupported, op, position);
        }

        return ClassifyWord(word, position);
    }

    CnkiToken ReadDollar(int position)
    {
        var word = ReadUntilBoundary();
        return word.Length > 1 && word.Skip(1).All(char.IsDigit)
            ? new CnkiToken(CnkiTokenKind.Unsupported, word, position)
            : ClassifyWord(word, position);
    }

    CnkiToken ReadWord(int position) => ClassifyWord(ReadUntilBoundary(), position);

    string ReadUntilBoundary()
    {
        var start = _index;
        while (_index < _text.Length)
        {
            var ch = _text[_index];
            if (char.IsWhiteSpace(ch) || ch is '(' or ')' or '=' or '+' or '*' or '-' or ',' or '\'' or '"')
                break;
            _index++;
        }

        if (_index == start)
            _index++;

        return _text[start.._index];
    }

    static CnkiToken ClassifyWord(string word, int position)
    {
        return word.ToUpperInvariant() switch
        {
            "AND" => new CnkiToken(CnkiTokenKind.And, word, position),
            "OR" => new CnkiToken(CnkiTokenKind.Or, word, position),
            "NOT" => new CnkiToken(CnkiTokenKind.Not, word, position),
            "BETWEEN" => new CnkiToken(CnkiTokenKind.Between, word, position),
            _ => new CnkiToken(CnkiTokenKind.Word, word, position)
        };
    }
}
