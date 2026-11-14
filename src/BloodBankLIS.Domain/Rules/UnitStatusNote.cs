using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Cycle 696: why a unit is out of routine use, in a few words, for the top-bar lookup: where a shipped unit went, and the
/// recorded reason of a recalled, quarantined, held, missing, damaged, discarded or returned unit. Long reasons are
/// shortened; a unit in routine use, or with no reason recorded, has no note.
/// </summary>
public static class UnitStatusNote
{
    public const int MaxReasonLength = 60;

    public static string? For(
        UnitStatus status,
        string? shippedToFacility = null,
        string? recallReason = null,
        string? quarantineReason = null,
        string? holdReason = null,
        string? missingReason = null,
        string? damagedReason = null,
        string? discardReason = null,
        string? supplierReturnReason = null) => status switch
    {
        UnitStatus.ShippedOut => Note("shipped to", shippedToFacility),
        UnitStatus.Recalled => Note("recalled", recallReason),
        UnitStatus.Quarantine => Note("quarantine", quarantineReason),
        UnitStatus.OnHold => Note("on hold", holdReason),
        UnitStatus.Missing => Note("missing", missingReason),
        UnitStatus.Damaged => Note("damaged", damagedReason),
        UnitStatus.Discarded => Note("discarded", discardReason),
        UnitStatus.ReturnedToSupplier => Note("returned to supplier", supplierReturnReason),
        _ => null
    };

    private static string? Note(string label, string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return null;
        }

        var text = reason.Trim();
        if (text.Length > MaxReasonLength)
        {
            text = text[..(MaxReasonLength - 1)].TrimEnd() + "…";
        }

        return label == "shipped to" ? $"shipped to {text}" : $"{label}: {text}";
    }
}
