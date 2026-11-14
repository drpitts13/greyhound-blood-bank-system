using BloodBankLIS.Application.Immunohematology;
using BloodBankLIS.Web.Components.Pages;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>OCD-073: the antibody ID worklist lists outside reports waiting to be confirmed here.</summary>
public class OutsideAntibodyWorklistTests : BunitContext
{
    // TEST-BB-867: each waiting report names the patient, antibody, reporting facility and date, says when a report was
    // never documented, and opens the patient's chart to confirm it; with none waiting the section says so.
    [Fact]
    public void OutsideReports_AreListedToConfirm()
    {
        var now = DateTime.UtcNow;
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/antibodies/outside-to-confirm"] = new List<OutsideAntibodyToConfirmDto>
            {
                new(7, 3, "MRN0003", "Doe, Jane", "Anti-Jka", "St. Mary's Hospital", new DateOnly(2025, 2, 1), now.AddDays(-4), "tech1", false),
                new(8, 4, "MRN0004", "Roe, Ann", "Anti-Fya", "County General", null, now.AddDays(-1), "tech2", true)
            }
        });
        var page = Render<AntibodyIdWorklist>();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-outside-antibody='7']")));

        Assert.Contains("Outside reports to confirm (2)", page.Find("[data-outside-to-confirm]").TextContent);
        var first = page.Find("[data-outside-antibody='7']");
        Assert.Contains("St. Mary's Hospital, identified 2025-02-01", first.TextContent);
        Assert.Contains("Report not documented", first.TextContent);
        Assert.Equal("/patients/3", first.QuerySelector("a")!.GetAttribute("href"));
        Assert.DoesNotContain("Report not documented", page.Find("[data-outside-antibody='8']").TextContent);
    }
}
