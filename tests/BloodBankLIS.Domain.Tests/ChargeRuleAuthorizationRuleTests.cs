using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ChargeRuleAuthorizationRuleTests
{
    [Fact]
    public void Create_WithoutPermission_IsHardStop()
    {
        var result = ChargeRuleAuthorizationRule.EvaluateCreate(hasAdminConfigEdit: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ChargeRuleAuthorizationRule.CreateCode, result.Code);
    }

    [Fact]
    public void Create_WithPermission_Passes()
    {
        var result = ChargeRuleAuthorizationRule.EvaluateCreate(hasAdminConfigEdit: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Update_WithoutPermission_IsHardStop()
    {
        var result = ChargeRuleAuthorizationRule.EvaluateUpdate(hasAdminConfigEdit: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ChargeRuleAuthorizationRule.UpdateCode, result.Code);
    }

    [Fact]
    public void Update_WithPermission_Passes()
    {
        var result = ChargeRuleAuthorizationRule.EvaluateUpdate(hasAdminConfigEdit: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Activate_WithoutPermission_IsHardStop()
    {
        var result = ChargeRuleAuthorizationRule.EvaluateActivate(hasAdminConfigActivate: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ChargeRuleAuthorizationRule.ActivateCode, result.Code);
    }

    [Fact]
    public void Activate_WithPermission_Passes()
    {
        var result = ChargeRuleAuthorizationRule.EvaluateActivate(hasAdminConfigActivate: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Deactivate_WithoutPermission_IsHardStop()
    {
        var result = ChargeRuleAuthorizationRule.EvaluateDeactivate(hasAdminConfigActivate: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ChargeRuleAuthorizationRule.DeactivateCode, result.Code);
    }

    [Fact]
    public void Deactivate_WithPermission_Passes()
    {
        var result = ChargeRuleAuthorizationRule.EvaluateDeactivate(hasAdminConfigActivate: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
