namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// A transfusion started at the bedside and not yet completed. After the facility's reminder hours it is
/// raised as a warning so the record gets finished. This is a documentation reminder, not a clinical
/// infusion time limit; the facility sets the hours (0 turns the reminder off).
/// </summary>
public static class TransfusionCompletionRule
{
    public const int DefaultReminderHours = 6;

    public static bool IsOverdue(DateTime? startUtc, DateTime nowUtc, int reminderHours) =>
        reminderHours > 0 && startUtc is DateTime start && nowUtc - start >= TimeSpan.FromHours(reminderHours);

    public static AlertCandidate Alert(long issueId, string unitNumber, string? mrn, DateTime startUtc) =>
        new(
            AlertRule.TransfusionNotCompleted,
            $"TXINCOMPLETE:ISSUE-{issueId}",
            AlertSeverity.Warning,
            $"Transfusion not completed: {unitNumber}",
            $"Started {startUtc:yyyy-MM-dd HH:mm} UTC{(string.IsNullOrWhiteSpace(mrn) ? "" : $" for MRN {mrn.Trim()}")} and not yet completed. " +
            "Record the stop time, volume, and outcome, or check whether the transfusion is still running.",
            $"/issuing?desk=transfuse&issueId={issueId}");
}
