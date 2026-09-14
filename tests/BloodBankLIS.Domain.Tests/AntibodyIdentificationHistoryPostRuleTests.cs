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
            && r.Severity == RuleSeverity.Warning);
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
        Assert.Contains(results, r =>
            r.Code == AntibodyIdentificationHistoryPostRule.AuthoritativeCode
            && r.Severity == RuleSeverity.Warning);
    }

    [Fact]
    public void CompletedWorkup_EmptyVerifyLabels_WarnsDisagree()
    {
        var results = AntibodyIdentificationHistoryPostRule.EvaluateCompletedWorkup(
            hasCompletedWorkupInScope: true,
            freeTextSpecificities: [],
            workupIdentifiedSpecificities: ["anti-K"]);

        Assert.Contains(results, r => r.Code == AntibodyIdentificationHistoryPostRule.AuthoritativeCode);
        Assert.Contains(results, r =>
            r.Code == AntibodyIdentificationHistoryPostRule.DisagreeCode
            && r.Severity == RuleSeverity.Warning);
    }

    [Fact]
    public void UnscopedOpenWorkup_AppliesToAnyPatientResult()
    {
        Assert.True(AntibodyIdentificationHistoryPostRule.AppliesToOpenWorkup(null, null, resultSpecimenId: 9, resultId: 3));
        Assert.False(AntibodyIdentificationHistoryPostRule.AppliesToCompletedWorkup(null, null, resultSpecimenId: 9, resultId: 3));
        Assert.True(AntibodyIdentificationHistoryPostRule.AppliesToCompletedWorkup(9, null, resultSpecimenId: 9, resultId: 3));
        Assert.True(AntibodyIdentificationHistoryPostRule.AppliesToOpenWorkup(9, null, resultSpecimenId: 9, resultId: 3));
        Assert.False(AntibodyIdentificationHistoryPostRule.AppliesToOpenWorkup(8, null, resultSpecimenId: 9, resultId: 3));
        Assert.True(AntibodyIdentificationHistoryPostRule.AppliesToOpenWorkup(
            8, null, resultSpecimenId: 9, resultId: 3, workupSpecimenUnusable: true));
        Assert.True(AntibodyIdentificationHistoryPostRule.AppliesToCompletedWorkup(null, 3, resultSpecimenId: 9, resultId: 3));
    }

    [Fact]
    public void RejectSpecimen_OpenWorkup_Warns()
    {
        var open = AntibodyIdentificationHistoryPostRule.EvaluateSpecimenRejectedOpenWorkup(true);
        var none = AntibodyIdentificationHistoryPostRule.EvaluateSpecimenRejectedOpenWorkup(false);

        Assert.Equal(RuleSeverity.Warning, open.Severity);
        Assert.Equal(AntibodyIdentificationHistoryPostRule.SpecimenOpenCode, open.Code);
        Assert.Equal(RuleSeverity.Pass, none.Severity);
    }

    [Fact]
    public void EditSpecimen_OpenWorkup_Warns()
    {
        var open = AntibodyIdentificationHistoryPostRule.EvaluateSpecimenEditedOpenWorkup(true);
        var none = AntibodyIdentificationHistoryPostRule.EvaluateSpecimenEditedOpenWorkup(false);

        Assert.Equal(RuleSeverity.Warning, open.Severity);
        Assert.Equal(AntibodyIdentificationHistoryPostRule.SpecimenEditCode, open.Code);
        Assert.Equal(RuleSeverity.Pass, none.Severity);
    }

    [Fact]
    public void InvalidateAntigen_OpenWorkup_Warns()
    {
        var open = AntibodyIdentificationHistoryPostRule.EvaluateAntigenInvalidateOpenWorkup(true);
        var none = AntibodyIdentificationHistoryPostRule.EvaluateAntigenInvalidateOpenWorkup(false);

        Assert.Equal(RuleSeverity.Warning, open.Severity);
        Assert.Equal(AntibodyIdentificationHistoryPostRule.AntigenInvalidateCode, open.Code);
        Assert.Equal(RuleSeverity.Pass, none.Severity);
    }

    [Fact]
    public void InvalidateBloodType_OpenWorkup_Warns()
    {
        var open = AntibodyIdentificationHistoryPostRule.EvaluateBloodTypeInvalidateOpenWorkup(true);
        var none = AntibodyIdentificationHistoryPostRule.EvaluateBloodTypeInvalidateOpenWorkup(false);

        Assert.Equal(RuleSeverity.Warning, open.Severity);
        Assert.Equal(AntibodyIdentificationHistoryPostRule.BloodTypeInvalidateCode, open.Code);
        Assert.Equal(RuleSeverity.Pass, none.Severity);
    }

    [Theory]
    [InlineData(AntibodyWorkupStatus.InProgress, true)]
    [InlineData(AntibodyWorkupStatus.PendingInterpretation, true)]
    [InlineData(AntibodyWorkupStatus.PendingSupervisorReview, true)]
    [InlineData(AntibodyWorkupStatus.Completed, false)]
    [InlineData(AntibodyWorkupStatus.Voided, false)]
    public void OpenStatuses(AntibodyWorkupStatus status, bool expected) =>
        Assert.Equal(expected, AntibodyIdentificationHistoryPostRule.IsOpen(status));

    [Theory]
    [InlineData(AntibodyWorkupStatus.InProgress, false)]
    [InlineData(AntibodyWorkupStatus.PendingInterpretation, false)]
    [InlineData(AntibodyWorkupStatus.PendingSupervisorReview, false)]
    [InlineData(AntibodyWorkupStatus.Completed, true)]
    [InlineData(AntibodyWorkupStatus.Voided, false)]
    public void CompletedStatuses(AntibodyWorkupStatus status, bool expected) =>
        Assert.Equal(expected, AntibodyIdentificationHistoryPostRule.IsCompleted(status));

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
    public void DeactivatePostedWorkup_IsWarning()
    {
        var result = AntibodyIdentificationHistoryPostRule.EvaluateManualHistoryDeactivatePostedWorkup(
            postedByCompletedWorkup: true);

        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal(AntibodyIdentificationHistoryPostRule.DeactivatePostedCode, result.Code);
    }

    [Fact]
    public void DeactivateNotPostedByWorkup_Passes()
    {
        var result = AntibodyIdentificationHistoryPostRule.EvaluateManualHistoryDeactivatePostedWorkup(
            postedByCompletedWorkup: false);

        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void MatchesPostedWorkupFinding_ByCatalogOrSpecificity()
    {
        (long? CatalogId, string Specificity)[] posted = [(10, "anti-K"), (null, "anti-E")];

        Assert.True(AntibodyIdentificationHistoryPostRule.MatchesPostedWorkupFinding(10, "anti-K", posted));
        Assert.True(AntibodyIdentificationHistoryPostRule.MatchesPostedWorkupFinding(null, "anti-E", posted));
        Assert.False(AntibodyIdentificationHistoryPostRule.MatchesPostedWorkupFinding(11, "anti-c", posted));
    }

    [Fact]
    public void AllocateOpenWorkup_IsWarning()
    {
        var result = AntibodyIdentificationHistoryPostRule.EvaluateAllocateOpenWorkup(hasOpenWorkupOnPatient: true);

        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal(AntibodyIdentificationHistoryPostRule.AllocateOpenCode, result.Code);
    }

    [Fact]
    public void AllocateNoOpenWorkup_Passes()
    {
        var result = AntibodyIdentificationHistoryPostRule.EvaluateAllocateOpenWorkup(hasOpenWorkupOnPatient: false);

        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void CrossmatchOpenWorkup_IsWarning()
    {
        var result = AntibodyIdentificationHistoryPostRule.EvaluateCrossmatchOpenWorkup(hasOpenWorkupOnPatient: true);

        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal(AntibodyIdentificationHistoryPostRule.CrossmatchOpenCode, result.Code);
    }

    [Fact]
    public void CrossmatchNoOpenWorkup_Passes()
    {
        var result = AntibodyIdentificationHistoryPostRule.EvaluateCrossmatchOpenWorkup(hasOpenWorkupOnPatient: false);

        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void IssueOpenWorkup_IsWarning()
    {
        var result = AntibodyIdentificationHistoryPostRule.EvaluateIssueOpenWorkup(hasOpenWorkupOnPatient: true);

        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal(AntibodyIdentificationHistoryPostRule.IssueOpenCode, result.Code);
    }

    [Fact]
    public void IssueNoOpenWorkup_Passes()
    {
        var result = AntibodyIdentificationHistoryPostRule.EvaluateIssueOpenWorkup(hasOpenWorkupOnPatient: false);

        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void TypeCorrectionOpenWorkup_IsWarning()
    {
        var result = AntibodyIdentificationHistoryPostRule.EvaluateTypeCorrectionOpenWorkup(true);

        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal(AntibodyIdentificationHistoryPostRule.TypeCorrectionOpenCode, result.Code);
    }

    [Fact]
    public void TypeCorrectionNoOpenWorkup_Passes()
    {
        var result = AntibodyIdentificationHistoryPostRule.EvaluateTypeCorrectionOpenWorkup(false);

        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void PendingTypeCorrection_IsHardStop()
    {
        var result = AntibodyIdentificationHistoryPostRule.EvaluatePendingTypeCorrection(true);

        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(AntibodyIdentificationHistoryPostRule.PendingTypeCorrectionCode, result.Code);
    }

    [Fact]
    public void NoPendingTypeCorrection_Passes()
    {
        var result = AntibodyIdentificationHistoryPostRule.EvaluatePendingTypeCorrection(false);

        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void PendingTypeCorrection_AppliesToUnscopedOrSameSpecimen()
    {
        Assert.True(AntibodyIdentificationHistoryPostRule.PendingTypeCorrectionApplies(null, 9));
        Assert.True(AntibodyIdentificationHistoryPostRule.PendingTypeCorrectionApplies(9, 9));
        Assert.False(AntibodyIdentificationHistoryPostRule.PendingTypeCorrectionApplies(8, 9));
    }

    [Fact]
    public void AntigenCompletedWorkup_IsWarning()
    {
        var result = AntibodyIdentificationHistoryPostRule.EvaluateAntigenCompletedWorkup(true);

        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal(AntibodyIdentificationHistoryPostRule.AntigenCompletedCode, result.Code);
    }

    [Fact]
    public void AntigenNoCompletedWorkup_Passes()
    {
        var result = AntibodyIdentificationHistoryPostRule.EvaluateAntigenCompletedWorkup(false);

        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void AntigenCompletedWorkup_PostedConflict_IsWarning()
    {
        var result = AntibodyIdentificationHistoryPostRule.EvaluateAntigenCompletedWorkup(
            hasCompletedWorkupInScope: false,
            postedIdentifiedConflictsWithType: true);

        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal(AntibodyIdentificationHistoryPostRule.AntigenCompletedCode, result.Code);
    }

    [Fact]
    public void BloodTypeCompletedWorkup_IsWarning()
    {
        var result = AntibodyIdentificationHistoryPostRule.EvaluateBloodTypeCompletedWorkup(true);

        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal(AntibodyIdentificationHistoryPostRule.BloodTypeCompletedCode, result.Code);
    }

    [Fact]
    public void BloodTypeNoCompletedWorkup_Passes()
    {
        var result = AntibodyIdentificationHistoryPostRule.EvaluateBloodTypeCompletedWorkup(false);

        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
