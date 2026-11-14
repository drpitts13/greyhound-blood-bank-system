using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

/// <summary>OCD-074 (decided 2026-11-10): an infant's specimen collected during a visit is valid for the admission.</summary>
public class NeonatalSpecimenTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private SpecimenService Specimens(BloodBankDbContext c) =>
        new(new EfRepository<Specimen>(c), new EfRepository<Patient>(c), new EfRepository<SpecimenTypeDefinition>(c), c, _factory.Clock,
            policy: new FacilityPolicyService(new EfRepository<SystemSetting>(c)),
            audit: new AuditWriter(c, _factory.Clock, _factory.CurrentUser), currentUser: _factory.CurrentUser,
            encounters: new EfRepository<Encounter>(c));

    private EncounterService Encounters(BloodBankDbContext c) =>
        new(new EfRepository<Encounter>(c), new EfRepository<Patient>(c), new EfRepository<OrderingProvider>(c),
            new OrderingProviderService(new EfRepository<OrderingProvider>(c), c), c, _factory.Clock,
            currentUser: _factory.CurrentUser, audit: new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            specimens: new EfRepository<Specimen>(c));

    private async Task<(Patient Patient, Encounter Visit)> InfantAsync(BloodBankDbContext c, string mrn, int ageDays)
    {
        var patient = new Patient
        {
            MedicalRecordNumber = mrn, LastName = "Baby", FirstName = mrn,
            DateOfBirth = DateOnly.FromDateTime(_factory.Clock.UtcNow).AddDays(-ageDays), Sex = Sex.Female
        };
        c.Patients.Add(patient);
        await c.SaveChangesAsync();
        var visit = new Encounter
        {
            PatientId = patient.Id, VisitNumber = $"V-{mrn}", EncounterType = EncounterType.Inpatient,
            Status = EncounterStatus.Active, AdmitUtc = _factory.Clock.UtcNow.AddDays(-1)
        };
        c.Encounters.Add(visit);
        await c.SaveChangesAsync();
        return (patient, visit);
    }

    private static AccessionSpecimenRequest Req(string accession, Patient patient, DateTime collected, long visitId) =>
        new(accession, patient.Id, "EDTA", collected,
            Identifier1Type: IdentityTokenType.MedicalRecordNumber, Identifier1Value: patient.MedicalRecordNumber,
            Identifier2Type: IdentityTokenType.DateOfBirth, Identifier2Value: patient.DateOfBirth.ToString("yyyy-MM-dd"),
            EncounterId: visitId);

    private static void Neonatal(BloodBankDbContext c, bool enabled)
    {
        var row = c.SystemSettings.SingleOrDefault(s => s.Key == FacilityPolicyKeys.NeonatalRulesEnabled);
        if (row is null)
        {
            c.SystemSettings.Add(new SystemSetting { Key = FacilityPolicyKeys.NeonatalRulesEnabled, Value = enabled ? "true" : "false" });
        }
        else
        {
            row.Value = enabled ? "true" : "false";
        }

        c.SaveChanges();
    }

    // TEST-BB-871: with the neonatal rules off, an infant's specimen keeps the usual expiration. On, a specimen collected
    // from an infant during a visit is valid until the infant reaches the age limit (the visit still open), keeping the
    // usual expiration for discharge; an infant past the limit, or a specimen with no visit, keeps the usual rules.
    // Discharging the visit ends the specimen at discharge, never before its usual expiration; audited.
    [Fact]
    public async Task InfantSpecimen_IsValidForTheAdmission_UntilDischarge()
    {
        await using var c = _factory.Create();
        var collected = _factory.Clock.UtcNow.AddHours(-1);
        var (baby, visit) = await InfantAsync(c, "MRN-NEO-1", ageDays: 10);

        var off = await Specimens(c).AccessionAsync(Req("ACC-NEO-OFF", baby, collected, visit.Id));
        Assert.True(off.Succeeded, off.Error);
        Assert.Null(off.Value!.NeonatalStandardExpiresUtc);
        var usual = off.Value.ExpiresUtc!.Value;

        Neonatal(c, enabled: true);
        var on = await Specimens(c).AccessionAsync(Req("ACC-NEO-ON", baby, collected, visit.Id));
        Assert.True(on.Succeeded, on.Error);
        Assert.Equal(NeonatalTestingRule.AgeLimitReachedUtc(baby.DateOfBirth, 120), on.Value!.ExpiresUtc);
        Assert.Equal(usual, on.Value.NeonatalStandardExpiresUtc);
        Assert.True(SpecimenDto.From(on.Value).NeonatalAdmission);
        Assert.True(await c.AuditEvents.AnyAsync(a => a.EntityType == nameof(Specimen) && a.EntityId == on.Value.Id
            && a.Reason!.Contains("valid for the admission")));

        var (older, olderVisit) = await InfantAsync(c, "MRN-NEO-2", ageDays: 200);
        var past = await Specimens(c).AccessionAsync(Req("ACC-NEO-OLD", older, collected, olderVisit.Id));
        Assert.Null(past.Value!.NeonatalStandardExpiresUtc);

        // Discharged five days later: the specimen ends at discharge, or at its usual expiration if that is later.
        var discharge = _factory.Clock.UtcNow.AddDays(5);
        var updated = await Encounters(c).UpdateAsync(baby.Id, visit.Id, new UpdateEncounterRequest(
            EncounterType.Inpatient, EncounterStatus.Discharged, visit.AdmitUtc, discharge, null, null, null, null, null, null));
        Assert.True(updated.Succeeded, updated.Error);
        var ended = await c.Specimens.AsNoTracking().SingleAsync(s => s.Id == on.Value.Id);
        Assert.Equal(discharge > usual ? discharge : usual, ended.ExpiresUtc);
        Assert.True(await c.AuditEvents.AnyAsync(a => a.EntityType == nameof(Specimen) && a.EntityId == on.Value.Id
            && a.Reason!.Contains("discharged")));
        Assert.Equal(usual, (await c.Specimens.AsNoTracking().SingleAsync(s => s.Id == off.Value.Id)).ExpiresUtc);
    }
}
