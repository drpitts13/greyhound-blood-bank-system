using System.Text.RegularExpressions;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// The bedside "Transfusion reaction suspected?" select (Cycle 596) bound a bool? with options valued @false and @true:
/// those render as no value attribute and an empty one, and a select cannot bind a bool at all (the binder casts the
/// checkbox's true or false), so no answer ever took and every finish was refused until Cycle 624. A yes/no select binds
/// through text; InspectionAnswer is the button form.
/// </summary>
public partial class OptionValueTests
{
    // TEST-BB-749: no page writes a boolean option value as a C# literal, and no select binds a bool field directly.
    [Fact]
    public void Options_DoNotUseBooleanLiteralValues()
    {
        var root = RepoRoot();
        var boolFields = Directory.EnumerateFiles(Path.Combine(root, "src", "BloodBankLIS.Web", "Components"), "*.razor", SearchOption.AllDirectories)
            .SelectMany(f => BoolField().Matches(File.ReadAllText(f)).Select(m => (File: f, Name: m.Groups[1].Value)))
            .ToList();
        var boundSelects = boolFields
            .Where(b => Regex.IsMatch(File.ReadAllText(b.File), $@"<select[^>]*@bind(-value)?=""{Regex.Escape(b.Name)}"""))
            .Select(b => $"{Path.GetRelativePath(root, b.File)}: select bound to bool {b.Name}")
            .ToList();
        Assert.True(boundSelects.Count == 0, "A select is bound to a bool (bind it through text):" + Environment.NewLine + string.Join(Environment.NewLine, boundSelects));

        var problems = Directory.EnumerateFiles(Path.Combine(root, "src", "BloodBankLIS.Web", "Components"), "*.razor", SearchOption.AllDirectories)
            .SelectMany(f => File.ReadAllLines(f).Select((line, i) => (File: f, Line: i + 1, Text: line)))
            .Where(l => BooleanOptionValue().IsMatch(l.Text))
            .Select(l => $"{Path.GetRelativePath(root, l.File)}:{l.Line}: {l.Text.Trim()}")
            .ToList();

        Assert.True(problems.Count == 0,
            "An option value is a C# boolean literal (write value=\"true\" or value=\"false\"):" + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    [GeneratedRegex(@"bool\??\s+(_?[A-Za-z]\w*)\s*(;|=|\{)")]
    private static partial Regex BoolField();

    [GeneratedRegex(@"value=""@\(?(true|false)\)?""")]
    private static partial Regex BooleanOptionValue();

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
