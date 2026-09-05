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
