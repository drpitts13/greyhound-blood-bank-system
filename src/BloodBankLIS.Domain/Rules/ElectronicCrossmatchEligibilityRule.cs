namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Electronic (computer) crossmatch is permitted only when its preconditions hold:
/// the patient's current ABO/Rh is confirmed, the antibody screen is negative, and
/// there is no antibody history. Otherwise a serologic crossmatch is required
/// (HardStop). Pure and deterministic (see docs/workflows.md section 4).
/// </summary>
public static class ElectronicCrossmatchEligibilityRule
{
    public const string Code = "XM-EC-ELIGIBLE";

    public static RuleResult Evaluate(
        bool currentAboRhConfirmed,
        bool antibodyScreenNegative,
        bool hasAntibodyHistory,
        bool hasSecondConcordantAboRh)
    {
        if (!currentAboRhConfirmed)
        {
            return RuleResult.HardStop(Code, "Electronic crossmatch requires a confirmed current ABO/Rh.");
        }

        if (!hasSecondConcordantAboRh)
        {
            return RuleResult.HardStop(Code, "Electronic crossmatch requires two concordant ABO/Rh determinations.");
        }

        if (!antibodyScreenNegative)
        {
            return RuleResult.HardStop(Code, "Electronic crossmatch requires a negative antibody screen.");
        }

        if (hasAntibodyHistory)
        {
            return RuleResult.HardStop(Code, "Electronic crossmatch is not permitted with a history of clinically significant antibodies.");
        }

        return RuleResult.Pass(Code);
    }
}
