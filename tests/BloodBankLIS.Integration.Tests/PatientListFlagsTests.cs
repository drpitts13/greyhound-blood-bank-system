using BloodBankLIS.Application.Patients;
using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Infrastructure.Persistence;

namespace BloodBankLIS.Integration.Tests;

public class PatientListFlagsTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public PatientListFlagsTests(SqliteContextFactory factory) => _factory = factory;

    private PatientService Service(BloodBankDbContext c) =>
        new(
            new EfRepository<Patient>(c),
            c,
            _factory.Clock,
            new SpecimenService(new EfRepository<Specimen>(c), new EfRepository<Patient>(c), new EfRepository<SpecimenTypeDefinition>(c),
                c, _factory.Clock, currentUser: _factory.CurrentUser),
            bloodTypes: new EfRepository<PatientBloodTypeHistory>(c),
            antibodies: new EfRepository<AntibodyHistory>(c),
            requirements: new EfRepository<SpecialTransfusionRequirement>(c),
            requirementDefinitions: new EfRepository<SpecialRequirementDefinition>(c),
            encounters: new EfRepository<Encounter>(c));

    // TEST-BB-505: the list says where the patient is (the current open visit), flags another open visit, and says
    // nothing for a patient with no open visit.
    [Fact]
    public async Task PatientList_ShowsWhereThePatientIs_FromTheCurrentOpenVisit()
    {
        await using var c = _factory.Create();
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var now = _factory.Clock.UtcNow;
        var admitted = new Patient { MedicalRecordNumber = $"MRN-LOC-{key}", LastName = $"Located{key}", FirstName = "Pat", DateOfBirth = new DateOnly(1970, 1, 1) };
        var home = new Patient { MedicalRecordNumber = $"MRN-HOME-{key}", LastName = $"Home{key}", FirstName = "Pat", DateOfBirth = new DateOnly(1971, 1, 1) };
        c.Patients.AddRange(admitted, home);
        await c.SaveChangesAsync();
        c.Encounters.AddRange(
            new Encounter { PatientId = admitted.Id, VisitNumber = $"V-OLD-{key}", EncounterType = EncounterType.Inpatient, Status = EncounterStatus.Active, AdmitUtc = now.AddDays(-5), AdmissionLocation = "4W" },
            new Encounter { PatientId = admitted.Id, VisitNumber = $"V-NEW-{key}", EncounterType = EncounterType.Inpatient, Status = EncounterStatus.Active, AdmitUtc = now.AddHours(-3), AdmissionLocation = "ED", CurrentLocation = "ICU 4" },
            new Encounter { PatientId = admitted.Id, VisitNumber = $"V-GONE-{key}", EncounterType = EncounterType.Outpatient, Status = EncounterStatus.Discharged, AdmitUtc = now.AddHours(-1), DischargeUtc = now },
            new Encounter { PatientId = home.Id, VisitNumber = $"V-HOME-{key}", EncounterType = EncounterType.Outpatient, Status = EncounterStatus.Active, AdmitUtc = now.AddDays(-9), DischargeUtc = now.AddDays(-8) });
        await c.SaveChangesAsync();

        var list = await Service(c).ListDtosAsync();

        var a = list.Single(p => p.Id == admitted.Id);
        Assert.Equal($"V-NEW-{key}", a.CurrentVisitNumber);
        Assert.Equal("Inpatient · ICU 4", a.CurrentVisitWhere);
        Assert.Equal(1, a.OtherOpenVisits);
        Assert.Equal($"Located{key}, Pat (MRN-LOC-{key}) · Inpatient · ICU 4 (+1 more open) · no type on file", a.PickerLabel);

        // An active visit that has a discharge time is not open.
        var h = list.Single(p => p.Id == home.Id);
        Assert.Null(h.CurrentVisitNumber);
        Assert.Null(h.CurrentVisitWhere);
        Assert.Equal(0, h.OtherOpenVisits);
    }

    // TEST-BB-436: the patient list shows the current ABO/Rh, active antibodies, and requirements in effect only.
    [Fact]
    public async Task PatientList_ShowsCurrentType_ActiveAntibodies_AndRequirementsInEffect()
    {
        await using var c = _factory.Create();
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var now = _factory.Clock.UtcNow;
        var flagged = new Patient { MedicalRecordNumber = $"MRN-FLAG-{key}", LastName = $"Flagged{key}", FirstName = "Pat", DateOfBirth = new DateOnly(1970, 1, 1) };
        var plain = new Patient { MedicalRecordNumber = $"MRN-PLAIN-{key}", LastName = $"Plain{key}", FirstName = "Pat", DateOfBirth = new DateOnly(1971, 1, 1) };
        c.Patients.AddRange(flagged, plain);
        await c.SaveChangesAsync();

        c.PatientBloodTypeHistory.AddRange(
            new PatientBloodTypeHistory { PatientId = flagged.Id, Abo = AboGroup.B, RhD = RhType.Positive, IsCurrent = false },
            new PatientBloodTypeHistory { PatientId = flagged.Id, Abo = AboGroup.B, RhD = RhType.Negative, IsCurrent = true },
            // A retired current row does not show.
            new PatientBloodTypeHistory { PatientId = plain.Id, Abo = AboGroup.A, RhD = RhType.Positive, IsCurrent = true, InvalidatedUtc = now });
        c.AntibodyHistory.AddRange(
            new AntibodyHistory { PatientId = flagged.Id, AntibodySpecificity = "anti-K", IsActive = true },
            new AntibodyHistory { PatientId = flagged.Id, AntibodySpecificity = "anti-E", IsActive = true },
            new AntibodyHistory { PatientId = flagged.Id, AntibodySpecificity = "anti-K", IsActive = true },
            new AntibodyHistory { PatientId = flagged.Id, AntibodySpecificity = "anti-c", IsActive = false });
        c.SpecialTransfusionRequirements.AddRange(
            new SpecialTransfusionRequirement { PatientId = flagged.Id, RequirementType = SpecialTransfusionRequirementType.Irradiated, Reason = "In effect", EffectiveUtc = now.AddDays(-1) },
            new SpecialTransfusionRequirement { PatientId = flagged.Id, RequirementType = SpecialTransfusionRequirementType.Irradiated, Reason = "Ended", EffectiveUtc = now.AddDays(-9), ExpiresUtc = now.AddDays(-2) },
            new SpecialTransfusionRequirement { PatientId = flagged.Id, RequirementType = SpecialTransfusionRequirementType.Irradiated, Reason = "Not started", EffectiveUtc = now.AddDays(3) },
            new SpecialTransfusionRequirement { PatientId = flagged.Id, RequirementType = SpecialTransfusionRequirementType.Irradiated, Reason = "Deactivated", EffectiveUtc = now.AddDays(-1), IsActive = false });
        // TEST-BB-440: an informational note in effect is counted apart from the enforced requirements.
        var note = new SpecialRequirementDefinition
        {
            Code = $"NOTE-{key}", Name = "Call first", Category = SpecialRequirementCategory.Informational,
            EnforcementKind = SpecialRequirementEnforcementKind.Informational, Instruction = "Call first", IsActive = true
        };
        c.SpecialRequirementDefinitions.Add(note);
        await c.SaveChangesAsync();
        c.SpecialTransfusionRequirements.Add(new SpecialTransfusionRequirement
        {
            PatientId = flagged.Id, RequirementDefinitionId = note.Id, RequirementType = SpecialTransfusionRequirementType.Other,
            Reason = "Note", EffectiveUtc = now.AddDays(-1)
        });
        await c.SaveChangesAsync();

        var list = await Service(c).ListDtosAsync();

        var f = list.Single(p => p.Id == flagged.Id);
        Assert.Equal("B-", f.CurrentBloodType);
        Assert.Equal(new[] { "anti-E", "anti-K" }, f.ActiveAntibodies);
        Assert.Equal(1, f.ActiveSpecialRequirements);
        Assert.Equal(1, f.InformationalNotes);
        Assert.EndsWith("· 1 special requirement · 1 note", f.PickerLabel);

        var p = list.Single(x => x.Id == plain.Id);
        Assert.Null(p.CurrentBloodType);
        Assert.Empty(p.ActiveAntibodies!);
        Assert.Equal(0, p.ActiveSpecialRequirements);
        Assert.Equal(0, p.InformationalNotes);
    }
}
