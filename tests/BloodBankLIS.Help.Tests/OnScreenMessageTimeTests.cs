namespace BloodBankLIS.Help.Tests;

/// <summary>
/// End-user testing pass 128: messages shown on screen and not kept (refusals, warnings, worklist notices, the printed
/// downtime sheet) give times in local time, as the pages do (ClinicalTimeText, Cycle 605); text saved with a record
/// keeps UTC and says so. These rules' messages had gone back to "… HH:mm UTC".
/// </summary>
public class OnScreenMessageTimeTests
{
    private static readonly string[] OnScreenRules =
    [
        "CrossmatchSpecimenRule.cs", "DowntimeBackEntryRule.cs", "LateReactionReportRule.cs", "ReactionWorkupSpecimenRule.cs",
        "ShipOutRule.cs", "TransfusionDocumentationRule.cs", "VerificationDeadlineRule.cs", "BloodOrderPreparationRule.cs"
    ];

    // TEST-BB-1149: no on-screen rule message formats a time as UTC.
    [Fact]
    public void OnScreenRuleMessages_UseLocalTime()
    {
        var root = RepoRoot();
        var problems = OnScreenRules
            .Select(name => Path.Combine(root, "src", "BloodBankLIS.Domain", "Rules", name))
            .SelectMany(f => File.ReadAllLines(f).Select((line, i) => (File: f, Line: i + 1, Text: line)))
            .Where(l => !l.Text.TrimStart().StartsWith("///", StringComparison.Ordinal) && l.Text.Contains(":yyyy-MM-dd HH:mm} UTC", StringComparison.Ordinal))
            .Select(l => $"{Path.GetFileName(l.File)}:{l.Line}: {l.Text.Trim()}")
            .ToList();

        Assert.True(problems.Count == 0, "An on-screen message gives a time in UTC:" + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "BloodBankLIS.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not find the repository root.");
    }
}
