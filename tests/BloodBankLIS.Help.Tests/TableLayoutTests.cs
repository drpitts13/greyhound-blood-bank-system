using System.Text.RegularExpressions;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// A table wider than its card must scroll inside the card, not run past it (a button off the edge of the pane is a
/// button nobody finds). Every top-level <c>table.bb</c> therefore sits in a <c>.table-scroll</c> box, except one that
/// holds a searchable dropdown, whose menu the box would clip; such a table must be laid out to fit instead.
/// </summary>
public partial class TableLayoutTests
{
    // TEST-BB-646
    [Fact]
    public void Tables_scroll_inside_their_card()
    {
        var root = RepoRoot();
        var files = Directory.EnumerateFiles(Path.Combine(root, "src", "BloodBankLIS.Web", "Components"), "*.razor", SearchOption.AllDirectories);
        var problems = new List<string>();

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            foreach (Match table in BbTable().Matches(text))
            {
                if (OpenTablesBefore(text, table.Index) > 0)
                    continue; // nested: the outer table's box already scrolls

                var end = CloseOf(text, table.Index);
                if (text[table.Index..end].Contains("FilterableComboBox", StringComparison.Ordinal))
                    continue;

                var before = text[..table.Index].TrimEnd();
                var previousLine = before[(before.LastIndexOf('\n') + 1)..];
                if (!previousLine.Contains("table-scroll", StringComparison.Ordinal))
                {
                    var line = text[..table.Index].Count(c => c == '\n') + 1;
                    problems.Add($"{Path.GetRelativePath(root, file)}:{line}");
                }
            }
        }

        Assert.True(problems.Count == 0,
            "These tables can run past their card; wrap each in <div class=\"table-scroll\">:" + Environment.NewLine
            + string.Join(Environment.NewLine, problems));
    }

    private static int OpenTablesBefore(string text, int index)
    {
        var head = text[..index];
        return TableOpen().Matches(head).Count - TableClose().Matches(head).Count;
    }

    private static int CloseOf(string text, int start)
    {
        var depth = 0;
        foreach (Match tag in TableTag().Matches(text, start))
        {
            depth += tag.Value.StartsWith("</", StringComparison.Ordinal) ? -1 : 1;
            if (depth == 0)
                return tag.Index + tag.Length;
        }

        return text.Length;
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

    [GeneratedRegex("<table class=\"bb\\b")]
    private static partial Regex BbTable();

    [GeneratedRegex("<table\\b")]
    private static partial Regex TableOpen();

    [GeneratedRegex("</table>")]
    private static partial Regex TableClose();

    [GeneratedRegex("</?table\\b[^>]*>")]
    private static partial Regex TableTag();
}
