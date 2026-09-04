using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class PatientMergeRuleTests
{
    [Fact]
    public void SelfMerge_IsHardStopped()
    {
        var results = PatientMergeRule.Evaluate(
            10, 10, PatientStatus.Active, PatientStatus.Active, null,
            AboGroup.Unknown, RhType.Unknown, AboGroup.Unknown, RhType.Unknown);
        Assert.Contains(results, r => r.Severity == RuleSeverity.HardStop && r.Code == PatientMergeRule.IdentityCode);
    }

    [Fact]
    public void SurvivorAlreadyMerged_IsHardStopped()
    {
        var results = PatientMergeRule.Evaluate(
            1, 2, PatientStatus.Merged, PatientStatus.Active, null,
            AboGroup.O, RhType.Positive, AboGroup.Unknown, RhType.Unknown);
        Assert.Contains(results, r => r.Severity == RuleSeverity.HardStop && r.Code == PatientMergeRule.StatusCode);
    }

    [Fact]
    public void DuplicateAlreadyMergedToSameSurvivor_Passes()
    {
        var results = PatientMergeRule.Evaluate(
            1, 2, PatientStatus.Active, PatientStatus.Merged, 1,
            AboGroup.Unknown, RhType.Unknown, AboGroup.Unknown, RhType.Unknown);
        Assert.DoesNotContain(results, r => r.Severity == RuleSeverity.HardStop);
    }

    [Fact]
    public void DuplicateAlreadyMergedToOther_IsHardStopped()
    {
        var results = PatientMergeRule.Evaluate(
            1, 2, PatientStatus.Active, PatientStatus.Merged, 99,
            AboGroup.Unknown, RhType.Unknown, AboGroup.Unknown, RhType.Unknown);
        Assert.Contains(results, r => r.Severity == RuleSeverity.HardStop && r.Code == PatientMergeRule.IdentityCode);
    }

    [Fact]
    public void DiscordantAbo_IsHardStopped()
    {
        var result = PatientMergeRule.EvaluateBloodType(AboGroup.O, RhType.Positive, AboGroup.A, RhType.Positive);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(PatientMergeRule.AboCode, result.Code);
    }

    [Fact]
    public void DiscordantRh_IsHardStopped()
    {
        var result = PatientMergeRule.EvaluateBloodType(AboGroup.O, RhType.Positive, AboGroup.O, RhType.Negative);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
    }

    [Fact]
    public void ConcordantTypes_Pass()
    {
        var results = PatientMergeRule.Evaluate(
            1, 2, PatientStatus.Active, PatientStatus.Active, null,
            AboGroup.O, RhType.Positive, AboGroup.O, RhType.Positive);
        Assert.DoesNotContain(results, r => r.Severity == RuleSeverity.HardStop);
        Assert.Contains(results, r => r.Code == PatientMergeRule.AboCode && r.Severity == RuleSeverity.Pass);
    }

    [Fact]
    public void OneSidedHistoricalType_IsWarning()
    {
        var result = PatientMergeRule.EvaluateBloodType(AboGroup.O, RhType.Positive, AboGroup.Unknown, RhType.Unknown);
        Assert.Equal(RuleSeverity.Warning, result.Severity);
    }

    [Fact]
    public void MergedRecord_CannotBeUsedClinically()
    {
        var result = PatientMergeRule.EvaluateClinicalUse(PatientStatus.Merged);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(PatientMergeRule.ClinicalUseCode, result.Code);
    }

    [Fact]
    public void ActiveAndInactiveRecords_RemainClinicallyUsable()
    {
        Assert.Equal(RuleSeverity.Pass, PatientMergeRule.EvaluateClinicalUse(PatientStatus.Active).Severity);
        Assert.Equal(RuleSeverity.Pass, PatientMergeRule.EvaluateClinicalUse(PatientStatus.Inactive).Severity);
    }
}
