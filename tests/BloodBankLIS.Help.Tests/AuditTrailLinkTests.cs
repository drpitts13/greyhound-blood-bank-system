namespace BloodBankLIS.Help.Tests;

/// <summary>TEST-BB-501: the unit page's audit link uses the query names the Audit page reads.</summary>
public class AuditTrailLinkTests
{
    [Fact]
    public void UnitAuditLink_MatchesTheAuditPageQueryNames()
    {
        var pages = Path.Combine(RepoRoot(), "src", "BloodBankLIS.Web", "Components", "Pages");
        var audit = File.ReadAllText(Path.Combine(pages, "Audit.razor"));
        var unit = File.ReadAllText(Path.Combine(pages, "UnitDetail.razor"));

        Assert.Contains("[SupplyParameterFromQuery(Name = \"entityType\")]", audit);
        Assert.Contains("[SupplyParameterFromQuery(Name = \"entityId\")]", audit);
        Assert.Contains("AuditUrls.For(\"BloodUnit\"", unit);
        Assert.Equal("/audit?entityType=Specimen&entityId=7", BloodBankLIS.Web.Services.AuditUrls.For("Specimen", 7));

        // The patient timeline and the specimen history link to their trails too.
        var shared = Path.Combine(RepoRoot(), "src", "BloodBankLIS.Web", "Components", "Shared");
        Assert.Contains("AuditUrls.For(\"Patient\"", File.ReadAllText(Path.Combine(shared, "PatientChartTimelineTab.razor")));
        Assert.Contains("AuditUrls.For(\"Specimen\"", File.ReadAllText(Path.Combine(shared, "SpecimenTimelinePanel.razor")));
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
