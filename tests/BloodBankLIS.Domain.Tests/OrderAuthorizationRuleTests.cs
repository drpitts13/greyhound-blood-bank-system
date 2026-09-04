using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class OrderAuthorizationRuleTests
{
    [Fact]
    public void Update_WithoutPatientWrite_IsHardStop()
    {
        var result = OrderAuthorizationRule.EvaluateUpdate(hasPatientWrite: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(OrderAuthorizationRule.UpdateCode, result.Code);
    }

    [Fact]
    public void Update_WithPatientWrite_Passes()
    {
        var result = OrderAuthorizationRule.EvaluateUpdate(hasPatientWrite: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Cancel_WithoutPatientWrite_IsHardStop()
    {
        var result = OrderAuthorizationRule.EvaluateCancel(hasPatientWrite: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(OrderAuthorizationRule.CancelCode, result.Code);
    }

    [Fact]
    public void Cancel_WithPatientWrite_Passes()
    {
        var result = OrderAuthorizationRule.EvaluateCancel(hasPatientWrite: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Link_WithoutPatientWrite_IsHardStop()
    {
        var result = OrderAuthorizationRule.EvaluateLink(hasPatientWrite: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(OrderAuthorizationRule.LinkCode, result.Code);
    }

    [Fact]
    public void Link_WithPatientWrite_Passes()
    {
        var result = OrderAuthorizationRule.EvaluateLink(hasPatientWrite: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
