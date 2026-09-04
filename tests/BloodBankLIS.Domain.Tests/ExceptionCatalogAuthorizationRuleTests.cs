using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ExceptionCatalogAuthorizationRuleTests
{
    [Fact]
    public void Create_WithoutPermission_IsHardStop()
    {
        var result = ExceptionCatalogAuthorizationRule.EvaluateCreate(hasAdminConfigEdit: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ExceptionCatalogAuthorizationRule.CreateCode, result.Code);
    }

    [Fact]
    public void Create_WithPermission_Passes()
    {
        var result = ExceptionCatalogAuthorizationRule.EvaluateCreate(hasAdminConfigEdit: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Update_WithoutPermission_IsHardStop()
    {
        var result = ExceptionCatalogAuthorizationRule.EvaluateUpdate(hasAdminConfigEdit: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ExceptionCatalogAuthorizationRule.UpdateCode, result.Code);
    }

    [Fact]
    public void Update_WithPermission_Passes()
    {
        var result = ExceptionCatalogAuthorizationRule.EvaluateUpdate(hasAdminConfigEdit: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Activate_WithoutPermission_IsHardStop()
    {
        var result = ExceptionCatalogAuthorizationRule.EvaluateActivate(hasAdminConfigActivate: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ExceptionCatalogAuthorizationRule.ActivateCode, result.Code);
    }

    [Fact]
    public void Activate_WithPermission_Passes()
    {
        var result = ExceptionCatalogAuthorizationRule.EvaluateActivate(hasAdminConfigActivate: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Deactivate_WithoutPermission_IsHardStop()
    {
        var result = ExceptionCatalogAuthorizationRule.EvaluateDeactivate(hasAdminConfigActivate: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ExceptionCatalogAuthorizationRule.DeactivateCode, result.Code);
    }

    [Fact]
    public void Deactivate_WithPermission_Passes()
    {
        var result = ExceptionCatalogAuthorizationRule.EvaluateDeactivate(hasAdminConfigActivate: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
