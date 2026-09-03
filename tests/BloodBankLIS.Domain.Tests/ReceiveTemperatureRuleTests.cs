using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ReceiveTemperatureRuleTests
{
    [Fact]
    public void NotRequired_PassesWithoutReading()
    {
        var result = ReceiveTemperatureRule.Evaluate(required: false, celsius: null);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void RequiredMissing_IsHardStop()
    {
        var result = ReceiveTemperatureRule.Evaluate(required: true, celsius: null);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ReceiveTemperatureRule.Code, result.Code);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(4.0)]
    [InlineData(10.0)]
    public void InRange_Passes(double celsius)
    {
        var result = ReceiveTemperatureRule.Evaluate(required: true, (decimal)celsius);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void TooCold_IsHardStop()
    {
        var result = ReceiveTemperatureRule.Evaluate(required: true, celsius: 0.5m);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
    }

    [Fact]
    public void TooWarm_IsHardStop()
    {
        var result = ReceiveTemperatureRule.Evaluate(required: true, celsius: 12m);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
    }
}
