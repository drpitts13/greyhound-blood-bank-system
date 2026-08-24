using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace BloodBankLIS.Integration.Tests;

public class DevelopmentSqliteBootstrapTests
{
    [Fact]
    public async Task InitializeAsync_RecreatesWhenRequiredColumnMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gbb-bootstrap-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<BloodBankDbContext>()
                .UseSqlite($"Data Source={path}")
                .Options;
            await using var context = new BloodBankDbContext(
                options,
                new SystemClock(),
                new StaticCurrentUser("test"));

            await context.Database.EnsureCreatedAsync();
            Assert.True(await ColumnExistsAsync(context, "TestServiceBillings", "ChargeCodeId"));

            await context.Database.ExecuteSqlRawAsync(
                """
                PRAGMA foreign_keys = OFF;
                DROP TABLE "TestServiceBillings";
                CREATE TABLE "TestServiceBillings" (
                    "Id" INTEGER PRIMARY KEY,
                    "BillingCode" TEXT NOT NULL,
                    "Description" TEXT NULL,
                    "Price" TEXT NULL,
                    "Trigger" INTEGER NOT NULL,
                    "TestCode" TEXT NOT NULL,
                    "IsActive" INTEGER NOT NULL,
                    "CreatedUtc" TEXT NOT NULL,
                    "CreatedBy" TEXT NOT NULL,
                    "ModifiedUtc" TEXT NULL,
                    "ModifiedBy" TEXT NULL,
                    "RowVersion" BLOB NULL
                );
                PRAGMA foreign_keys = ON;
                """);
            Assert.False(await ColumnExistsAsync(context, "TestServiceBillings", "ChargeCodeId"));

            await DevelopmentSqliteBootstrap.InitializeAsync(context, NullLogger.Instance);

            Assert.True(await ColumnExistsAsync(context, "TestServiceBillings", "ChargeCodeId"));
            Assert.True(await ColumnExistsAsync(context, "ProductBillings", "ChargeCodeId"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static async Task<bool> ColumnExistsAsync(BloodBankDbContext context, string table, string column)
    {
        await context.Database.OpenConnectionAsync();
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = $"PRAGMA table_info(\"{table}\")";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }
}
