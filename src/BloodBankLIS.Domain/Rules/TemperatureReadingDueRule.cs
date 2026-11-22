namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// A storage location's temperature reading is due (end-user testing pass 87: the last readings were two days old and
/// the plasma freezer had never been read, with nothing to say so). Only once the facility has set the interval, and only
/// for a location with a temperature range.
/// </summary>
public static class TemperatureReadingDueRule
{
    public static bool IsDue(bool hasRange, DateTime? lastReadingUtc, int intervalHours, DateTime nowUtc) =>
        hasRange && intervalHours > 0 && (lastReadingUtc is not DateTime last || nowUtc - last >= TimeSpan.FromHours(intervalHours));

    /// <summary>What the page says: "No reading recorded" or "Due since 2026-11-12 17:59".</summary>
    public static string? Text(bool hasRange, DateTime? lastReadingUtc, int intervalHours, DateTime nowUtc) =>
        !IsDue(hasRange, lastReadingUtc, intervalHours, nowUtc)
            ? null
            : lastReadingUtc is DateTime last
                ? $"Due since {ClinicalTimeText.Local(last.AddHours(intervalHours))}"
                : "No reading recorded";
}
