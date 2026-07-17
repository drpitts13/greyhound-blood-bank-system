using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Compares a newly determined ABO/Rh against the patient's current historical
/// record. A discrepancy is a Warning at result entry/verification; it contributes
/// a HardStop on the issue path if unresolved (see docs/safety-rules.md section 6).
/// The system never auto-resolves a discrepancy.
/// </summary>
public static class AboRhDeltaRule
{
    public const string DeltaCode = "RES-ABORH-DELTA";

    public static RuleResult Evaluate(AboRh? current, AboRh newResult)
    {
        if (current is null)
        {
            return RuleResult.Pass(DeltaCode, "No prior ABO/Rh on record.");
        }

        return current.Value == newResult
            ? RuleResult.Pass(DeltaCode)
            : RuleResult.Warning(DeltaCode, $"New ABO/Rh {newResult} disagrees with historical {current.Value}.");
    }
}
