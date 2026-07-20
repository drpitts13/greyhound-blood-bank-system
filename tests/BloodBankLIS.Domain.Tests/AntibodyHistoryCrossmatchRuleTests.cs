using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class AntibodyHistoryCrossmatchRuleTests
{
    [Fact]
    public void NoAntibodyHistory_AllowsSimpleOrComplex()
    {
        Assert.Equal(RuleSeverity.Pass,
            AntibodyHistoryCrossmatchRule.Evaluate(false, ResultValueType.Crossmatch, false).Severity);
        Assert.Equal(RuleSeverity.Pass,
            AntibodyHistoryCrossmatchRule.Evaluate(false, ResultValueType.ComplexCrossmatch, false).Severity);
    }

    [Fact]
    public void AntibodyHistory_Complex_PassesWithoutOverride()
    {
        var result = AntibodyHistoryCrossmatchRule.Evaluate(true, ResultValueType.ComplexCrossmatch, false);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void PositiveScreenOrHistory_Simple_RequiresOverride()
    {
        var blocked = AntibodyHistoryCrossmatchRule.Evaluate(true, ResultValueType.Crossmatch, false);
        Assert.Equal(RuleSeverity.Warning, blocked.Severity);
        Assert.Equal(AntibodyHistoryCrossmatchRule.RuleCode, blocked.Code);
        Assert.Contains("antibody screen", blocked.Message, StringComparison.OrdinalIgnoreCase);

        var allowed = AntibodyHistoryCrossmatchRule.Evaluate(true, ResultValueType.Crossmatch, true);
        Assert.Equal(RuleSeverity.Pass, allowed.Severity);
    }

    [Fact]
    public void InvalidResultType_HardStops()
    {
        var result = AntibodyHistoryCrossmatchRule.Evaluate(true, ResultValueType.Coded, false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
    }
}
