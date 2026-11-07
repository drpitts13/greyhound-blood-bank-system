using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-517: titers as a doubling dilution series: every dilution read to the first negative, the endpoint at the
// configured grade, an optional score, a warning for a reaction after a negative, and the change from the last titer.
public class TiterRuleTests
{
    private static readonly TiterSettings Settings = new(1, 2048, "AHG", "1+", Scored: true);

    private static TiterReading[] Read(params string[] grades) =>
        grades.Select((g, i) => new TiterReading(1 << i, g)).ToArray();

    [Fact]
    public void Dilutions_DoubleFromStartToEnd_AndBadSeriesAreRejected()
    {
        Assert.Equal([1, 2, 4, 8], TiterRule.Dilutions(1, 8));
        Assert.Equal(12, TiterRule.Dilutions(1, 2048).Count);
        Assert.Empty(TiterRule.Dilutions(1, 100));
        Assert.False(TiterRule.IsValidSeries(0, 8));
        Assert.False(TiterRule.IsValidSeries(8, 8));
        Assert.False(TiterRule.IsValidSeries(1, 1 << 14));
        Assert.True(TiterRule.IsValidSeries(2, 64));
    }

    [Fact]
    public void Endpoint_IsTheHighestDilutionAtTheEndpointGrade_WithScore()
    {
        var result = TiterRule.Evaluate(Settings, Read("4+", "3+", "2+", "1+", "1+", "w+", "0"));
        Assert.Equal(RuleSeverity.Pass, result.Result.Severity);
        Assert.Equal(16, result.Value!.Endpoint);
        Assert.Equal(12 + 10 + 8 + 5 + 5 + 2, result.Value.Score);
        Assert.Equal("Titer 1:16 (score 42)", result.Value.Interpretation);
        Assert.Null(result.Pattern);

        var weakCounts = TiterRule.Evaluate(Settings with { EndpointMinGrade = "w+", Scored = false }, Read("4+", "3+", "2+", "1+", "1+", "w+", "0"));
        Assert.Equal(32, weakCounts.Value!.Endpoint);
        Assert.Equal("Titer 1:32", weakCounts.Value.Interpretation);
    }

    [Fact]
    public void NoReaction_IsBelowTheFirstDilution()
    {
        var result = TiterRule.Evaluate(Settings, Read("0"));
        Assert.Null(result.Value!.Endpoint);
        Assert.Equal("<1:1", result.Value.EndpointLabel);
        Assert.Equal(0, result.Value.Score);
    }

    [Fact]
    public void MissingDilutions_BeforeTheFirstNegative_AreRefused_AndUnknownGradesToo()
    {
        var gap = TiterRule.Evaluate(Settings, [new TiterReading(1, "4+"), new TiterReading(4, "2+")]);
        Assert.Equal(TiterRule.MissingCode, gap.Result.Code);
        Assert.Contains("1:2", gap.Result.Message);
        Assert.Equal(TiterRule.GradeCode, TiterRule.Evaluate(Settings, [new TiterReading(1, "5+")]).Result.Code);
        Assert.Equal(TiterRule.GradeCode, TiterRule.Evaluate(Settings, [new TiterReading(3, "1+")]).Result.Code);
        Assert.Equal(TiterRule.ConfigCode, TiterRule.Evaluate(Settings with { EndpointMinGrade = "0" }, Read("1+")).Result.Code);
    }

    [Fact]
    public void AReactionAfterANegative_IsKept_AndWarns()
    {
        var result = TiterRule.Evaluate(Settings, Read("2+", "0", "1+"));
        Assert.Equal(RuleSeverity.Pass, result.Result.Severity);
        Assert.Equal(TiterRule.PatternCode, result.Pattern!.Code);
        Assert.Equal(3, result.Value!.Readings.Count);
        Assert.Equal(4, result.Value.Endpoint);
    }

    [Fact]
    public void Change_IsCountedInDilutionSteps()
    {
        var eight = TiterRule.Evaluate(Settings, Read("3+", "2+", "1+", "1+", "0")).Value!;
        var thirtyTwo = TiterRule.Evaluate(Settings, Read("4+", "3+", "2+", "2+", "1+", "1+", "0")).Value!;
        var none = TiterRule.Evaluate(Settings, Read("0")).Value!;
        Assert.Equal(2, TiterRule.CompareToPrevious(eight, thirtyTwo));
        Assert.Equal(-2, TiterRule.CompareToPrevious(thirtyTwo, eight));
        Assert.Equal(4, TiterRule.CompareToPrevious(none, eight));
        Assert.Equal("2 dilutions higher than 1:8 on 2026-10-01", TiterRule.DescribeChange(eight, new DateTime(2026, 10, 1), thirtyTwo));
        Assert.Equal("Same as 1:8 on 2026-10-01", TiterRule.DescribeChange(eight, new DateTime(2026, 10, 1), eight));
    }

    [Fact]
    public void Value_RoundTripsThroughJson()
    {
        var value = TiterRule.Evaluate(Settings, Read("2+", "1+", "0")).Value!;
        Assert.True(TiterResultValue.TryParse(value.Serialize(), out var back));
        Assert.Equal(value.Endpoint, back!.Endpoint);
        Assert.Equal(value.Readings, back.Readings);
        Assert.False(TiterResultValue.TryParse("Positive", out _));
    }
}
