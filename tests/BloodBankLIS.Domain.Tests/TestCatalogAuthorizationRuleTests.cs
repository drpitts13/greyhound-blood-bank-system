using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class TestCatalogAuthorizationRuleTests
{
    [Fact]
    public void Create_WithoutPermission_IsHardStop()
    {
        var result = TestCatalogAuthorizationRule.EvaluateCreate(hasAdminTestsManage: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(TestCatalogAuthorizationRule.CreateCode, result.Code);
    }

    [Fact]
    public void Create_WithPermission_Passes()
    {
        var result = TestCatalogAuthorizationRule.EvaluateCreate(hasAdminTestsManage: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Update_WithoutPermission_IsHardStop()
    {
        var result = TestCatalogAuthorizationRule.EvaluateUpdate(hasAdminTestsManage: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(TestCatalogAuthorizationRule.UpdateCode, result.Code);
    }

    [Fact]
    public void Update_WithPermission_Passes()
    {
        var result = TestCatalogAuthorizationRule.EvaluateUpdate(hasAdminTestsManage: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Activate_WithoutPermission_IsHardStop()
    {
        var result = TestCatalogAuthorizationRule.EvaluateActivate(hasAdminTestsManage: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(TestCatalogAuthorizationRule.ActivateCode, result.Code);
    }

    [Fact]
    public void Activate_WithPermission_Passes()
    {
        var result = TestCatalogAuthorizationRule.EvaluateActivate(hasAdminTestsManage: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Deactivate_WithoutPermission_IsHardStop()
    {
        var result = TestCatalogAuthorizationRule.EvaluateDeactivate(hasAdminTestsManage: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(TestCatalogAuthorizationRule.DeactivateCode, result.Code);
    }

    [Fact]
    public void Deactivate_WithPermission_Passes()
    {
        var result = TestCatalogAuthorizationRule.EvaluateDeactivate(hasAdminTestsManage: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Clone_WithoutPermission_IsHardStop()
    {
        var result = TestCatalogAuthorizationRule.EvaluateClone(hasAdminTestsManage: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(TestCatalogAuthorizationRule.CloneCode, result.Code);
    }

    [Fact]
    public void Clone_WithPermission_Passes()
    {
        var result = TestCatalogAuthorizationRule.EvaluateClone(hasAdminTestsManage: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
