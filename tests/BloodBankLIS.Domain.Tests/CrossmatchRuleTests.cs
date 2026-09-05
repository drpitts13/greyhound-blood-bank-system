using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class CrossmatchRuleTests
{
    [Fact]
    public void NotRequired_Passes()
    {
        var result = CrossmatchValidityRule.Evaluate(requiresCrossmatch: false, hasValidCrossmatch: false, isEmergencyRelease: false);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Required_WithValidCrossmatch_Passes()
    {
        var result = CrossmatchValidityRule.Evaluate(true, true, false);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Required_Missing_IsHardStop()
    {
        var result = CrossmatchValidityRule.Evaluate(true, false, isEmergencyRelease: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
    }

    [Fact]
    public void Required_Missing_UnderEmergency_IsOverridableWarning()
    {
        var result = CrossmatchValidityRule.Evaluate(true, false, isEmergencyRelease: true);
        Assert.Equal(RuleSeverity.Warning, result.Severity);
    }

    [Fact]
    public void ElectronicEligibility_AllPreconditions_Passes()
    {
        var result = ElectronicCrossmatchEligibilityRule.Evaluate(
            currentAboRhConfirmed: true, antibodyScreenNegative: true, hasAntibodyHistory: false, hasSecondConcordantAboRh: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Theory]
    [InlineData(false, true, false, true)]
    [InlineData(true, false, false, true)]
    [InlineData(true, true, true, true)]
    [InlineData(true, true, false, false)]
    public void ElectronicEligibility_AnyFailure_IsHardStop(bool aboRh, bool screenNeg, bool antibodyHx, bool secondAbo)
    {
        var result = ElectronicCrossmatchEligibilityRule.Evaluate(aboRh, screenNeg, antibodyHx, secondAbo);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
    }

    [Fact]
    public void ElectronicEligibility_Criteria_ReportEachCheck()
    {
        var results = ElectronicCrossmatchEligibilityRule.EvaluateCriteria(true, false, true, false);
        Assert.Equal(4, results.Count);
        Assert.Contains(results, r => r.Code == ElectronicCrossmatchEligibilityRule.ScreenCode && r.Severity == RuleSeverity.HardStop);
        Assert.Contains(results, r => r.Code == ElectronicCrossmatchEligibilityRule.HistoryCode && r.Severity == RuleSeverity.HardStop);
        Assert.Contains(results, r => r.Code == ElectronicCrossmatchEligibilityRule.SecondTypeCode && r.Severity == RuleSeverity.HardStop);
        Assert.Contains(results, r => r.Code == ElectronicCrossmatchEligibilityRule.CurrentTypeCode && r.Severity == RuleSeverity.Pass);
    }
}
