using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class PhaseCatalogAuthorizationRuleTests
{
    [Fact]
    public void Create_WithoutPermission_IsHardStop()
    {
        var result = PhaseCatalogAuthorizationRule.EvaluateCreate(hasAdminTestsManage: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(PhaseCatalogAuthorizationRule.CreateCode, result.Code);
    }

    [Fact]
    public void Create_WithPermission_Passes()
    {
        var result = PhaseCatalogAuthorizationRule.EvaluateCreate(hasAdminTestsManage: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Update_WithoutPermission_IsHardStop()
    {
        var result = PhaseCatalogAuthorizationRule.EvaluateUpdate(hasAdminTestsManage: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(PhaseCatalogAuthorizationRule.UpdateCode, result.Code);
    }

    [Fact]
    public void Update_WithPermission_Passes()
    {
        var result = PhaseCatalogAuthorizationRule.EvaluateUpdate(hasAdminTestsManage: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Activate_WithoutPermission_IsHardStop()
    {
        var result = PhaseCatalogAuthorizationRule.EvaluateActivate(hasAdminConfigActivate: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(PhaseCatalogAuthorizationRule.ActivateCode, result.Code);
    }

    [Fact]
    public void Activate_WithPermission_Passes()
    {
        var result = PhaseCatalogAuthorizationRule.EvaluateActivate(hasAdminConfigActivate: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Deactivate_WithoutPermission_IsHardStop()
    {
        var result = PhaseCatalogAuthorizationRule.EvaluateDeactivate(hasAdminConfigActivate: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(PhaseCatalogAuthorizationRule.DeactivateCode, result.Code);
    }

    [Fact]
    public void Deactivate_WithPermission_Passes()
    {
        var result = PhaseCatalogAuthorizationRule.EvaluateDeactivate(hasAdminConfigActivate: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
