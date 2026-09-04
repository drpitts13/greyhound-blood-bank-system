using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class InventoryAuthorizationRuleTests
{
    [Fact]
    public void QuarantineRelease_WithoutPermission_IsHardStop()
    {
        var result = InventoryAuthorizationRule.EvaluateQuarantineRelease(hasInventoryRelease: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(InventoryAuthorizationRule.QuarantineReleaseCode, result.Code);
    }

    [Fact]
    public void QuarantineRelease_WithPermission_Passes()
    {
        var result = InventoryAuthorizationRule.EvaluateQuarantineRelease(hasInventoryRelease: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void DirectedConversion_WithoutPermission_IsHardStop()
    {
        var result = InventoryAuthorizationRule.EvaluateDirectedConversion(hasInventoryRelease: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(InventoryAuthorizationRule.DirectedConversionCode, result.Code);
    }

    [Fact]
    public void DirectedConversion_WithPermission_Passes()
    {
        var result = InventoryAuthorizationRule.EvaluateDirectedConversion(hasInventoryRelease: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
