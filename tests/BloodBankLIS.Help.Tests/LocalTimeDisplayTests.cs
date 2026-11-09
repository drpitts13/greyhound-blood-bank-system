using System.Text.RegularExpressions;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// Times are stored in UTC and shown in the workstation's local time. Ninety pages and panels formatted the stored
/// UTC value as if it were local (Cycle 604): at 07:46 local, a specimen expiring in three minutes read "expires
/// 12:49" on the chart. A date that is a date, not a moment (a report's range, a billing service date, a recorded
/// pregnancy date), keeps its stored day and is listed here.
/// </summary>
public partial class LocalTimeDisplayTests
{
    private static readonly string[] DateKeys =
    [
        "_report.FromUtc.ToString(\"yyyy-MM-dd\")",
        "_report.ToUtc.ToString(\"yyyy-MM-dd\")",
        "c.ServiceDateUtc.ToString(\"yyyy-MM-dd\")",
        "RecentPregnancyUtc?.ToString(\"yyyy-MM-dd\")",
        "_report.FromUtc:yyyyMMdd",
    ];

    // TEST-BB-715: no page formats a stored UTC time without converting it to local time.
    [Fact]
    public void Pages_ShowStoredTimesInLocalTime()
    {
        var root = RepoRoot();
        var problems = Directory.EnumerateFiles(Path.Combine(root, "src", "BloodBankLIS.Web", "Components"), "*.razor", SearchOption.AllDirectories)
            .SelectMany(f => File.ReadAllLines(f).Select((line, i) => (File: f, Line: i + 1, Text: line)))
            .Where(l => UtcFormatted().Matches(l.Text).Any(m => !IsDateKey(l.Text, m)))
            .Select(l => $"{Path.GetRelativePath(root, l.File)}:{l.Line}: {l.Text.Trim()}")
            .ToList();

        Assert.True(problems.Count == 0,
            "A stored UTC time is shown without .ToLocalTime():" + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    private static bool IsDateKey(string line, Match match) =>
        DateKeys.Any(k => line.IndexOf(k, StringComparison.Ordinal) is var at && at >= 0
            && match.Index >= at && match.Index < at + k.Length);

    // A *Utc member formatted directly: x.FooUtc.ToString("…"), x.FooUtc?.ToString("…"), x.FooUtc.Value.ToString("…"),
    // or interpolated {x.FooUtc:…}.
    [GeneratedRegex(@"\b[A-Za-z_][\w.]*Utc(\?|\.Value)?(\.ToString\(""|:(yyyy|HH|u\b))")]
    private static partial Regex UtcFormatted();

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "BloodBankLIS.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not find the repository root.");
    }
}
