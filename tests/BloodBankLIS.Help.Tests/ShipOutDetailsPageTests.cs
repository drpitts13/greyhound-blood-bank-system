using BloodBankLIS.Web.Components.Pages;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>OCD-072 (decided 2026-11-10): the ship-out page asks for the packing details before the first scan.</summary>
public class ShipOutDetailsPageTests : BunitContext
{
    // TEST-BB-861: the scan field stays unavailable until the facility, container, packing temperature, packer and
    // receiving contact are all filled in; they then stay for every scan of the shipment.
    [Fact]
    public void ScanWaitsForThePackingDetails()
    {
        CannedApi.Register(this, new Dictionary<string, object>());
        var page = Render<InventoryShipOut>();

        page.Find("[data-ship-facility]").Input("St. Mary's Hospital");
        Assert.True(page.Find("[data-ship-scan]").HasAttribute("disabled"));
        Assert.Contains("Fill in the shipment above first", page.Find("[data-ship-scan]").GetAttribute("placeholder"));

        page.Find("[data-ship-container]").Input("COOLER-7");
        page.Find("[data-ship-temperature]").Input("4");
        page.Find("[data-ship-packed-by]").Input("tech1");
        Assert.True(page.Find("[data-ship-scan]").HasAttribute("disabled"));

        page.Find("[data-ship-contact]").Input("J. Ruiz, 555-0142");
        Assert.False(page.Find("[data-ship-scan]").HasAttribute("disabled"));
    }
}
