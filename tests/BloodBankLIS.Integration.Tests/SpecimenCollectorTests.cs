using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-513 (OCD-064): with the facility setting on, a tube is accepted only once its collector is recorded;
// collecting and receiving are not blocked, and with the setting off nothing changes.
public class SpecimenCollectorTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private SpecimenService Specimens(BloodBankDbContext c) =>
        new(new EfRepository<Specimen>(c), new EfRepository<Patient>(c), new EfRepository<SpecimenTypeDefinition>(c),
            c, _factory.Clock, currentUser: _factory.CurrentUser,
            audit: new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            policy: new FacilityPolicyService(new EfRepository<SystemSetting>(c)));

    private async Task RequireCollectorAsync()
    {
        await using var c = _factory.Create();
        c.SystemSettings.Add(new SystemSetting { Key = FacilityPolicyKeys.RequireSpecimenCollector, Value = "true" });
        await c.SaveChangesAsync();
    }

    private async Task<Patient> PatientAsync(string key)
    {
        await using var c = _factory.Create();
        var patient = new Patient { MedicalRecordNumber = "MRN-" + key, LastName = "Draw", FirstName = "Tube", DateOfBirth = new DateOnly(1985, 3, 4), Sex = Sex.Unknown };
        c.Patients.Add(patient);
        await c.SaveChangesAsync();
        return patient;
    }

    private AccessionSpecimenRequest Request(Patient p, string accession, string? collector, bool acceptNow) =>
        new(accession, p.Id, "EDTA", _factory.Clock.UtcNow.AddMinutes(-30), Collector: collector,
            Identifier1Value: p.MedicalRecordNumber, Identifier2Value: p.DateOfBirth.ToString("yyyy-MM-dd"), AcceptImmediately: acceptNow);

    [Fact]
    public async Task Required_AcceptIsRefusedUntilTheCollectorIsRecorded()
    {
        await RequireCollectorAsync();
        var patient = await PatientAsync("COLL-1");
        await using var c = _factory.Create();
        var service = Specimens(c);

        var acceptNow = await service.AccessionAsync(Request(patient, "ACC-COLL-NOW", null, acceptNow: true));
        Assert.False(acceptNow.Succeeded);
        Assert.Contains(SpecimenCollectorRule.Code, acceptNow.Error);
        Assert.False(await c.Specimens.AnyAsync(s => s.AccessionNumber == "ACC-COLL-NOW"));

        // Collecting and receiving are not blocked.
        var collected = await service.AccessionAsync(Request(patient, "ACC-COLL-1", " ", acceptNow: false));
        Assert.True(collected.Succeeded, collected.Error);
        Assert.True((await service.ReceiveByScanAsync("ACC-COLL-1")).Succeeded);

        var refused = await service.AcceptByScanAsync("ACC-COLL-1");
        Assert.False(refused.Succeeded);
        Assert.StartsWith("ACC-COLL-1: " + SpecimenCollectorRule.Code, refused.Error);
        Assert.Equal(SpecimenStatus.Received, (await c.Specimens.AsNoTracking().SingleAsync(s => s.AccessionNumber == "ACC-COLL-1")).Status);

        var tube = await c.Specimens.AsNoTracking().SingleAsync(s => s.AccessionNumber == "ACC-COLL-1");
        Assert.Null((await service.ListPendingReceiptAsync()).Single(r => r.SpecimenId == tube.Id).Collector);
        Assert.False((await service.RecordCollectorAsync(tube.Id, "  ")).Succeeded);
        var recorded = await service.RecordCollectorAsync(tube.Id, " RN Patel ");
        Assert.True(recorded.Succeeded, recorded.Error);
        Assert.Equal("RN Patel", (await service.ListPendingReceiptAsync()).Single(r => r.SpecimenId == tube.Id).Collector);
        Assert.True(await c.AuditEvents.AnyAsync(a => a.EntityType == nameof(Specimen) && a.EntityId == tube.Id
            && a.Reason == "Collector recorded before acceptance." && a.NewValueJson!.Contains("RN Patel")));
        Assert.True((await service.AcceptAsync(tube.Id)).Succeeded);
        // Once accepted, the collector is changed through the specimen edit, not here.
        Assert.False((await service.RecordCollectorAsync(tube.Id, "Someone else")).Succeeded);

        var withCollector = await service.AccessionAsync(Request(patient, "ACC-COLL-2", "RN Patel", acceptNow: true));
        Assert.True(withCollector.Succeeded, withCollector.Error);
        Assert.Equal(SpecimenStatus.Accepted, withCollector.Value!.Status);
    }

    [Fact]
    public async Task NotRequired_ByDefault_ATubeWithoutACollectorIsAccepted()
    {
        var patient = await PatientAsync("COLL-OFF");
        await using var c = _factory.Create();
        var r = await Specimens(c).AccessionAsync(Request(patient, "ACC-COLL-OFF", null, acceptNow: true));
        Assert.True(r.Succeeded, r.Error);
        Assert.Equal(SpecimenStatus.Accepted, r.Value!.Status);
    }
}
