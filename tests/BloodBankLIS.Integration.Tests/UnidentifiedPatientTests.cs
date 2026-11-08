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

// TEST-BB-591: an unidentified emergency patient is registered in one step with a temporary MRN and name for the day,
// sex and an estimated age required; unidentified charts are never possible duplicates; identifying one merges it
// (with its RhIG evaluations) into the identified chart, refusing its own MRN, an unknown MRN, and another unidentified chart.
public class UnidentifiedPatientTests
{
    private static PatientService Patients(SqliteContextFactory f, BloodBankDbContext c, IPermissionEvaluator? permissions = null) =>
        new(new EfRepository<Patient>(c), c, f.Clock,
            new SpecimenService(new EfRepository<Specimen>(c), new EfRepository<Patient>(c), new EfRepository<SpecimenTypeDefinition>(c), c, f.Clock, currentUser: f.CurrentUser),
            new AuditWriter(c, f.Clock, f.CurrentUser), f.CurrentUser, permissions, new EfRepository<PatientIdentifier>(c));

    private static PatientMergeService Merge(SqliteContextFactory f, BloodBankDbContext c, IPermissionEvaluator? permissions = null) =>
        new(new EfRepository<Patient>(c), new EfRepository<PatientIdentifier>(c), new EfRepository<AntibodyHistory>(c),
            new EfRepository<PatientBloodTypeHistory>(c), new EfRepository<Encounter>(c), new EfRepository<Order>(c),
            new EfRepository<Specimen>(c), new EfRepository<Allocation>(c), new EfRepository<Issue>(c), new EfRepository<Crossmatch>(c),
            new EfRepository<BloodUnit>(c), new EfRepository<SpecialTransfusionRequirement>(c), new EfRepository<TransfusionEvent>(c),
            new EfRepository<ReactionInvestigation>(c), new EfRepository<AntigenProfile>(c), new EfRepository<BillingEvent>(c),
            new EfRepository<TestResult>(c), c,
            audit: new AuditWriter(c, f.Clock, f.CurrentUser), currentUser: f.CurrentUser, permissions: permissions,
            mergeLedger: new EfRepository<PatientMerge>(c), mergeMoves: new EfRepository<PatientMergeMove>(c), clock: f.Clock,
            rhig: new EfRepository<RhigEvaluation>(c));

    [Fact]
    public async Task Unidentified_IsRegisteredInOneStep_AndMergedOnceIdentified()
    {
        using var factory = new SqliteContextFactory();
        await using var c = factory.Create();
        var patients = Patients(factory, c, new FixedPermissionEvaluator(1, PermissionCodes.PatientWrite));

        Assert.Contains("Choose the patient's sex", (await patients.CreateUnidentifiedAsync(new CreateUnidentifiedPatientRequest(null, 30))).Error);
        Assert.Contains("estimated age", (await patients.CreateUnidentifiedAsync(new CreateUnidentifiedPatientRequest(Sex.Female, null))).Error);
        Assert.Contains("0 to 120", (await patients.CreateUnidentifiedAsync(new CreateUnidentifiedPatientRequest(Sex.Female, 121))).Error);
        Assert.False((await Patients(factory, c, new FixedPermissionEvaluator(1, PermissionCodes.ResultEnter))
            .CreateUnidentifiedAsync(new CreateUnidentifiedPatientRequest(Sex.Female, 30))).Succeeded);

        var first = await patients.CreateUnidentifiedAsync(new CreateUnidentifiedPatientRequest(Sex.Female, 30, " Trauma bay 2 "));
        Assert.True(first.Succeeded, first.Error);
        var a = first.Value!;
        Assert.Equal(("UNK-20260530-01", "UNIDENTIFIED", "TRAUMA 20260530-01", new DateOnly(1996, 1, 1), Sex.Female, true),
            (a.MedicalRecordNumber, a.LastName, a.FirstName, a.DateOfBirth, a.Sex, a.Unidentified));
        Assert.Equal("Unidentified; estimated age 30 years. Trauma bay 2", a.Comment);
        Assert.True(await c.AuditEvents.AnyAsync(e => e.EntityType == nameof(Patient) && e.EntityId == a.Id
            && e.Reason == "Unidentified patient registered (UNK-20260530-01, estimated age 30)."));

        // Same day, same estimated age: the next number, and the two are never offered as duplicates.
        var second = (await patients.CreateUnidentifiedAsync(new CreateUnidentifiedPatientRequest(Sex.Unknown, 30))).Value!;
        Assert.Equal("UNK-20260530-02", second.MedicalRecordNumber);
        var mergeReader = Patients(factory, c, new FixedPermissionEvaluator(1, PermissionCodes.PatientMerge));
        Assert.DoesNotContain((await mergeReader.ListPossibleDuplicatePairsAsync()).Value!, p => p.First.Unidentified || p.Second.Unidentified);
        Assert.Equal([a.Id, second.Id], (await patients.ListUnidentifiedAsync()).Select(p => p.Id));

        var identified = new Patient { MedicalRecordNumber = "MRN-UNK-REAL", LastName = "Rivera", FirstName = "Ana", DateOfBirth = new DateOnly(1994, 6, 2), Sex = Sex.Female };
        c.Patients.Add(identified);
        await c.SaveChangesAsync();
        c.RhigEvaluations.Add(new RhigEvaluation { PatientId = a.Id, EventUtc = factory.Clock.UtcNow, RecordedBy = "tech-test", RecordedUtc = factory.Clock.UtcNow });
        await c.SaveChangesAsync();

        var merge = Merge(factory, c, new FixedPermissionEvaluator(1, PermissionCodes.PatientMerge));
        Assert.Contains("reason is required", (await merge.IdentifyAsync(a.Id, new IdentifyPatientRequest("MRN-UNK-REAL", " "))).Error);
        Assert.Contains("not this chart's", (await merge.IdentifyAsync(a.Id, new IdentifyPatientRequest(a.MedicalRecordNumber, "x"))).Error);
        Assert.Contains("No patient has that MRN", (await merge.IdentifyAsync(a.Id, new IdentifyPatientRequest("MRN-NOPE", "x"))).Error);
        Assert.Contains("another unidentified patient", (await merge.IdentifyAsync(a.Id, new IdentifyPatientRequest(second.MedicalRecordNumber, "x"))).Error);
        Assert.Contains("not an unidentified patient", (await merge.IdentifyAsync(identified.Id, new IdentifyPatientRequest(a.MedicalRecordNumber, "x"))).Error);
        Assert.False((await Merge(factory, c, new FixedPermissionEvaluator(1, PermissionCodes.PatientWrite))
            .IdentifyAsync(a.Id, new IdentifyPatientRequest("MRN-UNK-REAL", "Family ID"))).Succeeded);

        // TEST-BB-618: one unit issued under the temporary identity is still out (another was transfused).
        var product = new ProductType { ProductCode = "RBC-UNK", Name = "Red cells", ComponentClass = ComponentClass.RedBloodCells };
        c.ProductTypes.Add(product);
        await c.SaveChangesAsync();
        var outUnit = new BloodUnit { UnitNumber = "W-UNK-OUT", ProductTypeId = product.Id, Abo = AboGroup.O, RhD = RhType.Negative, ExpiresUtc = factory.Clock.UtcNow.AddDays(10), Status = UnitStatus.Issued };
        var doneUnit = new BloodUnit { UnitNumber = "W-UNK-DONE", ProductTypeId = product.Id, Abo = AboGroup.O, RhD = RhType.Negative, ExpiresUtc = factory.Clock.UtcNow.AddDays(10), Status = UnitStatus.Transfused };
        c.BloodUnits.AddRange(outUnit, doneUnit);
        await c.SaveChangesAsync();
        c.Issues.AddRange(
            new Issue { BloodProductId = outUnit.Id, PatientId = a.Id, IssuedUtc = factory.Clock.UtcNow, IssuedBy = "tech-test", IssueType = IssueType.EmergencyRelease, PatientIdentifier1 = a.MedicalRecordNumber, IssuedToLocation = "Trauma bay 2" },
            new Issue { BloodProductId = doneUnit.Id, PatientId = a.Id, IssuedUtc = factory.Clock.UtcNow, IssuedBy = "tech-test", IssueType = IssueType.EmergencyRelease, PatientIdentifier1 = a.MedicalRecordNumber, Status = IssueStatus.Transfused });
        await c.SaveChangesAsync();
        Assert.Empty(await merge.ListIssuedUnderEarlierIdentityAsync(a.Id));

        var done = await merge.IdentifyAsync(a.Id, new IdentifyPatientRequest(" MRN-UNK-REAL ", "ID from family at bedside"));
        Assert.True(done.Succeeded, done.Error);
        Assert.Equal(identified.Id, done.Value!.Id);
        var warning = Assert.Single(done.Warnings);
        Assert.Equal(IssuedUnderEarlierIdentityRule.Code, warning.Code);
        Assert.Contains("1 unit is still out under UNK-20260530-01", warning.Message);
        Assert.Contains("uses MRN MRN-UNK-REAL", warning.Message);
        var stillOut = Assert.Single(await merge.ListIssuedUnderEarlierIdentityAsync(identified.Id));
        Assert.Equal(("W-UNK-OUT", "UNK-20260530-01", "Trauma bay 2"), (stillOut.UnitNumber, stillOut.IssuedUnder, stillOut.IssuedToLocation));

        var merged = await c.Patients.AsNoTracking().SingleAsync(p => p.Id == a.Id);
        Assert.Equal((PatientStatus.Merged, (long?)identified.Id), (merged.Status, merged.MergedIntoPatientId));
        Assert.True(await c.RhigEvaluations.AnyAsync(r => r.PatientId == identified.Id));
        Assert.Contains("already merged", (await merge.IdentifyAsync(a.Id, new IdentifyPatientRequest("MRN-UNK-REAL", "x"))).Error);
        Assert.Equal([second.Id], (await patients.ListUnidentifiedAsync()).Select(p => p.Id));

        // Undoing the merge puts the RhIG evaluation back on the unidentified chart.
        var undo = await merge.UnmergeAsync(a.Id, "Wrong patient");
        Assert.True(undo.Succeeded, undo.Error);
        Assert.True(await c.RhigEvaluations.AnyAsync(r => r.PatientId == a.Id));
    }

    // TEST-BB-593: the demo has one unidentified trauma patient, added once.
    [Fact]
    public async Task Demo_HasOneUnidentifiedPatient()
    {
        using var factory = new SqliteContextFactory();
        await using (var db = factory.Create())
        {
            await DatabaseSeeder.SeedAsync(db);
            await DatabaseSeeder.SeedAsync(db);
        }

        await using var v = factory.Create();
        var demo = await v.Patients.AsNoTracking().SingleAsync(p => p.Unidentified);
        Assert.StartsWith(UnidentifiedPatientRule.MrnPrefix, demo.MedicalRecordNumber);
        Assert.Equal((UnidentifiedPatientRule.LastName, Sex.Male, PatientStatus.Active), (demo.LastName, demo.Sex, demo.Status));
        Assert.StartsWith("TRAUMA ", demo.FirstName);
    }
}
