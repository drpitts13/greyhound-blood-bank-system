using BloodBankLIS.Application.Patients;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class UserCommentPersistenceTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public UserCommentPersistenceTests(SqliteContextFactory factory) => _factory = factory;

    private PatientService Patients(BloodBankDbContext c) =>
        new(new EfRepository<Patient>(c), c, _factory.Clock,
            new SpecimenService(
                new EfRepository<Specimen>(c),
                new EfRepository<Patient>(c),
                new EfRepository<SpecimenTypeDefinition>(c),
                c,
                _factory.Clock));

    private SpecimenService Specimens(BloodBankDbContext c) =>
        new(new EfRepository<Specimen>(c), new EfRepository<Patient>(c), new EfRepository<SpecimenTypeDefinition>(c),
            c, _factory.Clock);

    private OrderService Orders(BloodBankDbContext c) =>
        new(new EfRepository<Order>(c), new EfRepository<OrderLine>(c), new EfRepository<OrderSpecimen>(c),
            new EfRepository<Encounter>(c), new EfRepository<OrderingLocation>(c), new EfRepository<Patient>(c),
            new EfRepository<Specimen>(c), new EfRepository<OrderingProvider>(c), new EfRepository<ProductType>(c),
            new EfRepository<TestDefinition>(c), new EfRepository<TestGrouper>(c), new FixedClock(DateTime.UtcNow), c,
            ruleEngine: null,
            audit: new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            results: new EfRepository<TestResult>(c));

    [Fact]
    public async Task Patient_CreateAndUpdate_PersistComment()
    {
        await using var c = _factory.Create();
        var mrn = $"MRN-CMT-{Guid.NewGuid():N}"[..20];
        var created = await Patients(c).CreateAsync(
            new CreatePatientRequest(mrn, "Comment", "Pat", null, new DateOnly(1984, 4, 4), Sex.Unknown,
                "See historical immunohematology."));
        Assert.True(created.Succeeded, created.Error);
        Assert.Equal("See historical immunohematology.", created.Value!.Comment);

        var updated = await Patients(c).UpdateAsync(created.Value.Id, new UpdatePatientRequest(
            "Comment", "Pat", null, new DateOnly(1984, 4, 4), Sex.Unknown, PatientStatus.Active,
            Comment: "See chart notes."));
        Assert.True(updated.Succeeded, updated.Error);
        Assert.Equal("See chart notes.", updated.Value!.Comment);
        Assert.Equal("See chart notes.", (await c.Patients.SingleAsync(p => p.Id == created.Value.Id)).Comment);
    }

    [Fact]
    public async Task Specimen_AccessionAndUpdate_PersistComment()
    {
        await using var c = _factory.Create();
        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-{Guid.NewGuid():N}",
            LastName = "Spec",
            FirstName = "Comment",
            DateOfBirth = new DateOnly(1991, 2, 2),
            Sex = Sex.Female
        };
        c.Patients.Add(patient);
        await c.SaveChangesAsync();

        var accessioned = await Specimens(c).AccessionAsync(new AccessionSpecimenRequest(
            $"ACC-{Guid.NewGuid():N}"[..16],
            patient.Id,
            "EDTA",
            _factory.Clock.UtcNow.AddHours(-1),
            Comment: "Specimen hemolyzed."));
        Assert.True(accessioned.Succeeded, accessioned.Error);
        Assert.Equal("Specimen hemolyzed.", accessioned.Value!.Comment);

        var updated = await Specimens(c).UpdateAsync(accessioned.Value.Id, new UpdateSpecimenRequest(
            accessioned.Value.CollectedUtc,
            Comment: "Quantity not sufficient."));
        Assert.True(updated.Succeeded, updated.Error);
        Assert.Equal("Quantity not sufficient.", updated.Value!.Comment);
    }

    [Fact]
    public async Task Order_CreateUpdate_AndLineComment_Persist()
    {
        await using var c = _factory.Create();
        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-{Guid.NewGuid():N}",
            LastName = "Ord",
            FirstName = "Comment",
            DateOfBirth = new DateOnly(1992, 3, 3),
            Sex = Sex.Male
        };
        c.Patients.Add(patient);
        var loc = new OrderingLocation { Code = $"LOC-{Guid.NewGuid():N}", Name = "Ward", IsActive = true };
        c.OrderingLocations.Add(loc);
        await c.SaveChangesAsync();

        var enc = await new EncounterService(
            new EfRepository<Encounter>(c),
            new EfRepository<Patient>(c),
            new EfRepository<OrderingProvider>(c),
            new OrderingProviderService(new EfRepository<OrderingProvider>(c), c),
            c,
            new FixedClock(DateTime.UtcNow)).CreateAsync(patient.Id, new CreateEncounterRequest(
            $"VIS-{Guid.NewGuid():N}"[..12], EncounterType.Inpatient, EncounterStatus.Active, DateTime.UtcNow, null,
            null, null, "4W", "4W", null, null, null, null));
        Assert.True(enc.Succeeded, enc.Error);

        var created = await Orders(c).CreateAsync(patient.Id, new CreateOrderRequest(
            enc.Value!.Id, loc.Id, $"ORD-{Guid.NewGuid():N}",
            [new OrderLineInputDto(OrderCategory.Test, "ABORH", null)],
            OrderPriority.Routine, DateTime.UtcNow, null, OrderSource.Manual, null, null,
            Comment: "Call results to the ordering location."));
        Assert.True(created.Succeeded, created.Error);
        Assert.Equal("Call results to the ordering location.", created.Value!.Comment);

        var listed = await Orders(c).ListByPatientAsync(patient.Id);
        Assert.Equal("Call results to the ordering location.", listed[0].Comment);

        var updated = await Orders(c).UpdateAsync(patient.Id, created.Value.Id, new UpdateOrderRequest(
            enc.Value.Id, loc.Id,
            [new OrderLineInputDto(OrderCategory.Test, "ABORH", null)],
            OrderPriority.Stat, null, Comment: "Requested as STAT."));
        Assert.True(updated.Succeeded, updated.Error);
        Assert.Equal("Requested as STAT.", updated.Value!.Comment);

        var line = await c.OrderLines.SingleAsync(l => l.OrderId == created.Value.Id && l.IsActive);
        var lineUpdated = await Orders(c).UpdateLineCommentAsync(
            patient.Id, created.Value.Id, line.Id, new UpdateOrderLineCommentRequest("See attached worksheet."));
        Assert.True(lineUpdated.Succeeded, lineUpdated.Error);
        Assert.Equal("See attached worksheet.", lineUpdated.Value!.Comment);
        Assert.Equal("See attached worksheet.", (await c.OrderLines.SingleAsync(l => l.Id == line.Id)).Comment);
    }
}
