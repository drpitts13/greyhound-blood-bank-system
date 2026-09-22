using System.Text;
using System.Text.RegularExpressions;
using BloodBankLIS.Domain.Reporting;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Pure check for the SQL report designer. Allows one SELECT (or WITH … SELECT)
/// whose FROM / JOIN targets are reporting views or CTEs defined in that statement.
/// Comments, writes, DDL, and clinical tables are rejected.
/// </summary>
public static partial class ReportSqlGuard
{
    public const string RuleCode = "RPT-SQL";

    private static readonly string[] ForbiddenKeywords =
    [
        "INSERT", "UPDATE", "DELETE", "MERGE", "DROP", "ALTER", "CREATE",
        "EXEC", "EXECUTE", "GRANT", "REVOKE", "TRUNCATE", "ATTACH", "DETACH",
        "PRAGMA", "VACUUM", "REINDEX", "INTO", "BACKUP", "RESTORE", "SHUTDOWN",
        "KILL", "DBCC", "OPENROWSET", "OPENDATASOURCE", "OPENQUERY", "BULK", "COPY"
    ];

    public static RuleResult Evaluate(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return RuleResult.HardStop(RuleCode, "SQL is required.");

        if (sql.Length > 8000)
            return RuleResult.HardStop(RuleCode, "SQL is limited to 8000 characters.");

        if (sql.Contains("--", StringComparison.Ordinal)
            || sql.Contains("/*", StringComparison.Ordinal)
            || sql.Contains("*/", StringComparison.Ordinal))
        {
            return RuleResult.HardStop(RuleCode, "SQL comments are not allowed.");
        }

        var stripped = StripStringLiterals(sql.Trim());
        var semicolonCount = stripped.Count(c => c == ';');
        if (semicolonCount > 1 || (semicolonCount == 1 && !stripped.TrimEnd().EndsWith(';')))
            return RuleResult.HardStop(RuleCode, "Only a single SQL statement is allowed.");

        var body = stripped.Trim().TrimEnd(';').Trim();
        var first = FirstWord(body);
        if (first is not ("SELECT" or "WITH"))
            return RuleResult.HardStop(RuleCode, "Only a SELECT statement is allowed.");

        foreach (var keyword in ForbiddenKeywords)
        {
            if (KeywordPattern(keyword).IsMatch(body))
                return RuleResult.HardStop(RuleCode, $"The statement contains forbidden keyword {keyword}.");
        }

        if (RoutineCallPattern().IsMatch(body))
            return RuleResult.HardStop(RuleCode, "Stored-procedure calls are not allowed.");

        var ctes = CteNames(body);
        var targets = FromJoinTargets(body);
        if (targets.Count == 0)
            return RuleResult.HardStop(RuleCode, "The statement must read from a reporting view.");

        foreach (var target in targets)
        {
            if (ctes.Contains(target))
                continue;

            if (!ReportViewCatalog.All.Contains(target))
                return RuleResult.HardStop(RuleCode, $"'{target}' is not a reporting view.");
        }

        return RuleResult.Pass(RuleCode);
    }

    private static HashSet<string> CteNames(string sql)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in CtePattern().Matches(sql))
            names.Add(match.Groups[1].Value);
        return names;
    }

    private static List<string> FromJoinTargets(string sql)
    {
        var targets = new List<string>();
        foreach (Match match in FromJoinPattern().Matches(sql))
            targets.Add(NormalizeIdentifier(match.Groups[1].Value));
        return targets;
    }

    private static string NormalizeIdentifier(string raw)
    {
        var name = raw.Trim().Trim('[', ']', '"', '`');
        var dot = name.LastIndexOf('.');
        if (dot >= 0 && dot < name.Length - 1)
            name = name[(dot + 1)..].Trim('[', ']', '"', '`');
        return name;
    }

    private static string FirstWord(string sql)
    {
        var match = FirstWordPattern().Match(sql);
        return match.Success ? match.Groups[1].Value.ToUpperInvariant() : string.Empty;
    }

    private static string StripStringLiterals(string sql)
    {
        var builder = new StringBuilder(sql.Length);
        for (var i = 0; i < sql.Length; i++)
        {
            if (sql[i] != '\'')
            {
                builder.Append(sql[i]);
                continue;
            }

            builder.Append("''");
            i++;
            while (i < sql.Length)
            {
                if (sql[i] == '\'' && i + 1 < sql.Length && sql[i + 1] == '\'')
                {
                    i += 2;
                    continue;
                }

                if (sql[i] == '\'')
                    break;

                i++;
            }
        }

        return builder.ToString();
    }

    private static Regex KeywordPattern(string keyword) =>
        new($@"\b{keyword}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    [GeneratedRegex(@"\b(?:XP_|SP_)[A-Za-z0-9_]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RoutineCallPattern();

    [GeneratedRegex(@"(?:\bWITH\s+(?:RECURSIVE\s+)?|,)\s*([A-Za-z_][A-Za-z0-9_]*)\s+AS\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CtePattern();

    [GeneratedRegex(@"\b(?:FROM|JOIN)\s+([A-Za-z_\[""`][A-Za-z0-9_\.\[\]""`]*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex FromJoinPattern();

    [GeneratedRegex(@"^\s*([A-Za-z]+)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex FirstWordPattern();
}
