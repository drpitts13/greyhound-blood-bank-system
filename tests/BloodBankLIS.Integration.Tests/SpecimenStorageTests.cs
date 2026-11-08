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

// TEST-BB-581: a tube is stored by scanning its accession or label barcode; a move keeps the earlier place (history and
// timeline), is audited, and needs specimen accession permission; unknown, cancelled, and same-place scans are refused.
public class SpecimenStorageTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public SpecimenStorageTests(SqliteContextFactory factory) => _factory = factory;

    private SpecimenStorageService Service(BloodBankDbContext c, IPermissionEvaluator? permissions = null) =>
        new(new EfRepository<Specimen>(c), new EfRepository<SpecimenStorageEvent>(c), new EfRepository<Patient>(c), c,
            _factory.Clock, _factory.CurrentUser, new AuditWriter(c, _factory.Clock, _factory.CurrentUser), permissions);

    [Fact]
    public async Task Tube_IsStoredByScan_AndAMoveKeepsWhereItWas()
    {
        await using var c = _factory.Create();
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var patient = new Patient { MedicalRecordNumber = $"MRN-STO-{key}", LastName = "Store", FirstName = "Tube", DateOfBirth = new DateOnly(1970, 1, 1) };
        c.Patients.Add(patient);
        await c.SaveChangesAsync();
        var tube = new Specimen
        {
            PatientId = patient.Id, AccessionNumber = $"ACC-STO-{key}", Barcode = $"BC-STO-{key}", SpecimenType = "EDTA",
            CollectedUtc = _factory.Clock.UtcNow.AddHours(-2), Status = SpecimenStatus.Accepted
        };
        var cancelled = new Specimen
        {
            PatientId = patient.Id, AccessionNumber = $"ACC-STX-{key}", SpecimenType = "EDTA",
            CollectedUtc = _factory.Clock.UtcNow.AddHours(-2), Status = SpecimenStatus.Cancelled
        };
        c.Specimens.AddRange(tube, cancelled);
        await c.SaveChangesAsync();

        var denied = Service(c, new FixedPermissionEvaluator(1, PermissionCodes.ResultEnter));
        Assert.False((await denied.StoreAsync(new StoreSpecimenRequest(tube.AccessionNumber, "Rack 3"))).Succeeded);

        var service = Service(c, new FixedPermissionEvaluator(1, PermissionCodes.SpecimenAccession));
        var first = await service.StoreAsync(new StoreSpecimenRequest($" bc-sto-{key.ToLowerInvariant()} ", " Rack 3 ", "A4"));
        Assert.True(first.Succeeded, first.Error);
        Assert.Equal(("Rack 3", null, "Store, Tube"), (first.Value!.Location, first.Value.PreviousLocation, first.Value.PatientName));

        Assert.Contains("already stored in Rack 3", (await service.StoreAsync(new StoreSpecimenRequest(tube.AccessionNumber, "rack 3"))).Error);
        Assert.Contains("No tube", (await service.StoreAsync(new StoreSpecimenRequest("ACC-NOPE", "Rack 3"))).Error);
        Assert.Contains("cancelled", (await service.StoreAsync(new StoreSpecimenRequest(cancelled.AccessionNumber, "Rack 3"))).Error);
        Assert.Contains("where the tube is stored", (await service.StoreAsync(new StoreSpecimenRequest(tube.AccessionNumber, " "))).Error);

        var moved = await service.StoreAsync(new StoreSpecimenRequest(tube.AccessionNumber, "Retention fridge 2"));
        Assert.True(moved.Succeeded, moved.Error);
        Assert.Equal("Rack 3", moved.Value!.PreviousLocation);

        var stored = await c.Specimens.AsNoTracking().SingleAsync(s => s.Id == tube.Id);
        Assert.Equal("Retention fridge 2", stored.StorageLocation);
        var history = await c.SpecimenStorageEvents.Where(e => e.SpecimenId == tube.Id).OrderBy(e => e.Id).ToListAsync();
        Assert.Equal(["Rack 3", "Retention fridge 2"], history.Select(h => h.Location));
        Assert.Equal("A4", history[0].Note);
        Assert.True(await c.AuditEvents.AnyAsync(a => a.EntityType == nameof(Specimen) && a.EntityId == tube.Id && a.Reason == "Tube moved from Rack 3 to Retention fridge 2."));

        var timeline = await new SpecimenTimelineService(new EfRepository<Specimen>(c), new EfRepository<TestResult>(c), new EfRepository<OrderSpecimen>(c),
            new EfRepository<Order>(c), new EfRepository<Crossmatch>(c), new EfRepository<Allocation>(c),
            new EfRepository<BloodUnit>(c), new EfRepository<Override>(c), new AuditQuery(c), _factory.Clock,
            new EfRepository<SpecimenStorageEvent>(c)).GetAsync(tube.Id);
        Assert.Contains(timeline!.Events, e => e.Title == "Stored in Rack 3" && e.Detail == "A4");
        Assert.Contains(timeline.Events, e => e.Title == "Moved to Retention fridge 2" && e.Detail == "From Rack 3");
    }

    [Fact]
    public void Rule_BoundsThePlace()
    {
        Assert.Equal(RuleSeverity.Pass, SpecimenStorageRule.Evaluate(SpecimenStatus.Rejected, null, "Rejected bin").Severity);
        Assert.Equal(RuleSeverity.HardStop, SpecimenStorageRule.Evaluate(SpecimenStatus.Accepted, null, new string('x', 101)).Severity);
    }
}
