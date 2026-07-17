using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class SpecimenExpirationRuleTests
{
    private static readonly DateTime Now = new(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Window = TimeSpan.FromHours(8);

    [Fact]
    public void NullExpiration_IsHardStop()
    {
        var result = SpecimenExpirationRule.Evaluate(null, Now, Window);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(SpecimenExpirationRule.ExpiredCode, result.Code);
    }

    [Fact]
    public void ExpiresInFuture_BeyondWindow_IsPass()
    {
        var result = SpecimenExpirationRule.Evaluate(Now.AddDays(2), Now, Window);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void ExpiresWithinWindow_IsWarning()
    {
        var result = SpecimenExpirationRule.Evaluate(Now.AddHours(4), Now, Window);
        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal(SpecimenExpirationRule.NearExpiryCode, result.Code);
    }

    [Fact]
    public void ExactlyAtExpiration_IsHardStop()
    {
        var result = SpecimenExpirationRule.Evaluate(Now, Now, Window);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
    }

    [Fact]
    public void OneTickPastExpiration_IsHardStop()
    {
        var result = SpecimenExpirationRule.Evaluate(Now.AddTicks(-1), Now, Window);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
    }
}
