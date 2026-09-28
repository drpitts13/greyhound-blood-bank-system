using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// SoftBank/SafeTrace coded appearance at intake. A specific defect
/// (clots, hemolysis, leaking, …) is a HardStop — return the unit to the supplier.
/// </summary>
public static class ReceiveAppearanceRule
{
    public const string Code = "INV-RCV-APPEAR";

    public static bool IsAcceptable(UnitAppearance appearance) =>
        appearance == UnitAppearance.Acceptable;

    public static RuleResult Evaluate(bool required, UnitAppearance appearance)
    {
        if (!required || IsAcceptable(appearance))
        {
            return RuleResult.Pass(Code);
        }

        return RuleResult.HardStop(
            Code,
            $"Do not receive a unit with appearance '{appearance}'. Return it to the supplier.");
    }

    /// <summary>
    /// Reason for returning a packing-list unit that will not be received.
    /// An acceptable appearance has no suggested reason unless staff typed one.
    /// </summary>
    public static string? SuggestedSupplierReturnReason(
        UnitAppearance appearance, string? notes, string? typedReason)
    {
        if (!string.IsNullOrWhiteSpace(typedReason))
        {
            return typedReason.Trim();
        }

        if (IsAcceptable(appearance))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(notes))
        {
            return notes.Trim();
        }

        return $"Do not receive appearance {appearance}.";
    }
}
