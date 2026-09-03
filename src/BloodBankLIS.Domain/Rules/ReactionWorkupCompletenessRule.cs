using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// AABB-style transfusion-reaction workup gate (clerical check, visual inspection,
/// post-transfusion DAT). SoftBank and SafeTrace block close until these steps are
/// recorded; Greyhound previously allowed close on free-text findings alone.
/// </summary>
public static class ReactionWorkupCompletenessRule
{
    public const string Code = "RXN-WORKUP-INCOMPLETE";

    public static RuleResult Evaluate(
        bool clericalCheckCompleted,
        bool visualInspectionCompleted,
        DatWorkupResult datResult,
        string? elutionResult)
    {
        if (!clericalCheckCompleted)
            return RuleResult.HardStop(Code, "Clerical check (patient ID, unit number, ABO/Rh) has not been recorded.");

        if (!visualInspectionCompleted)
            return RuleResult.HardStop(Code, "Visual inspection of the returned unit or segments has not been recorded.");

        if (datResult == DatWorkupResult.NotRecorded)
            return RuleResult.HardStop(Code, "Post-transfusion DAT result has not been recorded.");

        if (datResult == DatWorkupResult.Positive && string.IsNullOrWhiteSpace(elutionResult))
            return RuleResult.HardStop(Code, "DAT is positive; record the elution result or document why elution was not performed.");

        return RuleResult.Pass(Code);
    }
}
