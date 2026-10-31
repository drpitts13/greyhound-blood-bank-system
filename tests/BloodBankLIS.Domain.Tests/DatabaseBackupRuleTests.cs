using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-302: backup schedule, overdue, and retention rules.
public class DatabaseBackupRuleTests
{
    private static readonly DateTime Now = new(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);

    private static DatabaseBackup Backup(long id, double hoursAgo, bool good = true, bool deleted = false) => new()
    {
        Id = id,
        StartedUtc = Now.AddHours(-hoursAgo),
        CompletedUtc = Now.AddHours(-hoursAgo),
        Succeeded = good,
        Verified = good,
        FileDeletedUtc = deleted ? Now : null
    };

    [Fact]
    public void OnlyWrittenAndVerifiedBackupsCount()
    {
        Assert.True(DatabaseBackupRule.IsGood(Backup(1, 1)));
        Assert.False(DatabaseBackupRule.IsGood(new DatabaseBackup { Succeeded = true, Verified = false }));
        Assert.Equal(Now.AddHours(-5), DatabaseBackupRule.LastGoodUtc([Backup(1, 5), Backup(2, 1, good: false)]));
        Assert.Null(DatabaseBackupRule.LastGoodUtc([Backup(1, 1, good: false)]));
    }

    [Fact]
    public void DueAndOverdue_FollowTheLastGoodBackup()
    {
        var day = TimeSpan.FromHours(24);
        Assert.True(DatabaseBackupRule.IsDue([], Now, day));
        Assert.False(DatabaseBackupRule.IsDue([Backup(1, 23)], Now, day));
        Assert.True(DatabaseBackupRule.IsDue([Backup(1, 24)], Now, day));
        Assert.True(DatabaseBackupRule.IsDue([Backup(1, 2, good: false)], Now, day));

        Assert.True(DatabaseBackupRule.IsOverdue([Backup(1, 49)], Now, TimeSpan.FromHours(48)));
        Assert.False(DatabaseBackupRule.IsOverdue([Backup(1, 47)], Now, TimeSpan.FromHours(48)));
    }

    [Fact]
    public void Retention_KeepsTheNewestGoodFiles_AndIgnoresFailedOrRemoved()
    {
        var backups = new[]
        {
            Backup(1, 50), Backup(2, 40), Backup(3, 30, good: false), Backup(4, 20), Backup(5, 10),
            Backup(6, 60, deleted: true)
        };

        var remove = DatabaseBackupRule.SelectForRetention(backups, keep: 2);

        Assert.Equal([2L, 1L], remove.Select(b => b.Id));
        Assert.Empty(DatabaseBackupRule.SelectForRetention(backups, keep: 0));
    }

    [Fact]
    public void FileName_IsTimestampedByProvider()
    {
        Assert.Equal("bloodbanklis-20261031-120000.db", DatabaseBackupRule.FileName(Now, sqlServer: false));
        Assert.Equal("bloodbanklis-20261031-120000.bak", DatabaseBackupRule.FileName(Now, sqlServer: true));
    }
}
