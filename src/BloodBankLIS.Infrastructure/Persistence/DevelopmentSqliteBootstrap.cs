using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BloodBankLIS.Infrastructure.Persistence;

/// <summary>
/// Keeps the local SQLite development database aligned with the EF model.
/// Older dev databases were created with <see cref="RelationalDatabaseFacadeExtensions.EnsureCreatedAsync"/>,
/// which does not apply schema updates when the model changes.
/// </summary>
public static class DevelopmentSqliteBootstrap
{
    /// <summary>
    /// Tables introduced after the original EnsureCreated-only workflow. If any are missing,
    /// the file is recreated from the current model.
    /// </summary>
    private static readonly string[] RequiredTables =
    [
        "OrderingLocations",
        "OrderingProviders",
        "Encounters",
        "OrderSpecimens"
    ];

    public static async Task InitializeAsync(
        BloodBankDbContext context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var recreate = await NeedsRecreateAsync(context, cancellationToken);
        if (recreate)
        {
            logger.LogWarning(
                "SQLite development database schema is out of date. Recreating from the current EF model. " +
                "Demo data will be re-seeded on startup.");
            await context.Database.EnsureDeletedAsync(cancellationToken);
        }

        var created = await context.Database.EnsureCreatedAsync(cancellationToken);
        logger.LogInformation(
            recreate || created
                ? "SQLite development database created from EF model."
                : "SQLite development database is up to date.");
    }

    private static async Task<bool> NeedsRecreateAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (!await context.Database.CanConnectAsync(ct))
        {
            return false;
        }

        if (!await TableExistsAsync(context, "Patients", ct))
        {
            return false;
        }

        foreach (var table in RequiredTables)
        {
            if (!await TableExistsAsync(context, table, ct))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<bool> TableExistsAsync(
        BloodBankDbContext context,
        string tableName,
        CancellationToken ct)
    {
        await context.Database.OpenConnectionAsync(ct);
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = """
                SELECT COUNT(*) FROM sqlite_master
                WHERE type = 'table' AND name = $name
                """;
            var param = command.CreateParameter();
            param.ParameterName = "$name";
            param.Value = tableName;
            command.Parameters.Add(param);

            var result = await command.ExecuteScalarAsync(ct);
            return Convert.ToInt64(result) > 0;
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }
}
