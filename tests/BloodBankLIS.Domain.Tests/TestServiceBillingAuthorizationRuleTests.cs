using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class TestServiceBillingAuthorizationRuleTests
{
    [Fact]
    public void Create_WithoutPermission_IsHardStop()
    {
        var result = TestServiceBillingAuthorizationRule.EvaluateCreate(hasAdminConfigEdit: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(TestServiceBillingAuthorizationRule.CreateCode, result.Code);
    }

    [Fact]
    public void Create_WithPermission_Passes()
    {
        var result = TestServiceBillingAuthorizationRule.EvaluateCreate(hasAdminConfigEdit: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Update_WithoutPermission_IsHardStop()
    {
        var result = TestServiceBillingAuthorizationRule.EvaluateUpdate(hasAdminConfigEdit: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(TestServiceBillingAuthorizationRule.UpdateCode, result.Code);
    }

    [Fact]
    public void Update_WithPermission_Passes()
    {
        var result = TestServiceBillingAuthorizationRule.EvaluateUpdate(hasAdminConfigEdit: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Activate_WithoutPermission_IsHardStop()
    {
        var result = TestServiceBillingAuthorizationRule.EvaluateActivate(hasAdminConfigActivate: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(TestServiceBillingAuthorizationRule.ActivateCode, result.Code);
    }

    [Fact]
    public void Activate_WithPermission_Passes()
    {
        var result = TestServiceBillingAuthorizationRule.EvaluateActivate(hasAdminConfigActivate: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Deactivate_WithoutPermission_IsHardStop()
    {
        var result = TestServiceBillingAuthorizationRule.EvaluateDeactivate(hasAdminConfigActivate: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(TestServiceBillingAuthorizationRule.DeactivateCode, result.Code);
    }

    [Fact]
    public void Deactivate_WithPermission_Passes()
    {
        var result = TestServiceBillingAuthorizationRule.EvaluateDeactivate(hasAdminConfigActivate: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
