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

    /// <summary>
    /// Nullable columns added after EnsureCreated. Applied with ALTER TABLE when missing
    /// so local demo data is preserved when possible. SQL is fixed (not user input).
    /// </summary>
    private static readonly (string Table, string Column, string AlterSql)[] AdditiveColumns =
    [
        ("Issues", "Comment", """ALTER TABLE "Issues" ADD COLUMN "Comment" TEXT NULL""")
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
        if (!created && !recreate)
        {
            await ApplyAdditiveColumnsAsync(context, logger, cancellationToken);
        }

        logger.LogInformation(
            recreate || created
                ? "SQLite development database created from EF model."
                : "SQLite development database is up to date.");
    }

    private static async Task ApplyAdditiveColumnsAsync(
        BloodBankDbContext context,
        ILogger logger,
        CancellationToken ct)
    {
        foreach (var (table, column, alterSql) in AdditiveColumns)
        {
            if (!await TableExistsAsync(context, table, ct))
            {
                continue;
            }

            if (await ColumnExistsAsync(context, table, column, ct))
            {
                continue;
            }

            logger.LogWarning(
                "SQLite development database is missing {Table}.{Column}. Adding column via ALTER TABLE.",
                table,
                column);

            await context.Database.ExecuteSqlRawAsync(alterSql, ct);
        }
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

    private static async Task<bool> ColumnExistsAsync(
        BloodBankDbContext context,
        string tableName,
        string columnName,
        CancellationToken ct)
    {
        await context.Database.OpenConnectionAsync(ct);
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            // PRAGMA table_info does not accept bound parameters for the table name.
            command.CommandText = $"PRAGMA table_info(\"{tableName}\")";

            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var name = reader.GetString(1);
                if (string.Equals(name, columnName, StringComparison.OrdinalIgnoreCase))
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
