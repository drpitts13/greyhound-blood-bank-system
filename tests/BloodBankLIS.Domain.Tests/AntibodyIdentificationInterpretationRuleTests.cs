using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Tests;

public class AntibodyIdentificationInterpretationRuleTests
{
    [Fact]
    public void AssistIdentified_IsHardStop()
    {
        var result = AntibodyIdentificationInterpretationRule.EvaluateAssistMustNotIdentify(
        [
            new AntibodyIdentificationRecordedFinding("anti-K", "K", AntibodyIdClassification.Identified, AntibodyIdSource.Assist)
        ]);

        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(AntibodyIdentificationInterpretationRule.AssistIdentifiedCode, result.Code);
    }

    [Fact]
    public void TechnologistIdentified_IsAllowed()
    {
        var result = AntibodyIdentificationInterpretationRule.EvaluateAssistMustNotIdentify(
        [
            new AntibodyIdentificationRecordedFinding("anti-K", "K", AntibodyIdClassification.Identified, AntibodyIdSource.Technologist)
        ]);

        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void MissingInterpretation_HardStopsCompletion()
    {
        var evaluation = AntibodyIdentificationInterpretationRule.EvaluateCompletion(
            AntibodyWorkupStatus.InProgress,
            hasTechnologistInterpretation: false,
            findings: [],
            AntibodyIdentificationPolicy.Default,
            supervisorReviewed: false,
            supervisorAccepted: false,
            technologistUser: null,
            supervisorUser: null);

        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == AntibodyIdentificationInterpretationRule.InterpretationRequiredCode);
        Assert.Contains(evaluation.HardStops, r => r.Code == AntibodyIdentificationInterpretationRule.ReviewRequiredCode);
    }

    [Fact]
    public void SameUserReview_HardStopsWhenPolicyOn()
    {
        var evaluation = AntibodyIdentificationInterpretationRule.EvaluateCompletion(
            AntibodyWorkupStatus.PendingSupervisorReview,
            hasTechnologistInterpretation: true,
            findings:
            [
                new AntibodyIdentificationRecordedFinding("anti-K", "K", AntibodyIdClassification.Identified, AntibodyIdSource.Technologist)
            ],
            AntibodyIdentificationPolicy.Default,
            supervisorReviewed: true,
            supervisorAccepted: true,
            technologistUser: "tech-a",
            supervisorUser: "tech-a");

        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == AntibodyIdentificationInterpretationRule.ReviewSelfCode);
    }

    [Fact]
    public void DistinctSupervisorAccept_AllowsCompletion()
    {
        var evaluation = AntibodyIdentificationInterpretationRule.EvaluateCompletion(
            AntibodyWorkupStatus.PendingSupervisorReview,
            hasTechnologistInterpretation: true,
            findings:
            [
                new AntibodyIdentificationRecordedFinding("anti-K", "K", AntibodyIdClassification.Identified, AntibodyIdSource.Technologist)
            ],
            AntibodyIdentificationPolicy.Default,
            supervisorReviewed: true,
            supervisorAccepted: true,
            technologistUser: "tech-a",
            supervisorUser: "supervisor-b");

        Assert.True(evaluation.IsAllowed);
    }

    [Fact]
    public void SupervisorReject_HardStops()
    {
        var result = AntibodyIdentificationInterpretationRule.EvaluateSupervisorReview(
            AntibodyIdentificationPolicy.Default,
            supervisorReviewed: true,
            supervisorAccepted: false,
            technologistUser: "tech-a",
            supervisorUser: "supervisor-b");

        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(AntibodyIdentificationInterpretationRule.ReviewRejectedCode, result.Code);
    }

    [Fact]
    public void Void_RequiresReason()
    {
        var evaluation = AntibodyIdentificationInterpretationRule.EvaluateVoid(
            AntibodyWorkupStatus.InProgress, reason: "  ");

        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == AntibodyIdentificationInterpretationRule.VoidReasonCode);
    }

    [Fact]
    public void Void_Completed_IsHardStop()
    {
        var evaluation = AntibodyIdentificationInterpretationRule.EvaluateVoid(
            AntibodyWorkupStatus.Completed, reason: "opened on wrong patient");

        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == AntibodyIdentificationInterpretationRule.VoidCompletedCode);
    }

    [Fact]
    public void Void_AlreadyVoided_IsHardStop()
    {
        var evaluation = AntibodyIdentificationInterpretationRule.EvaluateVoid(
            AntibodyWorkupStatus.Voided, reason: "duplicate");

        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == AntibodyIdentificationInterpretationRule.VoidedCode);
    }

    [Fact]
    public void Void_InProgressWithReason_IsAllowed()
    {
        var evaluation = AntibodyIdentificationInterpretationRule.EvaluateVoid(
            AntibodyWorkupStatus.InProgress, reason: "Wrong panel lot selected.");

        Assert.True(evaluation.IsAllowed);
    }

    [Fact]
    public void IdentifiedOnPhenotypePositive_IsWarning()
    {
        var result = AntibodyIdentificationInterpretationRule.EvaluateIdentifiedVersusPatientType(
            AntibodyIdClassification.Identified,
            "K",
            "anti-K",
            [new PatientAntigenSnapshot("K", AntigenResult.Positive, FromGenotype: false)]);

        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal(AntibodyIdentificationInterpretationRule.IdentifiedPhenotypeConflictCode, result.Code);
    }

    [Fact]
    public void IdentifiedOnPredictedGenotypePositive_IsWarning()
    {
        var result = AntibodyIdentificationInterpretationRule.EvaluateIdentifiedVersusPatientType(
            AntibodyIdClassification.Identified,
            "K",
            "anti-K",
            [new PatientAntigenSnapshot("K", AntigenResult.Positive, FromGenotype: true)]);

        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal(AntibodyIdentificationInterpretationRule.IdentifiedGenotypeConflictCode, result.Code);
    }

    [Fact]
    public void IdentifiedOnPhenotypeNegative_IsPass()
    {
        var result = AntibodyIdentificationInterpretationRule.EvaluateIdentifiedVersusPatientType(
            AntibodyIdClassification.Identified,
            "K",
            "anti-K",
            [new PatientAntigenSnapshot("K", AntigenResult.Negative, FromGenotype: false)]);

        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void StaleInterpretation_BlocksSupervisorReview()
    {
        var result = AntibodyIdentificationInterpretationRule.EvaluateReadyForSupervisorReview(false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(AntibodyIdentificationInterpretationRule.InterpretationStaleCode, result.Code);
    }

    [Fact]
    public void PanelChangeAfterInterpretation_HardStops()
    {
        var interpreted = new DateTime(2026, 9, 4, 16, 0, 0, DateTimeKind.Utc);
        var result = AntibodyIdentificationInterpretationRule.EvaluateInterpretationCurrent(
            interpreted, interpreted.AddMinutes(1));
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(AntibodyIdentificationInterpretationRule.InterpretationStaleCode, result.Code);
    }

    [Fact]
    public void PanelChangeBeforeInterpretation_IsPass()
    {
        var interpreted = new DateTime(2026, 9, 4, 16, 0, 0, DateTimeKind.Utc);
        var result = AntibodyIdentificationInterpretationRule.EvaluateInterpretationCurrent(
            interpreted, interpreted.AddMinutes(-1));
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void PanelChangeAfterReview_HardStops()
    {
        var reviewed = new DateTime(2026, 9, 4, 16, 10, 0, DateTimeKind.Utc);
        var result = AntibodyIdentificationInterpretationRule.EvaluateReviewCurrent(
            reviewed, reviewed.AddSeconds(1));
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(AntibodyIdentificationInterpretationRule.ReviewStaleCode, result.Code);
    }

    [Fact]
    public void ActiveHistory_WarnsAtComplete()
    {
        var result = AntibodyIdentificationInterpretationRule.EvaluateHistoryRemainsAtCompletion(
        [
            new HistoricalAntibodySnapshot("anti-K", "K", AntibodyStatus.Identified, true)
        ]);
        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal(AntibodyIdentificationInterpretationRule.HistoryRemainsCode, result.Code);
    }

    [Fact]
    public void InactiveHistory_DoesNotWarnHistoryRemains()
    {
        var result = AntibodyIdentificationInterpretationRule.EvaluateHistoryRemainsAtCompletion(
        [
            new HistoricalAntibodySnapshot("anti-K", "K", AntibodyStatus.HistoricalOnly, false)
        ]);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void IdentifiedWhenAssistExcluded_Warns()
    {
        var result = AntibodyIdentificationInterpretationRule.EvaluateIdentifiedVersusAssistExclusion(
            [new AntibodyIdentificationRecordedFinding("anti-K", "K", AntibodyIdClassification.Identified, AntibodyIdSource.Technologist)],
            [new AntibodyIdentificationAssistFinding("anti-K", "K", AntibodyIdClassification.Excluded, "ruled out", 1, 0, 0, 0)]);
        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal(AntibodyIdentificationInterpretationRule.IdentifiedExcludedCode, result.Code);
    }

    [Fact]
    public void IdentifiedWhenAssistPossible_IsPass()
    {
        var result = AntibodyIdentificationInterpretationRule.EvaluateIdentifiedVersusAssistExclusion(
            [new AntibodyIdentificationRecordedFinding("anti-K", "K", AntibodyIdClassification.Identified, AntibodyIdSource.Technologist)],
            [new AntibodyIdentificationAssistFinding("anti-K", "K", AntibodyIdClassification.Possible, "pattern", 0, 0, 1, 0)]);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void UnexcludedLeftover_WarnsAtComplete()
    {
        var result = AntibodyIdentificationInterpretationRule.EvaluateUnexcludedAtCompletion(
            ["anti-E", "anti-K"], ["anti-K"]);
        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal(AntibodyIdentificationInterpretationRule.UnexcludedCode, result.Code);
        Assert.Contains("anti-E", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnexcludedOnlyIdentifiedSpecificity_IsPass()
    {
        var result = AntibodyIdentificationInterpretationRule.EvaluateUnexcludedAtCompletion(
            ["anti-K"], ["anti-K"]);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void IncompletePanelWithIdentified_HardStopsComplete()
    {
        var result = AntibodyIdentificationInterpretationRule.EvaluateIncompletePanelAtCompletion(
            incomplete: true, identifiedToPost: 1);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(AntibodyIdentificationAssistEvaluator.IncompleteReactionsCode, result.Code);
    }

    [Fact]
    public void IncompletePanelWithNoneIdentified_WarnsAtComplete()
    {
        var result = AntibodyIdentificationInterpretationRule.EvaluateIncompletePanelAtCompletion(
            incomplete: true, identifiedToPost: 0);
        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal(AntibodyIdentificationAssistEvaluator.IncompleteReactionsCode, result.Code);
    }

    [Fact]
    public void CompleteWithNoIdentified_IsWarning()
    {
        var result = AntibodyIdentificationInterpretationRule.EvaluateIdentifiedWillPost(0);
        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal(AntibodyIdentificationInterpretationRule.CompleteNoneCode, result.Code);
    }

    [Fact]
    public void CompleteWithIdentified_IsPass()
    {
        var result = AntibodyIdentificationInterpretationRule.EvaluateIdentifiedWillPost(2);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void AutocontrolPositiveWithoutDat_WarnsAtComplete()
    {
        var result = AntibodyIdentificationInterpretationRule.EvaluateDatIndicatedAtCompletion(
            autocontrolPositive: true, AntibodyIdDatResult.NotPerformed);
        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal(AntibodyIdentificationAssistEvaluator.DatIndicatedCode, result.Code);
    }

    [Fact]
    public void CompleteWarnings_WithoutAcknowledgment_HardStop()
    {
        var result = AntibodyIdentificationInterpretationRule.EvaluateCompleteAcknowledgment(
        [
            RuleResult.Warning(
                AntibodyIdentificationInterpretationRule.CompleteNoneCode,
                "No technologist-Identified antibodies will post.")
        ],
        acknowledgment: null);

        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(AntibodyIdentificationInterpretationRule.CompleteAckCode, result.Code);
    }

    [Fact]
    public void CompleteWarnings_WithAcknowledgment_Pass()
    {
        var result = AntibodyIdentificationInterpretationRule.EvaluateCompleteAcknowledgment(
        [
            RuleResult.Warning(
                AntibodyIdentificationInterpretationRule.UnexcludedCode,
                "Assistance cannot exclude anti-E.")
        ],
        acknowledgment: "Reviewed leftover CannotExclude. Not identifying anti-E.");

        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void ReviewWarnings_WithoutAcknowledgment_HardStop()
    {
        var result = AntibodyIdentificationInterpretationRule.EvaluateReviewAcknowledgment(
        [
            RuleResult.Warning(
                AntibodyIdentificationInterpretationRule.UnexcludedCode,
                "Assistance cannot exclude anti-E.")
        ],
        acknowledgment: null);

        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(AntibodyIdentificationInterpretationRule.ReviewAckCode, result.Code);
    }

    [Fact]
    public void CompleteWithoutClinicalWarnings_DoesNotRequireAcknowledgment()
    {
        var result = AntibodyIdentificationInterpretationRule.EvaluateCompleteAcknowledgment(
        [
            RuleResult.Warning(
                AntibodyIdentificationWorkupScopeRule.UnscopedCode,
                "Unscoped workup.")
        ],
        acknowledgment: null);

        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void ExpiredLot_HardStopsNewWorkup()
    {
        var result = AntibodyPanelLotValidityRule.Evaluate(true, new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 30));
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(AntibodyPanelLotValidityRule.ExpiredCode, result.Code);
    }

    [Fact]
    public void LotUsableThroughExpirationDate()
    {
        var result = AntibodyPanelLotValidityRule.Evaluate(true, new DateOnly(2026, 5, 30), new DateOnly(2026, 5, 30));
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void InactiveLot_HardStops()
    {
        var result = AntibodyPanelLotValidityRule.Evaluate(false, new DateOnly(2026, 12, 31), new DateOnly(2026, 5, 30));
        Assert.Equal(AntibodyPanelLotValidityRule.InactiveCode, result.Code);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
    }
}
