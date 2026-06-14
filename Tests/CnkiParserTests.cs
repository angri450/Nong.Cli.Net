using Angri450.Nong.Literature.Dsl;
using Xunit;

namespace Tests;

public class CnkiParserTests
{
    [Theory]
    [InlineData("SU=('腐植酸'+'腐殖酸')*('稀土'+'微肥')", 4)]
    [InlineData("SU=('腐植酸'+'腐殖酸')*('稀土'+'微肥')*('络合'+'配合'+'螯合'+'复合物')", 8)]
    [InlineData("AU=钱伟长 AND (AF=清华大学 OR AF=上海大学)", 3)]
    [InlineData("YE BETWEEN ('2000','2013')", 1)]
    [InlineData("DOI='10.1016/j.chemgeo.2007.05.018'", 1)]
    public void Parse_RequiredExamples_ReturnsQueryWithoutParseDiagnostics(string queryText, int terms)
    {
        var query = CnkiParser.Parse(queryText);

        Assert.Equal(queryText, query.Source);
        Assert.NotNull(query.Root);
        Assert.False(query.HasDiagnostics);
        Assert.Equal(terms, query.Terms.Count);
    }

    [Fact]
    public void Parse_SubjectExpression_BuildsBooleanAstWithFieldContext()
    {
        var query = CnkiParser.Parse("SU=('腐植酸'+'腐殖酸')*('稀土'+'微肥')");

        var and = Assert.IsType<CnkiBinaryNode>(query.Root);
        Assert.Equal(CnkiBooleanOperator.And, and.Operator);

        var leftOr = Assert.IsType<CnkiBinaryNode>(and.Left);
        Assert.Equal(CnkiBooleanOperator.Or, leftOr.Operator);
        Assert.Equal("SU", Assert.IsType<CnkiTermNode>(leftOr.Left).EffectiveField);
        Assert.Equal("腐植酸", Assert.IsType<CnkiTermNode>(leftOr.Left).Value);
        Assert.Equal("腐殖酸", Assert.IsType<CnkiTermNode>(leftOr.Right).Value);
    }

    [Fact]
    public void Parse_AuthorAffiliationExpression_RespectsParentheses()
    {
        var query = CnkiParser.Parse("AU=钱伟长 AND (AF=清华大学 OR AF=上海大学)");

        var and = Assert.IsType<CnkiBinaryNode>(query.Root);
        Assert.Equal(CnkiBooleanOperator.And, and.Operator);
        Assert.Equal("AU", Assert.IsType<CnkiTermNode>(and.Left).EffectiveField);

        var or = Assert.IsType<CnkiBinaryNode>(and.Right);
        Assert.Equal(CnkiBooleanOperator.Or, or.Operator);
        Assert.Equal("AF", Assert.IsType<CnkiTermNode>(or.Left).EffectiveField);
        Assert.Equal("AF", Assert.IsType<CnkiTermNode>(or.Right).EffectiveField);
    }

    [Fact]
    public void Parse_MinusBetweenTerms_BuildsAndNotNode()
    {
        var query = CnkiParser.Parse("TI=腐植酸-腐殖酸");

        var and = Assert.IsType<CnkiBinaryNode>(query.Root);
        Assert.Equal(CnkiBooleanOperator.And, and.Operator);
        Assert.Equal("腐植酸", Assert.IsType<CnkiTermNode>(and.Left).Value);
        Assert.Equal("腐殖酸", Assert.IsType<CnkiTermNode>(Assert.IsType<CnkiNotNode>(and.Right).Operand).Value);
    }

    [Fact]
    public void Parse_YearBetween_BuildsYearRangeTermNode()
    {
        var query = CnkiParser.Parse("YE BETWEEN ('2000','2013')");

        var between = Assert.IsType<CnkiTermNode>(query.Root);
        Assert.True(between.IsBetween);
        Assert.Equal("YE", between.EffectiveField);
        Assert.Equal("2000", between.BetweenStart);
        Assert.Equal("2013", between.BetweenEnd);
    }

    [Fact]
    public void Normalize_SubjectExpression_ExposesParsedFieldsTermsAndConceptGroups()
    {
        var normalized = CnkiQueryNormalizer.Normalize("SU=('腐植酸'+'腐殖酸')*('稀土'+'微肥')");

        var field = Assert.Single(normalized.ParsedFields);
        Assert.Equal("SU", field.NormalizedField);
        Assert.Equal(new[] { "腐植酸", "腐殖酸", "稀土", "微肥" }, field.Terms);

        Assert.Equal(2, normalized.ConceptGroups.Count);
        Assert.Equal(new[] { "腐植酸", "腐殖酸" }, normalized.ConceptGroups[0].Alternatives);
        Assert.Equal(new[] { "稀土", "微肥" }, normalized.ConceptGroups[1].Alternatives);
        Assert.Contains("su=", normalized.NormalizedExpression, StringComparison.Ordinal);
    }
}
