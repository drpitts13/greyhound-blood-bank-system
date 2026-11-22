using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Compliance;

/// <summary>
/// The alert raised when units are quarantined automatically after a storage excursion ("29 unit(s) quarantined at
/// FRIDGE-1"). End-user testing pass 94: two such alerts stayed open, Critical, two days after all but one unit had been
/// reviewed and released. Rechecked by the alert scanner: it says how many units at that location are still held for the
/// excursion, and closes when none is.
/// </summary>
public static class ExcursionQuarantineAlert
{
    public const string KeyPrefix = "TEMP-QUAR:";

    /// <summary>The location id in a key "TEMP-QUAR:{locationId}:{yyyyMMddHHmm}", or null.</summary>
    public static long? LocationId(string key)
    {
        var parts = key.Split(':');
        return parts.Length >= 2 && long.TryParse(parts[1], out var id) ? id : null;
    }

    /// <summary>
    /// As <see cref="Now"/>, but one open alert per location (end-user testing pass 127: two quarantine runs in one
    /// excursion each raised "1 unit(s) still quarantined at FRIDGE-1"). The first alert seen for a location is kept; any
    /// other for the same location is closed, since the held count is per location.
    /// </summary>
    public static AlertCandidate? NowOncePerLocation(Alert alert, IReadOnlyDictionary<long, int> heldByLocation, ISet<long> seenLocations)
    {
        if (LocationId(alert.Key) is long locationId && !seenLocations.Add(locationId))
        {
            return null;
        }

        return Now(alert, heldByLocation);
    }

    /// <summary>What the alert says now, or null when no unit at its location is still held for an excursion.</summary>
    public static AlertCandidate? Now(Alert alert, IReadOnlyDictionary<long, int> heldByLocation)
    {
        if (LocationId(alert.Key) is not long locationId)
        {
            return new AlertCandidate(alert.Source, alert.Key, alert.Severity, alert.Title, alert.Message, alert.Link);
        }

        var held = heldByLocation.GetValueOrDefault(locationId);
        if (held == 0)
        {
            return null;
        }

        var at = alert.Title.IndexOf(" at ", StringComparison.Ordinal);
        var place = at < 0 ? string.Empty : alert.Title[(at + 4)..];
        return new AlertCandidate(alert.Source, alert.Key, alert.Severity,
            $"{held} unit(s) still quarantined at {place}".TrimEnd(), alert.Message, alert.Link);
    }
}
