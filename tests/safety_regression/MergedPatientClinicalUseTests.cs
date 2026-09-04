using BloodBankLIS.Application.Compatibility;
using BloodBankLIS.Application.Immunohematology;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Application.Results;
using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;

namespace BloodBankLIS.Integration.Tests.SafetyRegression;

/// <summary>
/// A merged (losing) patient record must not accept testing, allocation, or crossmatch.
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

    [Fact]
    public async Task Accession_ToMergedPatient_IsHardStopped()
    {
        await using var c = _factory.Create();
        var (patient, _) = await SeedAsync(c, "MERGE-ACC");
        patient.Status = PatientStatus.Merged;
        await c.SaveChangesAsync();

        var result = await Specimens(c).AccessionAsync(
            new AccessionSpecimenRequest("ACC-MERGE-LOSER", patient.Id, "EDTA", _factory.Clock.UtcNow.AddHours(-1)));
        Assert.False(result.Succeeded);
        Assert.Equal(MergedMessage, result.Error);
    }

    [Fact]
    public async Task Accession_ToInactivePatient_IsAllowed()
    {
        await using var c = _factory.Create();
        var (patient, _) = await SeedAsync(c, "MERGE-ACC-INACT");
        patient.Status = PatientStatus.Inactive;
        await c.SaveChangesAsync();

        var result = await Specimens(c).AccessionAsync(
            new AccessionSpecimenRequest("ACC-MERGE-INACT", patient.Id, "EDTA", _factory.Clock.UtcNow.AddHours(-1)));
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task CreateOrder_ForMergedPatient_IsHardStopped()
    {
        await using var c = _factory.Create();
        var (patient, _) = await SeedAsync(c, "MERGE-ORD");
        var encounter = new Encounter
        {
            PatientId = patient.Id,
            VisitNumber = "VIS-MERGE-ORD",
            Status = EncounterStatus.Active
        };
        var location = new OrderingLocation { Code = "ED-MERGE", Name = "ED", IsActive = true };
        c.Encounters.Add(encounter);
        c.OrderingLocations.Add(location);
        patient.Status = PatientStatus.Merged;
        await c.SaveChangesAsync();

        var result = await Orders(c).CreateAsync(
            patient.Id,
            new CreateOrderRequest(
                encounter.Id,
                location.Id,
                "ORD-MERGE-LOSER",
                [new OrderLineInputDto(OrderCategory.Test, "TS", null)],
                OrderPriority.Routine,
                _factory.Clock.UtcNow,
                null,
                OrderSource.Manual,
                null,
                "tester"));
        Assert.False(result.Succeeded);
        Assert.Equal(MergedMessage, result.Error);
    }

    [Fact]
    public async Task EnterResult_OnMergedPatientSpecimen_IsHardStopped()
    {
        await using var c = _factory.Create();
        var (patient, _) = await SeedAsync(c, "MERGE-RES");
        var specimen = new Specimen
        {
            AccessionNumber = "ACC-MERGE-RES",
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

        var result = await Results(c).EnterResultAsync(new EnterResultRequest(specimen.Id, "ABSC", "Negative"));
        Assert.False(result.Succeeded);
        Assert.Equal(MergedMessage, result.Error);
    }

    [Fact]
    public async Task AddAntibody_OnMergedPatient_IsHardStopped()
    {
        await using var c = _factory.Create();
        var (patient, _) = await SeedAsync(c, "MERGE-AB");
        patient.Status = PatientStatus.Merged;
        await c.SaveChangesAsync();

        var result = await Immuno(c).AddAntibodyAsync(
            patient.Id, null, "Anti-K", AntibodyStatus.HistoricalOnly, null);
        Assert.False(result.Succeeded);
        Assert.Equal(MergedMessage, result.Error);
    }

    private static string MergedMessage =>
        PatientMergeRule.EvaluateClinicalUse(PatientStatus.Merged).Message;

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

    private SpecimenService Specimens(BloodBankDbContext c) =>
        new(new EfRepository<Specimen>(c), new EfRepository<Patient>(c), new EfRepository<SpecimenTypeDefinition>(c), c, _factory.Clock);

    private ResultService Results(BloodBankDbContext c) =>
        new(new EfRepository<TestResult>(c), new EfRepository<Specimen>(c), new EfRepository<PatientBloodTypeHistory>(c),
            c, _factory.Clock, _factory.CurrentUser, new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            patients: new EfRepository<Patient>(c));

    private OrderService Orders(BloodBankDbContext c) =>
        new(
            new EfRepository<Order>(c),
            new EfRepository<OrderLine>(c),
            new EfRepository<OrderSpecimen>(c),
            new EfRepository<Encounter>(c),
            new EfRepository<OrderingLocation>(c),
            new EfRepository<Patient>(c),
            new EfRepository<Specimen>(c),
            new EfRepository<OrderingProvider>(c),
            new EfRepository<ProductType>(c),
            new EfRepository<TestDefinition>(c),
            new EfRepository<TestGrouper>(c),
            _factory.Clock,
            c);

    private ImmunohematologyService Immuno(BloodBankDbContext c) =>
        new(
            new EfRepository<PatientBloodTypeHistory>(c),
            new EfRepository<AntibodyHistory>(c),
            new EfRepository<AntigenProfile>(c),
            new EfRepository<BloodAttributeDefinition>(c),
            new EfRepository<Patient>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser));

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
