using BloodBankLIS.Application.Compatibility;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests.SafetyRegression;

/// <summary>
/// A currently undetectable antibody must not restore electronic crossmatch
/// eligibility. Historical findings remain clinically significant.
/// </summary>
public class ElectronicXmHistoryRegressionTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public ElectronicXmHistoryRegressionTests(SqliteContextFactory factory) => _factory = factory;

    [Fact]
    public async Task DeactivatedAntibody_BlocksElectronicXm_WithExplainableReason()
    {
        await using var c = _factory.Create();
        var patient = new Patient
        {
            MedicalRecordNumber = "MRN-SAFE-EXM-HIST",
            LastName = "History",
            FirstName = "Kell",
            DateOfBirth = new DateOnly(1980, 4, 15)
        };
        c.Patients.Add(patient);
        await c.SaveChangesAsync();
        c.PatientBloodTypeHistory.AddRange(
            new PatientBloodTypeHistory
            {
                PatientId = patient.Id,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                IsCurrent = false,
                Source = BloodTypeSource.TestResult
            },
            new PatientBloodTypeHistory
            {
                PatientId = patient.Id,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                IsCurrent = true,
                Source = BloodTypeSource.TestResult
            });
        c.AntibodyHistory.Add(new AntibodyHistory
        {
            PatientId = patient.Id,
            AntibodySpecificity = "anti-K",
            Status = AntibodyStatus.Identified,
            IsActive = false,
            DeactivationReason = "Currently undetectable"
        });
        await c.SaveChangesAsync();

        var service = new ElectronicCrossmatchEligibilityService(
            new EfRepository<Patient>(c),
            new EfRepository<PatientBloodTypeHistory>(c),
            new EfRepository<AntibodyHistory>(c),
            new EfRepository<AntibodyIdentificationWorkup>(c),
            new AntibodyScreenCompatLoader(
                new EfRepository<TestResult>(c),
                new EfRepository<TestDefinition>(c),
                new EfRepository<AntibodyHistory>(c)),
            new FacilityPolicyService(new EfRepository<SystemSetting>(c)));

        var dto = await service.AssessAsync(patient.Id);
        Assert.False(dto!.Eligible);
        Assert.Contains(dto.Criteria, r => r.Code == ElectronicCrossmatchEligibilityRule.HistoryCode && !r.Satisfied);
        Assert.Contains("currently undetectable", dto.Criteria.First(r => r.Code == ElectronicCrossmatchEligibilityRule.HistoryCode).Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AntibodyAddedAfterEligibleAssess_BlocksElectronicXmRecord()
    {
        await using var c = _factory.Create();
        var patient = new Patient
        {
            MedicalRecordNumber = "MRN-SAFE-EXM-RACE",
            LastName = "Race",
            FirstName = "Kell",
            DateOfBirth = new DateOnly(1982, 8, 9)
        };
        c.Patients.Add(patient);
        var product = new ProductType
        {
            ProductCode = "RBC-EXM-RACE",
            Name = "RBC",
            ComponentClass = ComponentClass.RedBloodCells,
            RequiresCrossmatch = true
        };
        c.ProductTypes.Add(product);
        await c.SaveChangesAsync();
        c.PatientBloodTypeHistory.AddRange(
            new PatientBloodTypeHistory
            {
                PatientId = patient.Id,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                IsCurrent = false,
                Source = BloodTypeSource.TestResult
            },
            new PatientBloodTypeHistory
            {
                PatientId = patient.Id,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                IsCurrent = true,
                Source = BloodTypeSource.TestResult
            });
        var specimen = new Specimen
        {
            AccessionNumber = "ACC-EXM-RACE",
            PatientId = patient.Id,
            SpecimenType = "EDTA",
            CollectedUtc = _factory.Clock.UtcNow.AddHours(-2),
            ReceivedUtc = _factory.Clock.UtcNow.AddHours(-1),
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(2),
            Status = SpecimenStatus.Accepted
        };
        c.Specimens.Add(specimen);
        var unit = new BloodUnit
        {
            UnitNumber = "U-EXM-RACE",
            ProductTypeId = product.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(20),
            Status = UnitStatus.Available
        };
        c.BloodUnits.Add(unit);
        await c.SaveChangesAsync();

        var eligibility = new ElectronicCrossmatchEligibilityService(
            new EfRepository<Patient>(c),
            new EfRepository<PatientBloodTypeHistory>(c),
            new EfRepository<AntibodyHistory>(c),
            new EfRepository<AntibodyIdentificationWorkup>(c),
            new AntibodyScreenCompatLoader(
                new EfRepository<TestResult>(c),
                new EfRepository<TestDefinition>(c),
                new EfRepository<AntibodyHistory>(c)),
            new FacilityPolicyService(new EfRepository<SystemSetting>(c)));
        var assess = await eligibility.AssessAsync(patient.Id);
        Assert.True(assess!.Eligible);

        c.AntibodyHistory.Add(new AntibodyHistory
        {
            PatientId = patient.Id,
            AntibodySpecificity = "anti-K",
            Status = AntibodyStatus.Identified,
            IsActive = true
        });
        await c.SaveChangesAsync();

        var compatibility = new CompatibilityService(
            new InventoryRepository(c),
            new EfRepository<Crossmatch>(c),
            new EfRepository<Allocation>(c),
            new EfRepository<Patient>(c),
            new EfRepository<Specimen>(c),
            new EfRepository<ProductType>(c),
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
            c,
            _factory.Clock,
            _factory.CurrentUser,
            workups: new EfRepository<AntibodyIdentificationWorkup>(c));

        var recorded = await compatibility.RecordCrossmatchAsync(
            new RecordCrossmatchRequest(unit.Id, patient.Id, specimen.Id, CrossmatchMethod.Electronic));
        Assert.False(recorded.Succeeded);
        Assert.Contains(recorded.Evaluation!.HardStops, r => r.Code == ElectronicCrossmatchEligibilityRule.HistoryCode);
        Assert.False(await c.Crossmatches.AnyAsync(x =>
            x.PatientId == patient.Id && x.Method == CrossmatchMethod.Electronic));
    }
}
