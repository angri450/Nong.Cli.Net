using Angri450.Nong.Literature.Dsl;
using Xunit;

namespace Tests;

public class CnkiDslValidatorTests
{
    [Theory]
    [InlineData("SU=('腐植酸'+'腐殖酸')*('稀土'+'微肥')")]
    [InlineData("SU=('腐植酸'+'腐殖酸')*('稀土'+'微肥')*('络合'+'配合'+'螯合'+'复合物')")]
    [InlineData("AU=钱伟长 AND (AF=清华大学 OR AF=上海大学)")]
    [InlineData("YE BETWEEN ('2000','2013')")]
    [InlineData("DOI='10.1016/j.chemgeo.2007.05.018'")]
    public void Validate_RequiredExamples_AreValid(string query)
    {
        var result = CnkiDslValidator.Validate(query);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues.Select(issue => issue.Message)));
        Assert.Empty(result.Issues);
        Assert.NotNull(result.Query);
    }

    [Theory]
    [InlineData("TI='soil' % 'acid'", "%")]
    [InlineData("TI='soil' /SEN 3 'acid'", "/SEN")]
    [InlineData("TI='soil' /NEAR 3 'acid'", "/NEAR")]
    [InlineData("TI='soil' /PREV 3 'acid'", "/PREV")]
    [InlineData("TI='soil' /AFT 3 'acid'", "/AFT")]
    [InlineData("TI='soil' /PRG 3 'acid'", "/PRG")]
    [InlineData("TI='soil' $3 'acid'", "$3")]
    public void Validate_UnsupportedOperators_ReturnsValidationErrorWithLocation(string query, string op)
    {
        var result = CnkiDslValidator.Validate(query);

        Assert.False(result.IsValid);
        var issue = Assert.Single(result.Issues.Where(issue => issue.Message.Contains("Unsupported CNKI operator", StringComparison.Ordinal)));
        Assert.Equal("E006", issue.Id);
        Assert.Equal("Error", issue.Severity);
        Assert.Equal(query.IndexOf(op, StringComparison.Ordinal), issue.Position);
        Assert.Contains(op, issue.Message, StringComparison.Ordinal);
        Assert.Contains(op, issue.Context, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_UnknownField_ReturnsUsefulLocation()
    {
        var result = CnkiDslValidator.Validate("XX=腐植酸");

        Assert.False(result.IsValid);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("E006", issue.Id);
        Assert.Equal(0, issue.Position);
        Assert.Contains("Unsupported CNKI field 'XX'", issue.Message, StringComparison.Ordinal);
        Assert.Contains("XX=腐植酸", issue.Context, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_BetweenOnlySupportsYearField()
    {
        var result = CnkiDslValidator.Validate("CF BETWEEN ('1','10')");

        Assert.False(result.IsValid);
        var issue = Assert.Single(result.Issues.Where(issue => issue.Message.Contains("BETWEEN is only supported for YE", StringComparison.Ordinal)));
        Assert.Equal(0, issue.Position);
    }

    [Fact]
    public void Validate_YearRangeRequiresFourDigitYearsAndAscendingRange()
    {
        var result = CnkiDslValidator.Validate("YE BETWEEN ('2013','2000')");

        Assert.False(result.IsValid);
        var issue = Assert.Single(result.Issues);
        Assert.Contains("must be less than or equal", issue.Message, StringComparison.Ordinal);
        Assert.NotNull(issue.Position);
        Assert.Contains("2013", issue.Context, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_StringReportsParseDiagnosticsWithPosition()
    {
        var result = CnkiDslValidator.Validate("TI=('soil'");

        Assert.False(result.IsValid);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("E006", issue.Id);
        Assert.NotNull(issue.Position);
        Assert.Contains("Missing ')'", issue.Message, StringComparison.Ordinal);
        Assert.Contains("TI=('soil'", issue.Context, StringComparison.Ordinal);
    }
}
