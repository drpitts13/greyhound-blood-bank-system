using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Infrastructure.Persistence;

public static partial class DatabaseSeeder
{
    private const string RecalledDemoReason = "Supplier recall R-DEMO-02: donor subsequently reactive";

    /// <summary>
    /// Recalled board demo (Cycle 690): one group B red cell recalled this morning and still in the fridge, so the Recalled
    /// board, its home card and the shift handoff have a unit to pull. It has the status row a recall writes. Added once.
    /// </summary>
    private static async Task EnsureRecalledDemoAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.BloodUnits.AnyAsync(u => u.UnitNumber == "W000123RCL0001", ct))
        {
            return;
        }

        var redCells = await context.ProductTypes.FirstOrDefaultAsync(p => p.ProductCode == "RBC-LR", ct);
        var fridge = await context.InventoryLocations.FirstOrDefaultAsync(l => l.Code == "FRIDGE-1", ct);
        if (redCells is null || fridge is null)
        {
            return;
        }

        var unit = new BloodUnit
        {
            UnitNumber = "W000123RCL0001",
            ProductTypeId = redCells.Id,
            Abo = AboGroup.B,
            RhD = RhType.Positive,
            ExpiresUtc = DateTime.UtcNow.AddDays(18),
            CurrentLocationId = fridge.Id,
            Status = UnitStatus.Recalled,
            Volume = 300m,
            CollectionFacility = "Regional Blood Center",
            Supplier = "Regional Blood Center",
            CollectedUtc = DateTime.UtcNow.AddDays(-12),
            RecallReason = RecalledDemoReason
        };
        context.BloodUnits.Add(unit);
        await context.SaveChangesAsync(ct);
        context.InventoryStatusHistory.Add(new InventoryStatusHistory
        {
            BloodProductId = unit.Id,
            FromStatus = UnitStatus.Available,
            ToStatus = UnitStatus.Recalled,
            FromLocationId = fridge.Id,
            ToLocationId = fridge.Id,
            Reason = RecalledDemoReason,
            ChangedBy = "tech1",
            ChangedUtc = DateTime.UtcNow.AddHours(-3)
        });
        await context.SaveChangesAsync(ct);
    }
}
