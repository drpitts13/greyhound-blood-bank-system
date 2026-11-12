using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// RhIG evaluation support: this rule lists the facts on file and checks that a completed record says what was decided.
/// The suggested decision and calculated dose (OCD-069, decided 2026-11-10) are <see cref="RhigDoseRule"/>; the reviewer
/// confirms both by recording them.
/// </summary>
public static class RhigEvaluationRule
{
    /// <summary>
    /// When RhIG is to be given by, from the event and the facility's window (OCD-087): null when the window is not set,
    /// the evaluation is decided not a candidate, or RhIG was given. End-user testing, 2026-11-12: a delivery five days
    /// old waited undecided with nothing to say a time had passed.
    /// </summary>
    public static DateTime? GiveBy(DateTime eventUtc, int windowHours, RhigDecision decision, bool given) =>
        windowHours <= 0 || decision == RhigDecision.NotCandidate || given ? null : eventUtc.AddHours(windowHours);

    public const string CompleteCode = "RHIG-COMPLETE";

    /// <summary>Facts the reviewer weighs, as plain statements; no conclusion is drawn.</summary>
    public static IReadOnlyList<string> Facts(RhType? motherRh, bool antiDOnHistory, RhType infantRh, FetalBleedScreen screen, decimal? fetalBleedPercent,
        RhDVariant variant = RhDVariant.None)
    {
        var facts = new List<string>
        {
            motherRh switch
            {
                RhType.Negative => "Patient's current Rh type on file: negative.",
                RhType.Positive => "Patient's current Rh type on file: positive.",
                _ => "No current Rh type on file for the patient."
            }
        };
        if (variant != RhDVariant.None)
        {
            facts.Add(variant == RhDVariant.WeakD
                ? "Weak D is recorded for the patient: treated as Rh negative for RhIG."
                : "Partial D is recorded for the patient: treated as Rh negative for RhIG.");
        }

        if (antiDOnHistory)
        {
            facts.Add("Anti-D is on the patient's antibody history.");
        }

        facts.Add(infantRh switch
        {
            RhType.Positive => "Infant Rh: positive.",
            RhType.Negative => "Infant Rh: negative.",
            _ => "Infant Rh: not known."
        });
        facts.Add(screen switch
        {
            FetalBleedScreen.Positive => fetalBleedPercent is decimal pct
                ? $"Fetal bleed screen positive; quantitation {pct:0.###}% fetal cells."
                : "Fetal bleed screen positive; no quantitation recorded.",
            FetalBleedScreen.Negative => "Fetal bleed screen negative.",
            _ => "Fetal bleed screen not done."
        });
        return facts;
    }

    /// <summary>
    /// A record can be completed once it says what was decided: a candidate with the vials recommended (and, after a
    /// positive screen, the quantitation the dose came from); a non-candidate with the reason. Not a clinical rule.
    /// </summary>
    public static RuleResult EvaluateComplete(RhigDecision decision, int? vials, string? note, FetalBleedScreen screen, decimal? fetalBleedPercent)
    {
        return decision switch
        {
            RhigDecision.Undetermined => RuleResult.HardStop(CompleteCode, "Record whether the patient is a candidate for RhIG."),
            RhigDecision.Candidate when vials is null or < 1 => RuleResult.HardStop(CompleteCode, "Record the vials recommended."),
            RhigDecision.Candidate when screen == FetalBleedScreen.Positive && fetalBleedPercent is null =>
                RuleResult.HardStop(CompleteCode, "The fetal bleed screen is positive: record the quantitation the dose is based on."),
            RhigDecision.NotCandidate when string.IsNullOrWhiteSpace(note) => RuleResult.HardStop(CompleteCode, "Say why the patient is not a candidate."),
            _ => RuleResult.Pass(CompleteCode)
        };
    }

    public const string GivenCode = "RHIG-GIVEN";

    /// <summary>
    /// RhIG given is recorded on a completed evaluation that found a candidate, once: the lot, vials (1–50), and a time
    /// not in the future are required.
    /// </summary>
    public static RuleResult EvaluateGiven(bool completed, RhigDecision decision, bool alreadyGiven, string? lot, int vials, DateTime givenUtc, DateTime nowUtc)
    {
        if (!completed || decision != RhigDecision.Candidate)
        {
            return RuleResult.HardStop(GivenCode, "Record RhIG given on a completed evaluation that found the patient a candidate.");
        }

        if (alreadyGiven)
        {
            return RuleResult.HardStop(GivenCode, "RhIG given is already recorded for this evaluation.");
        }

        if (string.IsNullOrWhiteSpace(lot))
        {
            return RuleResult.HardStop(GivenCode, "Scan or type the RhIG lot number.");
        }

        if (vials is < 1 or > 50)
        {
            return RuleResult.HardStop(GivenCode, "Vials given must be between 1 and 50.");
        }

        return givenUtc > nowUtc.AddMinutes(5)
            ? RuleResult.HardStop(GivenCode, "The time given cannot be in the future.")
            : RuleResult.Pass(GivenCode);
    }

    /// <summary>A note when the vials given differ from those recommended; informational, not a stop.</summary>
    public static string? GivenDifference(int? recommended, int? given) =>
        recommended is int r && given is int g && r != g
            ? $"{g} vial{(g == 1 ? "" : "s")} given; {r} recommended."
            : null;

    /// <summary>
    /// An evaluation still needing work, for the downtime sheet: "Delivery 2026-11-07: candidate, 1 vial recommended;
    /// RhIG not yet given" or "Delivery 2026-11-07: not yet decided". Null when nothing is outstanding.
    /// </summary>
    public static string? OutstandingLine(RhigContext context, DateTime eventUtc, RhigDecision decision, int? vials, bool completed, bool given)
    {
        if (!completed)
        {
            return decision == RhigDecision.Undetermined
                ? Summary(context, eventUtc, decision, vials)
                : $"{Summary(context, eventUtc, decision, vials)}; evaluation not completed";
        }

        return decision == RhigDecision.Candidate && !given
            ? $"{Summary(context, eventUtc, decision, vials)}; RhIG not yet given"
            : null;
    }

    /// <summary>"Delivery 2026-11-07: candidate, 1 vial recommended", "…: not a candidate", "…: not yet decided".</summary>
    public static string Summary(RhigContext context, DateTime eventUtc, RhigDecision decision, int? vials)
    {
        var label = context switch
        {
            RhigContext.Antepartum => "Antepartum",
            RhigContext.Postpartum => "Delivery",
            _ => "Other exposure"
        };
        var head = $"{label} {eventUtc:yyyy-MM-dd}";
        return decision switch
        {
            RhigDecision.Candidate => vials is int n ? $"{head}: candidate, {n} vial{(n == 1 ? "" : "s")} recommended" : $"{head}: candidate",
            RhigDecision.NotCandidate => $"{head}: not a candidate",
            _ => $"{head}: not yet decided"
        };
    }

    public static RuleResult EvaluateValues(int? vials, decimal? fetalBleedPercent)
    {
        if (vials is < 0 or > 50)
        {
            return RuleResult.HardStop(CompleteCode, "Vials recommended must be between 0 and 50.");
        }

        return fetalBleedPercent is < 0m or > 100m
            ? RuleResult.HardStop(CompleteCode, "Fetal cells must be a percentage between 0 and 100.")
            : RuleResult.Pass(CompleteCode);
    }
}
