using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ReceiveAppearanceRuleTests
{
    [Fact]
    public void Acceptable_Passes()
    {
        var result = ReceiveAppearanceRule.Evaluate(required: true, UnitAppearance.Acceptable);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Theory]
    [InlineData(UnitAppearance.Clots)]
    [InlineData(UnitAppearance.Hemolysis)]
    [InlineData(UnitAppearance.Leaking)]
    public void Defect_IsHardStop(UnitAppearance appearance)
    {
        var result = ReceiveAppearanceRule.Evaluate(required: true, appearance);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ReceiveAppearanceRule.Code, result.Code);
    }

    [Fact]
    public void DefectWhenNotRequired_Passes()
    {
        var result = ReceiveAppearanceRule.Evaluate(required: false, UnitAppearance.Hemolysis);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
