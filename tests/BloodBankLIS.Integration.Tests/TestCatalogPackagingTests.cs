namespace BloodBankLIS.Integration.Tests;

/// <summary>
/// Gap 19: formal <c>TEST-BB-*</c> IDs in <c>docs/validation/TEST_CATALOG.md</c>
/// stay unique. This is evidence packaging, not a clinical control.
/// </summary>
public class TestCatalogPackagingTests
{
    [Fact]
    public void FormalIds_AreUnique()
    {
        var path = Path.Combine(FindRepoRoot(), "docs", "validation", "TEST_CATALOG.md");
        Assert.True(File.Exists(path), path);
        var ids = File.ReadAllLines(path)
            .Where(static line => line.StartsWith("| TEST-BB-", StringComparison.Ordinal))
            .Select(static line => line.Split('|', StringSplitOptions.TrimEntries)[1])
            .ToList();
        Assert.NotEmpty(ids);
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void CitedClasses_HaveSourceFiles()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(root, "docs", "validation", "TEST_CATALOG.md");
        var sources = Directory.GetFiles(Path.Combine(root, "tests"), "*.cs", SearchOption.AllDirectories);
        var missing = new List<string>();
        foreach (var line in File.ReadAllLines(path).Where(static l => l.StartsWith("| TEST-BB-", StringComparison.Ordinal)))
        {
            var cols = line.Split('|', StringSplitOptions.TrimEntries);
            var cited = cols[3].Trim('`');
            var className = cited.Split('.', 2)[0];
            if (!sources.Any(f => File.ReadAllText(f).Contains($"class {className}", StringComparison.Ordinal)))
            {
                missing.Add(cited);
            }
        }

        Assert.True(missing.Count == 0, "Catalog cites missing test classes: " + string.Join(", ", missing));
    }

    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "BloodBankLIS.slnx")))
                return dir.FullName;
        }

        throw new InvalidOperationException("Could not find BloodBankLIS.slnx from the test output directory.");
    }
}
