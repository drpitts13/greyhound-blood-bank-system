using BloodBankLIS.Application.Compatibility;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Persistence;

namespace BloodBankLIS.Integration.Tests.SafetyRegression;

/// <summary>
/// A merged (losing) patient record must not accept allocation or crossmatch.
/// Issue is blocked by the same rule inside <see cref="IssueGate"/>.
/// </summary>
public class MergedPatientClinicalUseTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public MergedPatientClinicalUseTests(SqliteContextFactory factory) => _factory = factory;

    [Fact]
    public async Task Allocate_ToMergedPatient_IsHardStopped()
    {
        await using var c = _factory.Create();
        var (patient, unit) = await SeedAsync(c, "MERGE-ALLOC");
        patient.Status = PatientStatus.Merged;
        patient.MergedIntoPatientId = 99;
        await c.SaveChangesAsync();

        var result = await Compatibility(c).AllocateUnitAsync(new AllocateUnitRequest(unit.Id, patient.Id));
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == PatientMergeRule.ClinicalUseCode);
    }

    [Fact]
    public async Task Crossmatch_OnMergedPatient_IsHardStopped()
    {
        await using var c = _factory.Create();
        var (patient, unit) = await SeedAsync(c, "MERGE-XM");
        var specimen = new Specimen
        {
            AccessionNumber = "ACC-MERGE-XM",
            PatientId = patient.Id,
            SpecimenType = "EDTA",
            CollectedUtc = _factory.Clock.UtcNow.AddHours(-2),
            ReceivedUtc = _factory.Clock.UtcNow.AddHours(-1),
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(2),
            Status = SpecimenStatus.Accepted
        };
        c.Specimens.Add(specimen);
        patient.Status = PatientStatus.Merged;
        await c.SaveChangesAsync();

        var result = await Compatibility(c).RecordCrossmatchAsync(
            new RecordCrossmatchRequest(unit.Id, patient.Id, specimen.Id, CrossmatchMethod.Serologic, CrossmatchResult.Compatible));
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == PatientMergeRule.ClinicalUseCode);
    }

    private CompatibilityService Compatibility(BloodBankDbContext c) =>
        new(new InventoryRepository(c), new EfRepository<Crossmatch>(c), new EfRepository<Allocation>(c),
            new EfRepository<Patient>(c), new EfRepository<Specimen>(c), new EfRepository<ProductType>(c),
            new EfRepository<PatientBloodTypeHistory>(c),
            new BloodAttributeCompatLoader(
                new EfRepository<AntibodyHistory>(c),
                new EfRepository<AntigenProfile>(c),
                new EfRepository<UnitBloodAttribute>(c),
                new EfRepository<BloodAttributeDefinition>(c)),
            new AntibodyScreenCompatLoader(
                new EfRepository<TestResult>(c),
                new EfRepository<TestDefinition>(c),
                new EfRepository<AntibodyHistory>(c)),
            c, _factory.Clock, _factory.CurrentUser);

    private async Task<(Patient Patient, BloodUnit Unit)> SeedAsync(BloodBankDbContext c, string key)
    {
        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-{key}",
            LastName = "Merged",
            FirstName = "Loser",
            DateOfBirth = new DateOnly(1960, 1, 1),
            Status = PatientStatus.Active
        };
        c.Patients.Add(patient);
        var productType = new ProductType
        {
            ProductCode = $"RBC-{key}",
            Name = "Test RBC",
            ComponentClass = ComponentClass.RedBloodCells,
            RequiresCrossmatch = true
        };
        c.ProductTypes.Add(productType);
        await c.SaveChangesAsync();

        var unit = new BloodUnit
        {
            UnitNumber = $"U-{key}",
            ProductTypeId = productType.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(20),
            Status = UnitStatus.Available
        };
        c.BloodUnits.Add(unit);
        await c.SaveChangesAsync();
        return (patient, unit);
    }
}
