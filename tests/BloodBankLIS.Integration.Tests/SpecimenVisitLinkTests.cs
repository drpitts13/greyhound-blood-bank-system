using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Infrastructure.Persistence;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-514: a tube accessioned for a patient is linked to the visit it was drawn on: the visit named (this patient's,
// not cancelled), else the patient's one open visit; with several open visits none is guessed.
public class SpecimenVisitLinkTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private SpecimenService Specimens(BloodBankDbContext c) =>
        new(new EfRepository<Specimen>(c), new EfRepository<Patient>(c), new EfRepository<SpecimenTypeDefinition>(c),
            c, _factory.Clock, currentUser: _factory.CurrentUser, encounters: new EfRepository<Encounter>(c));

    private async Task<(Patient Patient, Encounter Open)> SeedAsync(BloodBankDbContext c, string key)
    {
        var patient = new Patient { MedicalRecordNumber = "MRN-" + key, LastName = "Visit", FirstName = "Tube", DateOfBirth = new DateOnly(1975, 6, 7), Sex = Sex.Unknown };
        c.Patients.Add(patient);
        await c.SaveChangesAsync();
        var open = new Encounter { PatientId = patient.Id, VisitNumber = "V-OPEN-" + key, Status = EncounterStatus.Active, AdmitUtc = _factory.Clock.UtcNow.AddDays(-1) };
        c.Encounters.AddRange(open,
            new Encounter { PatientId = patient.Id, VisitNumber = "V-OLD-" + key, Status = EncounterStatus.Discharged, AdmitUtc = _factory.Clock.UtcNow.AddDays(-30), DischargeUtc = _factory.Clock.UtcNow.AddDays(-28) });
        await c.SaveChangesAsync();
        return (patient, open);
    }

    private AccessionSpecimenRequest Request(Patient p, string accession, long? visitId = null) =>
        new(accession, p.Id, "EDTA", _factory.Clock.UtcNow.AddMinutes(-15), EncounterId: visitId);

    [Fact]
    public async Task OneOpenVisit_IsTheTubesVisit_AndANamedVisitIsUsed()
    {
        await using var c = _factory.Create();
        var (patient, open) = await SeedAsync(c, "VL1");
        var service = Specimens(c);

        var inferred = await service.AccessionAsync(Request(patient, "ACC-VL-1"));
        Assert.True(inferred.Succeeded, inferred.Error);
        Assert.Equal(open.Id, inferred.Value!.EncounterId);
        Assert.Equal(open.Id, (await service.GetAsync(inferred.Value.Id))!.EncounterId);

        var old = c.Encounters.Single(e => e.VisitNumber == "V-OLD-VL1");
        var named = await service.AccessionAsync(Request(patient, "ACC-VL-2", old.Id));
        Assert.True(named.Succeeded, named.Error);
        Assert.Equal(old.Id, named.Value!.EncounterId);
    }

    [Fact]
    public async Task TwoOpenVisits_NoneIsGuessed()
    {
        await using var c = _factory.Create();
        var (patient, _) = await SeedAsync(c, "VL2");
        c.Encounters.Add(new Encounter { PatientId = patient.Id, VisitNumber = "V-ED-VL2", Status = EncounterStatus.Active, AdmitUtc = _factory.Clock.UtcNow.AddHours(-2) });
        await c.SaveChangesAsync();

        var r = await Specimens(c).AccessionAsync(Request(patient, "ACC-VL-3"));
        Assert.True(r.Succeeded, r.Error);
        Assert.Null(r.Value!.EncounterId);
    }

    [Fact]
    public async Task AnotherPatientsVisit_OrACancelledOne_IsRefused()
    {
        await using var c = _factory.Create();
        var (patient, _) = await SeedAsync(c, "VL3");
        var (other, otherOpen) = await SeedAsync(c, "VL4");
        var cancelled = new Encounter { PatientId = patient.Id, VisitNumber = "V-CXL-VL3", Status = EncounterStatus.Cancelled };
        c.Encounters.Add(cancelled);
        await c.SaveChangesAsync();
        var service = Specimens(c);

        var wrong = await service.AccessionAsync(Request(patient, "ACC-VL-4", otherOpen.Id));
        Assert.False(wrong.Succeeded);
        Assert.Contains("not this patient's", wrong.Error);

        var cxl = await service.AccessionAsync(Request(patient, "ACC-VL-5", cancelled.Id));
        Assert.False(cxl.Succeeded);
        Assert.Contains("V-CXL-VL3 is cancelled", cxl.Error);
        Assert.False(c.Specimens.Any(s => s.AccessionNumber == "ACC-VL-4" || s.AccessionNumber == "ACC-VL-5"));
        Assert.NotEqual(other.Id, patient.Id);
    }
}
