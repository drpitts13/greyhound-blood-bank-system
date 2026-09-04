using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class CompatibilityAuthorizationRuleTests
{
    [Fact]
    public void Allocate_WithoutPermission_IsHardStop()
    {
        var result = CompatibilityAuthorizationRule.EvaluateAllocate(hasCompatibilityAllocate: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(CompatibilityAuthorizationRule.AllocateCode, result.Code);
    }

    [Fact]
    public void Allocate_WithPermission_Passes()
    {
        var result = CompatibilityAuthorizationRule.EvaluateAllocate(hasCompatibilityAllocate: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Crossmatch_WithoutPermission_IsHardStop()
    {
        var result = CompatibilityAuthorizationRule.EvaluateCrossmatch(hasCompatibilityCrossmatch: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(CompatibilityAuthorizationRule.CrossmatchCode, result.Code);
    }

    [Fact]
    public void Crossmatch_WithPermission_Passes()
    {
        var result = CompatibilityAuthorizationRule.EvaluateCrossmatch(hasCompatibilityCrossmatch: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
