using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

var sqlite = GetArg(args, "--sqlite") ?? SqliteToSqlServerMigrator.ResolveDefaultSqliteConnectionString();
var sqlServer = GetArg(args, "--sqlserver") ?? SqliteToSqlServerMigrator.DefaultSqlServerConnectionString;

if (args.Contains("--help") || args.Contains("-h"))
{
    PrintUsage();
    return 0;
}

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddSimpleConsole(options =>
    {
        options.SingleLine = true;
        options.TimestampFormat = "HH:mm:ss ";
    });
    builder.SetMinimumLevel(LogLevel.Information);
});

var logger = loggerFactory.CreateLogger<SqliteToSqlServerMigrator>();
var migrator = new SqliteToSqlServerMigrator(logger);

try
{
    var sqlitePath = SqliteToSqlServerMigrator.ResolveSqliteFilePath(sqlite);
    logger.LogInformation("Source SQLite: {Path}", sqlitePath);
    logger.LogInformation("Target SQL Server: {Server}", MaskConnectionString(sqlServer));

    var result = await migrator.MigrateAsync(sqlite, sqlServer);

    Console.WriteLine();
    Console.WriteLine("Table row counts:");
    foreach (var (table, count) in result.TableCounts.OrderBy(kv => kv.Key, StringComparer.Ordinal))
    {
        if (count > 0)
        {
            Console.WriteLine($"  {table,-35} {count,6}");
        }
    }

    Console.WriteLine();
    Console.WriteLine($"Total rows copied: {result.TotalRowsCopied}");
    return 0;
}
catch (Exception ex)
{
    logger.LogError(ex, "Migration failed.");
    return 1;
}

static string? GetArg(string[] args, string name)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
        {
            return args[i + 1];
        }
    }

    return null;
}

static string MaskConnectionString(string connectionString) =>
    connectionString.Contains("Password=", StringComparison.OrdinalIgnoreCase)
        ? "(connection string hidden)"
        : connectionString;

static void PrintUsage()
{
    Console.WriteLine("""
        Blood Bank LIS — SQLite to SQL Server data migrator

        Usage:
          dotnet run --project src/BloodBankLIS.DbMigrator [--sqlite <conn>] [--sqlserver <conn>]

        Options:
          --sqlite     SQLite connection string (default: %LOCALAPPDATA%\BloodBankLIS\bloodbank.dev.db)
          --sqlserver  SQL Server connection string (default: localhost\SQLEXPRESS02 / BloodBankLIS)

        Stop the API before running so the SQLite file is not locked.
        """);
}
