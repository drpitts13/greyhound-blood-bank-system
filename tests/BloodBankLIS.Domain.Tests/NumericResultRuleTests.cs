using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-516: a numeric result is checked against its test's limits: decimal places and the reportable range refuse
// entry; the reference range flags L or H; critical limits flag LL or HH and warn.
public class NumericResultRuleTests
{
    private static readonly NumericLimits Limits = new(
        DecimalPlaces: 2, ReportableLow: 0m, ReportableHigh: 100m, ReferenceLow: 0.10m, ReferenceHigh: 0.30m, CriticalLow: 0.05m, CriticalHigh: 5m);

    [Theory]
    [InlineData("0.20", null)]
    [InlineData("0.10", null)]
    [InlineData("0.30", null)]
    [InlineData("0.09", "L")]
    [InlineData("0.31", "H")]
    [InlineData("0.04", "LL")]
    [InlineData("5.01", "HH")]
    [InlineData("5", "H")]
    public void Flags_FollowTheRanges_AtEachBoundary(string text, string? flag)
    {
        var result = NumericResultRule.Evaluate(text, Limits, "%");
        Assert.True(result.IsAllowed);
        Assert.Equal(flag, result.Flag);
        Assert.Equal(flag is "LL" or "HH", result.Critical is not null);
    }

    [Fact]
    public void DecimalsReportableRangeAndFormat_RefuseEntry()
    {
        Assert.Equal(NumericResultRule.DecimalsCode, NumericResultRule.Evaluate("0.123", Limits).Result.Code);
        var above = NumericResultRule.Evaluate("100.01", Limits, "%");
        Assert.False(above.IsAllowed);
        Assert.Equal(NumericResultRule.ReportableCode, above.Result.Code);
        Assert.Contains("Dilute and repeat", above.Result.Message);
        Assert.False(NumericResultRule.Evaluate("-0.01", Limits).IsAllowed);
        Assert.Equal(NumericResultRule.FormatCode, NumericResultRule.Evaluate("12,5", Limits).Result.Code);
        Assert.Equal("Enter a whole number.", NumericResultRule.Evaluate("1.5", new NumericLimits(DecimalPlaces: 0)).Result.Message);
    }

    [Fact]
    public void NoLimits_AcceptsAnyNumber_WithoutAFlag()
    {
        var result = NumericResultRule.Evaluate("123.456", new NumericLimits());
        Assert.True(result.IsAllowed);
        Assert.Null(result.Flag);
        Assert.Null(new NumericLimits().ReferenceText("%"));
        Assert.Equal("0.10–0.30 %", Limits.ReferenceText("%"));
    }
}
