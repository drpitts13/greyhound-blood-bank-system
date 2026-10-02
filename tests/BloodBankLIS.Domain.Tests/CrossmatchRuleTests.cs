using BloodBankLIS.Domain.Enums;
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
    public void Required_Incompatible_IsOverridableWarning()
    {
        var result = CrossmatchValidityRule.Evaluate(
            requiresCrossmatch: true,
            hasValidCrossmatch: false,
            isEmergencyRelease: false,
            hasIncompatibleCrossmatch: true);
        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Contains("override", result.Message, StringComparison.OrdinalIgnoreCase);
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
        Assert.Equal(9, results.Count);
        Assert.Contains(results, r => r.Code == ElectronicCrossmatchEligibilityRule.ScreenCode && r.Severity == RuleSeverity.HardStop);
        Assert.Contains(results, r => r.Code == ElectronicCrossmatchEligibilityRule.HistoryCode && r.Severity == RuleSeverity.HardStop);
        Assert.Contains(results, r => r.Code == ElectronicCrossmatchEligibilityRule.SecondTypeCode && r.Severity == RuleSeverity.HardStop);
        Assert.Contains(results, r => r.Code == ElectronicCrossmatchEligibilityRule.CurrentTypeCode && r.Severity == RuleSeverity.Pass);
        Assert.Contains(results, r => r.Code == ElectronicCrossmatchEligibilityRule.WorkupOpenCode && r.Severity == RuleSeverity.Pass);
        Assert.Contains(results, r => r.Code == ElectronicCrossmatchEligibilityRule.ExtendedXmCode && r.Severity == RuleSeverity.Pass);
    }

    [Fact]
    public void ElectronicEligibility_OpenAntibodyIdWorkup_IsHardStop()
    {
        var result = ElectronicCrossmatchEligibilityRule.Evaluate(
            currentAboRhConfirmed: true,
            antibodyScreenNegative: true,
            hasAntibodyHistory: false,
            hasSecondConcordantAboRh: true,
            hasOpenAntibodyIdWorkup: true);

        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ElectronicCrossmatchEligibilityRule.WorkupOpenCode, result.Code);
    }

    [Fact]
    public void ElectronicEligibility_CountMinimums_BelowThreshold_IsHardStop()
    {
        var result = ElectronicCrossmatchEligibilityRule.Evaluate(
            currentAboRhConfirmed: true,
            antibodyScreenNegative: true,
            hasAntibodyHistory: false,
            hasSecondConcordantAboRh: true,
            negativeScreenVisitCount: 1,
            negativeScreenSpecimenCount: 2,
            negativeScreenTestCount: 2,
            minimumVisits: 2,
            minimumSpecimens: 2,
            minimumTests: 2);

        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ElectronicCrossmatchEligibilityRule.VisitsCode, result.Code);
    }

    [Fact]
    public void ElectronicEligibility_CountMinimums_Met_Passes()
    {
        var result = ElectronicCrossmatchEligibilityRule.Evaluate(
            currentAboRhConfirmed: true,
            antibodyScreenNegative: true,
            hasAntibodyHistory: false,
            hasSecondConcordantAboRh: true,
            negativeScreenVisitCount: 2,
            negativeScreenSpecimenCount: 2,
            negativeScreenTestCount: 2,
            minimumVisits: 2,
            minimumSpecimens: 2,
            minimumTests: 2);

        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void CurrentCompatible_MatchesTheIssuePredicate()
    {
        var now = new DateTime(2026, 9, 30, 4, 0, 0, DateTimeKind.Utc);
        Assert.True(CrossmatchValidityRule.IsCurrentCompatible(CrossmatchResult.Compatible, null, now));
        Assert.True(CrossmatchValidityRule.IsCurrentCompatible(CrossmatchResult.Compatible, now.AddMinutes(1), now));
        Assert.False(CrossmatchValidityRule.IsCurrentCompatible(CrossmatchResult.Compatible, now, now));
        Assert.False(CrossmatchValidityRule.IsCurrentCompatible(CrossmatchResult.Incompatible, null, now));
    }

    [Fact]
    public void CountsAsIncompatible_MatchesTheIssuePredicate()
    {
        Assert.True(CrossmatchValidityRule.CountsAsIncompatible(CrossmatchResult.Incompatible));
        Assert.False(CrossmatchValidityRule.CountsAsIncompatible(CrossmatchResult.Compatible));
        Assert.False(CrossmatchValidityRule.CountsAsIncompatible(CrossmatchResult.NotPerformed));
    }

    [Fact]
    public void DescribeReadyToIssue_MatchesTheStandardIssueEvaluation()
    {
        var missing = CrossmatchValidityRule.DescribeReadyToIssue(true, false, false);
        var standardMissing = CrossmatchValidityRule.Evaluate(true, false, false, false, false);
        Assert.True(missing.BlocksRoutineIssue);
        Assert.Equal(standardMissing.Message, missing.Notice);

        var incompatible = CrossmatchValidityRule.DescribeReadyToIssue(true, false, true);
        var standardIncompatible = CrossmatchValidityRule.Evaluate(true, false, false, false, true);
        Assert.False(incompatible.BlocksRoutineIssue);
        Assert.Equal(standardIncompatible.Message, incompatible.Notice);

        var current = CrossmatchValidityRule.DescribeReadyToIssue(true, true, true);
        Assert.False(current.BlocksRoutineIssue);
        Assert.Null(current.Notice);

        var notRequired = CrossmatchValidityRule.DescribeReadyToIssue(false, false, false);
        Assert.False(notRequired.BlocksRoutineIssue);
        Assert.Null(notRequired.Notice);
    }

    [Fact]
    public void CurrentCompatibleExpiresUtc_UsesTheLatestCurrentCompatible()
    {
        var now = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var soon = now.AddHours(6);
        var later = now.AddDays(2);

        Assert.Null(CrossmatchValidityRule.CurrentCompatibleExpiresUtc([], now));
        Assert.Null(CrossmatchValidityRule.CurrentCompatibleExpiresUtc(
            [(CrossmatchResult.Compatible, now)], now));
        Assert.Null(CrossmatchValidityRule.CurrentCompatibleExpiresUtc(
            [(CrossmatchResult.Incompatible, later)], now));
        Assert.Equal(later, CrossmatchValidityRule.CurrentCompatibleExpiresUtc(
            [(CrossmatchResult.Compatible, soon), (CrossmatchResult.Compatible, later)], now));
        Assert.Null(CrossmatchValidityRule.CurrentCompatibleExpiresUtc(
            [(CrossmatchResult.Compatible, soon), (CrossmatchResult.Compatible, null)], now));
    }
}
