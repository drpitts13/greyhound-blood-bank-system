using BloodBankLIS.Infrastructure.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BloodBankLIS.Infrastructure.Persistence;

/// <summary>
/// One-time copy of Blood Bank LIS data from a SQLite development database into SQL Server.
/// Uses ADO.NET to avoid polluting the audit trail via <see cref="BloodBankDbContext.SaveChangesAsync"/>.
/// </summary>
public sealed class SqliteToSqlServerMigrator
{
    public const string DefaultSqlServerConnectionString =
        "Server=localhost\\SQLEXPRESS02;Database=BloodBankLIS;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

    /// <summary>FK-safe table order (parents before children).</summary>
    public static readonly IReadOnlyList<string> TableCopyOrder =
    [
        "Permissions",
        "Roles",
        "Users",
        "RolePermissions",
        "UserRoles",
        "ProductTypes",
        "InventoryLocations",
        "OrderingLocations",
        "OrderingProviders",
        "ExceptionDefinitions",
        "ChargeCodes",
        "BloodAttributeDefinitions",
        "SpecimenTypeDefinitions",
        "TestDefinitions",
        "SubtestDefinitions",
        "TestGroupers",
        "ProductAttributes",
        "InterfaceEndpoints",
        "Patients",
        "Encounters",
        "ProductAttributeAssignments",
        "ChargeRules",
        "Specimens",
        "Orders",
        "OrderLines",
        "OrderSpecimens",
        "BloodProducts",
        "TestResults",
        "PatientBloodTypeHistory",
        "AntibodyHistory",
        "AntigenProfiles",
        "UnitBloodAttributes",
        "InventoryStatusHistory",
        "Crossmatches",
        "Allocations",
        "Overrides",
        "Issues",
        "Returns",
        "TransfusionEvents",
        "HL7Messages",
        "InterfaceErrorQueue",
        "PrintJobs",
        "BillingEvents",
        "ElectronicSignatures",
        "AuditEvents",
        "ConfigurationChangeHistory"
    ];

    private static readonly HashSet<string> ExcludedColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "RowVersion"
    };

    private readonly ILogger _logger;

    public SqliteToSqlServerMigrator(ILogger<SqliteToSqlServerMigrator> logger) =>
        _logger = logger;

    public static string ResolveDefaultSqliteConnectionString()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BloodBankLIS");
        return $"Data Source={Path.Combine(dir, "bloodbank.dev.db")}";
    }

    public static string ResolveSqliteFilePath(string sqliteConnectionString)
    {
        var builder = new SqliteConnectionStringBuilder(sqliteConnectionString);
        var dataSource = builder.DataSource;
        if (string.IsNullOrWhiteSpace(dataSource))
        {
            throw new InvalidOperationException("SQLite connection string is missing Data Source.");
        }

        return Path.GetFullPath(dataSource);
    }

    public async Task<MigrateResult> MigrateAsync(
        string sqliteConnectionString,
        string sqlServerConnectionString,
        CancellationToken cancellationToken = default)
    {
        var sqlitePath = ResolveSqliteFilePath(sqliteConnectionString);
        if (!File.Exists(sqlitePath))
        {
            throw new FileNotFoundException($"SQLite database not found: {sqlitePath}");
        }

        await EnsureSqlServerDatabaseExistsAsync(sqlServerConnectionString, cancellationToken);
        await VerifySqlServerReachableAsync(sqlServerConnectionString, cancellationToken);
        await ApplySchemaAsync(sqlServerConnectionString, cancellationToken);
        await VerifySqlServerEmptyAsync(sqlServerConnectionString, cancellationToken);

        var tableCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        await using var sqlite = new SqliteConnection(sqliteConnectionString);
        await sqlite.OpenAsync(cancellationToken);

        await using var sqlServer = new SqlConnection(sqlServerConnectionString);
        await sqlServer.OpenAsync(cancellationToken);

        foreach (var table in TableCopyOrder)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!await SqliteTableExistsAsync(sqlite, table, cancellationToken))
            {
                _logger.LogWarning("SQLite table {Table} not found; skipping.", table);
                tableCounts[table] = 0;
                continue;
            }

            if (!await SqlServerTableExistsAsync(sqlServer, table, cancellationToken))
            {
                _logger.LogWarning("SQL Server table {Table} not found after migration; skipping.", table);
                tableCounts[table] = 0;
                continue;
            }

            var copied = await CopyTableAsync(sqlite, sqlServer, table, cancellationToken);
            tableCounts[table] = copied;

            if (copied > 0)
            {
                _logger.LogInformation("Copied {Count} row(s) into {Table}.", copied, table);
            }
            else
            {
                _logger.LogInformation("Table {Table} is empty in SQLite; nothing to copy.", table);
            }
        }

        var total = tableCounts.Values.Sum();
        _logger.LogInformation("Migration complete. {Total} total row(s) copied across {Tables} tables.", total, tableCounts.Count);

        return new MigrateResult(tableCounts, total);
    }

    private async Task EnsureSqlServerDatabaseExistsAsync(string connectionString, CancellationToken ct)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = builder.InitialCatalog;
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("SQL Server connection string must specify Initial Catalog (Database).");
        }

        builder.InitialCatalog = "master";
        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @sql nvarchar(max) = N'CREATE DATABASE [' + REPLACE(@dbName, ']', ']]') + N']';
            IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = @dbName)
                EXEC sp_executesql @sql;
            """;
        command.Parameters.AddWithValue("@dbName", databaseName);
        await command.ExecuteNonQueryAsync(ct);

        _logger.LogInformation("SQL Server database '{Database}' is ready.", databaseName);
    }

    private async Task VerifySqlServerReachableAsync(string connectionString, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        _logger.LogInformation("SQL Server connection verified.");
    }

    private async Task VerifySqlServerEmptyAsync(string connectionString, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);

        foreach (var table in TableCopyOrder)
        {
            if (!await SqlServerTableExistsAsync(connection, table, ct))
            {
                continue;
            }

            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM [{table}]";
            var count = Convert.ToInt32(await command.ExecuteScalarAsync(ct));
            if (count > 0)
            {
                throw new InvalidOperationException(
                    $"Target table '{table}' already contains {count} row(s). " +
                    "Aborting to prevent overwriting existing SQL Server data.");
            }
        }

        _logger.LogInformation("Target SQL Server database is empty.");
    }

    private async Task ApplySchemaAsync(string connectionString, CancellationToken ct)
    {
        var options = new DbContextOptionsBuilder<BloodBankDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await using var context = new BloodBankDbContext(options, new SystemClock(), new StaticCurrentUser("migration"));
        await context.Database.MigrateAsync(ct);
        _logger.LogInformation("EF Core migrations applied to SQL Server.");
    }

    private static async Task<bool> SqliteTableExistsAsync(SqliteConnection connection, string table, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*) FROM sqlite_master
            WHERE type = 'table' AND name = $name
            """;
        command.Parameters.AddWithValue("$name", table);
        return Convert.ToInt64(await command.ExecuteScalarAsync(ct)) > 0;
    }

    private static async Task<bool> SqlServerTableExistsAsync(SqlConnection connection, string table, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @table
            """;
        command.Parameters.AddWithValue("@table", table);
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct)) > 0;
    }

    private async Task<int> CopyTableAsync(
        SqliteConnection sqlite,
        SqlConnection sqlServer,
        string table,
        CancellationToken ct)
    {
        var sqliteColumns = await GetSqliteColumnsAsync(sqlite, table, ct);
        var sqlServerColumns = await GetSqlServerColumnsAsync(sqlServer, table, ct);

        var columns = sqliteColumns
            .Where(c => sqlServerColumns.ContainsKey(c))
            .Where(c => !ExcludedColumns.Contains(c))
            .ToList();

        if (columns.Count == 0)
        {
            _logger.LogWarning("No common columns for table {Table}; skipping.", table);
            return 0;
        }

        var quotedColumns = columns.Select(c => $"[{c}]").ToList();
        var selectSql = $"SELECT {string.Join(", ", quotedColumns)} FROM [{table}]";

        var rows = new List<object?[]>();
        await using (var select = sqlite.CreateCommand())
        {
            select.CommandText = selectSql;
            await using var reader = await select.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var values = new object?[columns.Count];
                reader.GetValues(values);
                rows.Add(values);
            }
        }

        if (rows.Count == 0)
        {
            return 0;
        }

        var hasIdentityId = columns.Any(c => string.Equals(c, "Id", StringComparison.OrdinalIgnoreCase))
            && await SqlServerColumnIsIdentityAsync(sqlServer, table, "Id", ct);

        await using var transaction = (SqlTransaction)await sqlServer.BeginTransactionAsync(ct);
        try
        {
            if (hasIdentityId)
            {
                await ExecuteNonQueryAsync(sqlServer, transaction, $"SET IDENTITY_INSERT [{table}] ON", ct);
            }

            var parameterNames = columns.Select((_, i) => $"@p{i}").ToList();
            var insertSql =
                $"INSERT INTO [{table}] ({string.Join(", ", quotedColumns)}) VALUES ({string.Join(", ", parameterNames)})";

            foreach (var row in rows)
            {
                await using var insert = sqlServer.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = insertSql;
                for (var i = 0; i < columns.Count; i++)
                {
                    var value = NormalizeValue(row[i], sqlServerColumns[columns[i]]);
                    insert.Parameters.AddWithValue(parameterNames[i], value ?? DBNull.Value);
                }

                await insert.ExecuteNonQueryAsync(ct);
            }

            if (hasIdentityId)
            {
                await ExecuteNonQueryAsync(sqlServer, transaction, $"SET IDENTITY_INSERT [{table}] OFF", ct);
                await ExecuteNonQueryAsync(sqlServer, transaction, $"DBCC CHECKIDENT ('[{table}]', RESEED)", ct);
            }

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }

        return rows.Count;
    }

    private static object? NormalizeValue(object? value, string sqlServerDataType)
    {
        if (value is DBNull or null)
        {
            return DBNull.Value;
        }

        if (value is string s && sqlServerDataType.Equals("date", StringComparison.OrdinalIgnoreCase))
        {
            if (DateOnly.TryParse(s, out var dateOnly))
            {
                return dateOnly;
            }
        }

        if (value is long l && sqlServerDataType.Equals("bit", StringComparison.OrdinalIgnoreCase))
        {
            return l != 0;
        }

        if (value is int i && sqlServerDataType.Equals("bit", StringComparison.OrdinalIgnoreCase))
        {
            return i != 0;
        }

        return value;
    }

    private static async Task ExecuteNonQueryAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string sql,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<List<string>> GetSqliteColumnsAsync(SqliteConnection connection, string table, CancellationToken ct)
    {
        var columns = new List<string>();
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info([{table}])";
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
    }

    private static async Task<Dictionary<string, string>> GetSqlServerColumnsAsync(
        SqlConnection connection,
        string table,
        CancellationToken ct)
    {
        var columns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COLUMN_NAME, DATA_TYPE
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @table
            ORDER BY ORDINAL_POSITION
            """;
        command.Parameters.AddWithValue("@table", table);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            columns[reader.GetString(0)] = reader.GetString(1);
        }

        return columns;
    }

    private static async Task<bool> SqlServerColumnIsIdentityAsync(
        SqlConnection connection,
        string table,
        string column,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COLUMNPROPERTY(OBJECT_ID(@fullName), @column, 'IsIdentity')
            """;
        command.Parameters.AddWithValue("@fullName", $"dbo.{table}");
        command.Parameters.AddWithValue("@column", column);
        var result = await command.ExecuteScalarAsync(ct);
        return result is int isIdentity && isIdentity == 1;
    }
}

public sealed record MigrateResult(IReadOnlyDictionary<string, int> TableCounts, int TotalRowsCopied);
