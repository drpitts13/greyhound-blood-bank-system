using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Components.Pages;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// End-user testing pass 261: the Expected inbound board marked a unit only "Overdue", while the other inventory boards
/// say how long (held, missing, on hold). It now says how long past its due time.
/// </summary>
public class ExpectedInboundOverdueTests : BunitContext
{
    // TEST-BB-1287: an overdue expected unit says how long it is overdue.
    [Fact]
    public void OverdueUnit_SaysHowLong()
    {
        var row = new ExpectedInboundWorkItemDto(5, "W000123ASN0001", DateTime.UtcNow.AddDays(-13), DateTime.UtcNow.AddDays(-12).AddHours(-1),
            true, "ASN-DEMO-01", "Regional Blood Center", AboGroup.O, RhType.Positive, 1);
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/inventory/units/expected"] = new List<ExpectedInboundWorkItemDto> { row }
        });
        var page = Render<Inventory>();
        page.FindAll("button").First(b => b.TextContent.Trim() == "Expected inbound").Click();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-expected-overdue]")));

        Assert.Equal("Overdue 12 d", page.Find("[data-expected-overdue]").TextContent.Trim());
    }
}
