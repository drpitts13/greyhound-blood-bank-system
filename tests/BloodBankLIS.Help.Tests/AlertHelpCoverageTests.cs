using System.Reflection;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Help.Tests;

// TEST-BB-388: every alert source is described on the alerts help page, so a new alert cannot ship undocumented.
public class AlertHelpCoverageTests
{
    private static IReadOnlyList<string> Sources() =>
        typeof(AlertRule)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

    private static string AlertsPage()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "BloodBankLIS.slnx")))
        {
            dir = dir.Parent;
        }

        return File.ReadAllText(Path.Combine(dir!.FullName, "docs", "user-manual", "clinical", "alerts.md"));
    }

    [Fact]
    public void EverySource_HasAHelpName_AndIsDescribedOnTheAlertsPage()
    {
        var page = AlertsPage();
        var sources = Sources();
        Assert.NotEmpty(sources);

        foreach (var source in sources)
        {
            var name = AlertRule.HelpName(source);
            Assert.True(name is not null, $"Alert source '{source}' has no AlertRule.HelpName.");
            Assert.True(page.Contains($"**{name}**", StringComparison.Ordinal), $"alerts.md does not describe **{name}** ({source}).");
        }
    }

    [Fact]
    public void ThePageCount_MatchesTheSources()
    {
        var count = Sources().Count;
        var words = new[] { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve" };
        Assert.Contains($"Alerts come from {words[count]} places", AlertsPage());
    }
}
