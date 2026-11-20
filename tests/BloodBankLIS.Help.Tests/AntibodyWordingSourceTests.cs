namespace BloodBankLIS.Help.Tests;

/// <summary>
/// A patient's antibody list is worded in one place (<c>OutsideAntibodyReportRule.HistoryEntry</c>). Four hand-written
/// copies had drifted apart (Cycle 584): the issue desk, the bench worklist, and the reaction investigation still said
/// nothing of outside reports after the chart did. A new copy of the wording fails here.
/// </summary>
public class AntibodyWordingSourceTests
{
    private const string Wording = "(historical / currently undetectable)";

    // TEST-BB-672
    [Fact]
    public void AntibodyListWording_LivesInOneRule()
    {
        var root = RepoRoot();
        var copies = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs", StringComparison.Ordinal) || f.EndsWith(".razor", StringComparison.Ordinal))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => !f.EndsWith("OutsideAntibodyReportRule.cs", StringComparison.Ordinal))
            .SelectMany(f => File.ReadAllLines(f).Select((line, i) => (File: f, Line: i + 1, Text: line)))
            .Where(l => l.Text.Contains(Wording, StringComparison.Ordinal) && !l.Text.TrimStart().StartsWith("///", StringComparison.Ordinal))
            .Select(l => $"{Path.GetRelativePath(root, l.File)}:{l.Line}")
            .ToList();

        Assert.True(copies.Count == 0,
            "Antibody list wording copied outside OutsideAntibodyReportRule.HistoryEntry; call it instead:" + Environment.NewLine
            + string.Join(Environment.NewLine, copies));
    }

    // TEST-BB-674: a special requirement's type is never shown by its code name ("CmvNegative"); screens use
    // SpecialRequirementLabel. Only the FHIR mapper sends the type as a code, where a code is what the receiver expects.
    [Fact]
    public void RequirementTypeCodeName_IsNotShown()
    {
        var root = RepoRoot();
        var uses = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs", StringComparison.Ordinal) || f.EndsWith(".razor", StringComparison.Ordinal))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => !f.EndsWith("FhirMapper.cs", StringComparison.Ordinal))
            .SelectMany(f => File.ReadAllLines(f).Select((line, i) => (File: f, Line: i + 1, Text: line)))
            .Where(l => l.Text.Contains("RequirementType.ToString()", StringComparison.Ordinal))
            .Select(l => $"{Path.GetRelativePath(root, l.File)}:{l.Line}")
            .ToList();

        Assert.True(uses.Count == 0,
            "A requirement type is shown by its code name; use SpecialRequirementLabel.TypeName or Display:" + Environment.NewLine
            + string.Join(Environment.NewLine, uses));
    }

    // TEST-BB-1348 (end-user testing pass 321): a screen shows saved times in local time like the rest of the app; a
    // time formatted and then labelled "UTC" had read five hours off (supplier order received, unit expected by) or,
    // after ToLocalTime, was labelled wrongly (authenticator set up).
    [Fact]
    public void ScreenTimes_AreNotLabelledUtc()
    {
        var root = RepoRoot();
        var uses = Directory.EnumerateFiles(Path.Combine(root, "src", "BloodBankLIS.Web"), "*.razor", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(f => File.ReadAllLines(f).Select((line, i) => (File: f, Line: i + 1, Text: line)))
            .Where(l => System.Text.RegularExpressions.Regex.IsMatch(l.Text, @"HH:mm""\)\s*UTC"))
            .Select(l => $"{Path.GetRelativePath(root, l.File)}:{l.Line}")
            .ToList();

        Assert.True(uses.Count == 0,
            "A screen labels a time UTC; show it with ToLocalTime() and no label:" + Environment.NewLine + string.Join(Environment.NewLine, uses));
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
