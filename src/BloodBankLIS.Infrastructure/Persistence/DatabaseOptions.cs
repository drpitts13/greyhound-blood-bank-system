using Microsoft.Extensions.Configuration;

namespace BloodBankLIS.Infrastructure.Persistence;

/// <summary>
/// Database provider selection. Development defaults to SQLite when LocalDB is unavailable.
/// Production should use SqlServer.
/// </summary>
public static class DatabaseOptions
{
    public const string ProviderKey = "Database:Provider";
    public const string SqlServer = "SqlServer";
    public const string Sqlite = "Sqlite";

    public static string ResolveConnectionString(Microsoft.Extensions.Configuration.IConfiguration configuration, bool isDevelopment)
    {
        var provider = configuration.GetValue(ProviderKey, isDevelopment ? Sqlite : SqlServer);
        var configured = configuration.GetConnectionString("BloodBankLIS");

        if (string.Equals(provider, Sqlite, StringComparison.OrdinalIgnoreCase))
        {
            // Use an explicit SQLite connection string when one is configured.
            if (!string.IsNullOrWhiteSpace(configured) && LooksLikeSqliteConnectionString(configured))
            {
                return configured;
            }

            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BloodBankLIS");
            Directory.CreateDirectory(dir);
            return $"Data Source={Path.Combine(dir, "bloodbank.dev.db")}";
        }

        return configured
            ?? SqliteToSqlServerMigrator.DefaultSqlServerConnectionString;
    }

    public static string ResolveProvider(Microsoft.Extensions.Configuration.IConfiguration configuration, bool isDevelopment) =>
        configuration.GetValue(ProviderKey, isDevelopment ? Sqlite : SqlServer);

    private static bool LooksLikeSqliteConnectionString(string connectionString) =>
        connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)
        && !connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase)
        && !connectionString.Contains("localdb", StringComparison.OrdinalIgnoreCase)
        && !connectionString.Contains("MSSQLLocalDB", StringComparison.OrdinalIgnoreCase);
}
