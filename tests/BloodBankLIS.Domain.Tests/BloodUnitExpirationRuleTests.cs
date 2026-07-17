using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class BloodUnitExpirationRuleTests
{
    private static readonly DateTime Now = new(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Window = TimeSpan.FromDays(3);

    [Fact]
    public void NotExpired_BeyondWindow_IsPass()
    {
        var result = BloodUnitExpirationRule.Evaluate(Now.AddDays(10), Now, Window);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
        Assert.False(BloodUnitExpirationRule.IsExpired(Now.AddDays(10), Now));
    }

    [Fact]
    public void WithinWindow_IsWarning()
    {
        var result = BloodUnitExpirationRule.Evaluate(Now.AddDays(1), Now, Window);
        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal(BloodUnitExpirationRule.NearExpiryCode, result.Code);
    }

    [Fact]
    public void AtExpiration_IsHardStop_AndExpired()
    {
        var result = BloodUnitExpirationRule.Evaluate(Now, Now, Window);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.True(BloodUnitExpirationRule.IsExpired(Now, Now));
    }

    [Fact]
    public void PastExpiration_IsHardStop()
    {
        var result = BloodUnitExpirationRule.Evaluate(Now.AddHours(-1), Now, Window);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
    }
}
