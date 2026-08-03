using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Pure computation of a modification result unit's new expiration date/time: the
/// modification date/time plus the rule's offset, but never later than the original
/// (source) unit's expiration date/time. For a pool, the caller passes the earliest
/// expiration among all source units, since the pooled result cannot outlive the
/// shortest-lived component pooled into it.
/// </summary>
public static class ModificationExpirationRule
{
    public static DateTime ComputeNewExpiresUtc(
        DateTime modificationUtc,
        ExpirationOffsetCode offset,
        DateTime originalExpiresUtc)
    {
        var candidate = modificationUtc + offset.ToTimeSpan();
        return candidate < originalExpiresUtc ? candidate : originalExpiresUtc;
    }

    /// <summary>The expiration ceiling for a set of source units: the earliest of their expirations.</summary>
    public static DateTime EarliestExpiration(IEnumerable<DateTime> sourceExpiresUtc) => sourceExpiresUtc.Min();
}
