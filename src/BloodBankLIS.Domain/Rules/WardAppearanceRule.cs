using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// SoftBank/SafeTrace coded appearance at ward / remote-issue receipt.
/// A defect is a HardStop — return the unit to the blood bank.
/// </summary>
public static class WardAppearanceRule
{
    public const string Code = "TX-WARD-APPEAR";

    public static bool IsAcceptable(UnitAppearance appearance) =>
        appearance == UnitAppearance.Acceptable;

    public static RuleResult Evaluate(UnitAppearance appearance)
    {
        if (IsAcceptable(appearance))
        {
            return RuleResult.Pass(Code);
        }

        return RuleResult.HardStop(
            Code,
            $"Do not acknowledge receipt of a unit with appearance '{appearance}'. Return it to the blood bank.");
    }
}
