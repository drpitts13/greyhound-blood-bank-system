using System.Security.Cryptography;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Infrastructure.Persistence;

/// <summary>Backup settings from the <c>Backup</c> configuration section.</summary>
public sealed record DatabaseBackupOptions(
    string Folder,
    int KeepCount = 14,
    int IntervalHours = 24,
    int OverdueHours = 48);

public sealed record DatabaseBackupDto(
    long Id,
    DateTime StartedUtc,
    DateTime? CompletedUtc,
    string Trigger,
    string RequestedBy,
    string Provider,
    string FileName,
    long? SizeBytes,
    string? Sha256,
    bool Succeeded,
    bool Verified,
    string? VerifyDetail,
    long? AuditEventCount,
    string? LastAuditHash,
    string? Error,
    DateTime? FileDeletedUtc)
{
    public static DatabaseBackupDto From(DatabaseBackup b) => new(
        b.Id, b.StartedUtc, b.CompletedUtc, b.Trigger, b.RequestedBy, b.Provider, Path.GetFileName(b.FilePath),
        b.SizeBytes, b.Sha256, b.Succeeded, b.Verified, b.VerifyDetail, b.AuditEventCount, b.LastAuditHash,
        b.Error, b.FileDeletedUtc);
}

public sealed record DatabaseBackupStatusDto(
    DateTime? LastGoodUtc,
    bool Overdue,
    int IntervalHours,
    int OverdueHours,
    int KeepCount,
    string Folder,
    IReadOnlyList<DatabaseBackupDto> Recent);

/// <summary>
/// Writes a consistent copy of the database and verifies it. SQLite uses VACUUM INTO and
/// then opens the copy read-only for <c>PRAGMA integrity_check</c>. SQL Server uses a
/// COPY_ONLY backup WITH CHECKSUM followed by RESTORE VERIFYONLY; that path runs on the
/// database server's file system. Every attempt, good or not, is logged and audited.
/// </summary>
public sealed class DatabaseBackupService
{
    private readonly BloodBankDbContext _db;
    private readonly DatabaseBackupOptions _options;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter _audit;
    private readonly AlertService? _alerts;

    public DatabaseBackupService(
        BloodBankDbContext db,
        DatabaseBackupOptions options,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        AlertService? alerts = null)
    {
        _db = db;
        _options = options;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
        _alerts = alerts;
    }

    private bool IsSqlite => _db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;

    public async Task<DatabaseBackupStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        var all = await _db.DatabaseBackups.AsNoTracking().ToListAsync(ct);
        var now = _clock.UtcNow;
        return new DatabaseBackupStatusDto(
            DatabaseBackupRule.LastGoodUtc(all),
            DatabaseBackupRule.IsOverdue(all, now, TimeSpan.FromHours(_options.OverdueHours)),
            _options.IntervalHours,
            _options.OverdueHours,
            _options.KeepCount,
            Path.GetFullPath(_options.Folder),
            all.OrderByDescending(b => b.StartedUtc).ThenByDescending(b => b.Id).Take(30).Select(DatabaseBackupDto.From).ToList());
    }

    public async Task<bool> IsScheduledBackupDueAsync(CancellationToken ct = default) =>
        _options.IntervalHours > 0
        && DatabaseBackupRule.IsDue(
            await _db.DatabaseBackups.AsNoTracking().ToListAsync(ct),
            _clock.UtcNow,
            TimeSpan.FromHours(_options.IntervalHours));

    public async Task<DatabaseBackupDto> RunAsync(string trigger, CancellationToken ct = default)
    {
        var started = _clock.UtcNow;
        var sqlServer = !IsSqlite;
        var folder = Path.GetFullPath(_options.Folder);
        var record = new DatabaseBackup
        {
            StartedUtc = started,
            Trigger = trigger,
            RequestedBy = _currentUser.UserName,
            Provider = sqlServer ? DatabaseOptions.SqlServer : DatabaseOptions.Sqlite,
            FilePath = Path.Combine(folder, DatabaseBackupRule.FileName(started, sqlServer))
        };

        try
        {
            if (sqlServer)
            {
                await BackupSqlServerAsync(record, ct);
            }
            else
            {
                Directory.CreateDirectory(folder);
                await BackupSqliteAsync(record, ct);
            }

            record.Succeeded = true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            record.Succeeded = false;
            record.Verified = false;
            record.Error = ex.Message;
        }

        record.CompletedUtc = _clock.UtcNow;
        _db.DatabaseBackups.Add(record);
        await _db.SaveChangesAsync(ct);
        _audit.Record(
            AuditEventType.Export,
            nameof(DatabaseBackup),
            record.Id,
            null,
            new { record.Trigger, record.Provider, File = Path.GetFileName(record.FilePath), record.SizeBytes, record.Sha256, record.Verified, record.AuditEventCount, record.Error },
            DatabaseBackupRule.IsGood(record) ? "Database backup written and verified." : "Database backup failed.");
        await _db.SaveChangesAsync(ct);

        if (DatabaseBackupRule.IsGood(record))
        {
            await ApplyRetentionAsync(ct);
        }
        else if (_alerts is not null)
        {
            await _alerts.RaiseAsync(new AlertCandidate(
                AlertRule.BackupFailed,
                $"BACKUP-FAILED:{record.Id}",
                AlertSeverity.Critical,
                "Database backup failed",
                $"The {trigger.ToLowerInvariant()} backup at {started:yyyy-MM-dd HH:mm} UTC did not complete or verify: {record.Error ?? record.VerifyDetail}",
                "/admin/backups"), ct);
        }

        return DatabaseBackupDto.From(record);
    }

    /// <summary>Raises the overdue alert while no good backup is inside the window, and resolves it once one is.</summary>
    public async Task SyncOverdueAlertAsync(CancellationToken ct = default)
    {
        if (_alerts is null || _options.OverdueHours <= 0)
        {
            return;
        }

        var all = await _db.DatabaseBackups.AsNoTracking().ToListAsync(ct);
        var overdue = DatabaseBackupRule.IsOverdue(all, _clock.UtcNow, TimeSpan.FromHours(_options.OverdueHours));
        var last = DatabaseBackupRule.LastGoodUtc(all);
        AlertCandidate[] found = overdue
            ?
            [
                new AlertCandidate(
                    AlertRule.BackupOverdue,
                    "BACKUP:OVERDUE",
                    AlertSeverity.Critical,
                    "No verified database backup",
                    last is DateTime when
                        ? $"The last verified backup was {when:yyyy-MM-dd HH:mm} UTC, more than {_options.OverdueHours} hours ago."
                        : "There is no verified database backup yet.",
                    "/admin/backups")
            ]
            : [];
        await _alerts.SyncAsync(AlertRule.BackupOverdue, found, ct);
    }

    private async Task BackupSqliteAsync(DatabaseBackup record, CancellationToken ct)
    {
        if (File.Exists(record.FilePath))
        {
            throw new IOException($"Backup file {record.FilePath} already exists.");
        }

        await _db.Database.ExecuteSqlRawAsync("VACUUM INTO {0}", [record.FilePath], ct);

        var info = new FileInfo(record.FilePath);
        record.SizeBytes = info.Length;
        record.Sha256 = await HashAsync(record.FilePath, ct);

        var connection = new SqliteConnectionStringBuilder
        {
            DataSource = record.FilePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString();
        await using var copy = new SqliteConnection(connection);
        await copy.OpenAsync(ct);
        await using (var check = copy.CreateCommand())
        {
            check.CommandText = "PRAGMA integrity_check";
            var result = Convert.ToString(await check.ExecuteScalarAsync(ct));
            record.Verified = string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase);
            record.VerifyDetail = $"integrity_check: {result}";
        }

        await using (var manifest = copy.CreateCommand())
        {
            manifest.CommandText = """SELECT COUNT(*), (SELECT "RecordHash" FROM "AuditEvents" ORDER BY "Id" DESC LIMIT 1) FROM "AuditEvents" """;
            await using var reader = await manifest.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                record.AuditEventCount = reader.GetInt64(0);
                record.LastAuditHash = reader.IsDBNull(1) ? null : reader.GetString(1);
            }
        }
    }

    private async Task BackupSqlServerAsync(DatabaseBackup record, CancellationToken ct)
    {
        var database = _db.Database.GetDbConnection().Database;
        var quoted = "[" + database.Replace("]", "]]", StringComparison.Ordinal) + "]";
        var manifest = await _db.AuditEvents.AsNoTracking()
            .OrderByDescending(a => a.Id)
            .Select(a => new { a.RecordHash })
            .FirstOrDefaultAsync(ct);
        record.AuditEventCount = await _db.AuditEvents.LongCountAsync(ct);
        record.LastAuditHash = manifest?.RecordHash;

        await _db.Database.ExecuteSqlRawAsync(
            $"BACKUP DATABASE {quoted} TO DISK = {{0}} WITH COPY_ONLY, CHECKSUM, INIT",
            [record.FilePath], ct);
        await _db.Database.ExecuteSqlRawAsync(
            "RESTORE VERIFYONLY FROM DISK = {0} WITH CHECKSUM",
            [record.FilePath], ct);
        record.Verified = true;
        record.VerifyDetail = "RESTORE VERIFYONLY WITH CHECKSUM succeeded.";

        // The file is on the database server; size and hash are recorded only when it is visible here.
        if (File.Exists(record.FilePath))
        {
            record.SizeBytes = new FileInfo(record.FilePath).Length;
            record.Sha256 = await HashAsync(record.FilePath, ct);
        }
    }

    private async Task ApplyRetentionAsync(CancellationToken ct)
    {
        var all = await _db.DatabaseBackups.ToListAsync(ct);
        var folder = Path.GetFullPath(_options.Folder);
        foreach (var old in DatabaseBackupRule.SelectForRetention(all, _options.KeepCount))
        {
            // Only remove files this service wrote inside the backup folder.
            var full = Path.GetFullPath(old.FilePath);
            if (full.StartsWith(folder, StringComparison.OrdinalIgnoreCase) && File.Exists(full))
            {
                File.Delete(full);
            }

            old.FileDeletedUtc = _clock.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
    }

    private static async Task<string> HashAsync(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, ct));
    }
}
