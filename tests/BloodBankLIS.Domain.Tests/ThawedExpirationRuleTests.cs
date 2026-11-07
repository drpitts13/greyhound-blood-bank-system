using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-565: a product thawed in place expires its code's thawed shelf life after the thaw, never after the frozen
// expiration; the shelf life is 1 to 720 hours.
public class ThawedExpirationRuleTests
{
    private static readonly DateTime Thaw = new(2026, 11, 7, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Expiration_IsTheShelfLifeAfterThaw_CappedAtTheFrozenExpiration()
    {
        Assert.Equal(Thaw.AddHours(6), ThawedExpirationRule.Compute(Thaw, 6, Thaw.AddDays(200)));
        Assert.Equal(Thaw.AddHours(24), ThawedExpirationRule.Compute(Thaw, 24, Thaw.AddDays(200)));
        Assert.Equal(Thaw.AddHours(3), ThawedExpirationRule.Compute(Thaw, 24, Thaw.AddHours(3)));
    }

    [Fact]
    public void ShelfLife_IsBounded_AndDescribed()
    {
        Assert.Equal(RuleSeverity.HardStop, ThawedExpirationRule.EvaluateShelfLife(0).Severity);
        Assert.Equal(RuleSeverity.HardStop, ThawedExpirationRule.EvaluateShelfLife(721).Severity);
        Assert.Equal(RuleSeverity.Pass, ThawedExpirationRule.EvaluateShelfLife(120).Severity);
        Assert.Equal("1 hour", ThawedExpirationRule.Describe(1));
        Assert.Equal("24 hours", ThawedExpirationRule.Describe(24));
        Assert.Equal("5 days", ThawedExpirationRule.Describe(120));
        Assert.Equal("50 hours", ThawedExpirationRule.Describe(50));
    }
}
