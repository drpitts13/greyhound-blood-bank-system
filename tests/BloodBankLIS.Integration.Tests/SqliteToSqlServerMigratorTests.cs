using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public sealed class SqliteToSqlServerMigratorTests
{
    [Fact]
    public void TableCopyOrder_CoversAllMappedTables()
    {
        var options = new DbContextOptionsBuilder<BloodBankDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new BloodBankDbContext(
            options,
            new BloodBankLIS.Infrastructure.Common.SystemClock(),
            new BloodBankLIS.Infrastructure.Common.StaticCurrentUser("test"));

        var mappedTables = context.Model.GetEntityTypes()
            .Select(e => e.GetTableName())
            .Where(n => n is not null)
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);

        var orderedTables = SqliteToSqlServerMigrator.TableCopyOrder.ToHashSet(StringComparer.Ordinal);

        Assert.Empty(mappedTables.Except(orderedTables));
        Assert.Empty(orderedTables.Except(mappedTables));
    }

    [Fact]
    public void ResolveDefaultSqliteConnectionString_PointsToLocalAppData()
    {
        var connectionString = SqliteToSqlServerMigrator.ResolveDefaultSqliteConnectionString();
        var path = SqliteToSqlServerMigrator.ResolveSqliteFilePath(connectionString);

        Assert.Contains("BloodBankLIS", path, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("bloodbank.dev.db", path, StringComparison.OrdinalIgnoreCase);
    }
}
