using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class AntibodyIdentificationHistoryPostRuleTests
{
    [Fact]
    public void OpenWorkup_WithPostingText_IsHardStop()
    {
        var result = AntibodyIdentificationHistoryPostRule.EvaluateOpenWorkup(
            hasOpenWorkupInScope: true,
            freeTextWouldPostHistory: true);

        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(AntibodyIdentificationHistoryPostRule.OpenWorkupCode, result.Code);
    }

    [Fact]
    public void OpenWorkup_NegativeText_DoesNotBlock()
    {
        var result = AntibodyIdentificationHistoryPostRule.EvaluateOpenWorkup(
            hasOpenWorkupInScope: true,
            freeTextWouldPostHistory: false);

        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void NoWorkup_AllowsFreeTextPost()
    {
        var open = AntibodyIdentificationHistoryPostRule.EvaluateOpenWorkup(false, true);
        var completed = AntibodyIdentificationHistoryPostRule.EvaluateCompletedWorkup(false, ["anti-K"], []);

        Assert.Equal(RuleSeverity.Pass, open.Severity);
        Assert.All(completed, r => Assert.Equal(RuleSeverity.Pass, r.Severity));
        Assert.False(AntibodyIdentificationHistoryPostRule.ShouldSkipFreeTextPost(false));
    }

    [Fact]
    public void CompletedWorkup_SkipsPost_AndWarnsOnDisagreement()
    {
        var results = AntibodyIdentificationHistoryPostRule.EvaluateCompletedWorkup(
            hasCompletedWorkupInScope: true,
            freeTextSpecificities: ["anti-K", "anti-E"],
            workupIdentifiedSpecificities: ["anti-K"]);

        Assert.True(AntibodyIdentificationHistoryPostRule.ShouldSkipFreeTextPost(true));
        Assert.Contains(results, r =>
            r.Code == AntibodyIdentificationHistoryPostRule.AuthoritativeCode
            && r.Severity == RuleSeverity.Pass);
        Assert.Contains(results, r =>
            r.Code == AntibodyIdentificationHistoryPostRule.DisagreeCode
            && r.Severity == RuleSeverity.Warning);
    }

    [Fact]
    public void CompletedWorkup_MatchingText_SkipsWithoutDisagree()
    {
        var results = AntibodyIdentificationHistoryPostRule.EvaluateCompletedWorkup(
            true, ["anti-K"], ["anti-K"]);

        Assert.DoesNotContain(results, r => r.Code == AntibodyIdentificationHistoryPostRule.DisagreeCode);
        Assert.Contains(results, r => r.Code == AntibodyIdentificationHistoryPostRule.AuthoritativeCode);
    }

    [Fact]
    public void UnscopedOpenWorkup_AppliesToAnyPatientResult()
    {
        Assert.True(AntibodyIdentificationHistoryPostRule.AppliesToOpenWorkup(null, null, resultSpecimenId: 9, resultId: 3));
        Assert.False(AntibodyIdentificationHistoryPostRule.AppliesToCompletedWorkup(null, null, resultSpecimenId: 9, resultId: 3));
        Assert.True(AntibodyIdentificationHistoryPostRule.AppliesToCompletedWorkup(9, null, resultSpecimenId: 9, resultId: 3));
        Assert.True(AntibodyIdentificationHistoryPostRule.AppliesToOpenWorkup(9, null, resultSpecimenId: 9, resultId: 3));
        Assert.False(AntibodyIdentificationHistoryPostRule.AppliesToOpenWorkup(8, null, resultSpecimenId: 9, resultId: 3));
        Assert.True(AntibodyIdentificationHistoryPostRule.AppliesToCompletedWorkup(null, 3, resultSpecimenId: 9, resultId: 3));
    }

    [Theory]
    [InlineData(AntibodyWorkupStatus.InProgress, true)]
    [InlineData(AntibodyWorkupStatus.PendingInterpretation, true)]
    [InlineData(AntibodyWorkupStatus.PendingSupervisorReview, true)]
    [InlineData(AntibodyWorkupStatus.Completed, false)]
    [InlineData(AntibodyWorkupStatus.Voided, false)]
    public void OpenStatuses(AntibodyWorkupStatus status, bool expected) =>
        Assert.Equal(expected, AntibodyIdentificationHistoryPostRule.IsOpen(status));

    [Fact]
    public void SpecificityCompare_IsOrdinal()
    {
        Assert.True(AntibodyIdentificationHistoryPostRule.SameSpecificities(["anti-K"], ["anti-K"]));
        Assert.False(AntibodyIdentificationHistoryPostRule.SameSpecificities(["anti-K"], ["anti-k"]));
    }

    [Fact]
    public void ManualHistoryAdd_OpenWorkup_IsHardStop()
    {
        var result = AntibodyIdentificationHistoryPostRule.EvaluateManualHistoryAdd(hasOpenWorkupOnPatient: true);

        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(AntibodyIdentificationHistoryPostRule.OpenWorkupCode, result.Code);
    }

    [Fact]
    public void ManualHistoryAdd_NoOpenWorkup_Passes()
    {
        var result = AntibodyIdentificationHistoryPostRule.EvaluateManualHistoryAdd(hasOpenWorkupOnPatient: false);

        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void ManualHistoryDeactivate_OpenWorkup_IsHardStop()
    {
        var result = AntibodyIdentificationHistoryPostRule.EvaluateManualHistoryDeactivate(hasOpenWorkupOnPatient: true);

        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(AntibodyIdentificationHistoryPostRule.OpenWorkupCode, result.Code);
    }

    [Fact]
    public void ManualHistoryDeactivate_NoOpenWorkup_Passes()
    {
        var result = AntibodyIdentificationHistoryPostRule.EvaluateManualHistoryDeactivate(hasOpenWorkupOnPatient: false);

        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Allocate_OpenWorkup_IsWarning()
    {
        var result = AntibodyIdentificationHistoryPostRule.EvaluateAllocateOpenWorkup(hasOpenWorkupOnPatient: true);

        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal(AntibodyIdentificationHistoryPostRule.AllocateOpenCode, result.Code);
    }

    [Fact]
    public void Allocate_NoOpenWorkup_Passes()
    {
        var result = AntibodyIdentificationHistoryPostRule.EvaluateAllocateOpenWorkup(hasOpenWorkupOnPatient: false);

        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Crossmatch_OpenWorkup_IsWarning()
    {
        var result = AntibodyIdentificationHistoryPostRule.EvaluateCrossmatchOpenWorkup(hasOpenWorkupOnPatient: true);

        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal(AntibodyIdentificationHistoryPostRule.CrossmatchOpenCode, result.Code);
    }

    [Fact]
    public void Crossmatch_NoOpenWorkup_Passes()
    {
        var result = AntibodyIdentificationHistoryPostRule.EvaluateCrossmatchOpenWorkup(hasOpenWorkupOnPatient: false);

        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Issue_OpenWorkup_IsWarning()
    {
        var result = AntibodyIdentificationHistoryPostRule.EvaluateIssueOpenWorkup(hasOpenWorkupOnPatient: true);

        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal(AntibodyIdentificationHistoryPostRule.IssueOpenCode, result.Code);
    }

    [Fact]
    public void Issue_NoOpenWorkup_Passes()
    {
        var result = AntibodyIdentificationHistoryPostRule.EvaluateIssueOpenWorkup(hasOpenWorkupOnPatient: false);

        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void SpecialRequirementOpenWorkup_IsWarning()
    {
        var result = AntibodyIdentificationHistoryPostRule.EvaluateSpecialRequirementOpenWorkup(hasOpenWorkupOnPatient: true);

        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal(AntibodyIdentificationHistoryPostRule.SpecialRequirementOpenCode, result.Code);
    }

    [Fact]
    public void SpecialRequirementNoOpenWorkup_Passes()
    {
        var result = AntibodyIdentificationHistoryPostRule.EvaluateSpecialRequirementOpenWorkup(hasOpenWorkupOnPatient: false);

        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void AntigenOpenWorkup_IsWarning()
    {
        var result = AntibodyIdentificationHistoryPostRule.EvaluateAntigenOpenWorkup(hasOpenWorkupOnPatient: true);

        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal(AntibodyIdentificationHistoryPostRule.AntigenOpenCode, result.Code);
    }

    [Fact]
    public void AntigenNoOpenWorkup_Passes()
    {
        var result = AntibodyIdentificationHistoryPostRule.EvaluateAntigenOpenWorkup(hasOpenWorkupOnPatient: false);

        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void BloodTypeOpenWorkup_IsWarning()
    {
        var result = AntibodyIdentificationHistoryPostRule.EvaluateBloodTypeOpenWorkup(hasOpenWorkupOnPatient: true);

        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal(AntibodyIdentificationHistoryPostRule.BloodTypeOpenCode, result.Code);
    }

    [Fact]
    public void BloodTypeNoOpenWorkup_Passes()
    {
        var result = AntibodyIdentificationHistoryPostRule.EvaluateBloodTypeOpenWorkup(hasOpenWorkupOnPatient: false);

        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
