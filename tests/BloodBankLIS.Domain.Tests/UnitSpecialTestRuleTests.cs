using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class UnitSpecialTestRuleTests
{
    [Fact]
    public void ACodeAndANegativeOrPositiveResult_AreRequired()
    {
        Assert.Equal(RuleSeverity.Pass, UnitSpecialTestRule.Evaluate("cmv", "neg", null, null).Severity);
        Assert.Equal(RuleSeverity.Pass, UnitSpecialTestRule.Evaluate("HBS", "Positive", null, null).Severity);
        Assert.Equal(RuleSeverity.HardStop, UnitSpecialTestRule.Evaluate(" ", "Negative", null, null).Severity);
        Assert.Equal(RuleSeverity.HardStop, UnitSpecialTestRule.Evaluate("CMV IgG!", "Negative", null, null).Severity);
        Assert.Equal(RuleSeverity.HardStop, UnitSpecialTestRule.Evaluate("CMV", "Equivocal", null, null).Severity);
        Assert.Equal("Negative", UnitSpecialTestRule.NormalizeResult(" NEG "));
        Assert.Null(UnitSpecialTestRule.NormalizeResult("indeterminate"));
    }

    [Fact]
    public void TheSameResultTwice_IsRefused_AndAChange_NeedsAReason()
    {
        var same = UnitSpecialTestRule.Evaluate("CMV", "Negative", "NEG", null);
        Assert.Equal(RuleSeverity.HardStop, same.Severity);
        Assert.Contains("already recorded Negative", same.Message);

        var noReason = UnitSpecialTestRule.Evaluate("CMV", "Positive", "Negative", "typo");
        Assert.Equal(RuleSeverity.HardStop, noReason.Severity);
        Assert.Contains("recorded Negative", noReason.Message);

        Assert.Equal(RuleSeverity.Pass,
            UnitSpecialTestRule.Evaluate("CMV", "Positive", "Negative", "Supplier corrected report 4471").Severity);
    }
}
