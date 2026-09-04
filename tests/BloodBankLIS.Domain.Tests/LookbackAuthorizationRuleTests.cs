using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class LookbackAuthorizationRuleTests
{
    [Fact]
    public void Recall_WithoutLookbackManage_IsHardStop()
    {
        var result = LookbackAuthorizationRule.EvaluateRecall(hasLookbackManage: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(LookbackAuthorizationRule.RecallCode, result.Code);
    }

    [Fact]
    public void Recall_WithLookbackManage_Passes()
    {
        var result = LookbackAuthorizationRule.EvaluateRecall(hasLookbackManage: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
