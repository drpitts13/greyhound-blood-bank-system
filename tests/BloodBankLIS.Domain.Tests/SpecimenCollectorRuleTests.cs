using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class SpecimenCollectorRuleTests
{
    // TEST-BB-513 (OCD-064): when required, a blank collector stops acceptance; otherwise it passes.
    [Theory]
    [InlineData(null, true, RuleSeverity.HardStop)]
    [InlineData("  ", true, RuleSeverity.HardStop)]
    [InlineData("RN Patel", true, RuleSeverity.Pass)]
    [InlineData(null, false, RuleSeverity.Pass)]
    public void Accept_NeedsTheCollector_OnlyWhenRequired(string? collector, bool required, RuleSeverity expected)
    {
        var result = SpecimenCollectorRule.EvaluateAccept(collector, required);
        Assert.Equal(expected, result.Severity);
        Assert.Equal(SpecimenCollectorRule.Code, result.Code);
    }
}
