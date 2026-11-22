using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Components.Pages;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// Entering units from a delivery, rendered: ABO and Rh(D) started at O / Positive and were carried from one unit to the
/// next, so a unit could be received under a type nobody read from its label (Cycle 593).
/// </summary>
public class DeliveryReceivePageTests : BunitContext
{
    // TEST-BB-696: ABO and Rh(D) start unchosen, and receiving without them names them as missing; nothing is sent.
    [Fact]
    public void UnitType_StartsUnchosen_AndIsRequired()
    {
        var delivery = new DeliveryDetailDto(5, 1, "SUP", "Regional Blood Center", "PL-5", DateTime.UtcNow, DateTime.UtcNow,
            DeliveryStatus.Open, false, [], []);
        CannedApi.Register(this, new Dictionary<string, object> { ["/api/inventory/deliveries/5"] = delivery });
        var page = Render<DeliveryReceive>(p => p.Add(x => x.Id, 5L));
        page.WaitForAssertion(() => Assert.Contains("Receive a unit", page.Markup));

        foreach (var label in new[] { "ABO", "Rh(D)" })
        {
            var select = page.FindAll("label").First(l => l.TextContent == label).ParentElement!.QuerySelector("select")!;
            Assert.True(string.IsNullOrEmpty(select.GetAttribute("value")));
            Assert.Equal("— Select —", select.QuerySelector("option")!.TextContent);
        }

        page.FindAll("input")[0].Change("W0001230000099");
        page.FindAll("button").First(b => b.TextContent.Trim() == "Receive unit").Click();
        page.WaitForAssertion(() => Assert.Matches(@"Enter the [^.]*ABO, Rh\(D\)[^.]* before receiving", page.Markup));
    }
}
