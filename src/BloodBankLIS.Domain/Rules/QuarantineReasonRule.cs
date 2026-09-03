using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// SoftBank/SafeTrace coded quarantine: quality holds use a catalog reason,
/// not free-text alone (AABB 21 CFR 606.165 disposition).
/// </summary>
public static class QuarantineReasonRule
{
    public const string Code = "INV-Q-REASON";

    public static RuleResult Evaluate(UnitQuarantineReason reason, string? notes)
    {
        if (reason == UnitQuarantineReason.Unspecified)
        {
            return RuleResult.HardStop(Code, "Select a quarantine reason.");
        }

        if (reason == UnitQuarantineReason.Other && string.IsNullOrWhiteSpace(notes))
        {
            return RuleResult.HardStop(Code, "Describe the other quarantine reason.");
        }

        return RuleResult.Pass(Code);
    }
}
