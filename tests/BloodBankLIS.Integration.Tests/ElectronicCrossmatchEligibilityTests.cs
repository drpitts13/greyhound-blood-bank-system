using BloodBankLIS.Application.Compatibility;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class ElectronicCrossmatchEligibilityTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public ElectronicCrossmatchEligibilityTests(SqliteContextFactory factory) => _factory = factory;

    private ElectronicCrossmatchEligibilityService Service(BloodBankDbContext c) =>
        new(
            new EfRepository<Patient>(c),
            new EfRepository<PatientBloodTypeHistory>(c),
            new EfRepository<AntibodyHistory>(c),
            new EfRepository<AntibodyIdentificationWorkup>(c),
            new AntibodyScreenCompatLoader(
                new EfRepository<TestResult>(c),
                new EfRepository<TestDefinition>(c),
                new EfRepository<AntibodyHistory>(c)),
            new FacilityPolicyService(new EfRepository<SystemSetting>(c)));

    [Fact]
    public async Task Assess_TwoConcordantTypesAndNegativeScreen_IsEligible()
    {
        await using var c = _factory.Create();
        var patient = new Patient
        {
            MedicalRecordNumber = "MRN-EXM-OK",
            LastName = "Eligible",
            FirstName = "Xm",
            DateOfBirth = new DateOnly(1988, 3, 1)
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
        await SetElectronicXmAsync(c, enabled: true);

        var dto = await Service(c).AssessAsync(patient.Id);
        Assert.NotNull(dto);
        Assert.True(dto!.Eligible);
        Assert.True(dto.FacilityAllowsElectronicCrossmatch);
        Assert.All(dto.Criteria, criterion => Assert.True(criterion.Satisfied));
    }

    [Fact]
    public async Task Assess_FacilityPolicyOff_IsNotEligible()
    {
        await using var c = _factory.Create();
        var patient = new Patient
        {
            MedicalRecordNumber = "MRN-EXM-OFF",
            LastName = "Policy",
            FirstName = "Off",
            DateOfBirth = new DateOnly(1988, 3, 1)
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
        await SetElectronicXmAsync(c, enabled: false);

        var dto = await Service(c).AssessAsync(patient.Id);
        Assert.NotNull(dto);
        Assert.False(dto!.FacilityAllowsElectronicCrossmatch);
        Assert.False(dto.Eligible);
        Assert.Contains(dto.Criteria, r => r.Code == ElectronicCrossmatchEligibilityRule.FacilityCode && !r.Satisfied);
    }

    [Fact]
    public async Task Assess_MissingSecondType_IsNotEligible()
    {
        await using var c = _factory.Create();
        var patient = new Patient
        {
            MedicalRecordNumber = "MRN-EXM-ONE",
            LastName = "Single",
            FirstName = "Type",
            DateOfBirth = new DateOnly(1988, 3, 1)
        };
        c.Patients.Add(patient);
        await c.SaveChangesAsync();
        c.PatientBloodTypeHistory.Add(new PatientBloodTypeHistory
        {
            PatientId = patient.Id,
            Abo = AboGroup.A,
            RhD = RhType.Negative,
            IsCurrent = true,
            Source = BloodTypeSource.TestResult
        });
        await SetElectronicXmAsync(c, enabled: true);

        var dto = await Service(c).AssessAsync(patient.Id);
        Assert.NotNull(dto);
        Assert.False(dto!.Eligible);
        Assert.Contains(dto.Criteria, r => r.Code == ElectronicCrossmatchEligibilityRule.SecondTypeCode && !r.Satisfied);
        Assert.Equal("Electronic crossmatch requires two concordant ABO/Rh determinations.", dto.BlockingReason);
    }

    [Fact]
    public async Task Assess_AntibodyHistory_BlocksEligibility()
    {
        await using var c = _factory.Create();
        var patient = new Patient
        {
            MedicalRecordNumber = "MRN-EXM-AB",
            LastName = "Antibody",
            FirstName = "Hist",
            DateOfBirth = new DateOnly(1988, 3, 1)
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
            IsActive = true
        });
        await SetElectronicXmAsync(c, enabled: true);

        var dto = await Service(c).AssessAsync(patient.Id);
        Assert.False(dto!.Eligible);
        Assert.Contains(dto.Criteria, r => r.Code == ElectronicCrossmatchEligibilityRule.HistoryCode && !r.Satisfied);
    }

    [Fact]
    public async Task Assess_DeactivatedAntibodyHistory_StillBlocksEligibility()
    {
        await using var c = _factory.Create();
        var patient = new Patient
        {
            MedicalRecordNumber = "MRN-EXM-AB-HIST",
            LastName = "Historical",
            FirstName = "AntiK",
            DateOfBirth = new DateOnly(1988, 3, 1)
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
            DeactivationReason = "Currently undetectable; historical record retained."
        });
        await SetElectronicXmAsync(c, enabled: true);

        var dto = await Service(c).AssessAsync(patient.Id);
        Assert.False(dto!.Eligible);
        Assert.Contains(dto.Criteria, r => r.Code == ElectronicCrossmatchEligibilityRule.HistoryCode && !r.Satisfied);
        Assert.Contains("currently undetectable", dto.BlockingReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Assess_OpenAntibodyIdWorkup_BlocksEligibility()
    {
        await using var c = _factory.Create();
        var patient = new Patient
        {
            MedicalRecordNumber = "MRN-EXM-ABID-OPEN",
            LastName = "Open",
            FirstName = "Workup",
            DateOfBirth = new DateOnly(1988, 3, 1)
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
        c.AntibodyIdentificationWorkups.Add(new AntibodyIdentificationWorkup
        {
            PatientId = patient.Id,
            PrimaryLotId = 1,
            Status = AntibodyWorkupStatus.InProgress
        });
        await SetElectronicXmAsync(c, enabled: true);

        var dto = await Service(c).AssessAsync(patient.Id);
        Assert.False(dto!.Eligible);
        Assert.Contains(dto.Criteria, r => r.Code == ElectronicCrossmatchEligibilityRule.WorkupOpenCode && !r.Satisfied);
        Assert.Contains("antibody-identification workup is open", dto.BlockingReason, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task SetElectronicXmAsync(BloodBankDbContext c, bool enabled)
    {
        var existing = await c.SystemSettings.FirstOrDefaultAsync(s => s.Key == FacilityPolicyKeys.AllowElectronicCrossmatch);
        if (existing is null)
        {
            c.SystemSettings.Add(new SystemSetting
            {
                Key = FacilityPolicyKeys.AllowElectronicCrossmatch,
                Value = enabled ? "true" : "false",
                Category = "Issue"
            });
        }
        else
        {
            existing.Value = enabled ? "true" : "false";
        }

        await c.SaveChangesAsync();
    }
}
