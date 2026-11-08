using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Infrastructure.Persistence;

public static partial class DatabaseSeeder
{
    /// <summary>
    /// Ship units demo: two group O red cells shipped yesterday to St. Mary's Hospital under SHIP-DEMO-01, so Ship units
    /// shows a recent shipment and the unit pages say where they went. Added once.
    /// </summary>
    private static async Task EnsureShipOutDemoAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.BloodUnits.AnyAsync(u => u.UnitNumber == "W000123SHP0001", ct))
        {
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
        context.BloodUnits.AddRange(Shipped("W000123SHP0001", RhType.Positive), Shipped("W000123SHP0002", RhType.Negative));
        await context.SaveChangesAsync(ct);
    }
}
