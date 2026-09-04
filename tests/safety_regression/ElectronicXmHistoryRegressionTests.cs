using BloodBankLIS.Application.Compatibility;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Persistence;

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
}
