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

    /// <summary>
    /// Scheduled-surgery demo: one red cell unit for MRN0022, needed a day after that patient's latest usable specimen
    /// expires, so Blood orders to prepare shows "Expires before needed-by" with "Collect a new specimen" as the next step.
    /// Added once; skipped when the patient has no usable specimen with an expiry.
    /// </summary>
    private static async Task EnsureSurgeryOrderDemoAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.Orders.AnyAsync(o => o.OrderNumber == "ORD-SURG-DEMO", ct))
        {
            return;
        }

        var patient = await context.Patients.FirstOrDefaultAsync(p => p.MedicalRecordNumber == "MRN0022", ct);
        var redCells = await context.ProductTypes.FirstOrDefaultAsync(p => p.ProductCode == "RBC-LR", ct);
        var encounter = patient is null ? null : await context.Encounters.OrderBy(e => e.Id).FirstOrDefaultAsync(e => e.PatientId == patient.Id, ct);
        var location = await context.OrderingLocations.OrderBy(l => l.Id).FirstOrDefaultAsync(ct);
        if (patient is null || redCells is null || encounter is null || location is null)
        {
            return;
        }

        // The same usable specimens the worklist considers; the latest-expiring one must end before the surgery.
        var now = DateTime.UtcNow;
        var usable = await context.Specimens
            .Where(s => s.PatientId == patient.Id
                && (s.Status == SpecimenStatus.Accepted || s.Status == SpecimenStatus.Received)
                && s.DiscardedUtc == null)
            .ToListAsync(ct);
        usable = usable.Where(s => s.ExpiresUtc is null || s.ExpiresUtc > now).ToList();
        if (usable.Count == 0 || usable.Any(s => s.ExpiresUtc is null))
        {
            return;
        }

        var order = new Order
        {
            OrderNumber = "ORD-SURG-DEMO",
            PatientId = patient.Id,
            EncounterId = encounter.Id,
            OrderingLocationId = location.Id,
            OrderCategory = OrderCategory.Product,
            OrderName = "Red Blood Cells",
            OrderType = OrderType.Other,
            ProductTypeId = redCells.Id,
            Priority = OrderPriority.Routine,
            Status = OrderStatus.New,
            FulfillmentStatus = FulfillmentStatus.Ordered,
            OrderedUtc = now.AddMinutes(-20),
            NeededByUtc = usable.Max(s => s.ExpiresUtc!.Value).AddDays(1),
            OrderingProvider = "Dr. Demo Surgeon",
            Comment = "Demo: scheduled surgery after the current specimen expires; a new specimen is needed."
        };
        context.Orders.Add(order);
        await context.SaveChangesAsync(ct);
        context.OrderLines.Add(new OrderLine
        {
            OrderId = order.Id, LineNumber = 1, LineCategory = OrderCategory.Product, LineName = redCells.Name, ProductTypeId = redCells.Id,
            OrderType = OrderType.Other, FulfillmentStatus = FulfillmentStatus.Ordered
        });
        await context.SaveChangesAsync(ct);
    }
}
