using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Components.Pages;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// End-user testing pass 83: a delivery short of its packing list could be completed with nothing to say why. The
/// delivery page now names what differs and keeps Complete delivery disabled until a note is given.
/// </summary>
public class DeliveryCompletionPageTests : BunitContext
{
    // TEST-BB-1096: the difference is named and a note is needed; a completed delivery shows its note.
    [Fact]
    public void ShortDelivery_NeedsANote_AndCompletedShowsIt()
    {
        var shortDelivery = new DeliveryDetailDto(7, 1, "SUP", "Regional Blood Center", "PL-7", DateTime.UtcNow, DateTime.UtcNow,
            DeliveryStatus.Open, true, [new DeliveryLineDto(1, ComponentClass.RedBloodCells, "RBC", 4, 2)], []);
        CannedApi.Register(this, new Dictionary<string, object> { ["/api/inventory/deliveries/7"] = shortDelivery });
        var page = Render<Delivery>(p => p.Add(x => x.Id, 7L));
        page.WaitForAssertion(() => Assert.Contains("Does not match the packing list: RBC 2 short", page.Find("[data-delivery-variance]").TextContent));
        Assert.True(page.Find("[data-delivery-complete]").HasAttribute("disabled"));
        page.Find("[data-delivery-completion-note]").Input("Supplier short 2 RBC, credit requested");
        Assert.False(page.Find("[data-delivery-complete]").HasAttribute("disabled"));
    }

    [Fact]
    public void CompletedDelivery_ShowsTheDifferenceAndNote()
    {
        var done = new DeliveryDetailDto(8, 1, "SUP", "Regional Blood Center", "PL-8", DateTime.UtcNow, DateTime.UtcNow,
            DeliveryStatus.Completed, true, [new DeliveryLineDto(1, ComponentClass.RedBloodCells, "RBC", 4, 2)], [],
            "Supplier short 2 RBC, credit requested", DateTime.UtcNow);
        CannedApi.Register(this, new Dictionary<string, object> { ["/api/inventory/deliveries/8"] = done });
        var page = Render<Delivery>(p => p.Add(x => x.Id, 8L));
        page.WaitForAssertion(() => Assert.Contains("not matching the packing list (RBC 2 short): Supplier short 2 RBC, credit requested",
            page.Find("[data-delivery-completed]").TextContent));
    }
}
