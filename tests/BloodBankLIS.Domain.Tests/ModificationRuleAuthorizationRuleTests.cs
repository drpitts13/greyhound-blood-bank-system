using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ModificationRuleAuthorizationRuleTests
{
    [Fact]
    public void Create_WithoutPermission_IsHardStop()
    {
        var result = ModificationRuleAuthorizationRule.EvaluateCreate(hasAdminModificationRulesManage: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ModificationRuleAuthorizationRule.CreateCode, result.Code);
    }

    [Fact]
    public void Create_WithPermission_Passes()
    {
        var result = ModificationRuleAuthorizationRule.EvaluateCreate(hasAdminModificationRulesManage: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Update_WithoutPermission_IsHardStop()
    {
        var result = ModificationRuleAuthorizationRule.EvaluateUpdate(hasAdminModificationRulesManage: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ModificationRuleAuthorizationRule.UpdateCode, result.Code);
    }

    [Fact]
    public void Update_WithPermission_Passes()
    {
        var result = ModificationRuleAuthorizationRule.EvaluateUpdate(hasAdminModificationRulesManage: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Activate_WithoutPermission_IsHardStop()
    {
        var result = ModificationRuleAuthorizationRule.EvaluateActivate(hasAdminConfigActivate: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ModificationRuleAuthorizationRule.ActivateCode, result.Code);
    }

    [Fact]
    public void Activate_WithPermission_Passes()
    {
        var result = ModificationRuleAuthorizationRule.EvaluateActivate(hasAdminConfigActivate: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Deactivate_WithoutPermission_IsHardStop()
    {
        var result = ModificationRuleAuthorizationRule.EvaluateDeactivate(hasAdminConfigActivate: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ModificationRuleAuthorizationRule.DeactivateCode, result.Code);
    }

    [Fact]
    public void Deactivate_WithPermission_Passes()
    {
        var result = ModificationRuleAuthorizationRule.EvaluateDeactivate(hasAdminConfigActivate: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
