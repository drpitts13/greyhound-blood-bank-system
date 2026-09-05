using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Keeps free-text ABID result verification from silently posting
/// <c>AntibodyHistory</c> when an antibody-identification workup is the
/// identification of record. Assistance and workup review still require
/// technologist judgment; this rule does not identify antibodies.
/// </summary>
public static class AntibodyIdentificationHistoryPostRule
{
    public const string OpenWorkupCode = "ABID-WORKUP-OPEN";
    public const string AllocateOpenCode = "ABID-ALLOC-OPEN";
    public const string IssueOpenCode = "ABID-ISSUE-OPEN";
    public const string CrossmatchOpenCode = "ABID-XM-OPEN";
    public const string SpecialRequirementOpenCode = "ABID-SR-OPEN";
    public const string AntigenOpenCode = "ABID-AG-OPEN";
    public const string BloodTypeOpenCode = "ABID-ABO-OPEN";
    public const string AuthoritativeCode = "ABID-WORKUP-AUTHORITATIVE";
    public const string DisagreeCode = "ABID-WORKUP-DISAGREE";

    public static bool IsOpen(AntibodyWorkupStatus status) =>
        status is AntibodyWorkupStatus.InProgress
            or AntibodyWorkupStatus.PendingInterpretation
            or AntibodyWorkupStatus.PendingSupervisorReview;

    public static bool AppliesToOpenWorkup(
        long? workupSpecimenId,
        long? workupSourceResultId,
        long resultSpecimenId,
        long resultId)
    {
        if (workupSourceResultId == resultId)
        {
            return true;
        }

        if (workupSpecimenId == resultSpecimenId)
        {
            return true;
        }

        return workupSpecimenId is null;
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
            "An open antibody-identification workup is the identification of record. Complete or void it before verifying a free-text ABID result that would post antibody history.");
    }

    /// <summary>
    /// Patient-chart add has no specimen. Any open workup on that patient is the
    /// identification of record for this path (OCD-023).
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
            RuleResult.Pass(
                AuthoritativeCode,
                "A completed antibody-identification workup is the identification of record. Free-text ABID verification will not post antibody history.")
        };

        if (!SameSpecificities(freeTextSpecificities, workupIdentifiedSpecificities))
        {
            results.Add(RuleResult.Warning(
                DisagreeCode,
                "The verified ABID text does not match the reviewed workup identification. History was not changed from the free-text result."));
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
