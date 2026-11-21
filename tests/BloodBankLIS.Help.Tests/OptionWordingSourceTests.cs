using System.Text.RegularExpressions;

namespace BloodBankLIS.Help.Tests;

/// <summary>Dropdown choices written into pages read as words (end-user testing loop 2 tick 8).</summary>
public partial class OptionWordingSourceTests
{
    // TEST-BB-1390: no page writes a choice as a run-together code name (the billing trigger lists offered UnitIssued,
    // UnitTransfused, TestVerified, ActionCompleted); a choice built from an enum goes through Ui.EnumText instead.
    [Fact]
    public void PageOptions_AreNotCodeNames()
    {
        var root = RepoRoot();
        var offenders = Directory.GetFiles(Path.Combine(root, "src", "BloodBankLIS.Web", "Components"), "*.razor", SearchOption.AllDirectories)
            .SelectMany(f => CodeNameOption().Matches(File.ReadAllText(f)).Select(m => $"{Path.GetFileName(f)}: {m.Groups[1].Value}"))
            .ToList();
        Assert.Empty(offenders);
    }

    // TEST-BB-1407 (end-user testing loop 2 tick 29): an enum field is not printed raw into markup (Crossmatch Settings
    // read "Complex Crossmatch (CXM) — ComplexCrossmatch", Transfusion Criteria "RedBloodCells"); it goes through Ui.EnumText.
    [Fact]
    public void PageMarkup_DoesNotPrintEnumFieldsRaw()
    {
        var root = RepoRoot();
        var offenders = Directory.GetFiles(Path.Combine(root, "src", "BloodBankLIS.Web", "Components"), "*.razor", SearchOption.AllDirectories)
            .SelectMany(f => RawEnumField().Matches(File.ReadAllText(f)).Select(m => $"{Path.GetFileName(f)}: {m.Value.Trim()}"))
            .ToList();
        Assert.Empty(offenders);
    }

    [GeneratedRegex(@"(?:^|[^\w(])@[A-Za-z_]+\.(?:ResultValueType|ModificationType|ComponentClass|EventType|IssueType)\s*(?:<|&| —| ·)", System.Text.RegularExpressions.RegexOptions.Multiline)]
    private static partial Regex RawEnumField();

    [GeneratedRegex(@">\s*([A-Z][a-z]+(?:[A-Z][a-z0-9]+)+)\s*</option>")]
    private static partial Regex CodeNameOption();

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
