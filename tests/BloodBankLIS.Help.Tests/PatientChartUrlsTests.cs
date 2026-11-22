using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// TEST-BB-488: chart links land on what they name (a tube's history, one order, one visit's orders), and the chart
/// reads the query names those links use.
/// </summary>
public class PatientChartUrlsTests
{
    [Fact]
    public void Links_NameTheTube_TheOrder_OrTheVisit()
    {
        Assert.Equal("/patients/12?tab=specimens&specimen=40", PatientChartUrls.Specimen(12, 40));
        Assert.Equal("/patients/12?tab=orders&order=ORD%2F0016%20A", PatientChartUrls.Order(12, " ORD/0016 A "));
        Assert.Equal("/patients/12?tab=orders", PatientChartUrls.Order(12, null));
        Assert.Equal("/patients/12?tab=orders", PatientChartUrls.Order(12, "  "));
        Assert.Equal("/patients/12?tab=orders&visit=7", PatientChartUrls.VisitOrders(12, 7));
    }

    [Fact]
    public void TheChart_ReadsEveryQueryNameTheLinksUse()
    {
        var chart = File.ReadAllText(Path.Combine(RepoRoot(), "src", "BloodBankLIS.Web", "Components", "Pages", "PatientDetail.razor"));

        foreach (var name in new[] { "tab", "specimen", "order", "visit" })
        {
            Assert.Contains($"[SupplyParameterFromQuery(Name = \"{name}\")]", chart);
        }
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
