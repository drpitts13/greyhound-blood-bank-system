using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class Hl7EndpointAuthorizationRuleTests
{
    [Fact]
    public void Create_WithoutPermission_IsHardStop()
    {
        var result = Hl7EndpointAuthorizationRule.EvaluateCreate(hasAdminHl7Manage: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(Hl7EndpointAuthorizationRule.CreateCode, result.Code);
    }

    [Fact]
    public void Create_WithPermission_Passes()
    {
        var result = Hl7EndpointAuthorizationRule.EvaluateCreate(hasAdminHl7Manage: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Update_WithoutPermission_IsHardStop()
    {
        var result = Hl7EndpointAuthorizationRule.EvaluateUpdate(hasAdminHl7Manage: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(Hl7EndpointAuthorizationRule.UpdateCode, result.Code);
    }

    [Fact]
    public void Update_WithPermission_Passes()
    {
        var result = Hl7EndpointAuthorizationRule.EvaluateUpdate(hasAdminHl7Manage: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Enable_WithoutPermission_IsHardStop()
    {
        var result = Hl7EndpointAuthorizationRule.EvaluateEnable(hasAdminHl7Manage: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(Hl7EndpointAuthorizationRule.EnableCode, result.Code);
    }

    [Fact]
    public void Enable_WithPermission_Passes()
    {
        var result = Hl7EndpointAuthorizationRule.EvaluateEnable(hasAdminHl7Manage: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Disable_WithoutPermission_IsHardStop()
    {
        var result = Hl7EndpointAuthorizationRule.EvaluateDisable(hasAdminHl7Manage: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(Hl7EndpointAuthorizationRule.DisableCode, result.Code);
    }

    [Fact]
    public void Disable_WithPermission_Passes()
    {
        var result = Hl7EndpointAuthorizationRule.EvaluateDisable(hasAdminHl7Manage: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
