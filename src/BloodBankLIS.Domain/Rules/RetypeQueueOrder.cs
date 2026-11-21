namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// The order and wording of the pending ABO/Rh retype board (end-user testing pass 132: the board listed units by when
/// they were received and showed no expiry date, so a unit two hours from outdating sat below a unit received earlier
/// with weeks to go, and nothing said how long a unit had been waiting). Units in date come first, soonest-expiring
/// first; outdated units follow (their retype only records the type before they move to Expired); ties keep the
/// received order.
/// </summary>
public static class RetypeQueueOrder
{
    public static IReadOnlyList<T> Sort<T>(IEnumerable<T> items, Func<T, DateTime?> expiresUtc, Func<T, DateTime> receivedUtc, DateTime nowUtc) =>
        items
            .OrderBy(i => expiresUtc(i) is DateTime e && e <= nowUtc ? 1 : 0)
            .ThenBy(i => expiresUtc(i) ?? DateTime.MaxValue)
            .ThenBy(receivedUtc)
            .ToList();

    /// <summary>"outdated", "expires in 5 h" or "expires in 3 d"; empty with no expiry.</summary>
    public static string ExpiryNote(DateTime? expiresUtc, DateTime nowUtc)
    {
        if (expiresUtc is not DateTime expires)
        {
            return string.Empty;
        }

        var left = expires - nowUtc;
        if (left <= TimeSpan.Zero)
        {
            return "outdated";
        }

        return left.TotalHours < 48 ? $"expires in {Math.Max(1, (int)Math.Ceiling(left.TotalHours))} h" : $"expires in {(int)left.TotalDays} d";
    }

    /// <summary>"waiting 4 h" or "waiting 2 d" since the unit was received.</summary>
    public static string WaitingNote(DateTime receivedUtc, DateTime nowUtc)
    {
        var waited = nowUtc - receivedUtc;
        if (waited < TimeSpan.Zero)
        {
            waited = TimeSpan.Zero;
        }

        return waited.TotalHours < 48 ? $"waiting {(int)waited.TotalHours} h" : $"waiting {(int)waited.TotalDays} d";
    }
}
