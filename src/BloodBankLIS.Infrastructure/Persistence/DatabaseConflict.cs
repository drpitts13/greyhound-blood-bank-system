using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Infrastructure.Persistence;

/// <summary>
/// Classifies save failures caused by another user's concurrent change: an optimistic
/// concurrency (RowVersion) miss, or a unique index/key collision. Uses provider error
/// numbers rather than message text so it works on both SQL Server and SQLite.
/// </summary>
public static partial class DatabaseConflict
{
    private const int SqlServerDuplicateIndex = 2601;
    private const int SqlServerDuplicateKey = 2627;
    private const int SqliteConstraintUnique = 2067;
    private const int SqliteConstraintPrimaryKey = 1555;

    public static bool TryDescribe(Exception exception, out string message)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (Find<DbUpdateConcurrencyException>(exception) is not null)
        {
            message = "This record was changed by another user. Refresh and try again.";
            return true;
        }

        if (TryGetUniqueViolationTable(exception, out var table))
        {
            message = DescribeDuplicate(table);
            return true;
        }

        message = string.Empty;
        return false;
    }

    /// <summary>
    /// True when the exception chain holds a unique index or key violation. <paramref name="table"/>
    /// is the table name when the provider message names it, else empty.
    /// </summary>
    public static bool TryGetUniqueViolationTable(Exception exception, out string table)
    {
        if (Find<SqlException>(exception) is { } sql
            && sql.Number is SqlServerDuplicateIndex or SqlServerDuplicateKey)
        {
            var match = SqlServerObject().Match(sql.Message);
            table = match.Success ? match.Groups["table"].Value : string.Empty;
            return true;
        }

        if (Find<SqliteException>(exception) is { } sqlite
            && sqlite.SqliteExtendedErrorCode is SqliteConstraintUnique or SqliteConstraintPrimaryKey)
        {
            var match = SqliteColumn().Match(sqlite.Message);
            table = match.Success ? match.Groups["table"].Value : string.Empty;
            return true;
        }

        table = string.Empty;
        return false;
    }

    private static string DescribeDuplicate(string table) => table switch
    {
        "Allocations" => "Another user reserved this unit. Refresh and select a different unit.",
        "Issues" => "Another user already issued this unit. Refresh inventory.",
        "BloodProducts" => "That unit number is already in inventory. Another user may have just received it.",
        "Specimens" => "That accession number is already in use. Refresh and try again.",
        "Orders" => "That order number is already in use. Refresh and try again.",
        "Encounters" => "That visit number is already in use. Refresh and try again.",
        "Patients" => "That medical record number is already in use.",
        _ => "Another user just saved a record with the same identifying value. Refresh and try again."
    };

    private static T? Find<T>(Exception? exception) where T : Exception
    {
        for (var e = exception; e is not null; e = e.InnerException)
        {
            if (e is T match)
            {
                return match;
            }
        }

        return null;
    }

    // "Cannot insert duplicate key row in object 'dbo.Specimens' with unique index ..."
    [GeneratedRegex(@"object '(?:[^'.]+\.)?(?<table>[^'.]+)'")]
    private static partial Regex SqlServerObject();

    // "UNIQUE constraint failed: Specimens.AccessionNumber"
    [GeneratedRegex(@"constraint failed: (?<table>[A-Za-z0-9_]+)\.")]
    private static partial Regex SqliteColumn();
}
