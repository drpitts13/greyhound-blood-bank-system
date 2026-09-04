using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class SubtestCatalogAuthorizationRuleTests
{
    [Fact]
    public void Create_WithoutPermission_IsHardStop()
    {
        var result = SubtestCatalogAuthorizationRule.EvaluateCreate(hasAdminTestsManage: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(SubtestCatalogAuthorizationRule.CreateCode, result.Code);
    }

    [Fact]
    public void Create_WithPermission_Passes()
    {
        var result = SubtestCatalogAuthorizationRule.EvaluateCreate(hasAdminTestsManage: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Update_WithoutPermission_IsHardStop()
    {
        var result = SubtestCatalogAuthorizationRule.EvaluateUpdate(hasAdminTestsManage: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(SubtestCatalogAuthorizationRule.UpdateCode, result.Code);
    }

    [Fact]
    public void Update_WithPermission_Passes()
    {
        var result = SubtestCatalogAuthorizationRule.EvaluateUpdate(hasAdminTestsManage: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Activate_WithoutPermission_IsHardStop()
    {
        var result = SubtestCatalogAuthorizationRule.EvaluateActivate(hasAdminConfigActivate: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(SubtestCatalogAuthorizationRule.ActivateCode, result.Code);
    }

    [Fact]
    public void Activate_WithPermission_Passes()
    {
        var result = SubtestCatalogAuthorizationRule.EvaluateActivate(hasAdminConfigActivate: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Deactivate_WithoutPermission_IsHardStop()
    {
        var result = SubtestCatalogAuthorizationRule.EvaluateDeactivate(hasAdminConfigActivate: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(SubtestCatalogAuthorizationRule.DeactivateCode, result.Code);
    }

    [Fact]
    public void Deactivate_WithPermission_Passes()
    {
        var result = SubtestCatalogAuthorizationRule.EvaluateDeactivate(hasAdminConfigActivate: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
