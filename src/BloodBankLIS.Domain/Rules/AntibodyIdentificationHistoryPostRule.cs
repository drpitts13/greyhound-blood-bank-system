using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Keeps free-text ABID result verification and patient-chart antibody add
/// or deactivate from changing <c>AntibodyHistory</c> when an antibody-identification
/// workup is the identification of record. Assistance and workup review still
/// require technologist judgment; this rule does not identify antibodies.
/// </summary>
public static class AntibodyIdentificationHistoryPostRule
{
    public const string OpenWorkupCode = "ABID-WORKUP-OPEN";
    public const string AllocateOpenCode = "ABID-ALLOC-OPEN";
    public const string IssueOpenCode = "ABID-ISSUE-OPEN";
    public const string CrossmatchOpenCode = "ABID-XM-OPEN";
    public const string AntigenOpenCode = "ABID-AG-OPEN";
    public const string BloodTypeOpenCode = "ABID-ABO-OPEN";
    public const string AntigenInvalidateCode = "ABID-AG-INVAL";
    public const string BloodTypeInvalidateCode = "ABID-ABO-INVAL";
    public const string TypeCorrectionOpenCode = "ABID-TYPE-CORR";
    public const string PendingTypeCorrectionCode = "ABID-TYPE-PENDING";
    public const string AntigenCompletedCode = "ABID-AG-DONE";
    public const string BloodTypeCompletedCode = "ABID-ABO-DONE";
    public const string SpecialRequirementOpenCode = "ABID-SR-OPEN";
    public const string SpecimenOpenCode = "ABID-SPEC-OPEN";
    public const string SpecimenEditCode = "ABID-SPEC-EDIT";
    public const string DeactivatePostedCode = "ABID-DEACT-POSTED";
    public const string AuthoritativeCode = "ABID-WORKUP-AUTHORITATIVE";
    public const string DisagreeCode = "ABID-WORKUP-DISAGREE";

    public static bool IsOpen(AntibodyWorkupStatus status) =>
        status is AntibodyWorkupStatus.InProgress
            or AntibodyWorkupStatus.PendingInterpretation
            or AntibodyWorkupStatus.PendingSupervisorReview;

    public static bool IsCompleted(AntibodyWorkupStatus status) =>
        status == AntibodyWorkupStatus.Completed;

    public static bool AppliesToOpenWorkup(
        long? workupSpecimenId,
        long? workupSourceResultId,
        long resultSpecimenId,
        long resultId,
        bool workupSpecimenUnusable = false)
    {
        if (workupSourceResultId == resultId)
        {
            return true;
        }

        if (workupSpecimenId == resultSpecimenId)
        {
            return true;
        }

        return workupSpecimenId is null || workupSpecimenUnusable;
    }

    public static bool AppliesToCompletedWorkup(
        long? workupSpecimenId,
        long? workupSourceResultId,
        long resultSpecimenId,
        long resultId) =>
        workupSourceResultId == resultId || workupSpecimenId == resultSpecimenId;

    public static RuleResult EvaluateOpenWorkup(bool hasOpenWorkupInScope, bool freeTextWouldPostHistory)
    {
        if (!hasOpenWorkupInScope || !freeTextWouldPostHistory)
        {
            return RuleResult.Pass(OpenWorkupCode);
        }

        return RuleResult.HardStop(
            OpenWorkupCode,
            "An open antibody-identification workup is the identification of record. Complete or void it before verifying a free-text ABID or blood-attribute antibody result that would post or deactivate antibody history.");
    }

    /// <summary>
    /// Patient-chart add has no specimen. Any open workup on that patient is
    /// the identification of record for this path (OCD-023).
    /// </summary>
    public static RuleResult EvaluateManualHistoryAdd(bool hasOpenWorkupOnPatient)
    {
        if (!hasOpenWorkupOnPatient)
        {
            return RuleResult.Pass(OpenWorkupCode);
        }

        return RuleResult.HardStop(
            OpenWorkupCode,
            "An open antibody-identification workup is the identification of record. Complete or void it before adding antibody history from the patient chart.");
    }

    /// <summary>
    /// Patient-chart deactivate has no specimen. Any open workup on that patient
    /// is the identification of record for this path (OCD-023). Deactivate after
    /// complete or void remains the authorized immuno path (OCD-017).
    /// </summary>
    public static RuleResult EvaluateManualHistoryDeactivate(bool hasOpenWorkupOnPatient)
    {
        if (!hasOpenWorkupOnPatient)
        {
            return RuleResult.Pass(OpenWorkupCode);
        }

        return RuleResult.HardStop(
            OpenWorkupCode,
            "An open antibody-identification workup is the identification of record. Complete or void it before deactivating antibody history from the patient chart.");
    }

    /// <summary>
    /// Deactivate after complete remains authorized (OCD-017). Warns when the row
    /// was posted by a completed workup so antigen-negative is not dropped silently.
    /// </summary>
    public static RuleResult EvaluateManualHistoryDeactivatePostedWorkup(bool postedByCompletedWorkup)
    {
        if (!postedByCompletedWorkup)
        {
            return RuleResult.Pass(DeactivatePostedCode);
        }

        return RuleResult.Warning(
            DeactivatePostedCode,
            "This antibody was posted by a completed antibody-identification workup (identification of record). Deactivating drops antigen-negative selection. Deactivate remains authorized. This warning does not identify antibodies.");
    }

    public static bool MatchesPostedWorkupFinding(
        long? antibodyCatalogId,
        string antibodySpecificity,
        IEnumerable<(long? CatalogId, string Specificity)> postedFindings)
    {
        foreach (var posted in postedFindings)
        {
            if (antibodyCatalogId is long catalogId && posted.CatalogId == catalogId)
            {
                return true;
            }

            if (string.Equals(posted.Specificity, antibodySpecificity, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Allocation may proceed (serologic XM / emergency still available). Warns that
    /// antigen-negative needs may change when the identification of record completes.
    /// </summary>
    public static RuleResult EvaluateAllocateOpenWorkup(bool hasOpenWorkupOnPatient)
    {
        if (!hasOpenWorkupOnPatient)
        {
            return RuleResult.Pass(AllocateOpenCode);
        }

        return RuleResult.Warning(
            AllocateOpenCode,
            "An open antibody-identification workup is the identification of record. Antigen-negative needs may change when the workup completes. This warning does not identify antibodies.");
    }

    /// <summary>
    /// Serologic XM may proceed (electronic XM is already HardStopped). Surfaces
    /// that identification of record is unfinished; does not identify antibodies.
    /// </summary>
    public static RuleResult EvaluateCrossmatchOpenWorkup(bool hasOpenWorkupOnPatient)
    {
        if (!hasOpenWorkupOnPatient)
        {
            return RuleResult.Pass(CrossmatchOpenCode);
        }

        return RuleResult.Warning(
            CrossmatchOpenCode,
            "An open antibody-identification workup is the identification of record. Antigen-negative needs may change when the workup completes. This warning does not identify antibodies or block serologic crossmatch.");
    }

    /// <summary>
    /// Phenotype may be recorded during identification. Changing it withdraws
    /// interpretation so complete cannot post against a prior type.
    /// </summary>
    public static RuleResult EvaluateAntigenOpenWorkup(bool hasOpenWorkupOnPatient)
    {
        if (!hasOpenWorkupOnPatient)
        {
            return RuleResult.Pass(AntigenOpenCode);
        }

        return RuleResult.Warning(
            AntigenOpenCode,
            "An open antibody-identification workup is the identification of record. Changing antigen type withdraws interpretation and review so they can be repeated against the current type. This warning does not identify antibodies.");
    }

    /// <summary>
    /// Manual ABO/Rh may be recorded during identification. Changing it withdraws
    /// interpretation so complete cannot post against a prior type.
    /// </summary>
    public static RuleResult EvaluateBloodTypeOpenWorkup(bool hasOpenWorkupOnPatient)
    {
        if (!hasOpenWorkupOnPatient)
        {
            return RuleResult.Pass(BloodTypeOpenCode);
        }

        return RuleResult.Warning(
            BloodTypeOpenCode,
            "An open antibody-identification workup is the identification of record. Changing ABO/Rh withdraws interpretation and review so they can be repeated against the current type. This warning does not identify antibodies.");
    }

    /// <summary>
    /// Type may still change after identification posts. Same-specimen or
    /// unscoped completed workups are in scope; this does not reopen or retract.
    /// </summary>
    public static RuleResult EvaluateAntigenCompletedWorkup(
        bool hasCompletedWorkupInScope,
        bool postedIdentifiedConflictsWithType = false)
    {
        if (!hasCompletedWorkupInScope && !postedIdentifiedConflictsWithType)
        {
            return RuleResult.Pass(AntigenCompletedCode);
        }

        return RuleResult.Warning(
            AntigenCompletedCode,
            "A completed antibody-identification workup is the identification of record. Changing antigen type does not reopen it or retract posted history. This warning does not identify antibodies.");
    }

    public static RuleResult EvaluateBloodTypeCompletedWorkup(bool hasCompletedWorkupInScope)
    {
        if (!hasCompletedWorkupInScope)
        {
            return RuleResult.Pass(BloodTypeCompletedCode);
        }

        return RuleResult.Warning(
            BloodTypeCompletedCode,
            "A completed antibody-identification workup is the identification of record. Changing ABO/Rh does not reopen it or retract posted history. This warning does not identify antibodies.");
    }

    /// <summary>
    /// Invalidate may proceed. Stored phenotype is not auto-reverted (OCD-022).
    /// Withdraws interpretation so complete cannot use a retracted type.
    /// </summary>
    public static RuleResult EvaluateAntigenInvalidateOpenWorkup(bool hasOpenWorkupOnPatient)
    {
        if (!hasOpenWorkupOnPatient)
        {
            return RuleResult.Pass(AntigenInvalidateCode);
        }

        return RuleResult.Warning(
            AntigenInvalidateCode,
            "A verified antigen result was invalidated while an antibody-identification workup was open. Stored phenotype is not auto-reverted. Interpretation and review are withdrawn so they can be repeated. This warning does not identify antibodies or block invalidate.");
    }

    /// <summary>
    /// Invalidate may proceed. Posted ABO history is not auto-reverted (OCD-017).
    /// Withdraws interpretation so complete cannot use a retracted type.
    /// </summary>
    public static RuleResult EvaluateBloodTypeInvalidateOpenWorkup(bool hasOpenWorkupOnPatient)
    {
        if (!hasOpenWorkupOnPatient)
        {
            return RuleResult.Pass(BloodTypeInvalidateCode);
        }

        return RuleResult.Warning(
            BloodTypeInvalidateCode,
            "A verified ABO/Rh result was invalidated while an antibody-identification workup was open. Posted blood-type history is not auto-reverted. Interpretation and review are withdrawn so they can be repeated. This warning does not identify antibodies or block invalidate.");
    }

    /// <summary>
    /// A type correction may be entered during identification. The stored type
    /// has not changed yet; complete and Accept HardStop until it is verified
    /// or the correction is invalidated.
    /// </summary>
    public static RuleResult EvaluateTypeCorrectionOpenWorkup(bool hasOpenWorkupOnPatient)
    {
        if (!hasOpenWorkupOnPatient)
        {
            return RuleResult.Pass(TypeCorrectionOpenCode);
        }

        return RuleResult.Warning(
            TypeCorrectionOpenCode,
            "An open antibody-identification workup is the identification of record. This ABO/Rh or antigen correction is not yet verified. Complete and supervisor Accept HardStop until it is verified or invalidated. This warning does not identify antibodies or change stored type.");
    }

    /// <summary>
    /// A scoped workup HardStops only for a pending type correction on the
    /// linked specimen. An unscoped workup HardStops for any current patient
    /// type correction. This does not identify antibodies.
    /// </summary>
    public static bool PendingTypeCorrectionApplies(long? workupSpecimenId, long? resultSpecimenId) =>
        workupSpecimenId is null || workupSpecimenId == resultSpecimenId;

    public static RuleResult EvaluatePendingTypeCorrection(bool hasPendingTypeCorrection)
    {
        if (!hasPendingTypeCorrection)
        {
            return RuleResult.Pass(PendingTypeCorrectionCode);
        }

        return RuleResult.HardStop(
            PendingTypeCorrectionCode,
            "An unverified ABO/Rh or antigen correction exists on the identification-of-record specimen. Verify or invalidate it before completing or accepting so interpretation is not recorded against a pending type. This does not identify antibodies.");
    }

    /// <summary>
    /// Reject may proceed (do not require override). Surfaces that identification
    /// of record is still open on an unusable specimen until void or re-link.
    /// </summary>
    public static RuleResult EvaluateSpecimenRejectedOpenWorkup(bool hasOpenWorkupOnSpecimen)
    {
        if (!hasOpenWorkupOnSpecimen)
        {
            return RuleResult.Pass(SpecimenOpenCode);
        }

        return RuleResult.Warning(
            SpecimenOpenCode,
            "An open antibody-identification workup is linked to this specimen. Rejecting makes the specimen unusable; the workup remains the identification of record for the patient until it is voided or linked to a usable specimen. Interpretation and review are withdrawn. This warning does not identify antibodies or block reject.");
    }

    /// <summary>
    /// Collection or expiration may be corrected during identification. Changing
    /// them withdraws interpretation so complete cannot post against a prior window.
    /// </summary>
    public static RuleResult EvaluateSpecimenEditedOpenWorkup(bool hasOpenWorkupOnSpecimen)
    {
        if (!hasOpenWorkupOnSpecimen)
        {
            return RuleResult.Pass(SpecimenEditCode);
        }

        return RuleResult.Warning(
            SpecimenEditCode,
            "An open antibody-identification workup is linked to this specimen. Changing collection or expiration withdraws interpretation and review so they can be repeated against the current specimen validity. This warning does not identify antibodies or block the edit.");
    }

    /// <summary>
    /// Special requirements may be documented during identification. Warns that
    /// antigen-negative needs may change when the workup completes.
    /// </summary>
    public static RuleResult EvaluateSpecialRequirementOpenWorkup(bool hasOpenWorkupOnPatient)
    {
        if (!hasOpenWorkupOnPatient)
        {
            return RuleResult.Pass(SpecialRequirementOpenCode);
        }

        return RuleResult.Warning(
            SpecialRequirementOpenCode,
            "An open antibody-identification workup is the identification of record. Antigen-negative needs may change when the workup completes. This warning does not identify antibodies or block documenting special requirements.");
    }

    /// <summary>
    /// Issue may proceed (do not require override). Surfaces that identification
    /// of record is unfinished. Emergency release is unchanged.
    /// </summary>
    public static RuleResult EvaluateIssueOpenWorkup(bool hasOpenWorkupOnPatient)
    {
        if (!hasOpenWorkupOnPatient)
        {
            return RuleResult.Pass(IssueOpenCode);
        }

        return RuleResult.Warning(
            IssueOpenCode,
            "An open antibody-identification workup is the identification of record. Antigen-negative needs may change when the workup completes. This warning does not identify antibodies or block issue.");
    }

    public static IReadOnlyList<RuleResult> EvaluateCompletedWorkup(
        bool hasCompletedWorkupInScope,
        IReadOnlyList<string> freeTextSpecificities,
        IReadOnlyList<string> workupIdentifiedSpecificities)
    {
        if (!hasCompletedWorkupInScope)
        {
            return [RuleResult.Pass(AuthoritativeCode)];
        }

        var results = new List<RuleResult>
        {
            RuleResult.Warning(
                AuthoritativeCode,
                "A completed antibody-identification workup is the identification of record. Verifying a free-text ABID or blood-attribute antibody result will not change antibody history.")
        };

        if (!SameSpecificities(freeTextSpecificities, workupIdentifiedSpecificities))
        {
            results.Add(RuleResult.Warning(
                DisagreeCode,
                "The verified antibody result does not match the reviewed workup identification. History was not changed from this result."));
        }

        return results;
    }

    public static bool ShouldSkipFreeTextPost(bool hasCompletedWorkupInScope) =>
        hasCompletedWorkupInScope;

    public static bool SameSpecificities(
        IReadOnlyList<string> left,
        IReadOnlyList<string> right)
    {
        var a = Normalize(left);
        var b = Normalize(right);
        return a.SetEquals(b);
    }

    private static HashSet<string> Normalize(IReadOnlyList<string> items) =>
        items
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToHashSet(StringComparer.Ordinal);
}
