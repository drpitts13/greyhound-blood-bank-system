using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Infrastructure.Persistence;

public static partial class DatabaseSeeder
{
    /// <summary>
    /// Blood orders to prepare demo: a STAT order for two red cell units for MRN0001 with nothing reserved yet, so the
    /// worklist and its home card have something to show. Added once.
    /// </summary>
    private static async Task EnsureBloodOrderDemoAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.Orders.AnyAsync(o => o.OrderNumber == "ORD-PREP-DEMO", ct))
        {
            return;
        }

        var patient = await context.Patients.FirstOrDefaultAsync(p => p.MedicalRecordNumber == "MRN0001", ct);
        var redCells = await context.ProductTypes.FirstOrDefaultAsync(p => p.ProductCode == "RBC-LR", ct);
        var encounter = patient is null ? null : await context.Encounters.OrderBy(e => e.Id).FirstOrDefaultAsync(e => e.PatientId == patient.Id, ct);
        var location = await context.OrderingLocations.OrderBy(l => l.Id).FirstOrDefaultAsync(ct);
        if (patient is null || redCells is null || encounter is null || location is null)
        {
            return;
        }

        var order = new Order
        {
            OrderNumber = "ORD-PREP-DEMO",
            PatientId = patient.Id,
            EncounterId = encounter.Id,
            OrderingLocationId = location.Id,
            OrderCategory = OrderCategory.Product,
            OrderName = "Red Blood Cells",
            OrderType = OrderType.Other,
            ProductTypeId = redCells.Id,
            Priority = OrderPriority.Stat,
            Status = OrderStatus.New,
            FulfillmentStatus = FulfillmentStatus.Ordered,
            OrderedUtc = DateTime.UtcNow.AddMinutes(-30),
            NeededByUtc = DateTime.UtcNow.AddHours(3),
            OrderingProvider = "Dr. Demo Surgeon",
            Comment = "Demo: STAT, two units for surgery; nothing reserved yet."
        };
        context.Orders.Add(order);
        await context.SaveChangesAsync(ct);
        context.OrderLines.AddRange(
            new OrderLine { OrderId = order.Id, LineNumber = 1, LineCategory = OrderCategory.Product, LineName = redCells.Name, ProductTypeId = redCells.Id, OrderType = OrderType.Other, FulfillmentStatus = FulfillmentStatus.Ordered },
            new OrderLine { OrderId = order.Id, LineNumber = 2, LineCategory = OrderCategory.Product, LineName = redCells.Name, ProductTypeId = redCells.Id, OrderType = OrderType.Other, FulfillmentStatus = FulfillmentStatus.Ordered });
        await context.SaveChangesAsync(ct);
    }
}
