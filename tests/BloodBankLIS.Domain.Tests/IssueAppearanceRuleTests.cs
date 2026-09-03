using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class IssueAppearanceRuleTests
{
    [Fact]
    public void Acceptable_Passes()
    {
        var result = IssueAppearanceRule.Evaluate(UnitAppearance.Acceptable);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Theory]
    [InlineData(UnitAppearance.Clots)]
    [InlineData(UnitAppearance.Hemolysis)]
    [InlineData(UnitAppearance.Leaking)]
    public void Defect_IsHardStop(UnitAppearance appearance)
    {
        var result = IssueAppearanceRule.Evaluate(appearance);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(IssueAppearanceRule.Code, result.Code);
    }
}
