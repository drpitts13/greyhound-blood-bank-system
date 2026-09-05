using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class InventoryLocationAuthorizationRuleTests
{
    [Fact]
    public void Create_WithoutPermission_IsHardStop()
    {
        var result = InventoryLocationAuthorizationRule.EvaluateCreate(hasAdminConfigEdit: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(InventoryLocationAuthorizationRule.CreateCode, result.Code);
    }

    [Fact]
    public void Create_WithPermission_Passes()
    {
        var result = InventoryLocationAuthorizationRule.EvaluateCreate(hasAdminConfigEdit: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Update_WithoutPermission_IsHardStop()
    {
        var result = InventoryLocationAuthorizationRule.EvaluateUpdate(hasAdminConfigEdit: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(InventoryLocationAuthorizationRule.UpdateCode, result.Code);
    }

    [Fact]
    public void Update_WithPermission_Passes()
    {
        var result = InventoryLocationAuthorizationRule.EvaluateUpdate(hasAdminConfigEdit: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Activate_WithoutPermission_IsHardStop()
    {
        var result = InventoryLocationAuthorizationRule.EvaluateActivate(hasAdminConfigActivate: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(InventoryLocationAuthorizationRule.ActivateCode, result.Code);
    }

    [Fact]
    public void Activate_WithPermission_Passes()
    {
        var result = InventoryLocationAuthorizationRule.EvaluateActivate(hasAdminConfigActivate: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Deactivate_WithoutPermission_IsHardStop()
    {
        var result = InventoryLocationAuthorizationRule.EvaluateDeactivate(hasAdminConfigActivate: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(InventoryLocationAuthorizationRule.DeactivateCode, result.Code);
    }

    [Fact]
    public void Deactivate_WithPermission_Passes()
    {
        var result = InventoryLocationAuthorizationRule.EvaluateDeactivate(hasAdminConfigActivate: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
