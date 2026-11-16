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

    // TEST-BB-1193: the issue desk's work lists say Loading… until read and name a failed read, instead of saying there
    // is nothing waiting (end-user testing pass 170).
    [Fact]
    public void IssuingWorklists_DoNotShowEmptyBeforeTheyAreRead()
    {
        Assert.Equal(("Loading…", false), BloodBankLIS.Web.Services.WorklistEmptyText.For(false, null, "No units currently in transit."));
        Assert.Equal(("No units currently in transit.", false), BloodBankLIS.Web.Services.WorklistEmptyText.For(true, null, "No units currently in transit."));
        var failed = BloodBankLIS.Web.Services.WorklistEmptyText.For(true, "Timed out.", "No units currently in transit.");
        Assert.True(failed.IsError);
        Assert.StartsWith("This list could not be loaded: Timed out.", failed.Text);

        var page = File.ReadAllText(Path.Combine(RepoRoot(), "src", "BloodBankLIS.Web", "Components", "Pages", "Issuing.razor"));
        foreach (var key in new[] { "second", "ready", "retro", "urgency", "transit", "outstanding", "disposition" })
        {
            Assert.Contains($"@EmptyList(\"{key}\", ", page);
            Assert.Contains($"NoteWorklist(\"{key}\", ", page);
        }

        Assert.DoesNotContain("<div class=\"muted\">No reserved units waiting to issue.</div>", page);
        Assert.Contains("_worklistsLoaded = true;", page);
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
