using BloodBankLIS.Domain.Entities;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Backup schedule and retention. A backup counts only when it was written and verified.
/// Retention removes the files of older verified backups beyond the keep count; the
/// log rows stay. Failed attempts are never counted toward the keep count.
/// </summary>
public static class DatabaseBackupRule
{
    public const string ManualTrigger = "Manual";
    public const string ScheduledTrigger = "Scheduled";

    public static bool IsGood(DatabaseBackup backup) => backup.Succeeded && backup.Verified;

    public static DateTime? LastGoodUtc(IEnumerable<DatabaseBackup> backups) =>
        backups.Where(IsGood).Select(b => b.CompletedUtc ?? b.StartedUtc).DefaultIfEmpty().Max() is var last && last != default
            ? last
            : null;

    /// <summary>A scheduled backup is due when there is no good backup inside the interval.</summary>
    public static bool IsDue(IEnumerable<DatabaseBackup> backups, DateTime nowUtc, TimeSpan interval) =>
        LastGoodUtc(backups) is not DateTime last || nowUtc - last >= interval;

    /// <summary>Overdue for alerting: no good backup inside the overdue window.</summary>
    public static bool IsOverdue(IEnumerable<DatabaseBackup> backups, DateTime nowUtc, TimeSpan overdue) =>
        LastGoodUtc(backups) is not DateTime last || nowUtc - last >= overdue;

    /// <summary>Good backups whose files should be removed so only the newest <paramref name="keep"/> remain.</summary>
    public static IReadOnlyList<DatabaseBackup> SelectForRetention(IEnumerable<DatabaseBackup> backups, int keep)
    {
        if (keep <= 0)
        {
            return [];
        }

        return backups
            .Where(b => IsGood(b) && b.FileDeletedUtc is null)
            .OrderByDescending(b => b.CompletedUtc ?? b.StartedUtc)
            .ThenByDescending(b => b.Id)
            .Skip(keep)
            .ToList();
    }

    /// <summary>File name for a backup started at <paramref name="startedUtc"/>.</summary>
    public static string FileName(DateTime startedUtc, bool sqlServer) =>
        $"bloodbanklis-{startedUtc:yyyyMMdd-HHmmss}.{(sqlServer ? "bak" : "db")}";
}
