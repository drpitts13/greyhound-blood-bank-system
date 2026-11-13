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

    // End-user testing pass 57: specimen collection, RhIG, reagent and delivery forms took their times in UTC, so a time
    // typed from the wall clock was five hours off. No form field is labelled UTC, and no page sends a typed local
    // value as if it were UTC.
    [Fact]
    public void Forms_TakeTimesInLocalTime()
    {
        var root = RepoRoot();
        var problems = Directory.EnumerateFiles(Path.Combine(root, "src", "BloodBankLIS.Web", "Components"), "*.razor", SearchOption.AllDirectories)
            .SelectMany(f => File.ReadAllLines(f).Select((line, i) => (File: f, Line: i + 1, Text: line)))
            .Where(l => l.Text.Contains("(UTC)</label>", StringComparison.Ordinal) || LocalSentAsUtc().IsMatch(l.Text))
            .Select(l => $"{Path.GetRelativePath(root, l.File)}:{l.Line}: {l.Text.Trim()}")
            .ToList();

        Assert.True(problems.Count == 0, "A form takes or sends a time as UTC:" + Environment.NewLine + string.Join(Environment.NewLine, problems));

        var typed = new DateTime(2026, 11, 13, 8, 30, 0, DateTimeKind.Unspecified);
        var utc = BloodBankLIS.Web.Services.LocalEntry.ToUtc(typed);
        Assert.Equal(DateTimeKind.Utc, utc.Kind);
        Assert.Equal(DateTime.SpecifyKind(typed, DateTimeKind.Local).ToUniversalTime(), utc);
        Assert.Equal(typed, BloodBankLIS.Web.Services.LocalEntry.FromUtc(utc));
    }

    // A typed value stamped as UTC without conversion (pass 58: the new visit's admit time); converting a stored UTC
    // value for display (followed by .ToLocalTime()) is fine.
    [GeneratedRegex(@"SpecifyKind\([^;]*?,\s*DateTimeKind\.Utc\)(?!\.ToLocalTime)")]
    private static partial Regex LocalSentAsUtc();

    // End-user testing pass 68: text built for a screen when it is asked for (search results, timelines, the supervisor
    // queue) printed UTC. Those services show local time; stored or sent text (alerts, reasons, HL7) keeps UTC.
    [Fact]
    public void DisplayServices_ShowLocalTime()
    {
        var root = RepoRoot();
        var files = new[]
        {
            "Search/UniversalLookupService.cs", "PatientWorkspace/PatientTimelineService.cs", "PatientWorkspace/OrderTimelineService.cs",
            "PatientWorkspace/SpecimenTimelineService.cs", "Compliance/SupervisorQueueService.cs"
        };
        var problems = files
            .Select(f => Path.Combine(root, "src", "BloodBankLIS.Application", f))
            .SelectMany(f => File.ReadAllLines(f).Select((line, i) => (File: f, Line: i + 1, Text: line)))
            .Where(l => l.Text.Contains("HH:mm} UTC", StringComparison.Ordinal) || l.Text.Contains("ClinicalTimeText.Utc(", StringComparison.Ordinal))
            .Select(l => $"{Path.GetRelativePath(root, l.File)}:{l.Line}: {l.Text.Trim()}")
            .ToList();

        Assert.True(problems.Count == 0, "A display service writes a UTC time:" + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    // TEST-BB-718: server messages never write a stored time in the "u" format ("…12:49:00Z") or as an unlabelled
    // date-time: on-screen text uses ClinicalTimeText.Local, saved text says UTC (Cycle 605).
    [Fact]
    public void ServerMessages_SayLocalOrUtc()
    {
        var root = RepoRoot();
        var problems = new[] { "BloodBankLIS.Domain", "BloodBankLIS.Application" }
            .SelectMany(project => Directory.EnumerateFiles(Path.Combine(root, "src", project), "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(f => File.ReadAllLines(f).Select((line, i) => (File: f, Line: i + 1, Text: line)))
            .Where(l => UFormat().IsMatch(l.Text) || UnlabelledTime().IsMatch(l.Text))
            .Select(l => $"{Path.GetRelativePath(root, l.File)}:{l.Line}: {l.Text.Trim()}")
            .ToList();

        Assert.True(problems.Count == 0,
            "A message writes a stored time without saying local or UTC:" + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    [GeneratedRegex(@"\{[^{}]*(Utc|utc|Now|now)[^{}:]*:u\}")]
    private static partial Regex UFormat();

    // An interpolated *Utc time as "yyyy-MM-dd HH:mm" not followed by " UTC" (and not converted with ToLocalTime first).
    [GeneratedRegex(@"\{(?![^{}]*ToLocalTime)[^{}]*Utc[^{}:]*:yyyy-MM-dd HH:mm(:ss)?\}(?! UTC)")]
    private static partial Regex UnlabelledTime();

    // Time-only renderings that are safe: lists of what was done in this sitting (a run's issued, received, returned,
    // documented, stored, and discarded units and tubes), and the timeline, which heads each day with its date.
    private static readonly string[] SameSittingTimes =
    [
        "done.IssuedLocal.ToString(\"HH:mm\")",
        "done.ReceivedLocal.ToString(\"HH:mm\")",
        "done.ReturnedLocal.ToString(\"HH:mm\")",
        "done.AtLocal.ToString(\"HH:mm\")",
        "d.DiscardedUtc.ToLocalTime().ToString(\"HH:mm:ss\")",
        "s.StoredUtc.ToLocalTime().ToString(\"HH:mm:ss\")",
        "e.AtUtc.ToLocalTime().ToString(\"HH:mm\")",
    ];

    // TEST-BB-739: no page shows a time without its day (Cycle 618): "Needed 07:30" for tomorrow, "Transfusing since
    // 22:10" from yesterday, or a cooler checked days ago read as today. Use ClockText.Short; a list of this sitting's
    // work, or a day-headed timeline, is named above.
    [Fact]
    public void Pages_DoNotShowATimeWithoutItsDay()
    {
        var root = RepoRoot();
        var problems = Directory.EnumerateFiles(Path.Combine(root, "src", "BloodBankLIS.Web", "Components"), "*.razor", SearchOption.AllDirectories)
            .SelectMany(f => File.ReadAllLines(f).Select((line, i) => (File: f, Line: i + 1, Text: line)))
            .Where(l => TimeOnly().IsMatch(l.Text) && !SameSittingTimes.Any(k => l.Text.Contains(k, StringComparison.Ordinal)))
            .Select(l => $"{Path.GetRelativePath(root, l.File)}:{l.Line}: {l.Text.Trim()}")
            .ToList();

        Assert.True(problems.Count == 0,
            "A time is shown without its day (use ClockText.Short):" + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    [GeneratedRegex(@"ToString\(""HH:mm(:ss)?""\)|:HH:mm(:ss)?\}")]
    private static partial Regex TimeOnly();

    // TEST-BB-766, TEST-BB-812: a stored UTC time matched into a variable (x.FooUtc is DateTime t) is not formatted without
    // conversion either (Cycle 633): six unit-expiry displays (Ready to issue, Retrospective, In transit, Issued units,
    // the chart's Products and Product History) and two more showed the stored UTC as local, out of reach of the guard
    // above because the variable's name did not end in Utc. A value the text labels "UTC" is fine.
    [Fact]
    public void Pages_ConvertUtcMatchedIntoAVariable()
    {
        var root = RepoRoot();
        var problems = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "src", "BloodBankLIS.Web", "Components"), "*.razor", SearchOption.AllDirectories))
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                foreach (Match m in UtcMatchedVariable().Matches(lines[i]))
                {
                    var variable = m.Groups[1].Value;
                    // The variable is often used inside the @if block that matched it, on the next lines (Cycle 655:
                    // the Alerts page's acknowledged and cleared times), so the rest of the line and a few after count.
                    var rest = lines[i][(m.Index + m.Length)..] + " " + string.Join(" ", lines.Skip(i + 1).Take(BlockLines));
                    var formatted = rest.Contains(variable + ".ToString(", StringComparison.Ordinal)
                        || rest.Contains("{" + variable + ":", StringComparison.Ordinal);
                    var converted = rest.Contains(variable + ".ToLocalTime()", StringComparison.Ordinal)
                        || Regex.IsMatch(rest, @"ClockText\.\w+\(" + Regex.Escape(variable) + @"\b");
                    var labelledUtc = Regex.IsMatch(rest, @"\{" + Regex.Escape(variable) + @":[^}]*\} UTC")
                        || Regex.IsMatch(rest, Regex.Escape(variable) + @"\.ToString\(""[^""]*""\) UTC");
                    if (formatted && !converted && !labelledUtc)
                    {
                        problems.Add($"{Path.GetRelativePath(root, file)}:{i + 1}: {lines[i].Trim()}");
                    }
                }
            }
        }

        Assert.True(problems.Count == 0,
            "A stored UTC time matched into a variable is shown without .ToLocalTime():" + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    private const int BlockLines = 4;

    [GeneratedRegex(@"Utc(?:\?\.Value|\.Value)? is (?:DateTime|\{ \}) (\w+)")]
    private static partial Regex UtcMatchedVariable();

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
