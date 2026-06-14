using Angri450.Nong.Literature.Dsl;
using Xunit;

namespace Tests;

public class CnkiLexerTests
{
    [Fact]
    public void Tokenize_RequiredSubjectExpression_EmitsFieldTermsAndOperators()
    {
        var tokens = CnkiLexer.Tokenize("SU=('腐植酸'+'腐殖酸')*('稀土'+'微肥')");

        Assert.Collection(
            tokens,
            token => AssertToken(token, CnkiTokenKind.Word, "SU", 0),
            token => AssertToken(token, CnkiTokenKind.Equal, "=", 2),
            token => AssertToken(token, CnkiTokenKind.LeftParen, "(", 3),
            token => AssertToken(token, CnkiTokenKind.Quoted, "腐植酸", 4),
            token => AssertToken(token, CnkiTokenKind.Plus, "+", 9),
            token => AssertToken(token, CnkiTokenKind.Quoted, "腐殖酸", 10),
            token => AssertToken(token, CnkiTokenKind.RightParen, ")", 15),
            token => AssertToken(token, CnkiTokenKind.Star, "*", 16),
            token => AssertToken(token, CnkiTokenKind.LeftParen, "(", 17),
            token => AssertToken(token, CnkiTokenKind.Quoted, "稀土", 18),
            token => AssertToken(token, CnkiTokenKind.Plus, "+", 22),
            token => AssertToken(token, CnkiTokenKind.Quoted, "微肥", 23),
            token => AssertToken(token, CnkiTokenKind.RightParen, ")", 27),
            token => AssertToken(token, CnkiTokenKind.End, string.Empty, 28));
    }

    [Theory]
    [InlineData("TI='soil' % 'acid'", "%")]
    [InlineData("TI='soil' /SEN 3 'acid'", "/SEN")]
    [InlineData("TI='soil' /NEAR 3 'acid'", "/NEAR")]
    [InlineData("TI='soil' /PREV 3 'acid'", "/PREV")]
    [InlineData("TI='soil' /AFT 3 'acid'", "/AFT")]
    [InlineData("TI='soil' /PRG 3 'acid'", "/PRG")]
    [InlineData("TI='soil' $3 'acid'", "$3")]
    public void Tokenize_UnsupportedOperators_PreservesOperatorPosition(string query, string op)
    {
        var unsupported = CnkiLexer.Tokenize(query).Single(token => token.Kind == CnkiTokenKind.Unsupported);

        Assert.Equal(op, unsupported.RawText);
        Assert.Equal(query.IndexOf(op, StringComparison.Ordinal), unsupported.Position);
    }

    [Fact]
    public void Tokenize_Keywords_AreCaseInsensitive()
    {
        var kinds = CnkiLexer.Tokenize("au=钱伟长 and not af=清华大学 or ye between ('2000','2013')")
            .Select(token => token.Kind)
            .ToArray();

        Assert.Contains(CnkiTokenKind.And, kinds);
        Assert.Contains(CnkiTokenKind.Not, kinds);
        Assert.Contains(CnkiTokenKind.Or, kinds);
        Assert.Contains(CnkiTokenKind.Between, kinds);
    }

    private static void AssertToken(CnkiToken token, CnkiTokenKind kind, string text, int position)
    {
        Assert.Equal(kind, token.Kind);
        Assert.Equal(text, token.Text);
        Assert.Equal(position, token.Position);
    }
}
