using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ExpirationModificationCodeAuthorizationRuleTests
{
    [Fact]
    public void Create_WithoutPermission_IsHardStop()
    {
        var result = ExpirationModificationCodeAuthorizationRule.EvaluateCreate(hasAdminModificationRulesManage: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ExpirationModificationCodeAuthorizationRule.CreateCode, result.Code);
    }

    [Fact]
    public void Create_WithPermission_Passes()
    {
        var result = ExpirationModificationCodeAuthorizationRule.EvaluateCreate(hasAdminModificationRulesManage: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Update_WithoutPermission_IsHardStop()
    {
        var result = ExpirationModificationCodeAuthorizationRule.EvaluateUpdate(hasAdminModificationRulesManage: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ExpirationModificationCodeAuthorizationRule.UpdateCode, result.Code);
    }

    [Fact]
    public void Update_WithPermission_Passes()
    {
        var result = ExpirationModificationCodeAuthorizationRule.EvaluateUpdate(hasAdminModificationRulesManage: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Activate_WithoutPermission_IsHardStop()
    {
        var result = ExpirationModificationCodeAuthorizationRule.EvaluateActivate(hasAdminConfigActivate: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ExpirationModificationCodeAuthorizationRule.ActivateCode, result.Code);
    }

    [Fact]
    public void Activate_WithPermission_Passes()
    {
        var result = ExpirationModificationCodeAuthorizationRule.EvaluateActivate(hasAdminConfigActivate: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Deactivate_WithoutPermission_IsHardStop()
    {
        var result = ExpirationModificationCodeAuthorizationRule.EvaluateDeactivate(hasAdminConfigActivate: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ExpirationModificationCodeAuthorizationRule.DeactivateCode, result.Code);
    }

    [Fact]
    public void Deactivate_WithPermission_Passes()
    {
        var result = ExpirationModificationCodeAuthorizationRule.EvaluateDeactivate(hasAdminConfigActivate: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
