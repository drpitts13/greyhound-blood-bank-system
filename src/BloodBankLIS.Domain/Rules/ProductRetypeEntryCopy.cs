using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Entry-form wording for unit ABO/Rh retype. Names the interpreted type
/// after record. This is UX for an already stored interpretation,
/// not a new regulatory claim.
/// </summary>
public static class ProductRetypeEntryCopy
{
    public static string Format(AboGroup abo, RhType? rh)
    {
        var group = abo == AboGroup.Unknown ? "?" : abo.ToString();
        if (rh is null)
        {
            return $"{group} (Anti-D not performed)";
        }

        var sign = rh switch
        {
            RhType.Positive => "+",
            RhType.Negative => "\u2212",
            _ => "?"
        };
        return $"{group}{sign}";
    }

    public static string Saved(AboGroup abo, RhType? rh) =>
        $"Retype recorded as {Format(abo, rh)}. Unit is Available.";

    public static string SavedQuarantine(AboGroup abo, RhType? rh, string? discrepancy) =>
        string.IsNullOrWhiteSpace(discrepancy)
            ? $"Retype recorded as {Format(abo, rh)}. Unit moved to Quarantine."
            : $"Retype recorded as {Format(abo, rh)}. {discrepancy}";

    public static string VerifiedAvailable(AboGroup abo, RhType? rh) =>
        Saved(abo, rh);

    public static string VerifiedQuarantine(AboGroup abo, RhType? rh, string? discrepancy) =>
        SavedQuarantine(abo, rh, discrepancy);
}
