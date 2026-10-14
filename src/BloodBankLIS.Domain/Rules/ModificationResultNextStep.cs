using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// What staff should do with a unit a modification just created.
/// A quarantined result names where it is and how to release it.
/// An assigned result, or a unit that is not on the quarantine board, does not invent that step.
/// </summary>
public static class ModificationResultNextStep
{
    public static string? ReleaseCue(UnitStatus status, string locationLabel)
    {
        if (status != UnitStatus.Quarantine)
        {
            return null;
        }

        if (locationLabel == StorageLocationLabel.None)
        {
            return "In quarantine. No storage location is recorded. Release it before it can be issued.";
        }

        return $"In quarantine at {locationLabel}. Release it before it can be issued.";
    }

    public static string? ReleaseHref(UnitStatus status, long unitId, bool canRelease)
    {
        if (status != UnitStatus.Quarantine || !canRelease || unitId <= 0)
        {
            return null;
        }

        return $"/inventory?board=quarantine&unitId={unitId}#quality-quarantine";
    }

    public static long? QuarantineReleaseTarget(string? board, long? unitId, IEnumerable<long> quarantineUnitIds)
    {
        if (!string.Equals(board, "quarantine", StringComparison.OrdinalIgnoreCase) || unitId is not long id || id <= 0)
        {
            return null;
        }

        return quarantineUnitIds.Contains(id) ? id : null;
    }
}
