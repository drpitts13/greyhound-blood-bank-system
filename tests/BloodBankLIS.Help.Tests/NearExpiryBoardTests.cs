using BloodBankLIS.Application.Admin;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Components.Pages;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// The Near expiry board's discard, rendered. It sent free text as the reason code and the second verifier as the
/// comment, so the server refused it (Cycle 607). It now offers the coded reasons, a comment, and the verifier, and the
/// use-it actions come before Discard.
/// </summary>
public class NearExpiryBoardTests : BunitContext
{
    // TEST-BB-720: opening Discard on a near-expiry row offers the coded discard reasons (not free text) and a comment,
    // and Discard is the last action on the row.
    [Fact]
    public void Discard_OffersCodedReasons_AndComesLast()
    {
        var row = new NearExpiryWorkItemDto(9, "W0001230009999", DateTime.UtcNow.AddHours(6), UnitStatus.Available,
            AboGroup.O, RhType.Positive, 1, 1, "Main Blood Bank Refrigerator");
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/inventory/units/near-expiry"] = new List<NearExpiryWorkItemDto> { row },
            ["/api/reference/discard-reasons"] = new List<UnitDiscardReasonRefDto>
            {
                new("EXPIRED", "Expired", false, 1),
                new("OTHER", "Other", true, 9)
            }
        });
        var page = Render<Inventory>();
        page.FindAll("button").First(b => b.TextContent.Trim() == "Near expiry").Click();
        page.WaitForAssertion(() => Assert.Contains("W0001230009999", page.Markup));

        var cell = page.FindAll("tbody tr").First(r => r.TextContent.Contains("W0001230009999")).QuerySelector("td.right")!;
        Assert.Equal("Discard", cell.Children.Last().TextContent.Trim());

        cell.Children.Last().Click();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-near-discard-reason]")));
        var options = page.Find("[data-near-discard-reason]").QuerySelectorAll("option").Select(o => o.GetAttribute("value")).ToList();
        Assert.Equal(["", "EXPIRED", "OTHER"], options);
        Assert.Contains("Comment", page.Markup);
    }
}
