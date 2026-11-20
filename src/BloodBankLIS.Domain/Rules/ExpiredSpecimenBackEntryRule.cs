using BloodBankLIS.Domain.Entities;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// OCD-091 (decided 2026-11-14): a result read on paper while its specimen was still acceptable, entered after the
/// specimen has expired. The time tested must fall within the specimen's validity, and a reason is given. A patient test
/// result (option C) must also have been tested during a recorded outage; a crossmatch (option B) need not. The specimen
/// stays expired for anything new: the entry only documents what was done.
/// </summary>
public static class ExpiredSpecimenBackEntryRule
{
    public const string Code = "SPEC-EXPIRED-BACKENTRY";
    public const int MaxReasonLength = 500;

    public enum Kind
    {
        PatientTest,
        Crossmatch
    }

    /// <summary>The facility setting's default for each kind (OCD-091 as decided): 2 for patient tests, 1 for crossmatches.</summary>
    public static int DefaultRequirement(Kind kind) => kind == Kind.Crossmatch ? 1 : 2;

    /// <summary>The checks that hold at entry and again at verification (no reason needed then).</summary>
    /// <param name="requirement">Facility setting: 0 refused; 1 tested before expiry; 2 also in a recorded outage. The decided default when null.</param>
    public static RuleResult EvaluateTime(Kind kind, DateTime testedUtc, DateTime? specimenExpiresUtc, IEnumerable<DowntimeEpisode> outages,
        int? requirement = null)
    {
        var need = requirement ?? DefaultRequirement(kind);
        if (need <= 0)
        {
            return RuleResult.HardStop(Code,
                $"The specimen has expired. This facility does not enter a {(kind == Kind.Crossmatch ? "crossmatch" : "patient test")} read on paper on a specimen that has expired since (OCD-091); test a new specimen.");
        }

        if (specimenExpiresUtc is DateTime expires && testedUtc >= expires)
        {
            return RuleResult.HardStop(Code,
                $"The specimen had expired ({ClinicalTimeText.Local(expires)}) before the time tested; the result cannot be entered on it.");
        }

        if (need >= 2 && DowntimeEpisodeRule.Covering(outages, testedUtc) is null)
        {
            return RuleResult.HardStop(Code,
                $"The specimen has expired. A {(kind == Kind.Crossmatch ? "crossmatch" : "patient test")} read on paper can be entered on it only when the time tested falls in a recorded outage (OCD-091). Record the outage on the Downtime page first, or check the time tested.");
        }

        return RuleResult.Pass(Code);
    }

    public static RuleResult Evaluate(Kind kind, DateTime testedUtc, DateTime? specimenExpiresUtc, IEnumerable<DowntimeEpisode> outages, string? reason,
        int? requirement = null)
    {
        var time = EvaluateTime(kind, testedUtc, specimenExpiresUtc, outages, requirement);
        if (time.Severity == RuleSeverity.HardStop)
        {
            return time;
        }

        var text = reason?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return RuleResult.HardStop(Code, "The specimen has expired since it was tested. Give the reason for entering the result now (OCD-091).");
        }

        return text.Length > MaxReasonLength
            ? RuleResult.HardStop(Code, $"Keep the reason to {MaxReasonLength} characters.")
            : RuleResult.Pass(Code);
    }
}
