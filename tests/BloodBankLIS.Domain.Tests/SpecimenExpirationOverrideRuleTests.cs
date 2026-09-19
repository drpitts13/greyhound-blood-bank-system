using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class SpecimenExpirationOverrideRuleTests
{
    private static readonly DateTime Collected = new(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PolicyExpires = Collected.AddHours(168);

    [Fact]
    public void MatchingPolicyExpiry_PassesWithoutOverride()
    {
        var result = SpecimenExpirationOverrideRule.Evaluate(PolicyExpires, PolicyExpires, overrideAuthorized: false);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
        Assert.Equal(SpecimenExpirationOverrideRule.Code, result.Code);
    }

    [Fact]
    public void MatchingPolicyExpiry_IgnoresSubSecondDifference()
    {
        var requested = PolicyExpires.AddMilliseconds(400);
        var result = SpecimenExpirationOverrideRule.Evaluate(PolicyExpires, requested, overrideAuthorized: false);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void DifferentExpiry_WithoutOverride_IsWarning()
    {
        var result = SpecimenExpirationOverrideRule.Evaluate(
            PolicyExpires, PolicyExpires.AddHours(24), overrideAuthorized: false);
        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal(SpecimenExpirationOverrideRule.Code, result.Code);
        Assert.Contains("authorized override", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DifferentExpiry_WithOverride_Passes()
    {
        var result = SpecimenExpirationOverrideRule.Evaluate(
            PolicyExpires, PolicyExpires.AddHours(-12), overrideAuthorized: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
        Assert.Contains("authorized override", result.Message, StringComparison.OrdinalIgnoreCase);
    }
}
