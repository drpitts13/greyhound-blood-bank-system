using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ReceiveVisualInspectionRuleTests
{
    [Fact]
    public void NotRequired_PassesEvenWhenUnacceptable()
    {
        var result = ReceiveVisualInspectionRule.Evaluate(required: false, acceptable: false);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void RequiredAndAcceptable_Passes()
    {
        var result = ReceiveVisualInspectionRule.Evaluate(required: true, acceptable: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void RequiredAndUnacceptable_IsHardStop()
    {
        var result = ReceiveVisualInspectionRule.Evaluate(required: true, acceptable: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ReceiveVisualInspectionRule.Code, result.Code);
    }
}
