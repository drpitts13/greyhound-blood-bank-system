using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-588: a stored tube is discarded by scan once it is past its retention date, or early with a reason; a tube on an
// unfinished order or an open reaction investigation is kept; the place stays on its history, the discard is audited, and
// the due-for-discard list follows the facility's retention days (none when not set).
public class SpecimenDiscardTests
{
    private static SpecimenStorageService Service(SqliteContextFactory f, BloodBankDbContext c, IPermissionEvaluator? permissions = null) =>
        new(new EfRepository<Specimen>(c), new EfRepository<SpecimenStorageEvent>(c), new EfRepository<Patient>(c), c,
            f.Clock, f.CurrentUser, new AuditWriter(c, f.Clock, f.CurrentUser), permissions,
            new EfRepository<OrderSpecimen>(c), new EfRepository<Order>(c), new EfRepository<ReactionInvestigation>(c),
            new FacilityPolicyService(new EfRepository<SystemSetting>(c)));

    // TEST-BB-1377 (end-user testing pass 338): a stored tube past its retention date that is still kept (here, on an
    // unfinished order) is not listed as due for discard, since a discard would refuse it.
    [Fact]
    public async Task KeptTube_IsNotDueForDiscard()
    {
        using var factory = new SqliteContextFactory();
        await using (var db = factory.Create())
        {
            await DatabaseSeeder.SeedAsync(db);
        }

        await using var c = factory.Create();
        var now = factory.Clock.UtcNow;
        var patient = new Patient { MedicalRecordNumber = "MRN-DSC-K", LastName = "Kept", FirstName = "Tube", DateOfBirth = new DateOnly(1970, 1, 1) };
        var location = new OrderingLocation { Code = "LOC-DSC-K", Name = "BB", IsActive = true };
        c.AddRange(patient, location);
        await c.SaveChangesAsync();
        var encounter = new Encounter { PatientId = patient.Id, VisitNumber = "VIS-DSC-K", EncounterType = EncounterType.Inpatient, Status = EncounterStatus.Active, AdmitUtc = now.AddDays(-20) };
        var kept = new Specimen { PatientId = patient.Id, AccessionNumber = "ACC-DSC-KEPT", SpecimenType = "EDTA", Status = SpecimenStatus.Expired, CollectedUtc = now.AddDays(-13), ExpiresUtc = now.AddDays(-10) };
        var free = new Specimen { PatientId = patient.Id, AccessionNumber = "ACC-DSC-FREE", SpecimenType = "EDTA", Status = SpecimenStatus.Expired, CollectedUtc = now.AddDays(-13), ExpiresUtc = now.AddDays(-10) };
        c.AddRange(encounter, kept, free);
        await c.SaveChangesAsync();
        var order = new Order { PatientId = patient.Id, EncounterId = encounter.Id, OrderingLocationId = location.Id, OrderNumber = "ORD-DSC-K", OrderCategory = OrderCategory.Test, OrderName = "ABSC", Status = OrderStatus.New, OrderedUtc = now.AddDays(-13) };
        c.Orders.Add(order);
        await c.SaveChangesAsync();
        c.OrderSpecimens.Add(new OrderSpecimen { OrderId = order.Id, SpecimenId = kept.Id, IsPrimary = true });
        (await c.SystemSettings.SingleAsync(s => s.Key == FacilityPolicyKeys.SpecimenRetentionDays)).Value = "7";
        await c.SaveChangesAsync();

        var service = Service(factory, c, new FixedPermissionEvaluator(1, PermissionCodes.SpecimenAccession));
        Assert.True((await service.StoreAsync(new StoreSpecimenRequest(kept.AccessionNumber, "Rack K"))).Succeeded);
        Assert.True((await service.StoreAsync(new StoreSpecimenRequest(free.AccessionNumber, "Rack K"))).Succeeded);

        var due = (await service.ListDueForDiscardAsync()).Where(t => t.PatientId == patient.Id).ToList();
        Assert.Equal([free.Id], due.Select(t => t.SpecimenId));
        Assert.Equal("Kept: order ORD-DSC-K not finished", (await service.ListInPlaceAsync("Rack K")).Single(t => t.SpecimenId == kept.Id).KeptBecause);
    }

    [Fact]
    public async Task Tube_IsDiscardedWhenDue_OrEarlyWithAReason_AndKeptWhileStillNeeded()
    {
        using var factory = new SqliteContextFactory();
        await using (var db = factory.Create())
        {
            await DatabaseSeeder.SeedAsync(db);
        }

        await using var c = factory.Create();
        var now = factory.Clock.UtcNow;
        var patient = new Patient { MedicalRecordNumber = "MRN-DSC-1", LastName = "Discard", FirstName = "Tube", DateOfBirth = new DateOnly(1970, 1, 1) };
        c.Patients.Add(patient);
        await c.SaveChangesAsync();
        Specimen Tube(string accession, int expiredDaysAgo) => new()
        {
            PatientId = patient.Id, AccessionNumber = accession, SpecimenType = "EDTA", Status = SpecimenStatus.Accepted,
            CollectedUtc = now.AddDays(-expiredDaysAgo - 3), ExpiresUtc = now.AddDays(-expiredDaysAgo)
        };
        var old = Tube("ACC-DSC-OLD", 10);
        var recent = Tube("ACC-DSC-NEW", 1);
        c.Specimens.AddRange(old, recent);
        await c.SaveChangesAsync();

        var service = Service(factory, c, new FixedPermissionEvaluator(1, PermissionCodes.SpecimenAccession));
        Assert.True((await service.StoreAsync(new StoreSpecimenRequest(old.AccessionNumber, "Retention fridge R9"))).Succeeded);
        Assert.True((await service.StoreAsync(new StoreSpecimenRequest(recent.AccessionNumber, "Retention fridge R9"))).Succeeded);

        // No retention period set: nothing is listed and a discard needs a reason.
        Assert.Empty(await service.ListDueForDiscardAsync());
        Assert.Contains("No retention period is set", (await service.DiscardAsync(new DiscardSpecimenRequest(old.AccessionNumber))).Error);

        // The seeder writes the catalog default (0); the facility sets 7 days.
        var setting = await c.SystemSettings.SingleAsync(s => s.Key == FacilityPolicyKeys.SpecimenRetentionDays);
        Assert.Equal("0", setting.Value);
        setting.Value = "7";
        await c.SaveChangesAsync();

        var due = Assert.Single(await service.ListDueForDiscardAsync(), t => t.PatientId == patient.Id);
        Assert.Equal((old.AccessionNumber, "Retention fridge R9", old.ExpiresUtc!.Value.AddDays(7)), (due.AccessionNumber, due.Location, due.KeepUntilUtc!.Value));
        var inFridge = await service.ListInPlaceAsync("Retention fridge R9");
        Assert.Equal(recent.ExpiresUtc!.Value.AddDays(7), inFridge.Single(t => t.SpecimenId == recent.Id).KeepUntilUtc);

        Assert.False((await Service(factory, c, new FixedPermissionEvaluator(1, PermissionCodes.ResultEnter))
            .DiscardAsync(new DiscardSpecimenRequest(old.AccessionNumber))).Succeeded);

        var discarded = await service.DiscardAsync(new DiscardSpecimenRequest(old.AccessionNumber.ToLowerInvariant()));
        Assert.True(discarded.Succeeded, discarded.Error);
        Assert.Equal(("Retention fridge R9", (string?)null), (discarded.Value!.PreviousLocation, discarded.Value.EarlyReason));
        Assert.Contains("already discarded", (await service.DiscardAsync(new DiscardSpecimenRequest(old.AccessionNumber))).Error);
        Assert.Contains("was discarded", (await service.StoreAsync(new StoreSpecimenRequest(old.AccessionNumber, "Rack 1"))).Error);
        Assert.DoesNotContain(await service.ListDueForDiscardAsync(), t => t.SpecimenId == old.Id);
        Assert.DoesNotContain(await service.ListInPlaceAsync("Retention fridge R9"), t => t.SpecimenId == old.Id);

        var row = await c.Specimens.AsNoTracking().SingleAsync(s => s.Id == old.Id);
        Assert.Equal((null, now, "tech-test"), (row.StorageLocation, row.DiscardedUtc, row.DiscardedBy));
        var last = await c.SpecimenStorageEvents.AsNoTracking().Where(e => e.SpecimenId == old.Id).OrderByDescending(e => e.Id).FirstAsync();
        Assert.True(last.Discarded);
        Assert.Equal("Retention fridge R9", last.PreviousLocation);
        Assert.True(await c.AuditEvents.AnyAsync(a => a.EntityType == nameof(Specimen) && a.EntityId == old.Id && a.Reason == "Tube discarded from Retention fridge R9."));
        Assert.DoesNotContain("Discarded", await service.KnownPlacesAsync());

        // Before its keep-until date: a reason is needed and kept.
        Assert.Contains("give a reason to discard it early", (await service.DiscardAsync(new DiscardSpecimenRequest(recent.AccessionNumber, " "))).Error);
        var early = await service.DiscardAsync(new DiscardSpecimenRequest(recent.AccessionNumber, " Duplicate draw "));
        Assert.True(early.Succeeded, early.Error);
        Assert.Equal("Duplicate draw", early.Value!.EarlyReason);
        Assert.Equal("Duplicate draw", (await c.Specimens.AsNoTracking().SingleAsync(s => s.Id == recent.Id)).DiscardReason);
        Assert.True(await c.AuditEvents.AnyAsync(a => a.EntityId == recent.Id && a.Reason == "Tube discarded early from Retention fridge R9: Duplicate draw"));

        var timeline = await new SpecimenTimelineService(new EfRepository<Specimen>(c), new EfRepository<TestResult>(c), new EfRepository<OrderSpecimen>(c),
            new EfRepository<Order>(c), new EfRepository<Crossmatch>(c), new EfRepository<Allocation>(c),
            new EfRepository<BloodUnit>(c), new EfRepository<Override>(c), new AuditQuery(c), factory.Clock,
            new EfRepository<SpecimenStorageEvent>(c)).GetAsync(recent.Id);
        Assert.Contains(timeline!.Events, e => e.Title == "Tube discarded" && e.Detail == "From Retention fridge R9 · Early: Duplicate draw");

        // The demo reaction's pre-transfusion tube is on an open investigation: kept, whatever the reason.
        var reactionTube = await c.Specimens.AsNoTracking().SingleAsync(s => s.AccessionNumber == "ACC0014");
        Assert.True(await c.ReactionInvestigations.AnyAsync(r => r.PreTransfusionSpecimenId == reactionTube.Id && r.Status != ReactionInvestigationStatus.Closed));
        Assert.Contains("reaction investigation that is still open",
            (await service.DiscardAsync(new DiscardSpecimenRequest("ACC0014", "Rack clean-out"))).Error);

        // A tube on an unfinished order is kept.
        var investigated = (await c.ReactionInvestigations.AsNoTracking().ToListAsync())
            .SelectMany(r => new[] { r.PreTransfusionSpecimenId, r.PostTransfusionSpecimenId }).OfType<long>().ToHashSet();
        var openOrderTube = await (from os in c.OrderSpecimens
                                   join o in c.Orders on os.OrderId equals o.Id
                                   join s in c.Specimens on os.SpecimenId equals s.Id
                                   where o.Status == OrderStatus.New || o.Status == OrderStatus.InProcess
                                   select new { s.Id, s.AccessionNumber, o.OrderNumber }).ToListAsync();
        var onOrder = openOrderTube.First(t => !investigated.Contains(t.Id));
        var refused = await service.DiscardAsync(new DiscardSpecimenRequest(onOrder.AccessionNumber, "Rack clean-out"));
        Assert.Contains($"{onOrder.OrderNumber}", refused.Error);
        Assert.Contains("not finished", refused.Error);
    }

    // TEST-BB-1430: facility policy Discard tubes automatically when storage ends. Off (the default) nothing is discarded;
    // on, each tube past its storage days is recorded as discarded by the system, a kept tube stays, and the discarded
    // tubes are listed with their former place to pull.
    [Fact]
    public async Task AutoDiscard_RecordsDueTubesAsDiscarded_OnlyWhenTheFacilityTurnsItOn()
    {
        using var factory = new SqliteContextFactory();
        await using (var db = factory.Create())
        {
            await DatabaseSeeder.SeedAsync(db);
        }

        await using var c = factory.Create();
        var now = factory.Clock.UtcNow;
        var patient = new Patient { MedicalRecordNumber = "MRN-DSC-AUTO", LastName = "Auto", FirstName = "Tube", DateOfBirth = new DateOnly(1970, 1, 1) };
        var location = new OrderingLocation { Code = "LOC-DSC-A", Name = "BB", IsActive = true };
        c.AddRange(patient, location);
        await c.SaveChangesAsync();
        var encounter = new Encounter { PatientId = patient.Id, VisitNumber = "VIS-DSC-A", EncounterType = EncounterType.Inpatient, Status = EncounterStatus.Active, AdmitUtc = now.AddDays(-20) };
        Specimen Tube(string accession, int expiredDaysAgo) => new()
        {
            PatientId = patient.Id, AccessionNumber = accession, SpecimenType = "EDTA", Status = SpecimenStatus.Expired,
            CollectedUtc = now.AddDays(-expiredDaysAgo - 3), ExpiresUtc = now.AddDays(-expiredDaysAgo)
        };
        var due = Tube("ACC-AUTO-DUE", 10);
        var kept = Tube("ACC-AUTO-KEPT", 10);
        var recent = Tube("ACC-AUTO-NEW", 1);
        c.AddRange(encounter, due, kept, recent);
        await c.SaveChangesAsync();
        var order = new Order { PatientId = patient.Id, EncounterId = encounter.Id, OrderingLocationId = location.Id, OrderNumber = "ORD-AUTO-K", OrderCategory = OrderCategory.Test, OrderName = "ABSC", Status = OrderStatus.New, OrderedUtc = now.AddDays(-13) };
        c.Orders.Add(order);
        await c.SaveChangesAsync();
        c.OrderSpecimens.Add(new OrderSpecimen { OrderId = order.Id, SpecimenId = kept.Id, IsPrimary = true });
        (await c.SystemSettings.SingleAsync(s => s.Key == FacilityPolicyKeys.SpecimenRetentionDays)).Value = "7";
        await c.SaveChangesAsync();

        var service = Service(factory, c, new FixedPermissionEvaluator(1, PermissionCodes.SpecimenAccession));
        foreach (var tube in new[] { due, kept, recent })
        {
            Assert.True((await service.StoreAsync(new StoreSpecimenRequest(tube.AccessionNumber, "Rack A"))).Succeeded);
        }

        // The catalog default is off: nothing is discarded.
        Assert.Equal("false", (await c.SystemSettings.SingleAsync(s => s.Key == FacilityPolicyKeys.SpecimenAutoDiscard)).Value);
        Assert.Equal(0, await service.AutoDiscardDueAsync());

        (await c.SystemSettings.SingleAsync(s => s.Key == FacilityPolicyKeys.SpecimenAutoDiscard)).Value = "true";
        await c.SaveChangesAsync();
        Assert.Equal(1, await service.AutoDiscardDueAsync());
        Assert.Equal(0, await service.AutoDiscardDueAsync());

        var rows = await c.Specimens.AsNoTracking().Where(s => s.PatientId == patient.Id).ToDictionaryAsync(s => s.Id);
        Assert.Equal((null, now, SpecimenDiscardRule.AutomaticBy), (rows[due.Id].StorageLocation, rows[due.Id].DiscardedUtc, rows[due.Id].DiscardedBy));
        Assert.Null(rows[kept.Id].DiscardedUtc);
        Assert.Null(rows[recent.Id].DiscardedUtc);
        var pulled = Assert.Single(await service.ListAutoDiscardedAsync(), t => t.PatientId == patient.Id);
        Assert.Equal((due.AccessionNumber, "Rack A"), (pulled.AccessionNumber, pulled.Location));
        Assert.True(await c.AuditEvents.AnyAsync(a => a.EntityId == due.Id && a.Reason != null && a.Reason.Contains("discarded automatically from Rack A")));
    }
}
