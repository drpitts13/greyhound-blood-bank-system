using System.Data;
using System.Globalization;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Reporting;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Infrastructure.Reporting;

public sealed class AdoReportSqlExecutor : IReportSqlExecutor
{
    private readonly BloodBankDbContext _db;

    public AdoReportSqlExecutor(BloodBankDbContext db) => _db = db;

    public async Task<ReportGrid> QueryAsync(string sql, int maxRows, CancellationToken cancellationToken = default)
    {
        var guard = ReportSqlGuard.Evaluate(sql);
        if (guard.Severity == RuleSeverity.HardStop)
            throw new InvalidOperationException(guard.Message);

        var connection = _db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
            await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql.Trim().TrimEnd(';');
            command.CommandTimeout = ReportLimits.SqlTimeoutSeconds;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var columns = new List<string>(reader.FieldCount);
            for (var i = 0; i < reader.FieldCount; i++)
                columns.Add(UniqueName(columns, reader.GetName(i)));

            var rows = new List<IReadOnlyList<string?>>();
            var truncated = false;
            while (await reader.ReadAsync(cancellationToken))
            {
                if (rows.Count >= maxRows)
                {
                    truncated = true;
                    break;
                }

                var cells = new string?[reader.FieldCount];
                for (var i = 0; i < reader.FieldCount; i++)
                    cells[i] = Format(reader.GetValue(i));
                rows.Add(cells);
            }

            return new ReportGrid { Columns = columns, Rows = rows, Truncated = truncated };
        }
        finally
        {
            if (openedHere)
                await connection.CloseAsync();
        }
    }

    private static string UniqueName(List<string> existing, string name)
    {
        var candidate = string.IsNullOrWhiteSpace(name) ? "column" : name;
        if (!existing.Contains(candidate, StringComparer.OrdinalIgnoreCase))
            return candidate;

        var n = 2;
        while (existing.Contains(candidate + "_" + n, StringComparer.OrdinalIgnoreCase))
            n++;
        return candidate + "_" + n;
    }

    private static string? Format(object value)
    {
        if (value is null or DBNull)
            return null;
        return value switch
        {
            DateTime date => ReportValueFormat.Date(date),
            DateTimeOffset offset => ReportValueFormat.Date(offset.UtcDateTime),
            bool flag => ReportValueFormat.YesNo(flag),
            byte[] => null,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }
}
