using BloodBankLIS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Infrastructure.Persistence;

public static partial class DatabaseSeeder
{
    private const string EquipmentDemoNote = "Demo interval only: set each check's interval from the facility's SOP or the manufacturer.";

    /// <summary>
    /// Equipment register demo (Cycle 708): a centrifuge whose check is overdue, a cell washer due today, and a heat
    /// block out of service after a failed check, so Equipment shows each state. The intervals are examples, not
    /// recommendations. Added once.
    /// </summary>
    private static async Task EnsureEquipmentDemoAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.Equipment.AnyAsync(ct))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var centrifuge = new Equipment { Code = "CENT-01", Name = "Serologic centrifuge", Kind = "Centrifuge", Location = "Bench 1", SerialNumber = "DEMO-1001" };
        var washer = new Equipment { Code = "CW-01", Name = "Cell washer", Kind = "Cell washer", Location = "Bench 1", SerialNumber = "DEMO-2002" };
        var heatBlock = new Equipment
        {
            Code = "HB-01",
            Name = "Heat block",
            Kind = "Heat block",
            Location = "Bench 2",
            OutOfServiceUtc = now.AddHours(-3),
            OutOfServiceReason = "Temperature check read 33.0 °C; service called (demo).",
            OutOfServiceBy = "demo"
        };
        context.Equipment.AddRange(centrifuge, washer, heatBlock);
        await context.SaveChangesAsync(ct);

        var timer = new EquipmentCheck { EquipmentId = centrifuge.Id, Name = "Timer and speed check", IntervalDays = 90, Instructions = EquipmentDemoNote };
        var daily = new EquipmentCheck { EquipmentId = washer.Id, Name = "Daily check", IntervalDays = 1, Instructions = EquipmentDemoNote };
        var temperature = new EquipmentCheck { EquipmentId = heatBlock.Id, Name = "Temperature check", IntervalDays = 1, Instructions = EquipmentDemoNote };
        context.EquipmentChecks.AddRange(timer, daily, temperature);
        await context.SaveChangesAsync(ct);

        context.EquipmentCheckRecords.AddRange(
            new EquipmentCheckRecord { EquipmentCheckId = timer.Id, EquipmentId = centrifuge.Id, PerformedUtc = now.AddDays(-95), PerformedBy = "demo", Passed = true, Note = "3400 rpm, timer within 5 s" },
            new EquipmentCheckRecord { EquipmentCheckId = daily.Id, EquipmentId = washer.Id, PerformedUtc = now.AddDays(-1), PerformedBy = "demo", Passed = true },
            new EquipmentCheckRecord { EquipmentCheckId = temperature.Id, EquipmentId = heatBlock.Id, PerformedUtc = now.AddHours(-3), PerformedBy = "demo", Passed = false, Note = "Read 33.0 °C; taken out of service, service called" });
        await context.SaveChangesAsync(ct);
    }
}
