using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Gates that keep antibody identification under technologist judgment.
/// The assist engine may propose exclusions and possible specificities; it
/// must never be treated as an identified antibody.
/// </summary>
public static class AntibodyIdentificationInterpretationRule
{
    public const string AssistAdvisoryCode = "ABID-ASSIST-ADVISORY";
    public const string AssistIdentifiedCode = "ABID-ASSIST-IDENTIFIED";
    public const string InterpretationRequiredCode = "ABID-INTERP-REQUIRED";
    public const string ReviewRequiredCode = "ABID-REVIEW-REQUIRED";
    public const string ReviewSelfCode = "ABID-REVIEW-SELF";
    public const string ReviewRejectedCode = "ABID-REVIEW-REJECTED";
    public const string VoidedCode = "ABID-VOID";
    public const string VoidReasonCode = "ABID-VOID-REASON";
    public const string VoidCompletedCode = "ABID-VOID-COMPLETED";
    public const string IdentifiedPhenotypeConflictCode = "ABID-INTERP-PHENO";
    public const string IdentifiedGenotypeConflictCode = "ABID-INTERP-GENO";
    public const string CompleteNoneCode = "ABID-COMPLETE-NONE";
    public const string InterpretationStaleCode = "ABID-INTERP-STALE";
    public const string ReviewStaleCode = "ABID-REVIEW-STALE";
    public const string UnexcludedCode = "ABID-UNEXCLUDED";
    public const string IdentifiedExcludedCode = "ABID-INTERP-EXCLUDED";
    public const string HistoryRemainsCode = "ABID-HIST-REMAINS";
    public const string CompleteAckCode = "ABID-COMPLETE-ACK";
    public const string ReviewAckCode = "ABID-REVIEW-ACK";

    public static RuleResult AssistIsAdvisory() =>
        RuleResult.Pass(
            AssistAdvisoryCode,
            "Panel evaluation is assistance only. A technologist must interpret the antigram. The system does not identify antibodies.");

    public static RuleResult EvaluateAssistMustNotIdentify(
        IEnumerable<AntibodyIdentificationRecordedFinding> findings)
    {
        var assistIdentified = findings
            .Where(f => f.Source == AntibodyIdSource.Assist && f.Classification == AntibodyIdClassification.Identified)
            .Select(f => f.Specificity)
            .ToList();

        return assistIdentified.Count == 0
            ? RuleResult.Pass(AssistIdentifiedCode)
            : RuleResult.HardStop(
                AssistIdentifiedCode,
                "Assisted findings cannot be recorded as Identified: "
                + string.Join(", ", assistIdentified)
                + ". A technologist must classify identified antibodies.");
    }

    public static RuleResult EvaluateInterpretationRecorded(bool hasTechnologistInterpretation) =>
        hasTechnologistInterpretation
            ? RuleResult.Pass(InterpretationRequiredCode)
            : RuleResult.HardStop(
                InterpretationRequiredCode,
                "Technologist interpretation is required before this workup can be completed.");

    public static RuleResult EvaluateSupervisorReview(
        AntibodyIdentificationPolicy policy,
        bool supervisorReviewed,
        bool supervisorAccepted,
        string? technologistUser,
        string? supervisorUser)
    {
        if (!policy.RequireSupervisorReview)
        {
            return RuleResult.Pass(ReviewRequiredCode);
        }

        if (!supervisorReviewed)
        {
            return RuleResult.HardStop(
                ReviewRequiredCode,
                "Supervisor review is required before this antibody-identification workup can be completed.");
        }

        if (!supervisorAccepted)
        {
            return RuleResult.HardStop(
                ReviewRejectedCode,
                "Supervisor rejected the interpretation. The workup cannot be completed.");
        }

        if (policy.BlockSelfReview
            && !string.IsNullOrWhiteSpace(technologistUser)
            && string.Equals(technologistUser, supervisorUser, StringComparison.OrdinalIgnoreCase))
        {
            return RuleResult.HardStop(
                ReviewSelfCode,
                "The technologist who interpreted this workup cannot also perform supervisor review.");
        }

        return RuleResult.Pass(ReviewRequiredCode);
    }

    public static RuleResult EvaluateNotVoided(AntibodyWorkupStatus status) =>
        status == AntibodyWorkupStatus.Voided
            ? RuleResult.HardStop(VoidedCode, "A voided antibody-identification workup cannot be completed.")
            : RuleResult.Pass(VoidedCode);

    public static RuleResult EvaluateVoidReason(string? reason) =>
        string.IsNullOrWhiteSpace(reason)
            ? RuleResult.HardStop(
                VoidReasonCode,
                "A reason is required to void an antibody-identification workup.")
            : RuleResult.Pass(VoidReasonCode);

    public static RuleResult EvaluateCanVoid(AntibodyWorkupStatus status) =>
        status switch
        {
            AntibodyWorkupStatus.Completed => RuleResult.HardStop(
                VoidCompletedCode,
                "A completed antibody-identification workup cannot be voided. History already posted must be changed through an authorized immuno path."),
            AntibodyWorkupStatus.Voided => RuleResult.HardStop(
                VoidedCode,
                "This antibody-identification workup is already voided."),
            _ => RuleResult.Pass(VoidedCode)
        };

    public static RuleEvaluation EvaluateVoid(AntibodyWorkupStatus status, string? reason) =>
        new([EvaluateCanVoid(status), EvaluateVoidReason(reason)]);

    public static RuleResult EvaluateReadyForSupervisorReview(bool interpretationCurrent) =>
        interpretationCurrent
            ? RuleResult.Pass(InterpretationStaleCode)
            : RuleResult.HardStop(
                InterpretationStaleCode,
                "Panel reactions, selected cells, or DAT changed after the last technologist interpretation. Re-record interpretation before supervisor review.");

    public static RuleResult EvaluateInterpretationCurrent(
        DateTime? interpretedUtc,
        DateTime? lastPanelChangeUtc) =>
        interpretedUtc is DateTime interpreted
        && lastPanelChangeUtc is DateTime changed
        && changed > interpreted
            ? RuleResult.HardStop(
                InterpretationStaleCode,
                "Panel reactions, selected cells, or DAT changed after the last technologist interpretation. Re-record interpretation before completing.")
            : RuleResult.Pass(InterpretationStaleCode);

    public static RuleResult EvaluateReviewCurrent(
        DateTime? reviewedUtc,
        DateTime? lastPanelChangeUtc) =>
        reviewedUtc is DateTime reviewed
        && lastPanelChangeUtc is DateTime changed
        && changed > reviewed
            ? RuleResult.HardStop(
                ReviewStaleCode,
                "Panel reactions, selected cells, or DAT changed after supervisor review. A new supervisor review is required before completing.")
            : RuleResult.Pass(ReviewStaleCode);

    public static RuleResult EvaluateHistoryRemainsAtCompletion(
        IEnumerable<HistoricalAntibodySnapshot> history)
    {
        var active = (history ?? [])
            .Where(h => h.IsActive && !string.IsNullOrWhiteSpace(h.Specificity))
            .Select(h => h.Specificity.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return active.Count == 0
            ? RuleResult.Pass(HistoryRemainsCode)
            : RuleResult.Warning(
                HistoryRemainsCode,
                "Existing antibody history remains on the record and still drives antigen-negative selection: "
                + string.Join(", ", active)
                + ". Completing does not remove or deactivate historical antibodies.");
    }

    public static RuleResult EvaluateIdentifiedVersusAssistExclusion(
        IEnumerable<AntibodyIdentificationRecordedFinding> identified,
        IEnumerable<AntibodyIdentificationAssistFinding> assistFindings)
    {
        var excluded = (assistFindings ?? [])
            .Where(f => f.Classification == AntibodyIdClassification.Excluded)
            .ToList();
        var hits = (identified ?? [])
            .Where(i => i.Classification == AntibodyIdClassification.Identified)
            .Where(i => excluded.Any(e =>
                (!string.IsNullOrWhiteSpace(i.AttributeCode)
                 && string.Equals(i.AttributeCode, e.AttributeCode, StringComparison.Ordinal))
                || string.Equals(i.Specificity, e.Specificity, StringComparison.Ordinal)))
            .Select(i => i.Specificity)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return hits.Count == 0
            ? RuleResult.Pass(IdentifiedExcludedCode)
            : RuleResult.Warning(
                IdentifiedExcludedCode,
                "Assistance would exclude "
                + string.Join(", ", hits)
                + " on the reactions entered. Confirm the identification. Assistance is not an identification and does not block the technologist.");
    }

    public static RuleResult EvaluateUnexcludedAtCompletion(
        IEnumerable<string> cannotExcludeSpecificities,
        IEnumerable<string> identifiedSpecificities)
    {
        var identified = (identifiedSpecificities ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToHashSet(StringComparer.Ordinal);
        var leftover = (cannotExcludeSpecificities ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s) && !identified.Contains(s.Trim()))
            .Select(s => s.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return leftover.Count == 0
            ? RuleResult.Pass(UnexcludedCode)
            : RuleResult.Warning(
                UnexcludedCode,
                "Assistance cannot exclude "
                + string.Join(", ", leftover)
                + ". Completing still posts only technologist-Identified antibodies. Unexcluded specificities are not identified.");
    }

    public static RuleResult EvaluateIncompletePanelAtCompletion(bool incomplete, int identifiedToPost)
    {
        if (!incomplete)
        {
            return RuleResult.Pass(AntibodyIdentificationAssistEvaluator.IncompleteReactionsCode);
        }

        return identifiedToPost > 0
            ? RuleResult.HardStop(
                AntibodyIdentificationAssistEvaluator.IncompleteReactionsCode,
                "Panel or selected cells are missing interpretive-phase reactions. Enter those reactions before posting Identified antibodies. Completing does not identify an antibody from a blank cell.")
            : RuleResult.Warning(
                AntibodyIdentificationAssistEvaluator.IncompleteReactionsCode,
                "Panel or selected cells are missing interpretive-phase reactions. Completing posts nothing. A blank cell is not an exclusion.");
    }

    public static RuleResult EvaluateCompleteAcknowledgment(
        IEnumerable<RuleResult> completionResults,
        string? acknowledgment)
    {
        var needingAck = (completionResults ?? [])
            .Where(r => r.Severity == RuleSeverity.Warning && RequiresCompleteAcknowledgment(r.Code))
            .Select(r => r.Code)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (needingAck.Count == 0)
        {
            return RuleResult.Pass(CompleteAckCode);
        }

        return string.IsNullOrWhiteSpace(acknowledgment)
            ? RuleResult.HardStop(
                CompleteAckCode,
                "Completion warnings require an acknowledgment: "
                + string.Join(", ", needingAck)
                + ". Record that leftover CannotExclude, history, DAT, none-identified, or conflicting findings were reviewed. Acknowledgment does not identify antibodies.")
            : RuleResult.Pass(CompleteAckCode);
    }

    public static RuleResult EvaluateReviewAcknowledgment(
        IEnumerable<RuleResult> reviewResults,
        string? acknowledgment)
    {
        var needingAck = (reviewResults ?? [])
            .Where(r => r.Severity == RuleSeverity.Warning && RequiresCompleteAcknowledgment(r.Code))
            .Select(r => r.Code)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (needingAck.Count == 0)
        {
            return RuleResult.Pass(ReviewAckCode);
        }

        return string.IsNullOrWhiteSpace(acknowledgment)
            ? RuleResult.HardStop(
                ReviewAckCode,
                "Supervisor acceptance of completion warnings requires an acknowledgment: "
                + string.Join(", ", needingAck)
                + ". Record that leftover CannotExclude, history, DAT, none-identified, or conflicting findings were reviewed. Acknowledgment does not identify antibodies.")
            : RuleResult.Pass(ReviewAckCode);
    }

    public static bool RequiresCompleteAcknowledgment(string? code) =>
        code is CompleteNoneCode
            or IdentifiedPhenotypeConflictCode
            or IdentifiedGenotypeConflictCode
            or UnexcludedCode
            or IdentifiedExcludedCode
            or HistoryRemainsCode
            or AntibodyIdentificationAssistEvaluator.DatIndicatedCode
            or AntibodyIdentificationAssistEvaluator.HistoricalUndetectedCode
            or AntibodyIdentificationAssistEvaluator.IncompleteReactionsCode
            or AntibodyIdentificationAssistEvaluator.SelectedCellNeededCode
            or AntibodyIdentificationWorkupScopeRule.SpecimenExpiredCode
            or AntibodyIdentificationWorkupScopeRule.SpecimenUnacceptedCode;

    public static RuleResult EvaluateIdentifiedWillPost(int identifiedCount) =>
        identifiedCount == 0
            ? RuleResult.Warning(
                CompleteNoneCode,
                "No technologist-Identified antibodies will post to antibody history. Complete only if the interpretation is that none were identified.")
            : RuleResult.Pass(
                CompleteNoneCode,
                $"{identifiedCount} technologist-Identified specificit{(identifiedCount == 1 ? "y" : "ies")} will post to antibody history.");

    public static RuleResult EvaluateDatIndicatedAtCompletion(
        bool autocontrolPositive,
        AntibodyIdDatResult dat)
    {
        if (autocontrolPositive && dat == AntibodyIdDatResult.NotPerformed)
        {
            return RuleResult.Warning(
                AntibodyIdentificationAssistEvaluator.DatIndicatedCode,
                "Autocontrol is reactive and DAT has not been recorded. Perform DAT when applicable. Completing still does not identify an antibody.");
        }

        return RuleResult.Pass(AntibodyIdentificationAssistEvaluator.DatIndicatedCode);
    }

    public static RuleResult EvaluateIdentifiedVersusPatientType(
        AntibodyIdClassification classification,
        string? antigenCode,
        string antibodyName,
        IReadOnlyList<PatientAntigenSnapshot> patientAntigens)
    {
        if (classification != AntibodyIdClassification.Identified
            || string.IsNullOrWhiteSpace(antigenCode))
        {
            return RuleResult.Pass(IdentifiedPhenotypeConflictCode);
        }

        var match = patientAntigens.FirstOrDefault(p =>
            string.Equals(p.AttributeCode, antigenCode, StringComparison.Ordinal)
            && p.Result == AntigenResult.Positive);
        if (match is null)
        {
            return RuleResult.Pass(IdentifiedPhenotypeConflictCode);
        }

        var code = match.FromGenotype ? IdentifiedGenotypeConflictCode : IdentifiedPhenotypeConflictCode;
        var source = match.FromGenotype ? "predicted genotype" : "phenotype";
        return RuleResult.Warning(
            code,
            $"Identifying {antibodyName} when the patient {source} is {antigenCode}-positive is unexpected for an alloantibody. Confirm autoantibody, recent transfusion, or a typing error. This does not replace technologist judgment.");
    }

    public static RuleEvaluation EvaluateCompletion(
        AntibodyWorkupStatus status,
        bool hasTechnologistInterpretation,
        IEnumerable<AntibodyIdentificationRecordedFinding> findings,
        AntibodyIdentificationPolicy policy,
        bool supervisorReviewed,
        bool supervisorAccepted,
        string? technologistUser,
        string? supervisorUser,
        DateTime? interpretedUtc = null,
        DateTime? lastPanelChangeUtc = null,
        DateTime? reviewedUtc = null)
    {
        return new RuleEvaluation(
        [
            EvaluateNotVoided(status),
            EvaluateInterpretationRecorded(hasTechnologistInterpretation),
            EvaluateAssistMustNotIdentify(findings),
            EvaluateSupervisorReview(policy, supervisorReviewed, supervisorAccepted, technologistUser, supervisorUser),
            EvaluateInterpretationCurrent(interpretedUtc, lastPanelChangeUtc),
            EvaluateReviewCurrent(reviewedUtc, lastPanelChangeUtc)
        ]);
    }
}
