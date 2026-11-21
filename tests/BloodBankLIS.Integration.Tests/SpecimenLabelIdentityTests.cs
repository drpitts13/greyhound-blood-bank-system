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

// TEST-BB-349: accession records the identifiers read from the tube label, never copies them from the record,
// and records who drew the specimen and where.
public class SpecimenLabelIdentityTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private SpecimenService Specimens(BloodBankDbContext c) =>
        new(new EfRepository<Specimen>(c), new EfRepository<Patient>(c), new EfRepository<SpecimenTypeDefinition>(c),
            c, _factory.Clock, currentUser: _factory.CurrentUser,
            audit: new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            policy: new FacilityPolicyService(new EfRepository<SystemSetting>(c)));

    private async Task<Patient> PatientAsync(string mrn)
    {
        await using var c = _factory.Create();
        var patient = new Patient { MedicalRecordNumber = mrn, LastName = "Label", FirstName = "Tube", DateOfBirth = new DateOnly(1981, 3, 9), Sex = Sex.Unknown };
        c.Patients.Add(patient);
        await c.SaveChangesAsync();
        return patient;
    }

    private AccessionSpecimenRequest Request(string accession, Patient p, string? id1, string? id2) =>
        new(accession, p.Id, "EDTA", _factory.Clock.UtcNow.AddMinutes(-30),
            Identifier1Value: id1, Identifier2Value: id2, Collector: "RN Okafor", DrawLocation: "5E Cardiac");

    [Fact]
    public async Task WithoutLabelIdentifiers_IsRefused_AndNothingIsAccessioned()
    {
        var patient = await PatientAsync("MRN-LBL-NONE");
        await using var c = _factory.Create();
        var r = await Specimens(c).AccessionAsync(Request("ACC-LBL-NONE", patient, null, null));
        Assert.False(r.Succeeded);
        Assert.Contains(SpecimenLabelIdentityRule.Code, r.Error);
        Assert.False(await c.Specimens.AnyAsync(s => s.AccessionNumber == "ACC-LBL-NONE"));
    }

    [Fact]
    public async Task LabelIdentifiersThatDoNotMatch_AreRefused()
    {
        var patient = await PatientAsync("MRN-LBL-WRONG");
        await using var c = _factory.Create();
        var r = await Specimens(c).AccessionAsync(Request("ACC-LBL-WRONG", patient, "MRN-SOMEONE-ELSE", "1981-03-09"));
        Assert.False(r.Succeeded);
        Assert.False(await c.Specimens.AnyAsync(s => s.AccessionNumber == "ACC-LBL-WRONG"));
    }

    [Fact]
    public async Task MatchingLabelIdentifiers_AreRecorded_WithCollectorAndDrawLocation()
    {
        var patient = await PatientAsync("MRN-LBL-OK");
        await using var c = _factory.Create();
        var r = await Specimens(c).AccessionAsync(Request("ACC-LBL-OK", patient, "MRN-LBL-OK", "1981-03-09"));
        Assert.True(r.Succeeded, r.Error);
        var s = await c.Specimens.AsNoTracking().SingleAsync(x => x.AccessionNumber == "ACC-LBL-OK");
        Assert.Equal("MRN-LBL-OK", s.Identifier1Value);
        Assert.Equal("RN Okafor", s.Collector);
        Assert.Equal("5E Cardiac", s.DrawLocation);
        Assert.True(await c.AuditEvents.AnyAsync(a => a.EntityType == nameof(Specimen) && a.EntityId == s.Id && a.NewValueJson!.Contains("RN Okafor")));
    }

    [Fact]
    public async Task WhenTheFacilityPrefills_TheRecordIsUsed()
    {
        var patient = await PatientAsync("MRN-LBL-PRE");
        await using var c = _factory.Create();
        c.SystemSettings.Add(new SystemSetting { Key = FacilityPolicyKeys.PrefillPatientIdentifiers, Value = "true" });
        await c.SaveChangesAsync();
        var r = await Specimens(c).AccessionAsync(Request("ACC-LBL-PRE", patient, null, null));
        Assert.True(r.Succeeded, r.Error);
    }
}
