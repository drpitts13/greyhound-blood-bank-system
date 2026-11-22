using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Compliance;

/// <summary>
/// The alert the expiry sweep raises ("8 unit(s) expired … Remove them from storage"). End-user testing pass 95: it stayed
/// open until acknowledged, whether the units had been discarded or not. Rechecked by the alert scanner: it says how many
/// of the units that sweep expired are still on a shelf (status Expired), and closes when none is.
/// </summary>
public static class ExpiredUnitsAlert
{
    public const string KeyPrefix = "EXPIRED:";

    /// <summary>
    /// Where an expired-units alert opens: the Discard expired desk, where each unit is scanned and discarded (end-user
    /// testing pass 229; before, the Inventory search filtered to Expired, which lists them but discards nothing).
    /// </summary>
    public const string DiscardLink = "/inventory/discard-expired";

    /// <summary>The link alerts raised before pass 229 carry; moved to <see cref="DiscardLink"/> on the next scan.</summary>
    public const string OldLink = "/inventory?status=Expired";

    /// <summary>The sweep time in a key "EXPIRED:yyyyMMddHHmmss", as written there.</summary>
    public static string? SweepStamp(string key) =>
        key.StartsWith(KeyPrefix, StringComparison.Ordinal) && key.Length > KeyPrefix.Length ? key[KeyPrefix.Length..] : null;

    /// <summary>
    /// What the alert says now: unchanged while every unit is still to remove; "2 of 8 expired unit(s) still to remove"
    /// once some are dealt with; null when none of that sweep's units is still marked Expired.
    /// </summary>
    public static AlertCandidate? Now(Alert alert, IReadOnlyDictionary<string, int> stillOnShelfBySweep)
    {
        var same = new AlertCandidate(alert.Source, alert.Key, alert.Severity, alert.Title, alert.Message,
            alert.Link == OldLink ? DiscardLink : alert.Link);
        if (SweepStamp(alert.Key) is not { } stamp)
        {
            return same;
        }

        var left = stillOnShelfBySweep.GetValueOrDefault(stamp);

        if (left == 0)
        {
            return null;
        }

        // The count the sweep moved, from the message it wrote ("The scheduled sweep moved 8 unit(s) to Expired …").
        var moved = System.Text.RegularExpressions.Regex.Match(alert.Message, @"moved (\d+) unit");
        if (!moved.Success || !int.TryParse(moved.Groups[1].Value, out var swept) || left >= swept)
        {
            return same;
        }

        return same with { Title = $"{left} of {swept} expired unit(s) still to remove" + (alert.Title.Contains("reserved for patients", StringComparison.Ordinal) ? " (some were reserved for patients)" : string.Empty) };
    }
}
