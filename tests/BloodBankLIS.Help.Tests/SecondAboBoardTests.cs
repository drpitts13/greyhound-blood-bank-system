using BloodBankLIS.Application.Issuing;
using BloodBankLIS.Web.Components.Pages;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// Second ABO/Rh due, rendered: the board listed patients waiting on a second type with no next step, so staff had to
/// find out on the chart whether a second ABO/Rh was already ordered (Cycle 613). Each row now says so or orders one.
/// </summary>
public class SecondAboBoardTests : BunitContext
{
    // TEST-BB-729: a patient with an open ABO/Rh order shows its number linked to the order; one with nothing ordered
    // gets Order ABO/Rh, opening the chart's add-order form with ABO/Rh ticked.
    [Fact]
    public void Rows_SayWhetherAnAboRhIsOrdered()
    {
        const string reason = "Second concordant ABO/Rh is required before non-emergency red-cell issue.";
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/issues/pending-second-abo"] = new List<SecondAboWorkItemDto>
            {
                new(7, "MRN0007", "Demo, Ordered", "A Pos", 2, reason, "ORD-ABO-1"),
                new(8, "MRN0008", "Demo, Nothing", "O Neg", 1, reason)
            }
        });

        var page = Render<Issuing>();
        page.WaitForAssertion(() => Assert.Contains(page.FindAll("button"), b => b.TextContent.StartsWith("Second ABO/Rh")));
        page.FindAll("button").First(b => b.TextContent.StartsWith("Second ABO/Rh")).Click();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-second-abo-next='7']")));

        var ordered = page.Find("[data-second-abo-next='7']");
        Assert.Contains("Ordered", ordered.TextContent);
        Assert.Equal("ORD-ABO-1", ordered.QuerySelector("a")!.TextContent.Trim());
        Assert.Contains("/patients/7?tab=orders", ordered.QuerySelector("a")!.GetAttribute("href"));

        var order = page.Find("[data-second-abo-next='8'] a");
        Assert.Equal("Order ABO/Rh", order.TextContent.Trim());
        Assert.Equal("/patients/8?panel=abo-rh", order.GetAttribute("href"));
    }
}
