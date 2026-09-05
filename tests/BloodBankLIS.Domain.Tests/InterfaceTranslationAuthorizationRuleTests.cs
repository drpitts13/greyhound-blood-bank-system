using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class InterfaceTranslationAuthorizationRuleTests
{
    [Fact]
    public void Replace_WithoutPermission_IsHardStop()
    {
        var result = InterfaceTranslationAuthorizationRule.EvaluateReplace(hasAdminHl7Manage: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(InterfaceTranslationAuthorizationRule.ReplaceCode, result.Code);
    }

    [Fact]
    public void Replace_WithPermission_Passes()
    {
        var result = InterfaceTranslationAuthorizationRule.EvaluateReplace(hasAdminHl7Manage: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
