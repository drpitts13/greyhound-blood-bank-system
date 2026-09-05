using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Immunohematology;
using BloodBankLIS.Application.Results;
using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class AntibodyIdentificationTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public AntibodyIdentificationTests(SqliteContextFactory factory) => _factory = factory;

    private ResultService Results(BloodBankDbContext c, bool withWorkups = false) =>
        new(new EfRepository<TestResult>(c), new EfRepository<Specimen>(c), new EfRepository<PatientBloodTypeHistory>(c),
            c, _factory.Clock, _factory.CurrentUser, new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            new EfRepository<TestDefinition>(c),
            antibodies: new EfRepository<AntibodyHistory>(c),
            bloodAttributes: new EfRepository<BloodAttributeDefinition>(c),
            antibodyWorkups: withWorkups ? new EfRepository<AntibodyIdentificationWorkup>(c) : null,
            antibodyFindings: withWorkups ? new EfRepository<AntibodyIdentificationFinding>(c) : null);

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

    [Fact]
    public async Task CreateWorkup_LinksSourceResultIdFromSpecimenAbid()
    {
        var (patientId, specimenId) = await SeedAsync("MRN-ABID-LINK", "ACC-ABID-LINK");
        var (_, lotId) = await SeedPanelAsync();
        long resultId;

        await using (var context = _factory.Create())
        {
            var entered = await Results(context).EnterResultAsync(
                new EnterResultRequest(specimenId, "ABID", "anti-K"));
            Assert.True(entered.Succeeded, entered.Error);
            resultId = entered.Value!.Id;
        }

        await using var check = _factory.Create();
        var created = await Abid(check).CreateWorkupAsync(
            patientId, new CreateAntibodyIdWorkupRequest(specimenId, lotId));
        Assert.True(created.Succeeded, created.Error);
        Assert.Equal(resultId, created.Value!.SourceResultId);
    }

    [Fact]
    public async Task VerifyAbid_OpenWorkupOnSpecimen_HardStopsAndDoesNotPost()
    {
        var (patientId, specimenId) = await SeedAsync("MRN-ABID-OPEN", "ACC-ABID-OPEN");
        var (_, lotId) = await SeedPanelAsync();
        long resultId;

        await using (var context = _factory.Create())
        {
            var created = await Abid(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(specimenId, lotId));
            Assert.True(created.Succeeded, created.Error);

            var entered = await Results(context, withWorkups: true).EnterResultAsync(
                new EnterResultRequest(specimenId, "ABID", "anti-K"));
            Assert.True(entered.Succeeded, entered.Error);
            resultId = entered.Value!.Id;

            var verified = await Results(context, withWorkups: true).VerifyResultAsync(resultId);
            Assert.False(verified.Succeeded);
            Assert.Contains(verified.Evaluation!.HardStops, r =>
                r.Code == AntibodyIdentificationHistoryPostRule.OpenWorkupCode);
        }

        await using var check = _factory.Create();
        var result = await check.TestResults.SingleAsync(r => r.Id == resultId);
        Assert.Equal(ResultStatus.Entered, result.Status);
        Assert.Empty(await check.AntibodyHistory.Where(a => a.PatientId == patientId).ToListAsync());
    }

    [Fact]
    public async Task VerifyAbid_AfterCompletedWorkup_DoesNotDuplicateHistory()
    {
        var (patientId, specimenId) = await SeedAsync("MRN-ABID-DONE", "ACC-ABID-DONE");
        var (attrId, lotId) = await SeedPanelAsync();
        var workupId = await CompleteReviewedWorkupAsync(patientId, specimenId, lotId, attrId, "anti-K");

        await using (var context = _factory.Create())
        {
            var entered = await Results(context, withWorkups: true).EnterResultAsync(
                new EnterResultRequest(specimenId, "ABID", "anti-K"));
            Assert.True(entered.Succeeded, entered.Error);
            var verified = await Results(context, withWorkups: true).VerifyResultAsync(entered.Value!.Id);
            Assert.True(verified.Succeeded, verified.Error);
            Assert.DoesNotContain(
                verified.Evaluation?.Warnings ?? [],
                w => w.Code == AntibodyIdentificationHistoryPostRule.DisagreeCode);
        }

        await using var check = _factory.Create();
        var history = Assert.Single(await check.AntibodyHistory.Where(a => a.PatientId == patientId && a.IsActive).ToListAsync());
        Assert.Equal("anti-K", history.AntibodySpecificity);
        Assert.Equal(attrId, history.BloodAttributeDefinitionId);
        Assert.True(workupId > 0);
    }

    [Fact]
    public async Task VerifyAbid_AfterVoidedWorkup_PostsHistory()
    {
        var (patientId, specimenId) = await SeedAsync("MRN-ABID-VPOST", "ACC-ABID-VPOST");
        var (_, lotId) = await SeedPanelAsync();
        long resultId;

        await using (var context = _factory.Create())
        {
            var created = await Abid(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(specimenId, lotId));
            Assert.True(created.Succeeded, created.Error);
            var voided = await Abid(context).VoidAsync(
                created.Value!.Id, new VoidAntibodyIdWorkupRequest("Wrong lot; restart identification."));
            Assert.True(voided.Succeeded, voided.Error);

            var entered = await Results(context, withWorkups: true).EnterResultAsync(
                new EnterResultRequest(specimenId, "ABID", "anti-K"));
            Assert.True(entered.Succeeded, entered.Error);
            resultId = entered.Value!.Id;
            var verified = await Results(context, withWorkups: true).VerifyResultAsync(resultId);
            Assert.True(verified.Succeeded, verified.Error);
        }

        await using var check = _factory.Create();
        var history = Assert.Single(await check.AntibodyHistory.Where(a => a.PatientId == patientId && a.IsActive).ToListAsync());
        Assert.Equal("anti-K", history.AntibodySpecificity);
        Assert.Equal(resultId, history.SourceResultId);
    }

    [Fact]
    public async Task VerifyAbid_AfterCompletedWorkup_DisagreeWarnsAndDoesNotPostExtra()
    {
        var (patientId, specimenId) = await SeedAsync("MRN-ABID-DIS", "ACC-ABID-DIS");
        var (attrId, lotId) = await SeedPanelAsync();
        await CompleteReviewedWorkupAsync(patientId, specimenId, lotId, attrId, "anti-K");

        await using var context = _factory.Create();
        var entered = await Results(context, withWorkups: true).EnterResultAsync(
            new EnterResultRequest(specimenId, "ABID", "anti-K, anti-E"));
        Assert.True(entered.Succeeded, entered.Error);
        var verified = await Results(context, withWorkups: true).VerifyResultAsync(entered.Value!.Id);
        Assert.True(verified.Succeeded, verified.Error);
        Assert.Contains(verified.Evaluation!.Warnings, w =>
            w.Code == AntibodyIdentificationHistoryPostRule.DisagreeCode);

        var history = Assert.Single(await context.AntibodyHistory.Where(a => a.PatientId == patientId && a.IsActive).ToListAsync());
        Assert.Equal("anti-K", history.AntibodySpecificity);
        Assert.DoesNotContain(await context.AntibodyHistory.Where(a => a.PatientId == patientId).ToListAsync(),
            a => a.AntibodySpecificity == "anti-E");
    }

    private AntibodyIdentificationService Abid(BloodBankDbContext c, ICurrentUser? user = null)
    {
        var current = user ?? _factory.CurrentUser;
        return new AntibodyIdentificationService(
            new EfRepository<AntibodyPanelManufacturer>(c),
            new EfRepository<AntibodyPanelLot>(c),
            new EfRepository<AntibodyPanelCell>(c),
            new EfRepository<AntibodyPanelCellAntigen>(c),
            new EfRepository<AntibodyIdentificationWorkup>(c),
            new EfRepository<AntibodyIdentificationWorkupLot>(c),
            new EfRepository<AntibodyIdentificationReaction>(c),
            new EfRepository<AntibodyIdentificationFinding>(c),
            new EfRepository<AntibodyHistory>(c),
            new EfRepository<AntigenProfile>(c),
            new EfRepository<BloodAttributeDefinition>(c),
            new EfRepository<Patient>(c),
            new EfRepository<Specimen>(c),
            new FacilityPolicyService(new EfRepository<SystemSetting>(c)),
            c,
            _factory.Clock,
            current,
            new AuditWriter(c, _factory.Clock, current),
            results: new EfRepository<TestResult>(c),
            testDefinitions: new EfRepository<TestDefinition>(c));
    }

    private async Task<long> CompleteReviewedWorkupAsync(
        long patientId, long specimenId, long lotId, long attrId, string specificity)
    {
        long workupId;
        await using (var context = _factory.Create())
        {
            var created = await Abid(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(specimenId, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;
            var ahg = created.Value.Cells
                .Where(c => c.Role != PanelCellRole.Autocontrol)
                .Select(c => new RecordAntibodyIdReactionRequest(c.CellId, "AHG", ReactionGrade.Negative))
                .ToList();
            var recorded = await Abid(context).RecordReactionsAsync(workupId, ahg);
            Assert.True(recorded.Succeeded, recorded.Error);

            var interpreted = await Abid(context).RecordInterpretationAsync(
                workupId,
                new RecordAntibodyIdInterpretationRequest(
                    $"{specificity} identified by pattern.",
                    [new AntibodyIdInterpretationItem(attrId, specificity, AntibodyIdClassification.Identified, "Technologist identification")]));
            Assert.True(interpreted.Succeeded, interpreted.Error);
        }

        await using (var context = _factory.Create())
        {
            var reviewed = await Abid(context, new TestCurrentUser("supervisor-abid", "WS-2"))
                .ReviewAsync(workupId, new ReviewAntibodyIdWorkupRequest(
                    true,
                    $"Agree with {specificity}.",
                    "Reviewed leftover CannotExclude, history, DAT, none-identified, and conflicting findings."));
            Assert.True(reviewed.Succeeded, reviewed.Error);
        }

        await using (var context = _factory.Create())
        {
            var completed = await Abid(context).CompleteAsync(
                workupId,
                new CompleteAntibodyIdWorkupRequest(
                    "Reviewed leftover CannotExclude, history, DAT, none-identified, and conflicting findings."));
            Assert.True(completed.Succeeded, completed.Error);
        }

        return workupId;
    }

    private async Task<(long KellId, long LotId)> SeedPanelAsync()
    {
        await using var context = _factory.Create();
        await EnsureCatalogAsync(context);
        var now = _factory.Clock.UtcNow;
        var kell = await context.BloodAttributeDefinitions.SingleAsync(a => a.Code == "K");

        var manufacturer = new AntibodyPanelManufacturer
        {
            Code = $"M-{Guid.NewGuid():N}"[..12],
            Name = "Test Manufacturer",
            IsActive = true,
            IsDraft = false,
            EffectiveUtc = now,
            Version = 1
        };
        context.AntibodyPanelManufacturers.Add(manufacturer);
        await context.SaveChangesAsync();

        var lot = new AntibodyPanelLot
        {
            ManufacturerId = manufacturer.Id,
            LotNumber = $"LOT-{Guid.NewGuid():N}"[..12],
            ExpiresOn = new DateOnly(2027, 12, 31),
            PanelName = "Test panel",
            IsActive = true
        };
        context.AntibodyPanelLots.Add(lot);
        await context.SaveChangesAsync();

        var pos = new AntibodyPanelCell { LotId = lot.Id, CellNumber = "1", Role = PanelCellRole.Panel, SortOrder = 1 };
        var neg = new AntibodyPanelCell { LotId = lot.Id, CellNumber = "2", Role = PanelCellRole.Panel, SortOrder = 2 };
        var ac = new AntibodyPanelCell { LotId = lot.Id, CellNumber = "AC", Role = PanelCellRole.Autocontrol, SortOrder = 3 };
        context.AntibodyPanelCells.AddRange(pos, neg, ac);
        await context.SaveChangesAsync();

        context.AntibodyPanelCellAntigens.AddRange(
            new AntibodyPanelCellAntigen { CellId = pos.Id, BloodAttributeDefinitionId = kell.Id, Expression = AntigenExpression.Present },
            new AntibodyPanelCellAntigen { CellId = neg.Id, BloodAttributeDefinitionId = kell.Id, Expression = AntigenExpression.Absent });
        await context.SaveChangesAsync();

        return (kell.Id, lot.Id);
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
