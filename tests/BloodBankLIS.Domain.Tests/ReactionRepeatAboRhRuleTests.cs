using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Tests;

public class ReactionRepeatAboRhRuleTests
{
    [Fact]
    public void BlankRepeat_Passes()
    {
        var result = ReactionRepeatAboRhRule.Evaluate(
            new AboRh(AboGroup.O, RhType.Positive), null, "Patient");
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void MatchingDisplayText_Passes()
    {
        var expected = new AboRh(AboGroup.A, RhType.Positive);
        var result = ReactionRepeatAboRhRule.Evaluate(expected, "A Positive", "Patient");
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Discrepancy_IsWarning()
    {
        var expected = new AboRh(AboGroup.O, RhType.Positive);
        var result = ReactionRepeatAboRhRule.Evaluate(expected, "A+", "Patient");
        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal(ReactionRepeatAboRhRule.Code, result.Code);
        Assert.Contains("A+", result.Message);
        Assert.Contains("O+", result.Message);
    }

    [Theory]
    [InlineData("O+")]
    [InlineData("O Positive")]
    [InlineData("O|Positive")]
    public void TryParseDisplay_AcceptsCommonForms(string text)
    {
        Assert.True(ReactionRepeatAboRhRule.TryParseDisplay(text, out var parsed));
        Assert.Equal(new AboRh(AboGroup.O, RhType.Positive), parsed);
    }
}
