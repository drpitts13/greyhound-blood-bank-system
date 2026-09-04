using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ResultAuthorizationRuleTests
{
    [Fact]
    public void Verify_WithoutPermission_IsHardStop()
    {
        var result = ResultAuthorizationRule.EvaluateVerify(hasResultVerify: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ResultAuthorizationRule.VerifyCode, result.Code);
    }

    [Fact]
    public void Verify_WithPermission_Passes()
    {
        var result = ResultAuthorizationRule.EvaluateVerify(hasResultVerify: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
