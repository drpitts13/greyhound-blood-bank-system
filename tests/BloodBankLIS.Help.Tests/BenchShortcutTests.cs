using System.Text.RegularExpressions;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// TEST-BB-470: J and K open the next or previous test on the test worklist (wwwroot/ui.js). The shortcut clicks only
/// the row's own open/hide button and never a save or verify button, and it does nothing while typing in a field.
/// </summary>
public class BenchShortcutTests
{
    private static readonly string Root = FindRoot();
    private static readonly string Script = File.ReadAllText(Path.Combine(Root, "src", "BloodBankLIS.Web", "wwwroot", "ui.js"));

    private static string BenchSection()
    {
        var start = Script.IndexOf("Test worklist: J opens the next test", StringComparison.Ordinal);
        Assert.True(start >= 0, "The bench shortcut section is missing from ui.js.");
        // The section is the last block in the file.
        return Script[start..];
    }

    [Fact]
    public void Shortcut_clicks_only_row_toggles_and_ignores_typing()
    {
        var section = BenchSection();
        Assert.Contains("button[data-bench-toggle]", section);
        Assert.Contains("typing(e.target)", section);
        Assert.Single(Regex.Matches(section, @"\.click\(\)"));
        Assert.DoesNotContain("submit", section, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Worklist_rows_carry_the_toggle_and_entry_markers()
    {
        var page = File.ReadAllText(Path.Combine(Root, "src", "BloodBankLIS.Web", "Components", "Pages", "TestWorklist.razor"));
        Assert.Single(Regex.Matches(page, @"\bdata-bench-toggle\b"));
        Assert.Contains("data-bench-open=", page);
        Assert.Contains("<tr data-bench-entry>", page);
        Assert.Contains("<div data-bench-result><TestResultEntryPanel", page);
    }

    [Fact]
    public void Shortcut_is_listed_in_the_help_panel() =>
        Assert.Contains("['J / K (test worklist)'", Script);

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "BloodBankLIS.slnx")) && !Directory.Exists(Path.Combine(dir.FullName, "src", "BloodBankLIS.Web")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
