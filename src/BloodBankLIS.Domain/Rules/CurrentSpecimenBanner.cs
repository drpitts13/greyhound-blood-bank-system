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

    /// <param name="AwaitingAccession">
    /// When there is no current specimen, or it blocks issue: a newer in-date tube drawn and not yet received, or received
    /// and not yet accepted. It is not the current specimen until accepted; receiving it beats drawing again.
    /// </param>
    public sealed record Cue(
        string? AccessionNumber,
        string Message,
        bool BlocksIssue,
        bool ExpiresSoon,
        string? AwaitingAccession = null,
        string? AwaitingNote = null);

    /// <param name="display">How a time is shown (the chart passes local time; Cycle 604). Defaults to the stored UTC.</param>
    public static Cue Describe(IEnumerable<Fact> specimens, DateTime nowUtc, Func<DateTime, DateTime>? display = null)
    {
        string Shown(DateTime? utc) => utc is DateTime t ? (display?.Invoke(t) ?? t).ToString("yyyy-MM-dd HH:mm") : string.Empty;

        var current = specimens
            .Where(s => s.Status == SpecimenStatus.Accepted)
            .OrderByDescending(s => s.CollectedUtc)
            .ThenBy(s => s.AccessionNumber, StringComparer.Ordinal)
            .FirstOrDefault();
        // The newest in-date tube on its way (drawn, or received and not yet accepted), drawn after the current one.
        var awaiting = specimens
            .Where(s => s.Status is SpecimenStatus.Collected or SpecimenStatus.Received
                && (s.ExpiresUtc is null || s.ExpiresUtc > nowUtc)
                && (current is null || s.CollectedUtc > current.CollectedUtc))
            .OrderByDescending(s => s.CollectedUtc)
            .ThenBy(s => s.AccessionNumber, StringComparer.Ordinal)
            .FirstOrDefault();
        string? AwaitingNote() => awaiting is null
            ? null
            : awaiting.Status == SpecimenStatus.Collected
                ? $"Tube {awaiting.AccessionNumber} was drawn {Shown(awaiting.CollectedUtc)} and is not yet received: receive it rather than drawing again."
                : $"Tube {awaiting.AccessionNumber} is received and not yet accepted: accept it rather than drawing again.";

        if (current is null)
        {
            return new Cue(null, "No current specimen.", true, false, awaiting?.AccessionNumber, AwaitingNote());
        }

        var result = SpecimenExpirationRule.Evaluate(current.ExpiresUtc, nowUtc, NearExpiryWindow);
        var accession = current.AccessionNumber;
        if (result.Code == SpecimenExpirationRule.ExpiredCode && result.Severity == RuleSeverity.HardStop)
        {
            var message = current.ExpiresUtc is null
                ? $"Specimen {accession} has no expiration."
                : $"Specimen {accession} expired {Shown(current.ExpiresUtc)}.";
            return new Cue(accession, message, true, false, awaiting?.AccessionNumber, AwaitingNote());
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
