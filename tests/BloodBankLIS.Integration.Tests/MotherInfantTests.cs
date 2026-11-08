using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Patients;
using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-606: an infant's chart is linked to the mother's by MRN (audited, permission, rule checks); each chart shows
// the other with current type and active antibodies; the link is removed only with a reason; merging the mother's
// chart into another moves the infant's link to the surviving chart and undoing the merge moves it back.
public class MotherInfantTests
{
    private static PatientService Patients(SqliteContextFactory f, BloodBankDbContext c, IPermissionEvaluator? permissions = null) =>
        new(new EfRepository<Patient>(c), c, f.Clock,
            new SpecimenService(new EfRepository<Specimen>(c), new EfRepository<Patient>(c), new EfRepository<SpecimenTypeDefinition>(c), c, f.Clock, currentUser: f.CurrentUser),
            new AuditWriter(c, f.Clock, f.CurrentUser), f.CurrentUser, permissions, new EfRepository<PatientIdentifier>(c),
            bloodTypes: new EfRepository<PatientBloodTypeHistory>(c), antibodies: new EfRepository<AntibodyHistory>(c));

    private static PatientMergeService Merge(SqliteContextFactory f, BloodBankDbContext c) =>
        new(new EfRepository<Patient>(c), new EfRepository<PatientIdentifier>(c), new EfRepository<AntibodyHistory>(c),
            new EfRepository<PatientBloodTypeHistory>(c), new EfRepository<Encounter>(c), new EfRepository<Order>(c),
            new EfRepository<Specimen>(c), new EfRepository<Allocation>(c), new EfRepository<Issue>(c), new EfRepository<Crossmatch>(c),
            new EfRepository<BloodUnit>(c), new EfRepository<SpecialTransfusionRequirement>(c), new EfRepository<TransfusionEvent>(c),
            new EfRepository<ReactionInvestigation>(c), new EfRepository<AntigenProfile>(c), new EfRepository<BillingEvent>(c),
            new EfRepository<TestResult>(c), c,
            audit: new AuditWriter(c, f.Clock, f.CurrentUser), currentUser: f.CurrentUser,
            mergeLedger: new EfRepository<PatientMerge>(c), mergeMoves: new EfRepository<PatientMergeMove>(c), clock: f.Clock);

    [Fact]
    public async Task Infant_IsLinkedToTheMother_AndTheLinkFollowsAMerge()
    {
        using var factory = new SqliteContextFactory();
        await using var c = factory.Create();
        var mother = new Patient { MedicalRecordNumber = "MRN-MOM", LastName = "Rivera", FirstName = "Ana", DateOfBirth = new DateOnly(1994, 3, 14), Sex = Sex.Female };
        var survivor = new Patient { MedicalRecordNumber = "MRN-MOM-KEEP", LastName = "Rivera", FirstName = "Ana", DateOfBirth = new DateOnly(1994, 3, 14), Sex = Sex.Female };
        var infant = new Patient { MedicalRecordNumber = "MRN-BABY", LastName = "Rivera", FirstName = "Baby Girl", DateOfBirth = new DateOnly(2026, 5, 29), Sex = Sex.Female };
        c.Patients.AddRange(mother, survivor, infant);
        await c.SaveChangesAsync();
        c.PatientBloodTypeHistory.Add(new PatientBloodTypeHistory { PatientId = mother.Id, Abo = AboGroup.O, RhD = RhType.Negative, IsCurrent = true });
        c.AntibodyHistory.Add(new AntibodyHistory { PatientId = mother.Id, AntibodySpecificity = "anti-D", IsActive = true });
        await c.SaveChangesAsync();

        var patients = Patients(factory, c, new FixedPermissionEvaluator(1, PermissionCodes.PatientWrite));
        Assert.False((await Patients(factory, c, new FixedPermissionEvaluator(1, PermissionCodes.ResultEnter))
            .LinkMotherAsync(infant.Id, new LinkMotherRequest("MRN-MOM"))).Succeeded);
        Assert.Contains("No patient has that MRN", (await patients.LinkMotherAsync(infant.Id, new LinkMotherRequest("MRN-NOPE"))).Error);
        Assert.Contains("must be before", (await patients.LinkMotherAsync(mother.Id, new LinkMotherRequest("MRN-BABY"))).Error);

        var linked = await patients.LinkMotherAsync(infant.Id, new LinkMotherRequest(" MRN-MOM "));
        Assert.True(linked.Succeeded, linked.Error);
        Assert.Contains("already linked", (await patients.LinkMotherAsync(infant.Id, new LinkMotherRequest("MRN-MOM"))).Error);
        Assert.True(await c.AuditEvents.AnyAsync(a => a.EntityType == nameof(Patient) && a.EntityId == infant.Id && a.Reason == "Mother linked: MRN-MOM."));

        var infantSide = (await patients.GetFamilyAsync(infant.Id))!;
        Assert.Equal(("MRN-MOM", "Rivera, Ana"), (infantSide.Mother!.MedicalRecordNumber, infantSide.Mother.Name));
        Assert.Equal(["anti-D"], infantSide.Mother.ActiveAntibodies);
        Assert.NotNull(infantSide.Mother.CurrentBloodType);
        var motherSide = (await patients.GetFamilyAsync(mother.Id))!;
        Assert.Null(motherSide.Mother);
        Assert.Equal("MRN-BABY", Assert.Single(motherSide.Infants).MedicalRecordNumber);
        Assert.Null(await patients.GetFamilyAsync(-1));

        // The mother's chart merged into another: the infant follows to the surviving chart, and back on undo.
        var merged = await Merge(factory, c).MergeAsync(survivor.Id, mother.Id, "Same person");
        Assert.True(merged.Succeeded, merged.Error);
        Assert.Equal(survivor.Id, (await c.Patients.AsNoTracking().SingleAsync(p => p.Id == infant.Id)).MotherPatientId);
        var undone = await Merge(factory, c).UnmergeAsync(mother.Id, "Wrong merge");
        Assert.True(undone.Succeeded, undone.Error);
        Assert.Equal(mother.Id, (await c.Patients.AsNoTracking().SingleAsync(p => p.Id == infant.Id)).MotherPatientId);

        Assert.Contains("reason", (await patients.UnlinkMotherAsync(infant.Id, new UnlinkMotherRequest(" "))).Error);
        var unlinked = await patients.UnlinkMotherAsync(infant.Id, new UnlinkMotherRequest("Linked to the wrong chart"));
        Assert.True(unlinked.Succeeded, unlinked.Error);
        Assert.Null((await c.Patients.AsNoTracking().SingleAsync(p => p.Id == infant.Id)).MotherPatientId);
        Assert.True(await c.AuditEvents.AnyAsync(a => a.EntityId == infant.Id && a.Reason == "Mother link removed: Linked to the wrong chart"));
        Assert.Contains("no mother linked", (await patients.UnlinkMotherAsync(infant.Id, new UnlinkMotherRequest("Again"))).Error);
    }

    // TEST-BB-607: the demo newborn MRN0027 is linked to the RhIG demo mother MRN0025, added once.
    [Fact]
    public async Task Demo_NewbornIsLinkedToTheRhigMother()
    {
        using var factory = new SqliteContextFactory();
        await using (var db = factory.Create())
        {
            await DatabaseSeeder.SeedAsync(db);
            await DatabaseSeeder.SeedAsync(db);
        }

        await using var v = factory.Create();
        var mother = await v.Patients.AsNoTracking().SingleAsync(p => p.MedicalRecordNumber == "MRN0025");
        var infant = await v.Patients.AsNoTracking().SingleAsync(p => p.MedicalRecordNumber == "MRN0027");
        Assert.Equal(mother.Id, infant.MotherPatientId);
        Assert.Equal(1, await v.Patients.CountAsync(p => p.MotherPatientId == mother.Id));
    }
}
