using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-333
public class DerivedFromSourceRuleTests
{
    [Fact]
    public void Reject_WithoutResults_Passes() =>
        Assert.Equal(RuleSeverity.Pass, DerivedFromSourceRule.EvaluateReject([]).Severity);

    [Fact]
    public void Reject_WithResults_NamesThem()
    {
        var r = DerivedFromSourceRule.EvaluateReject(["ABORH", "ABSC"]);
        Assert.Equal(RuleSeverity.HardStop, r.Severity);
        Assert.Contains("ABORH, ABSC", r.Message);
    }

    [Fact]
    public void TypeRetired_IsAWarningNamingTheType() =>
        Assert.Contains("A Positive", DerivedFromSourceRule.TypeRetired("A Positive").Message);
}
