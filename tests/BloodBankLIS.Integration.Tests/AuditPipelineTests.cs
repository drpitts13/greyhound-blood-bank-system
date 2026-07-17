using BloodBankLIS.Domain.Audit;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class AuditPipelineTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public AuditPipelineTests(SqliteContextFactory factory) => _factory = factory;

    private static Patient NewPatient(string mrn) => new()
    {
        MedicalRecordNumber = mrn,
        LastName = "Audit",
        FirstName = "Test",
        DateOfBirth = new DateOnly(1975, 6, 15),
        Sex = Sex.Male
    };

    [Fact]
    public async Task Create_WritesCreateAuditEvent_WithMetadata()
    {
        await using var context = _factory.Create();
        var patient = NewPatient("MRN-AUD-1");

        context.Patients.Add(patient);
        await context.SaveChangesAsync();

        var audit = await context.AuditEvents
            .Where(a => a.EntityType == nameof(Patient) && a.EntityId == patient.Id)
            .SingleAsync();

        Assert.Equal(AuditEventType.Create, audit.EventType);
        Assert.Equal("tech-test", audit.UserName);
        Assert.Equal("WORKSTATION-1", audit.Workstation);
        Assert.Equal(_factory.Clock.UtcNow, audit.OccurredUtc);
        Assert.Null(audit.OldValueJson);
        Assert.NotNull(audit.NewValueJson);
        Assert.Contains("MRN-AUD-1", audit.NewValueJson!);
    }

    [Fact]
    public async Task CreatedMetadata_IsStamped()
    {
        await using var context = _factory.Create();
        var patient = NewPatient("MRN-AUD-2");

        context.Patients.Add(patient);
        await context.SaveChangesAsync();

        Assert.Equal(_factory.Clock.UtcNow, patient.CreatedUtc);
        Assert.Equal("tech-test", patient.CreatedBy);
    }

    [Fact]
    public async Task Update_WritesUpdateAuditEvent_WithOldAndNewValues()
    {
        long id;
        await using (var context = _factory.Create())
        {
            var patient = NewPatient("MRN-AUD-3");
            context.Patients.Add(patient);
            await context.SaveChangesAsync();
            id = patient.Id;
        }

        await using (var context = _factory.Create())
        {
            var patient = await context.Patients.SingleAsync(p => p.Id == id);
            patient.LastName = "Changed";
            await context.SaveChangesAsync();
        }

        await using (var context = _factory.Create())
        {
            var update = await context.AuditEvents
                .Where(a => a.EntityType == nameof(Patient) && a.EntityId == id && a.EventType == AuditEventType.Update)
                .SingleAsync();

            Assert.NotNull(update.OldValueJson);
            Assert.Contains("Audit", update.OldValueJson!);     // original last name
            Assert.Contains("Changed", update.NewValueJson!);   // new last name

            var modifiedBy = await context.Patients.Where(p => p.Id == id).Select(p => p.ModifiedBy).SingleAsync();
            Assert.Equal("tech-test", modifiedBy);
        }
    }

    [Fact]
    public async Task DuplicateMrn_ViolatesUniqueIndex()
    {
        await using var context = _factory.Create();
        context.Patients.Add(NewPatient("MRN-DUP"));
        await context.SaveChangesAsync();

        context.Patients.Add(NewPatient("MRN-DUP"));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
}
