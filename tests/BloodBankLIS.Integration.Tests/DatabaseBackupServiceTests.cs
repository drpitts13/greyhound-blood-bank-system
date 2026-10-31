using System.Security.Cryptography;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-302: SQLite backup, verification, manifest, retention, failure alert, and overdue alert.
public class DatabaseBackupServiceTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "bbl-backup-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        _factory.Dispose();
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private AlertService Alerts(BloodBankDbContext c) =>
        new(new EfRepository<Alert>(c), c, _factory.Clock, _factory.CurrentUser, new AuditWriter(c, _factory.Clock, _factory.CurrentUser));

    private DatabaseBackupService Service(BloodBankDbContext c, string? folder = null, int keep = 14) =>
        new(c, new DatabaseBackupOptions(folder ?? _folder, keep, 24, 48), _factory.Clock, _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser), Alerts(c));

    [Fact]
    public async Task Backup_WritesAVerifiedCopy_WithHashAndAuditManifest()
    {
        await using var c = _factory.Create();
        new AuditWriter(c, _factory.Clock, _factory.CurrentUser).Record(AuditEventType.Update, "Probe", 1, null, new { Value = 1 }, "probe");
        await c.SaveChangesAsync();
        var auditCount = await c.AuditEvents.LongCountAsync();
        var lastHash = await c.AuditEvents.OrderByDescending(a => a.Id).Select(a => a.RecordHash).FirstAsync();

        var result = await Service(c).RunAsync(DatabaseBackupRule.ManualTrigger);

        Assert.True(result.Succeeded, result.Error);
        Assert.True(result.Verified);
        Assert.Equal("integrity_check: ok", result.VerifyDetail);
        Assert.Equal("bloodbanklis-20260530-120000.db", result.FileName);
        var path = Path.Combine(_folder, result.FileName);
        Assert.True(File.Exists(path));
        Assert.Equal(new FileInfo(path).Length, result.SizeBytes);
        await using (var stream = File.OpenRead(path))
        {
            Assert.Equal(Convert.ToHexString(await SHA256.HashDataAsync(stream)), result.Sha256);
        }

        Assert.Equal(auditCount, result.AuditEventCount);
        Assert.Equal(lastHash, result.LastAuditHash);

        // The copy is a real database: the probe audit row is in it.
        await using (var copy = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False"))
        {
            await copy.OpenAsync();
            await using var command = copy.CreateCommand();
            command.CommandText = """SELECT COUNT(*) FROM "AuditEvents" WHERE "EntityType" = 'Probe'""";
            Assert.Equal(1L, (long)(await command.ExecuteScalarAsync())!);
        }

        Assert.True(await c.AuditEvents.AnyAsync(e => e.EntityType == nameof(DatabaseBackup) && e.EventType == AuditEventType.Export));
        var status = await Service(c).GetStatusAsync();
        Assert.Equal(_factory.Clock.UtcNow, status.LastGoodUtc);
        Assert.False(status.Overdue);
        Assert.False(await Service(c).IsScheduledBackupDueAsync());
    }

    [Fact]
    public async Task Retention_RemovesOlderFiles_AndKeepsTheLogRows()
    {
        await using var c = _factory.Create();
        var start = _factory.Clock.UtcNow;
        var names = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            _factory.Clock.UtcNow = start.AddMinutes(i);
            names.Add((await Service(c, keep: 2).RunAsync(DatabaseBackupRule.ScheduledTrigger)).FileName);
        }

        Assert.False(File.Exists(Path.Combine(_folder, names[0])));
        Assert.True(File.Exists(Path.Combine(_folder, names[1])));
        Assert.True(File.Exists(Path.Combine(_folder, names[2])));
        Assert.Equal(3, await c.DatabaseBackups.CountAsync());
        Assert.NotNull((await c.DatabaseBackups.SingleAsync(b => b.FilePath.EndsWith(names[0]))).FileDeletedUtc);
        _factory.Clock.UtcNow = start;
    }

    [Fact]
    public async Task Failure_IsLogged_AndRaisesACriticalAlert()
    {
        await using var c = _factory.Create();
        Directory.CreateDirectory(_folder);
        var blocker = Path.Combine(_folder, "not-a-folder");
        await File.WriteAllTextAsync(blocker, "x");

        var result = await Service(c, folder: blocker).RunAsync(DatabaseBackupRule.ManualTrigger);

        Assert.False(result.Succeeded);
        Assert.False(result.Verified);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
        var alert = await c.Alerts.SingleAsync(a => a.Source == AlertRule.BackupFailed);
        Assert.Equal(AlertSeverity.Critical, alert.Severity);
        Assert.True(await Service(c).IsScheduledBackupDueAsync());
    }

    [Fact]
    public async Task OverdueAlert_OpensWithNoBackup_AndResolvesAfterAGoodOne()
    {
        await using var c = _factory.Create();
        var service = Service(c);

        await service.SyncOverdueAlertAsync();
        var overdue = await c.Alerts.SingleAsync(a => a.Source == AlertRule.BackupOverdue);
        Assert.Null(overdue.ResolvedUtc);
        Assert.Contains("no verified database backup", overdue.Message, StringComparison.OrdinalIgnoreCase);

        await service.RunAsync(DatabaseBackupRule.ManualTrigger);
        await service.SyncOverdueAlertAsync();
        Assert.NotNull((await c.Alerts.SingleAsync(a => a.Id == overdue.Id)).ResolvedUtc);
    }
}
