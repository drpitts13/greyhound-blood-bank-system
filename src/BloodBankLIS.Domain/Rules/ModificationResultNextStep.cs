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
            return "In quarantine. No storage location is recorded. Scan the new unit number before release.";
        }

        return $"In quarantine at {locationLabel}. Scan the new unit number before release.";
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

    public static string AliquotParentCue(bool confirmed, string? unitNumber, UnitStatus status, decimal? remainingVolume)
    {
        if (!confirmed)
        {
            return "The parent was left in place. Its remaining status and volume could not be confirmed.";
        }

        var number = string.IsNullOrWhiteSpace(unitNumber) ? "The parent" : unitNumber.Trim();
        if (remainingVolume is null)
        {
            return $"{number} stayed {status}. Its volume is not recorded.";
        }

        return $"{number} stayed {status} with {remainingVolume.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)} mL remaining.";
    }

    public readonly record struct RetiredSourceFact(string? UnitNumber, bool Confirmed, UnitStatus? Status);

    public static string? RetiredSourceCue(IReadOnlyList<RetiredSourceFact> sources)
    {
        if (sources.Count == 0)
        {
            return null;
        }

        return string.Join(" ", sources.Select(DescribeRetiredSource));
    }

    private static string DescribeRetiredSource(RetiredSourceFact source)
    {
        var number = string.IsNullOrWhiteSpace(source.UnitNumber) ? "A source" : source.UnitNumber.Trim();
        if (!source.Confirmed || source.Status is null)
        {
            return $"{number} was retired. Its current status could not be confirmed.";
        }

        return $"{number} is {source.Status}.";
    }
}
