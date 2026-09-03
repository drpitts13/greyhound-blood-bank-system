using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// SoftBank/SafeTrace coded appearance immediately before issue.
/// A defect (clots, hemolysis, leaking, …) is a HardStop — do not issue.
/// </summary>
public static class IssueAppearanceRule
{
    public const string Code = "ISS-APPEAR";

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
            $"Do not issue a unit with appearance '{appearance}'. Quarantine or discard it.");
    }
}
