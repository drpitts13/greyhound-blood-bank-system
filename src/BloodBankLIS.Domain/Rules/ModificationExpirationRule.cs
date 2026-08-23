using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Pure computation of a modification result unit's new expiration date/time: an
/// anchor (modification time or collection time) plus the rule's offset, but never
/// later than the original (source) unit's expiration date/time. For a pool, the
/// caller passes the earliest expiration among all source units, since the pooled
/// result cannot outlive the shortest-lived component pooled into it.
/// </summary>
public static class ModificationExpirationRule
{
    public const string CollectionRequiredCode = "MOD-COLLECTION-REQUIRED";

    public static DateTime ComputeNewExpiresUtc(
        DateTime anchorUtc,
        ExpirationOffsetCode offset,
        DateTime originalExpiresUtc)
    {
        var candidate = anchorUtc + offset.ToTimeSpan();
        return candidate < originalExpiresUtc ? candidate : originalExpiresUtc;
    }

    /// <summary>The expiration ceiling for a set of source units: the earliest of their expirations.</summary>
    public static DateTime EarliestExpiration(IEnumerable<DateTime> sourceExpiresUtc) => sourceExpiresUtc.Min();

    /// <summary>
    /// Resolves a unit's collection timestamp. Prefers <paramref name="collectedUtc"/>,
    /// then <paramref name="collectionDateTime"/> treated as UTC when Kind is Unspecified.
    /// </summary>
    public static DateTime? ResolveCollectionUtc(DateTime? collectedUtc, DateTime? collectionDateTime)
    {
        if (collectedUtc.HasValue)
        {
            return AsUtc(collectedUtc.Value);
        }

        if (collectionDateTime.HasValue)
        {
            return AsUtc(collectionDateTime.Value);
        }

        return null;
    }

    /// <summary>
    /// Chooses the calculation anchor. Collection-relative dating requires every source
    /// to have a collection timestamp; the earliest of those is used so a pool cannot
    /// outlive the oldest component's collection-based dating.
    /// </summary>
    public static bool TryResolveAnchorUtc(
        ExpirationRelativeTo relativeTo,
        DateTime modificationUtc,
        IEnumerable<DateTime?> collectionUtc,
        out DateTime anchorUtc,
        out string? errorCode)
    {
        anchorUtc = default;
        errorCode = null;

        if (relativeTo == ExpirationRelativeTo.ModificationDateTime)
        {
            anchorUtc = modificationUtc;
            return true;
        }

        DateTime? earliest = null;
        foreach (var collection in collectionUtc)
        {
            if (!collection.HasValue)
            {
                errorCode = CollectionRequiredCode;
                return false;
            }

            if (earliest is null || collection.Value < earliest.Value)
            {
                earliest = collection.Value;
            }
        }

        if (earliest is null)
        {
            errorCode = CollectionRequiredCode;
            return false;
        }

        anchorUtc = earliest.Value;
        return true;
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
