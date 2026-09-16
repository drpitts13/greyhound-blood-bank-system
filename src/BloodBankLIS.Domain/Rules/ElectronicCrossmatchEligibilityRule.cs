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
    public const string ExtendedXmCode = "XM-EC-EXTXM";
    public const string FacilityCode = "XM-EC-POLICY";
    public const string VisitsCode = "XM-EC-VISITS";
    public const string SpecimensCode = "XM-EC-SPECIMENS";
    public const string TestsCode = "XM-EC-TESTS";

    public static IReadOnlyList<RuleResult> EvaluateCriteria(
        bool currentAboRhConfirmed,
        bool antibodyScreenNegative,
        bool hasAntibodyHistory,
        bool hasSecondConcordantAboRh,
        bool hasOpenAntibodyIdWorkup = false,
        bool requiresExtendedCrossmatch = false,
        int negativeScreenVisitCount = 0,
        int negativeScreenSpecimenCount = 0,
        int negativeScreenTestCount = 0,
        int minimumVisits = 0,
        int minimumSpecimens = 0,
        int minimumTests = 0)
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
                : RuleResult.Pass(WorkupOpenCode, "No open antibody-identification workup."),
            requiresExtendedCrossmatch
                ? RuleResult.HardStop(
                    ExtendedXmCode,
                    "Electronic crossmatch is not permitted while an extended-crossmatch special requirement is active.")
                : RuleResult.Pass(ExtendedXmCode, "No active extended-crossmatch special requirement."),
            CountCriterion(VisitsCode, "visits", negativeScreenVisitCount, minimumVisits),
            CountCriterion(SpecimensCode, "specimens", negativeScreenSpecimenCount, minimumSpecimens),
            CountCriterion(TestsCode, "antibody screens", negativeScreenTestCount, minimumTests)
        ];
    }

    public static RuleResult Evaluate(
        bool currentAboRhConfirmed,
        bool antibodyScreenNegative,
        bool hasAntibodyHistory,
        bool hasSecondConcordantAboRh,
        bool hasOpenAntibodyIdWorkup = false,
        bool requiresExtendedCrossmatch = false,
        int negativeScreenVisitCount = 0,
        int negativeScreenSpecimenCount = 0,
        int negativeScreenTestCount = 0,
        int minimumVisits = 0,
        int minimumSpecimens = 0,
        int minimumTests = 0)
    {
        var firstStop = EvaluateCriteria(
                currentAboRhConfirmed,
                antibodyScreenNegative,
                hasAntibodyHistory,
                hasSecondConcordantAboRh,
                hasOpenAntibodyIdWorkup,
                requiresExtendedCrossmatch,
                negativeScreenVisitCount,
                negativeScreenSpecimenCount,
                negativeScreenTestCount,
                minimumVisits,
                minimumSpecimens,
                minimumTests)
            .FirstOrDefault(r => r.Severity == RuleSeverity.HardStop);
        return firstStop ?? RuleResult.Pass(Code);
    }

    private static RuleResult CountCriterion(string code, string noun, int actual, int minimum)
    {
        if (minimum <= 0 || actual >= minimum)
        {
            return RuleResult.Pass(
                code,
                $"Negative antibody screen {noun} meet the electronic XM minimum ({actual} of {Math.Max(minimum, 0)}).");
        }

        return RuleResult.HardStop(
            code,
            $"Electronic crossmatch requires at least {minimum} {noun} with no positive antibody screen reaction (found {actual}).");
    }
}
