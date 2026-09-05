using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class FacilityPolicyAuthorizationRuleTests
{
    [Fact]
    public void Update_WithoutPermission_IsHardStop()
    {
        var result = FacilityPolicyAuthorizationRule.EvaluateUpdate(hasAdminConfigEdit: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(FacilityPolicyAuthorizationRule.UpdateCode, result.Code);
    }

    [Fact]
    public void Update_WithPermission_Passes()
    {
        var result = FacilityPolicyAuthorizationRule.EvaluateUpdate(hasAdminConfigEdit: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
