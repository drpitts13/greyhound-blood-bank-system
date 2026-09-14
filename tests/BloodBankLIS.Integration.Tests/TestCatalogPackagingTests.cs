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
