using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class InstrumentInterfaceAuthorizationRuleTests
{
    [Fact]
    public void Create_WithoutPermission_IsHardStop()
    {
        var result = InstrumentInterfaceAuthorizationRule.EvaluateCreate(hasManage: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(InstrumentInterfaceAuthorizationRule.CreateCode, result.Code);
    }

    [Fact]
    public void Create_WithPermission_Passes()
    {
        var result = InstrumentInterfaceAuthorizationRule.EvaluateCreate(hasManage: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Update_WithoutPermission_IsHardStop()
    {
        var result = InstrumentInterfaceAuthorizationRule.EvaluateUpdate(hasManage: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(InstrumentInterfaceAuthorizationRule.UpdateCode, result.Code);
    }

    [Fact]
    public void Update_WithPermission_Passes()
    {
        var result = InstrumentInterfaceAuthorizationRule.EvaluateUpdate(hasManage: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Enable_WithoutPermission_IsHardStop()
    {
        var result = InstrumentInterfaceAuthorizationRule.EvaluateEnable(hasManage: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(InstrumentInterfaceAuthorizationRule.EnableCode, result.Code);
    }

    [Fact]
    public void Enable_WithPermission_Passes()
    {
        var result = InstrumentInterfaceAuthorizationRule.EvaluateEnable(hasManage: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Disable_WithoutPermission_IsHardStop()
    {
        var result = InstrumentInterfaceAuthorizationRule.EvaluateDisable(hasManage: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(InstrumentInterfaceAuthorizationRule.DisableCode, result.Code);
    }

    [Fact]
    public void Disable_WithPermission_Passes()
    {
        var result = InstrumentInterfaceAuthorizationRule.EvaluateDisable(hasManage: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
