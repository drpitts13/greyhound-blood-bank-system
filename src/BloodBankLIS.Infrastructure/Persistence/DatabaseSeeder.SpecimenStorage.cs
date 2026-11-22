using BloodBankLIS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Infrastructure.Persistence;

public static partial class DatabaseSeeder
{
    /// <summary>
    /// Store tubes demo: the transfusion reaction demo's pre-transfusion tube (ACC0014) was put in the bench rack and then
    /// moved to the retention refrigerator, so its chart, timeline, the reaction page, and the lookup show where it is and
    /// where it was. Added once.
    /// </summary>
    private static async Task EnsureSpecimenStorageDemoAsync(BloodBankDbContext context, CancellationToken ct)
    {
        var tube = await context.Specimens.FirstOrDefaultAsync(s => s.AccessionNumber == "ACC0014", ct);
        if (tube is null || await context.SpecimenStorageEvents.AnyAsync(e => e.SpecimenId == tube.Id, ct))
        {
            return;
        }

        var bench = tube.CollectedUtc.AddHours(2);
        var retention = tube.CollectedUtc.AddHours(14);
        context.SpecimenStorageEvents.AddRange(
            new SpecimenStorageEvent { SpecimenId = tube.Id, Location = "Bench rack 1", StoredUtc = bench, StoredBy = "DEMO_TECH", Note = "B3" },
            new SpecimenStorageEvent
            {
                SpecimenId = tube.Id, Location = "Retention fridge R2", PreviousLocation = "Bench rack 1", StoredUtc = retention,
                StoredBy = "DEMO_TECH", Note = "End of shift"
            });
        tube.StorageLocation = "Retention fridge R2";
        tube.StoredUtc = retention;
        tube.StoredBy = "DEMO_TECH";
        await context.SaveChangesAsync(ct);
    }
}
