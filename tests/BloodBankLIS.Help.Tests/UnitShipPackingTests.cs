using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Components.Pages;

namespace BloodBankLIS.Help.Tests;

/// <summary>The unit page says how a shipped unit was packed and who receives it (Cycle 682, OCD-072).</summary>
public class UnitShipPackingTests
{
    // TEST-BB-893: the unit's packing details reach the unit view and read as one line; a shipment from before the
    // details were kept says they were not recorded rather than showing blanks.
    [Fact]
    public void ShippedUnit_ShowsPackingAndReceivingContact()
    {
        var unit = new BloodUnit
        {
            UnitNumber = "U-SHP", Status = UnitStatus.ShippedOut, ShippedToFacility = "St. Mary's Hospital",
            ShipContainer = "COOLER-12", ShipPackingTemperatureC = 4.0m, ShipPackedBy = "tech1",
            ShipReceivingContact = "St. Mary's blood bank, 555-0142"
        };

        var dto = BloodUnitDto.From(unit);

        Assert.Equal(("COOLER-12", 4.0m, "tech1", "St. Mary's blood bank, 555-0142"),
            (dto.ShipContainer, dto.ShipPackingTemperatureC, dto.ShipPackedBy, dto.ShipReceivingContact));
        Assert.Equal("Packed in COOLER-12 at 4.0 °C by tech1 · Receiving contact: St. Mary's blood bank, 555-0142",
            UnitDetail.ShipPackingText(dto));

        var older = BloodUnitDto.From(new BloodUnit { UnitNumber = "U-OLD", Status = UnitStatus.ShippedOut, ShippedToFacility = "St. Mary's Hospital" });
        Assert.Equal("Packing details: not recorded", UnitDetail.ShipPackingText(older));
    }
}
