using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Infrastructure.Persistence;

public static partial class DatabaseSeeder
{
    private const string SupplierReturnDemoReason = "Supplier recall R-DEMO-01";

    /// <summary>
    /// Return to supplier demo (Cycle 686): two group A red cells returned yesterday for one recall, so Return to supplier
    /// shows a recent return and its return list. Each has the status row a return writes (who, when, reason). Added once.
    /// </summary>
    private static async Task EnsureSupplierReturnDemoAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.BloodUnits.AnyAsync(u => u.UnitNumber == "W000123RTS0001" || u.UnitNumber == "W000123RTS0002", ct))
        {
            return;
        }

        var redCells = await context.ProductTypes.FirstOrDefaultAsync(p => p.ProductCode == "RBC-LR", ct);
        var fridge = await context.InventoryLocations.FirstOrDefaultAsync(l => l.Code == "FRIDGE-1", ct);
        if (redCells is null || fridge is null)
        {
            return;
        }

        var returned = DateTime.UtcNow.AddDays(-1);
        BloodUnit Returned(string number, RhType rh) => new()
        {
            UnitNumber = number,
            ProductTypeId = redCells.Id,
            Abo = AboGroup.A,
            RhD = rh,
            ExpiresUtc = DateTime.UtcNow.AddDays(20),
            CurrentLocationId = fridge.Id,
            Status = UnitStatus.ReturnedToSupplier,
            Volume = 300m,
            CollectionFacility = "Regional Blood Center",
            Supplier = "Regional Blood Center",
            CollectedUtc = DateTime.UtcNow.AddDays(-15),
            SupplierReturnReason = SupplierReturnDemoReason
        };
        var units = new[] { Returned("W000123RTS0001", RhType.Positive), Returned("W000123RTS0002", RhType.Positive) };
        context.BloodUnits.AddRange(units);
        await context.SaveChangesAsync(ct);

        for (var i = 0; i < units.Length; i++)
        {
            context.InventoryStatusHistory.Add(new InventoryStatusHistory
            {
                BloodProductId = units[i].Id,
                FromStatus = UnitStatus.Available,
                ToStatus = UnitStatus.ReturnedToSupplier,
                FromLocationId = fridge.Id,
                ToLocationId = fridge.Id,
                Reason = SupplierReturnDemoReason,
                ChangedBy = "tech1",
                ChangedUtc = returned.AddMinutes(i)
            });
        }

        await context.SaveChangesAsync(ct);
    }
}
