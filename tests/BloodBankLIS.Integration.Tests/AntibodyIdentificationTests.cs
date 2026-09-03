using BloodBankLIS.Application.Results;
using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class AntibodyIdentificationTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public AntibodyIdentificationTests(SqliteContextFactory factory) => _factory = factory;

    private ResultService Results(BloodBankDbContext c) =>
        new(new EfRepository<TestResult>(c), new EfRepository<Specimen>(c), new EfRepository<PatientBloodTypeHistory>(c),
            c, _factory.Clock, _factory.CurrentUser, new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            new EfRepository<TestDefinition>(c),
            antibodies: new EfRepository<AntibodyHistory>(c),
            bloodAttributes: new EfRepository<BloodAttributeDefinition>(c));

    private SpecimenService Specimens(BloodBankDbContext c) =>
        new(new EfRepository<Specimen>(c), new EfRepository<Patient>(c), new EfRepository<SpecimenTypeDefinition>(c),
            c, _factory.Clock);

    [Fact]
    public async Task VerifyAbid_PostsCatalogAntibodiesToHistory()
    {
        var (patientId, specimenId) = await SeedAsync("MRN-ABID-K", "ACC-ABID-K");
        long resultId;

        await using (var context = _factory.Create())
        {
            var entered = await Results(context).EnterResultAsync(
                new EnterResultRequest(specimenId, "ABID", "anti-K, anti-E"));
            Assert.True(entered.Succeeded, entered.Error);
            resultId = entered.Value!.Id;
            Assert.Empty(await context.AntibodyHistory.Where(a => a.PatientId == patientId).ToListAsync());
        }

        await using (var context = _factory.Create())
        {
            var verified = await Results(context).VerifyResultAsync(resultId);
            Assert.True(verified.Succeeded, verified.Error);
        }

        await using var check = _factory.Create();
        var history = await check.AntibodyHistory
            .Where(a => a.PatientId == patientId && a.IsActive)
            .ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.Contains(history, a => a.AntibodySpecificity == "anti-K" && a.BloodAttributeDefinitionId is not null);
        Assert.Contains(history, a => a.AntibodySpecificity == "anti-E" && a.BloodAttributeDefinitionId is not null);
        Assert.All(history, a => Assert.Equal(resultId, a.SourceResultId));
    }

    [Fact]
    public async Task VerifyAbid_Negative_DoesNotPostHistory()
    {
        var (patientId, specimenId) = await SeedAsync("MRN-ABID-NEG", "ACC-ABID-NEG");

        await using var context = _factory.Create();
        var entered = await Results(context).EnterResultAsync(
            new EnterResultRequest(specimenId, "ABID", "None identified"));
        Assert.True(entered.Succeeded, entered.Error);
        var verified = await Results(context).VerifyResultAsync(entered.Value!.Id);
        Assert.True(verified.Succeeded, verified.Error);
        Assert.Empty(await context.AntibodyHistory.Where(a => a.PatientId == patientId).ToListAsync());
    }

    [Fact]
    public async Task VerifyAbid_UnmatchedAnti_PostsFreeTextAndWarns()
    {
        var (patientId, specimenId) = await SeedAsync("MRN-ABID-VEL", "ACC-ABID-VEL");

        await using var context = _factory.Create();
        var entered = await Results(context).EnterResultAsync(
            new EnterResultRequest(specimenId, "ABID", "anti-Vel"));
        Assert.True(entered.Succeeded, entered.Error);
        var verified = await Results(context).VerifyResultAsync(entered.Value!.Id);
        Assert.True(verified.Succeeded, verified.Error);
        Assert.Contains(verified.Evaluation!.Warnings, w => w.Code == AntibodyIdentificationParser.UnmatchedRuleCode);

        var row = Assert.Single(await context.AntibodyHistory.Where(a => a.PatientId == patientId).ToListAsync());
        Assert.Equal("anti-Vel", row.AntibodySpecificity);
        Assert.Null(row.BloodAttributeDefinitionId);
        Assert.True(row.IsActive);
    }

    private async Task<(long PatientId, long SpecimenId)> SeedAsync(string mrn, string accession)
    {
        await using var context = _factory.Create();
        await EnsureCatalogAsync(context);

        var patient = new Patient
        {
            MedicalRecordNumber = mrn,
            LastName = "Abid",
            FirstName = "Tester",
            DateOfBirth = new DateOnly(1985, 3, 1),
            Sex = Sex.Unknown
        };
        context.Patients.Add(patient);
        await context.SaveChangesAsync();

        var specimen = await Specimens(context).AccessionAsync(
            new AccessionSpecimenRequest(accession, patient.Id, "EDTA", _factory.Clock.UtcNow.AddHours(-1)));
        Assert.True(specimen.Succeeded, specimen.Error);
        return (patient.Id, specimen.Value!.Id);
    }

    private async Task EnsureCatalogAsync(BloodBankDbContext context)
    {
        if (!await context.BloodAttributeDefinitions.AnyAsync())
        {
            var now = _factory.Clock.UtcNow;
            context.BloodAttributeDefinitions.AddRange(
                new BloodAttributeDefinition { Code = "K", Name = "Kell", AntibodyName = "anti-K", IsClinicallySignificant = true, SortOrder = 1, IsActive = true, IsDraft = false, EffectiveUtc = now, Version = 1 },
                new BloodAttributeDefinition { Code = "E", Name = "Rh E", AntibodyName = "anti-E", IsClinicallySignificant = true, SortOrder = 2, IsActive = true, IsDraft = false, EffectiveUtc = now, Version = 1 });
        }

        if (!await context.TestDefinitions.AnyAsync(t => t.Code == "ABID"))
        {
            context.TestDefinitions.Add(new TestDefinition
            {
                Code = "ABID",
                Name = "Antibody Identification",
                Category = TestCategory.AntibodyIdentification,
                ResultValueType = ResultValueType.FreeText,
                VerificationRequired = true,
                ContributesToAntibodyHistory = true,
                ContributesToCompatibility = true,
                IsActive = true,
                IsDraft = false,
                EffectiveUtc = _factory.Clock.UtcNow,
                Version = 1
            });
        }

        await context.SaveChangesAsync();
    }
}
