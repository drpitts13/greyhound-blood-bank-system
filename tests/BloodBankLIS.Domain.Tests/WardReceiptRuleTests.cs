using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class WardReceiptRuleTests
{
    [Fact]
    public void NotRequired_PassesEvenWhenNotReceived()
    {
        var result = WardReceiptRule.Evaluate(required: false, received: false);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
        Assert.Equal(WardReceiptRule.Code, result.Code);
    }

    [Fact]
    public void RequiredAndReceived_Passes()
    {
        var result = WardReceiptRule.Evaluate(required: true, received: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void RequiredAndMissing_IsHardStop()
    {
        var result = WardReceiptRule.Evaluate(required: true, received: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(WardReceiptRule.Code, result.Code);
    }
}
