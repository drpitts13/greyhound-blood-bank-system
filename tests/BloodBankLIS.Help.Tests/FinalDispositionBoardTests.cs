using BloodBankLIS.Application.Issuing;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Components.Pages;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// Overdue final disposition, rendered: each row had only the unit, patient, and times, so closing it meant noting the
/// unit and looking it up again on another desk (Cycle 611). Rows now carry the same next step as Issued units.
/// </summary>
public class FinalDispositionBoardTests : BunitContext
{
    // TEST-BB-724: an overdue unit offers Return and Document transfusion, and Document opens the Transfusion desk on it.
    [Fact]
    public void OverdueRow_OffersTheNextStep()
    {
        var issued = DateTime.UtcNow.AddDays(-3);
        var outstanding = new OutstandingIssueWorkItemDto(
            41, 5, "MRN0022", "Demo-Multi, Riley", "1980-01-01", 9, "W0001230009205", "O+", false, null,
            null, false, issued, issued.AddMinutes(20), "nurse1", null, "4W", IssueStatus.Issued, ProductName: "Red Blood Cells");
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/issues/overdue-disposition"] = new List<OverdueDispositionWorkItemDto>
            {
                new(41, 9, "W0001230009205", "Demo-Multi, Riley (MRN0022)", issued, issued.AddDays(1))
            },
            ["/api/issues/outstanding"] = new List<OutstandingIssueWorkItemDto> { outstanding }
        });

        var page = Render<Issuing>();
        page.WaitForAssertion(() => Assert.Contains(page.FindAll("button"), b => b.TextContent.StartsWith("Final disposition")));
        page.FindAll("button").First(b => b.TextContent.StartsWith("Final disposition")).Click();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-disposition-row='41']")));

        var actions = page.Find("[data-disposition-row='41']").QuerySelectorAll("button").Select(b => b.TextContent.Trim()).ToList();
        Assert.Equal(["Return", "Document transfusion"], actions);

        page.Find("[data-disposition-row='41']").QuerySelectorAll("button").Last().Click();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-tx-reaction]")));
        Assert.Contains("W0001230009205", page.Markup);
    }
}
