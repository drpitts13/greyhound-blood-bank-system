namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// A massive transfusion protocol still open with no unit issued under it for the facility's reminder minutes
/// (OCD-076; default 240, 0 turns it off). While a protocol is open, massive-transfusion issue stays available for
/// the patient, so one left open after the bleeding is controlled is raised as a reminder to close it. A reminder, not
/// a block: it closes nothing and stops no issue.
/// </summary>
public static class MtpIdleRule
{
    public const int DefaultReminderMinutes = 240;

    /// <summary>The last activity: the latest issue under the protocol, or its activation when nothing was issued.</summary>
    public static DateTime LastActivityUtc(DateTime activatedUtc, DateTime? lastIssueUtc) =>
        lastIssueUtc is DateTime issued && issued > activatedUtc ? issued : activatedUtc;

    public static bool IsIdle(DateTime lastActivityUtc, DateTime nowUtc, int reminderMinutes) =>
        reminderMinutes > 0 && nowUtc - lastActivityUtc >= TimeSpan.FromMinutes(reminderMinutes);

    public static string Notice(DateTime lastActivityUtc, DateTime nowUtc, bool anyIssued)
    {
        var minutes = (int)Math.Max(0, (nowUtc - lastActivityUtc).TotalMinutes);
        var span = minutes >= 120 ? $"{minutes / 60} h" : $"{minutes} min";
        return anyIssued
            ? $"No unit issued under it for {span}. Close it if the bleeding is controlled."
            : $"No unit issued since it was called {span} ago. Close it if it is no longer needed.";
    }

    public static AlertCandidate Alert(long activationId, string? mrn, DateTime lastActivityUtc, DateTime nowUtc, bool anyIssued) =>
        new(
            AlertRule.MtpStillOpen,
            $"MTPIDLE:{activationId}",
            AlertSeverity.Warning,
            $"Massive transfusion protocol still open{(string.IsNullOrWhiteSpace(mrn) ? "" : $": MRN {mrn.Trim()}")}",
            $"MTP #{activationId}. {Notice(lastActivityUtc, nowUtc, anyIssued)} While it is open, massive-transfusion issue stays available for this patient.",
            "/issuing?desk=issue");
}
