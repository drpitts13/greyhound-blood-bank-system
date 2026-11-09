using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// The specimen the issue gate will read, and whether that specimen already
/// blocks issue. Latest accepted draw wins. The near-expiry window is the
/// issue-gate default of 12 hours.
/// </summary>
public static class CurrentSpecimenBanner
{
    public static readonly TimeSpan NearExpiryWindow = TimeSpan.FromHours(12);

    public sealed record Fact(
        string AccessionNumber,
        SpecimenStatus Status,
        DateTime CollectedUtc,
        DateTime? ExpiresUtc);

    public sealed record Cue(
        string? AccessionNumber,
        string Message,
        bool BlocksIssue,
        bool ExpiresSoon);

    /// <param name="display">How a time is shown (the chart passes local time; Cycle 604). Defaults to the stored UTC.</param>
    public static Cue Describe(IEnumerable<Fact> specimens, DateTime nowUtc, Func<DateTime, DateTime>? display = null)
    {
        string Shown(DateTime? utc) => utc is DateTime t ? (display?.Invoke(t) ?? t).ToString("yyyy-MM-dd HH:mm") : string.Empty;

        var current = specimens
            .Where(s => s.Status == SpecimenStatus.Accepted)
            .OrderByDescending(s => s.CollectedUtc)
            .ThenBy(s => s.AccessionNumber, StringComparer.Ordinal)
            .FirstOrDefault();
        if (current is null)
        {
            return new Cue(null, "No current specimen.", true, false);
        }

        var result = SpecimenExpirationRule.Evaluate(current.ExpiresUtc, nowUtc, NearExpiryWindow);
        var accession = current.AccessionNumber;
        if (result.Code == SpecimenExpirationRule.ExpiredCode && result.Severity == RuleSeverity.HardStop)
        {
            var message = current.ExpiresUtc is null
                ? $"Specimen {accession} has no expiration."
                : $"Specimen {accession} expired {Shown(current.ExpiresUtc)}.";
            return new Cue(accession, message, true, false);
        }

        if (result.Severity == RuleSeverity.Warning)
        {
            return new Cue(
                accession,
                $"Specimen {accession} expires soon, at {Shown(current.ExpiresUtc)}.",
                false,
                true);
        }

        return new Cue(
            accession,
            $"Specimen {accession} expires {Shown(current.ExpiresUtc)}.",
            false,
            false);
    }
}
