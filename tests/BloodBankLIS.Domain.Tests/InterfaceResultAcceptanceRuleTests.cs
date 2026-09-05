using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class InterfaceResultAcceptanceRuleTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("F")]
    [InlineData("C")]
    [InlineData("P")]
    [InlineData("R")]
    [InlineData("f")]
    public void AcceptedStatuses_Pass(string? status)
    {
        Assert.Equal(RuleSeverity.Pass, InterfaceResultAcceptanceRule.Evaluate(status).Severity);
    }

    [Theory]
    [InlineData("X")]
    [InlineData("D")]
    [InlineData("I")]
    [InlineData("W")]
    public void DestructiveStatuses_HardStop(string status)
    {
        var result = InterfaceResultAcceptanceRule.Evaluate(status);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(InterfaceResultAcceptanceRule.Code, result.Code);
    }
}
