using BloodBankLIS.Infrastructure.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BloodBankLIS.Infrastructure.Persistence;

/// <summary>
/// Enables EF Core CLI tooling (migrations) to construct the context without
/// running the application. Uses a placeholder connection string; the actual
/// connection is supplied at runtime from configuration.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<BloodBankDbContext>
{
    public BloodBankDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("BLOODBANK_CONNECTION")
            ?? SqliteToSqlServerMigrator.DefaultSqlServerConnectionString;

        var options = new DbContextOptionsBuilder<BloodBankDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new BloodBankDbContext(options, new SystemClock(), new StaticCurrentUser("migration"));
    }
}
