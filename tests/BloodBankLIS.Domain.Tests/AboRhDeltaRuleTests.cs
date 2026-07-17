using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Tests;

public class AboRhDeltaRuleTests
{
    [Fact]
    public void NoPriorRecord_Passes()
    {
        var result = AboRhDeltaRule.Evaluate(null, new AboRh(AboGroup.O, RhType.Positive));
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void MatchingType_Passes()
    {
        var current = new AboRh(AboGroup.A, RhType.Negative);
        var result = AboRhDeltaRule.Evaluate(current, new AboRh(AboGroup.A, RhType.Negative));
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Discrepancy_IsWarning_WithDeltaCode()
    {
        var current = new AboRh(AboGroup.O, RhType.Positive);
        var result = AboRhDeltaRule.Evaluate(current, new AboRh(AboGroup.A, RhType.Positive));
        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal(AboRhDeltaRule.DeltaCode, result.Code);
    }
}
