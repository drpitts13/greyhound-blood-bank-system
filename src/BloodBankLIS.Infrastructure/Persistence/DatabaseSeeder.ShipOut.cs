using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Infrastructure.Persistence;

public static partial class DatabaseSeeder
{
    /// <summary>
    /// Ship units demo: two group O red cells shipped yesterday to St. Mary's Hospital under SHIP-DEMO-01, so Ship units
    /// shows a recent shipment and the unit pages say where they went. Added once; the packing details (OCD-072) are
    /// filled in on the demo units once if they were added before those were recorded.
    /// </summary>
    private static async Task EnsureShipOutDemoAsync(BloodBankDbContext context, CancellationToken ct)
    {
        var existing = await context.BloodUnits
            .Where(u => u.UnitNumber == "W000123SHP0001" || u.UnitNumber == "W000123SHP0002").ToListAsync(ct);
        if (existing.Count > 0)
        {
            var unpacked = existing.Where(u => u.ShipContainer is null).ToList();
            foreach (var unit in unpacked)
            {
                Pack(unit);
            }

            if (unpacked.Count > 0)
            {
                await context.SaveChangesAsync(ct);
            }

            return;
        }

        var redCells = await context.ProductTypes.FirstOrDefaultAsync(p => p.ProductCode == "RBC-LR", ct);
        var fridge = await context.InventoryLocations.FirstOrDefaultAsync(l => l.Code == "FRIDGE-1", ct);
        if (redCells is null || fridge is null)
        {
            return;
        }

        var shipped = DateTime.UtcNow.AddDays(-1);
        BloodUnit Shipped(string number, RhType rh) => new()
        {
            UnitNumber = number,
            ProductTypeId = redCells.Id,
            Abo = AboGroup.O,
            RhD = rh,
            ExpiresUtc = DateTime.UtcNow.AddDays(25),
            CurrentLocationId = fridge.Id,
            Status = UnitStatus.ShippedOut,
            Volume = 300m,
            CollectionFacility = "Regional Blood Center",
            Supplier = "Regional Blood Center",
            CollectedUtc = DateTime.UtcNow.AddDays(-10),
            ShippedToFacility = "St. Mary's Hospital",
            ShippedUtc = shipped,
            OutboundShipmentReference = "SHIP-DEMO-01"
        };
        var units = new[] { Shipped("W000123SHP0001", RhType.Positive), Shipped("W000123SHP0002", RhType.Negative) };
        foreach (var unit in units)
        {
            Pack(unit);
        }

        context.BloodUnits.AddRange(units);
        await context.SaveChangesAsync(ct);
    }

    private static void Pack(BloodUnit unit)
    {
        unit.ShipContainer = "COOLER-12";
        unit.ShipPackingTemperatureC = 4.0m;
        unit.ShipPackedBy = "tech1";
        unit.ShipReceivingContact = "St. Mary's blood bank, 555-0142";
    }
}
