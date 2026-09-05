namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Electronic (computer) crossmatch is permitted only when its preconditions hold:
/// the patient's current ABO/Rh is confirmed, the antibody screen is negative,
/// there is no antibody history, and no antibody-identification workup is open.
/// Otherwise a serologic crossmatch is required (HardStop). Pure and deterministic
/// (see docs/workflows.md section 4).
/// </summary>
public static class ElectronicCrossmatchEligibilityRule
{
    public const string Code = "XM-EC-ELIGIBLE";

    public const string CurrentTypeCode = "XM-EC-ABORH";
    public const string SecondTypeCode = "XM-EC-SECOND";
    public const string ScreenCode = "XM-EC-SCREEN";
    public const string HistoryCode = "XM-EC-HISTORY";
    public const string WorkupOpenCode = "XM-EC-ABID-OPEN";
    public const string FacilityCode = "XM-EC-POLICY";

    public static IReadOnlyList<RuleResult> EvaluateCriteria(
        bool currentAboRhConfirmed,
        bool antibodyScreenNegative,
        bool hasAntibodyHistory,
        bool hasSecondConcordantAboRh,
        bool hasOpenAntibodyIdWorkup = false)
    {
        return
        [
            currentAboRhConfirmed
                ? RuleResult.Pass(CurrentTypeCode, "Current ABO/Rh is confirmed.")
                : RuleResult.HardStop(CurrentTypeCode, "Electronic crossmatch requires a confirmed current ABO/Rh."),
            hasSecondConcordantAboRh
                ? RuleResult.Pass(SecondTypeCode, "Two concordant ABO/Rh determinations are on file.")
                : RuleResult.HardStop(SecondTypeCode, "Electronic crossmatch requires two concordant ABO/Rh determinations."),
            antibodyScreenNegative
                ? RuleResult.Pass(ScreenCode, "Current antibody screen is negative.")
                : RuleResult.HardStop(ScreenCode, "Electronic crossmatch requires a negative antibody screen."),
            hasAntibodyHistory
                ? RuleResult.HardStop(HistoryCode, "Electronic crossmatch is not permitted with a history of clinically significant antibodies, including antibodies that are currently undetectable.")
                : RuleResult.Pass(HistoryCode, "No clinically significant antibody history."),
            hasOpenAntibodyIdWorkup
                ? RuleResult.HardStop(
                    WorkupOpenCode,
                    "Electronic crossmatch is not permitted while an antibody-identification workup is open. Complete or void it, or record a serologic crossmatch.")
                : RuleResult.Pass(WorkupOpenCode, "No open antibody-identification workup.")
        ];
    }

    public static RuleResult Evaluate(
        bool currentAboRhConfirmed,
        bool antibodyScreenNegative,
        bool hasAntibodyHistory,
        bool hasSecondConcordantAboRh,
        bool hasOpenAntibodyIdWorkup = false)
    {
        var firstStop = EvaluateCriteria(
                currentAboRhConfirmed,
                antibodyScreenNegative,
                hasAntibodyHistory,
                hasSecondConcordantAboRh,
                hasOpenAntibodyIdWorkup)
            .FirstOrDefault(r => r.Severity == RuleSeverity.HardStop);
        return firstStop ?? RuleResult.Pass(Code);
    }
}
