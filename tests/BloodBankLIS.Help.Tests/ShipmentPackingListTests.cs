using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Web.Components.Pages;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace BloodBankLIS.Help.Tests;

/// <summary>A shipment's packing list is a sheet of its own, not a print of the Ship units page (Cycle 683).</summary>
public class ShipmentPackingListTests : BunitContext
{
    private static readonly DateTime Shipped = DateTime.UtcNow.AddHours(-2);

    private static BloodUnitDto Unit(string number) => BloodUnitDto.From(new BloodUnit
    {
        UnitNumber = number, Abo = AboGroup.O, RhD = RhType.Negative, ExpiresUtc = DateTime.UtcNow.AddDays(20),
        Status = UnitStatus.ShippedOut, ShippedToFacility = "St. Mary's Hospital", ShippedUtc = Shipped, OutboundShipmentReference = "SHIP-9"
    });

    private static OutboundShipmentDto Shipment(string reference, string? container) => new(
        "St. Mary's Hospital", reference, Shipped, Shipped, [Unit($"{reference}-A"), Unit($"{reference}-B")],
        container, container is null ? null : 4.0m, container is null ? null : "tech1",
        container is null ? null : "St. Mary's blood bank, 555-0142");

    private IRenderedComponent<ShipmentPackingList> Open(string? key, params OutboundShipmentDto[] shipments)
    {
        CannedApi.Register(this, new Dictionary<string, object> { ["/api/inventory/shipments"] = shipments.ToList() });
        Services.GetRequiredService<NavigationManager>().NavigateTo(
            key is null ? "/inventory/ship-out/packing-list" : $"/inventory/ship-out/packing-list?key={Uri.EscapeDataString(key)}");
        return Render<ShipmentPackingList>();
    }

    // TEST-BB-895: the packing list shows only the named shipment: where it goes, the packing details, the receiving
    // contact and its units, with blanks for the receiving blood bank.
    [Fact]
    public void PackingList_ShowsTheNamedShipment()
    {
        var page = Open(ShipOutRule.ShipmentKey("St. Mary's Hospital", "SHIP-9", Shipped),
            Shipment("SHIP-8", "COOLER-3"), Shipment("SHIP-9", "COOLER-12"));
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-packing-list]")));

        var sheet = page.Find("[data-packing-list]").TextContent;
        Assert.Contains("COOLER-12", sheet);
        Assert.Contains("4.0 °C", sheet);
        Assert.Contains("St. Mary's blood bank, 555-0142", page.Find("[data-packing-contact]").TextContent);
        Assert.Equal(2, page.FindAll("[data-packing-units] tbody tr").Count);
        Assert.Contains("SHIP-9-A", sheet);
        Assert.DoesNotContain("SHIP-8", sheet);
        Assert.Contains("Temperature on arrival", sheet);
        Assert.Empty(page.FindAll("[data-packing-not-recorded]"));
    }

    // TEST-BB-896: an older shipment without packing details says to write them by hand; an unknown or missing key says
    // no shipment was found rather than printing an empty sheet.
    [Theory]
    [InlineData("older")]
    [InlineData("unknown")]
    [InlineData("missing")]
    public void PackingList_OlderShipment_AndNoMatch(string kind)
    {
        switch (kind)
        {
            case "older":
                var older = Open(ShipOutRule.ShipmentKey("St. Mary's Hospital", "SHIP-9", Shipped), Shipment("SHIP-9", null));
                older.WaitForAssertion(() => Assert.NotNull(older.Find("[data-packing-not-recorded]")));
                break;
            case "unknown":
                var none = Open("NOWHERE|X", Shipment("SHIP-9", "COOLER-12"));
                none.WaitForAssertion(() => Assert.NotNull(none.Find("[data-packing-not-found]")));
                Assert.Empty(none.FindAll("[data-packing-print]"));
                break;
            default:
                var missing = Open(null);
                missing.WaitForAssertion(() => Assert.Contains("no shipment was named", missing.Markup));
                Assert.Empty(missing.FindAll("[data-packing-list]"));
                break;
        }
    }

    // TEST-BB-897: on Ship units, a recent shipment's Print packing list opens that shipment's sheet, not a print of the page.
    [Fact]
    public void ShipUnits_RecentShipment_LinksToItsPackingList()
    {
        CannedApi.Register(this, new Dictionary<string, object> { ["/api/inventory/shipments"] = new List<OutboundShipmentDto> { Shipment("SHIP-9", "COOLER-12") } });
        var page = Render<InventoryShipOut>();
        page.WaitForAssertion(() => Assert.Contains("SHIP-9", page.Find("[data-recent-shipments]").TextContent));

        page.FindAll("[data-recent-shipments] button").First(b => b.TextContent.Trim() == "Units").Click();
        var link = page.Find("[data-shipment-packing-list]");
        Assert.Equal("/inventory/ship-out/packing-list?key=" + Uri.EscapeDataString(ShipOutRule.ShipmentKey("St. Mary's Hospital", "SHIP-9", Shipped)),
            link.GetAttribute("href"));
        Assert.Empty(page.FindAll("[onclick='window.print()']"));
    }
}
