namespace BloodBankLIS.Help.Tests;

public class LoadingStateSourceTests
{
    // TEST-BB-1192: Minimum stock does not say no levels are configured while it is still loading them, or when
    // loading failed (end-user testing pass 169).
    [Fact]
    public void MinimumStock_SaysLoadingBeforeSayingNoneConfigured()
    {
        var page = File.ReadAllText(Path.Combine(RepoRoot(), "src", "BloodBankLIS.Web", "Components", "Pages", "MinimumStock.razor"));
        var loading = page.IndexOf("@if (!_loaded)", StringComparison.Ordinal);
        var error = page.IndexOf("else if (_loadError is not null)", StringComparison.Ordinal);
        var none = page.IndexOf("No minimum stock levels are configured.", StringComparison.Ordinal);
        Assert.True(loading >= 0 && error > loading && none > error, "Loading and the load error must be checked before the empty message.");
        Assert.Contains("_loaded = true;", page);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "BloodBankLIS.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not find the repository root.");
    }
}
