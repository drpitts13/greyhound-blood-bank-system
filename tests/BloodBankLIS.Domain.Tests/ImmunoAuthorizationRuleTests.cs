using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ImmunoAuthorizationRuleTests
{
    [Fact]
    public void ManualBloodType_WithoutOverride_IsHardStop()
    {
        var result = ImmunoAuthorizationRule.EvaluateManualBloodType(hasImmunoOverride: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ImmunoAuthorizationRule.ManualBloodTypeCode, result.Code);
    }

    [Fact]
    public void ManualBloodType_WithOverride_Passes()
    {
        var result = ImmunoAuthorizationRule.EvaluateManualBloodType(hasImmunoOverride: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void AntibodyAdd_WithoutRecord_IsHardStop()
    {
        var result = ImmunoAuthorizationRule.EvaluateAntibodyAdd(hasImmunoRecord: false);
        Assert.Equal(ImmunoAuthorizationRule.AntibodyAddCode, result.Code);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
    }

    [Fact]
    public void AntibodyDeactivate_WithoutOverride_IsHardStop()
    {
        var result = ImmunoAuthorizationRule.EvaluateAntibodyDeactivate(hasImmunoOverride: false);
        Assert.Equal(ImmunoAuthorizationRule.AntibodyDeactivateCode, result.Code);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
    }

    [Fact]
    public void AntigenProfile_WithoutRecord_IsHardStop()
    {
        var result = ImmunoAuthorizationRule.EvaluateAntigenProfile(hasImmunoRecord: false);
        Assert.Equal(ImmunoAuthorizationRule.AntigenProfileCode, result.Code);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
    }

    [Fact]
    public void SpecialRequirementAdd_WithoutRecord_IsHardStop()
    {
        var result = ImmunoAuthorizationRule.EvaluateSpecialRequirementAdd(hasImmunoRecord: false);
        Assert.Equal(ImmunoAuthorizationRule.SpecialRequirementAddCode, result.Code);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
    }

    [Fact]
    public void SpecialRequirementDeactivate_WithoutOverride_IsHardStop()
    {
        var result = ImmunoAuthorizationRule.EvaluateSpecialRequirementDeactivate(hasImmunoOverride: false);
        Assert.Equal(ImmunoAuthorizationRule.SpecialRequirementDeactivateCode, result.Code);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
    }
}
