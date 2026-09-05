using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class OrderingLocationAuthorizationRuleTests
{
    [Fact]
    public void Create_WithoutPermission_IsHardStop()
    {
        var result = OrderingLocationAuthorizationRule.EvaluateCreate(hasAdminConfigEdit: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(OrderingLocationAuthorizationRule.CreateCode, result.Code);
    }

    [Fact]
    public void Create_WithPermission_Passes()
    {
        var result = OrderingLocationAuthorizationRule.EvaluateCreate(hasAdminConfigEdit: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Update_WithoutPermission_IsHardStop()
    {
        var result = OrderingLocationAuthorizationRule.EvaluateUpdate(hasAdminConfigEdit: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(OrderingLocationAuthorizationRule.UpdateCode, result.Code);
    }

    [Fact]
    public void Update_WithPermission_Passes()
    {
        var result = OrderingLocationAuthorizationRule.EvaluateUpdate(hasAdminConfigEdit: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Activate_WithoutPermission_IsHardStop()
    {
        var result = OrderingLocationAuthorizationRule.EvaluateActivate(hasAdminConfigActivate: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(OrderingLocationAuthorizationRule.ActivateCode, result.Code);
    }

    [Fact]
    public void Activate_WithPermission_Passes()
    {
        var result = OrderingLocationAuthorizationRule.EvaluateActivate(hasAdminConfigActivate: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Deactivate_WithoutPermission_IsHardStop()
    {
        var result = OrderingLocationAuthorizationRule.EvaluateDeactivate(hasAdminConfigActivate: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(OrderingLocationAuthorizationRule.DeactivateCode, result.Code);
    }

    [Fact]
    public void Deactivate_WithPermission_Passes()
    {
        var result = OrderingLocationAuthorizationRule.EvaluateDeactivate(hasAdminConfigActivate: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
