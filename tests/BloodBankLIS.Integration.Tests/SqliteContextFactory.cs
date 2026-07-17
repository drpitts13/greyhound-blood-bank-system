using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

/// <summary>
/// Builds a <see cref="BloodBankDbContext"/> backed by an in-memory SQLite database.
/// The schema is created from the model via EnsureCreated. (CI validates the
/// SQL Server migration separately; SQLite lets these tests run anywhere.)
/// </summary>
public sealed class SqliteContextFactory : IDisposable
{
    private readonly SqliteConnection _connection;

    public FixedClock Clock { get; } = new(new DateTime(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc));

    public TestCurrentUser CurrentUser { get; } = new("tech-test", "WORKSTATION-1");

    public SqliteContextFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var context = Create();
        context.Database.EnsureCreated();
        TestCatalogSeeder.EnsureSpecimenTypesAsync(context, Clock.UtcNow).GetAwaiter().GetResult();
    }

    public BloodBankDbContext Create()
    {
        var options = new DbContextOptionsBuilder<BloodBankDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new BloodBankDbContext(options, Clock, CurrentUser);
    }

    public void Dispose() => _connection.Dispose();
}

public sealed class FixedClock : IClock
{
    public FixedClock(DateTime utcNow) => UtcNow = utcNow;

    public DateTime UtcNow { get; set; }
}

public sealed class TestCurrentUser : ICurrentUser
{
    public TestCurrentUser(string userName, string? workstation)
    {
        UserName = userName;
        Workstation = workstation;
    }

    public string UserName { get; }

    public string? Workstation { get; }
}
