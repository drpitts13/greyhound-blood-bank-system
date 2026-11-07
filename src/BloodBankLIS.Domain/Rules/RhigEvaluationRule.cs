using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// RhIG evaluation support. Candidacy and dosing are facility decisions not yet confirmed (OCD-069): this rule lists the
/// facts on file without concluding from them, and checks only that a completed record says what was decided.
/// </summary>
public static class RhigEvaluationRule
{
    public const string CompleteCode = "RHIG-COMPLETE";

    /// <summary>Facts the reviewer weighs, as plain statements; no conclusion is drawn.</summary>
    public static IReadOnlyList<string> Facts(RhType? motherRh, bool antiDOnHistory, RhType infantRh, FetalBleedScreen screen, decimal? fetalBleedPercent)
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
