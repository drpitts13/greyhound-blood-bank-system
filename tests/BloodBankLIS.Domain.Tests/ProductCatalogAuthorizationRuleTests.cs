using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ProductCatalogAuthorizationRuleTests
{
    [Fact]
    public void Create_WithoutPermission_IsHardStop()
    {
        var result = ProductCatalogAuthorizationRule.EvaluateCreate(hasAdminProductsManage: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ProductCatalogAuthorizationRule.CreateCode, result.Code);
    }

    [Fact]
    public void Create_WithPermission_Passes()
    {
        var result = ProductCatalogAuthorizationRule.EvaluateCreate(hasAdminProductsManage: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Update_WithoutPermission_IsHardStop()
    {
        var result = ProductCatalogAuthorizationRule.EvaluateUpdate(hasAdminProductsManage: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ProductCatalogAuthorizationRule.UpdateCode, result.Code);
    }

    [Fact]
    public void Update_WithPermission_Passes()
    {
        var result = ProductCatalogAuthorizationRule.EvaluateUpdate(hasAdminProductsManage: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Activate_WithoutPermission_IsHardStop()
    {
        var result = ProductCatalogAuthorizationRule.EvaluateActivate(hasAdminConfigActivate: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ProductCatalogAuthorizationRule.ActivateCode, result.Code);
    }

    [Fact]
    public void Activate_WithPermission_Passes()
    {
        var result = ProductCatalogAuthorizationRule.EvaluateActivate(hasAdminConfigActivate: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Deactivate_WithoutPermission_IsHardStop()
    {
        var result = ProductCatalogAuthorizationRule.EvaluateDeactivate(hasAdminConfigActivate: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ProductCatalogAuthorizationRule.DeactivateCode, result.Code);
    }

    [Fact]
    public void Deactivate_WithPermission_Passes()
    {
        var result = ProductCatalogAuthorizationRule.EvaluateDeactivate(hasAdminConfigActivate: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
