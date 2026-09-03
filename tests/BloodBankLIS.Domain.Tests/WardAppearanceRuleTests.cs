using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class WardAppearanceRuleTests
{
    [Fact]
    public void Acceptable_Passes()
    {
        var result = WardAppearanceRule.Evaluate(UnitAppearance.Acceptable);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Theory]
    [InlineData(UnitAppearance.Clots)]
    [InlineData(UnitAppearance.Hemolysis)]
    [InlineData(UnitAppearance.Leaking)]
    public void Defect_IsHardStop(UnitAppearance appearance)
    {
        var result = WardAppearanceRule.Evaluate(appearance);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(WardAppearanceRule.Code, result.Code);
    }
}
