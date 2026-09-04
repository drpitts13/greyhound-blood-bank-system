using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ImmunoAuthorizationRuleTests
{
    [Fact]
    public void ManualBloodType_WithoutOverride_IsHardStop()
    {
        var result = ImmunoAuthorizationRule.EvaluateManualBloodType(hasImmunoOverride: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ImmunoAuthorizationRule.ManualBloodTypeCode, result.Code);
    }

    [Fact]
    public void ManualBloodType_WithOverride_Passes()
    {
        var result = ImmunoAuthorizationRule.EvaluateManualBloodType(hasImmunoOverride: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
