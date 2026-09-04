using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class DeviationAuthorizationRuleTests
{
    [Fact]
    public void Manage_WithoutPermission_IsHardStop()
    {
        var result = DeviationAuthorizationRule.EvaluateManage(hasDeviationManage: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(DeviationAuthorizationRule.ManageCode, result.Code);
    }

    [Fact]
    public void Manage_WithPermission_Passes()
    {
        var result = DeviationAuthorizationRule.EvaluateManage(hasDeviationManage: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
