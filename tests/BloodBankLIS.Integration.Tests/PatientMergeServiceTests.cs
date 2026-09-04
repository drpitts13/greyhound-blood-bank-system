using BloodBankLIS.Application.Patients;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Persistence;

namespace BloodBankLIS.Integration.Tests;

public class PatientMergeServiceTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public PatientMergeServiceTests(SqliteContextFactory factory) => _factory = factory;

    [Fact]
    public async Task Merge_ReassignsHistory_AndRetiresDuplicate()
    {
        await using var c = _factory.Create();
        var (survivor, duplicate) = await SeedPairAsync(c, "MAN-OK");
        c.AntibodyHistory.Add(new AntibodyHistory
        {
            PatientId = duplicate.Id,
            AntibodySpecificity = "anti-E",
            Status = AntibodyStatus.Identified,
            IsActive = true
        });
        c.Encounters.Add(new Encounter
        {
            PatientId = duplicate.Id,
            VisitNumber = "VIS-MAN-OK",
            Status = EncounterStatus.Active,
            AdmitUtc = _factory.Clock.UtcNow
        });
        await c.SaveChangesAsync();

        var result = await Merge(c).MergeAsync(survivor.Id, duplicate.Id, "Same person; registration duplicate.");
        Assert.True(result.Succeeded);
        Assert.Equal(PatientStatus.Merged, (await c.Patients.FindAsync(duplicate.Id))!.Status);
        Assert.Equal(survivor.Id, (await c.Patients.FindAsync(duplicate.Id))!.MergedIntoPatientId);
        Assert.True(c.AntibodyHistory.Any(a => a.PatientId == survivor.Id && a.AntibodySpecificity == "anti-E"));
        Assert.True(c.Encounters.Any(e => e.VisitNumber == "VIS-MAN-OK" && e.PatientId == survivor.Id));
        Assert.True(c.PatientIdentifiers.Any(i =>
            i.PatientId == survivor.Id
            && i.IdentifierType == IdentityTokenType.PriorMedicalRecordNumber
            && i.Value == duplicate.MedicalRecordNumber));
    }

    [Fact]
    public async Task Merge_DiscordantAbo_IsHardStopped()
    {
        await using var c = _factory.Create();
        var (survivor, duplicate) = await SeedPairAsync(c, "MAN-ABO");
        c.PatientBloodTypeHistory.AddRange(
            new PatientBloodTypeHistory
            {
                PatientId = survivor.Id,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                IsCurrent = true,
                Source = BloodTypeSource.TestResult
            },
            new PatientBloodTypeHistory
            {
                PatientId = duplicate.Id,
                Abo = AboGroup.A,
                RhD = RhType.Positive,
                IsCurrent = true,
                Source = BloodTypeSource.TestResult
            });
        await c.SaveChangesAsync();

        var result = await Merge(c).MergeAsync(survivor.Id, duplicate.Id, "Attempted merge.");
        Assert.False(result.Succeeded);
        Assert.Contains("ABO", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(PatientStatus.Active, (await c.Patients.FindAsync(duplicate.Id))!.Status);
    }

    [Fact]
    public async Task Merge_Self_IsHardStopped()
    {
        await using var c = _factory.Create();
        var (survivor, _) = await SeedPairAsync(c, "MAN-SELF");

        var result = await Merge(c).MergeAsync(survivor.Id, survivor.Id, "Self.");
        Assert.False(result.Succeeded);
        Assert.Contains("cannot be merged into itself", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FindByMrn_FollowsMergedPointer()
    {
        await using var c = _factory.Create();
        var (survivor, duplicate) = await SeedPairAsync(c, "MAN-FOL");
        Assert.True((await Merge(c).MergeAsync(survivor.Id, duplicate.Id, "Follow test.")).Succeeded);

        var resolved = await Merge(c).FindByMrnAsync(duplicate.MedicalRecordNumber, followMerge: true);
        Assert.NotNull(resolved);
        Assert.Equal(survivor.Id, resolved!.Id);
    }

    private static PatientMergeService Merge(BloodBankDbContext c) =>
        new(
            new EfRepository<Patient>(c),
            new EfRepository<PatientIdentifier>(c),
            new EfRepository<AntibodyHistory>(c),
            new EfRepository<PatientBloodTypeHistory>(c),
            new EfRepository<Encounter>(c),
            new EfRepository<Order>(c),
            new EfRepository<Specimen>(c),
            new EfRepository<Allocation>(c),
            new EfRepository<Issue>(c),
            new EfRepository<Crossmatch>(c),
            new EfRepository<BloodUnit>(c),
            new EfRepository<SpecialTransfusionRequirement>(c),
            new EfRepository<TransfusionEvent>(c),
            new EfRepository<ReactionInvestigation>(c),
            new EfRepository<AntigenProfile>(c),
            new EfRepository<BillingEvent>(c),
            new EfRepository<TestResult>(c),
            c);

    private static async Task<(Patient Survivor, Patient Duplicate)> SeedPairAsync(BloodBankDbContext c, string key)
    {
        var survivor = new Patient
        {
            MedicalRecordNumber = $"MRN-{key}-S",
            LastName = "Survivor",
            FirstName = "Pat",
            DateOfBirth = new DateOnly(1975, 3, 3)
        };
        var duplicate = new Patient
        {
            MedicalRecordNumber = $"MRN-{key}-D",
            LastName = "Duplicate",
            FirstName = "Pat",
            DateOfBirth = new DateOnly(1975, 3, 3)
        };
        c.Patients.AddRange(survivor, duplicate);
        await c.SaveChangesAsync();
        return (survivor, duplicate);
    }
}
