using BloodBankLIS.Application.Admin;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-306: PBM ordering criteria (OCD-038). Thresholds are fixtures, not shipped values.
public class TransfusionIndicationTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private TransfusionIndicationService Indications(BloodBankDbContext c) =>
        new(new EfRepository<TransfusionIndicationCriterion>(c),
            new EfRepository<TestResult>(c),
            new EfRepository<ProductType>(c),
            new EfRepository<OrderLine>(c),
            new EfRepository<Order>(c),
            new EfRepository<Patient>(c),
            new EfRepository<OrderingLocation>(c),
            new FacilityPolicyService(new EfRepository<SystemSetting>(c)),
            _factory.Clock);

    private OrderService Orders(BloodBankDbContext c) =>
        new(new EfRepository<Order>(c), new EfRepository<OrderLine>(c), new EfRepository<OrderSpecimen>(c),
            new EfRepository<Encounter>(c), new EfRepository<OrderingLocation>(c), new EfRepository<Patient>(c),
            new EfRepository<Specimen>(c), new EfRepository<OrderingProvider>(c), new EfRepository<ProductType>(c),
            new EfRepository<TestDefinition>(c), new EfRepository<TestGrouper>(c),
            _factory.Clock, c,
            currentUser: _factory.CurrentUser,
            indications: Indications(c));

    private TransfusionIndicationAdminService Admin(BloodBankDbContext c)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new(new EfRepository<TransfusionIndicationCriterion>(c), c, _factory.Clock, _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser, env),
            new ConfigurationHistoryWriter(c, _factory.Clock, _factory.CurrentUser, env));
    }

    [Fact]
    public async Task EvaluationOff_ChangesNothing_AndNoCriteriaIsReportedWhenOn()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);

        var off = await Indications(c).EvaluateAsync(f.Patient.Id, [f.RedCells.Id], _factory.Clock.UtcNow, null);
        Assert.Equal(PbmStatus.NotEvaluated, Assert.Single(off).Status);

        await SetAsync(c, FacilityPolicyKeys.PbmEvaluateOrders, true);
        var none = await Indications(c).EvaluateAsync(f.Patient.Id, [f.RedCells.Id], _factory.Clock.UtcNow, null);
        Assert.Equal(PbmStatus.NoCriteria, Assert.Single(none).Status);
        Assert.False(none[0].ReasonRequired);
    }

    [Fact]
    public async Task LabCriterion_UsesTheLatestVerifiedCurrentResult()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);
        await SetAsync(c, FacilityPolicyKeys.PbmEvaluateOrders, true);
        await AddCriteriaAsync(c);
        var now = _factory.Clock.UtcNow;

        await AddResultAsync(c, f, "LABX", "6.5", now.AddHours(-3));
        var met = await Indications(c).EvaluateAsync(f.Patient.Id, [f.RedCells.Id], now, null);
        Assert.Equal(PbmStatus.Met, met[0].Status);
        Assert.Equal("LOWX", met[0].MetCode);

        // A newer verified result above the threshold puts the order outside criteria.
        await AddResultAsync(c, f, "LABX", "9.4", now.AddHours(-1));
        // Unverified and superseded rows are ignored.
        await AddResultAsync(c, f, "LABX", "5.0", now.AddMinutes(-30), ResultStatus.Entered);
        var outside = await Indications(c).EvaluateAsync(f.Patient.Id, [f.RedCells.Id], now, null);
        Assert.Equal(PbmStatus.OutsideCriteria, outside[0].Status);
        Assert.Contains("LABX 9.4", outside[0].Detail);
        Assert.Equal(["BLEEDX"], outside[0].Options.Select(o => o.Code));
        Assert.False(outside[0].ReasonRequired);

        await SetAsync(c, FacilityPolicyKeys.PbmRequireJustification, true);
        var mustExplain = await Indications(c).EvaluateAsync(f.Patient.Id, [f.RedCells.Id], now, null);
        Assert.True(mustExplain[0].ReasonRequired);
        var attested = await Indications(c).EvaluateAsync(f.Patient.Id, [f.RedCells.Id], now, [new ProductIndicationInputDto(f.RedCells.Id, "BLEEDX")]);
        Assert.Equal(PbmStatus.Met, attested[0].Status);
    }

    [Fact]
    public async Task ManualOrderOutsideCriteria_NeedsAReason_InterfaceOrderIsRecorded_AndBothAreListedForReview()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);
        await SetAsync(c, FacilityPolicyKeys.PbmEvaluateOrders, true);
        await SetAsync(c, FacilityPolicyKeys.PbmRequireJustification, true);
        await AddCriteriaAsync(c);
        await AddResultAsync(c, f, "LABX", "9.4", _factory.Clock.UtcNow.AddHours(-1));

        var refused = await Orders(c).CreateAsync(f.Patient.Id, Request(f, "PBM-1", OrderSource.Manual, null));
        Assert.False(refused.Succeeded);
        Assert.StartsWith(TransfusionIndicationRule.JustificationCode, refused.Error);
        Assert.False(await c.Orders.AnyAsync(o => o.OrderNumber == "PBM-1"));

        var explained = await Orders(c).CreateAsync(f.Patient.Id, Request(f, "PBM-2", OrderSource.Manual,
            [new ProductIndicationInputDto(f.RedCells.Id, null, "Symptomatic, provider review")]));
        Assert.True(explained.Succeeded, explained.Error);
        var line = await c.OrderLines.SingleAsync(l => l.OrderId == explained.Value!.Id && l.LineCategory == OrderCategory.Product);
        Assert.Equal(PbmStatus.OutsideCriteria, line.PbmStatus);
        Assert.Equal("Symptomatic, provider review", line.PbmJustification);
        Assert.Contains("LABX 9.4", line.PbmDetail);

        var hl7 = await Orders(c).CreateFromHl7Async(f.Patient.Id, Request(f, "PBM-3", OrderSource.Hl7, null));
        Assert.True(hl7.Succeeded, hl7.Error);
        Assert.Equal(PbmStatus.OutsideCriteria,
            (await c.OrderLines.SingleAsync(l => l.OrderId == hl7.Value!.Id && l.LineCategory == OrderCategory.Product)).PbmStatus);

        var attested = await Orders(c).CreateAsync(f.Patient.Id, Request(f, "PBM-4", OrderSource.Manual,
            [new ProductIndicationInputDto(f.RedCells.Id, "BLEEDX")]));
        var attestedLine = await c.OrderLines.SingleAsync(l => l.OrderId == attested.Value!.Id && l.LineCategory == OrderCategory.Product);
        Assert.Equal(PbmStatus.Met, attestedLine.PbmStatus);
        Assert.Equal("BLEEDX", attestedLine.PbmIndicationCode);

        var review = await Indications(c).ListOutsideCriteriaAsync(_factory.Clock.UtcNow.AddDays(-1), _factory.Clock.UtcNow.AddDays(1));
        Assert.Equal(["PBM-2", "PBM-3"], review.Select(r => r.OrderNumber).OrderBy(n => n));
        Assert.All(review, r => Assert.Equal("MRN-PBM", r.Mrn));
    }

    [Fact]
    public async Task Catalog_ValidatesAndKeepsHistory()
    {
        await using var c = _factory.Create();
        var admin = Admin(c);

        var noThreshold = await admin.CreateAsync(new SaveTransfusionIndicationCriterionRequest(
            "x1", "Missing threshold", ComponentClass.Platelets, "plt", IndicationComparison.LessThan, null, null, 24, 10));
        Assert.False(noThreshold.Succeeded);

        var created = await admin.CreateAsync(new SaveTransfusionIndicationCriterionRequest(
            "x1", "Fixture rule", ComponentClass.Platelets, "plt", IndicationComparison.LessThan, 1m, "u", 48, 10));
        Assert.True(created.Succeeded);
        Assert.Equal("X1", created.Value!.Code);
        Assert.Equal("PLT", created.Value.LabTestCode);
        Assert.False((await admin.CreateAsync(new SaveTransfusionIndicationCriterionRequest(
            "X1", "Duplicate", ComponentClass.Platelets, null, null, null, null, 24, 20))).Succeeded);
        Assert.True((await admin.SetActiveAsync(created.Value.Id, false)).Succeeded);
        Assert.True(await c.ConfigurationChangeHistory.AnyAsync(h => h.EntityType == nameof(TransfusionIndicationCriterion)));
    }

    private sealed record Fixture(Patient Patient, Encounter Encounter, OrderingLocation Location, Specimen Specimen, ProductType RedCells);

    private CreateOrderRequest Request(Fixture f, string number, OrderSource source, IReadOnlyList<ProductIndicationInputDto>? indications) =>
        new(f.Encounter.Id, f.Location.Id, number,
            [new OrderLineInputDto(OrderCategory.Product, null, f.RedCells.Id)],
            OrderPriority.Routine, _factory.Clock.UtcNow, null, source, source == OrderSource.Hl7 ? "EHR" : "Manual", null,
            Indications: indications);

    // TEST-BB-319 (OCD-038): the unverified draft set loads inactive, never overwrites, and never evaluates.
    [Fact]
    public async Task LoadDrafts_AddsInactive_WithHistory_AndOrdersStillSeeNoCriteria()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);

        var loaded = await Admin(c).LoadDraftsAsync();

        Assert.Equal(PbmDraftCriteria.All.Count, loaded.Added.Count);
        Assert.Empty(loaded.Skipped);
        var rows = await c.TransfusionIndicationCriteria.ToListAsync();
        Assert.All(rows, r => Assert.False(r.IsActive));
        Assert.Equal(PbmDraftCriteria.All.Count, await c.ConfigurationChangeHistory.CountAsync(h =>
            h.EntityType == nameof(TransfusionIndicationCriterion) && h.ChangeReason!.Contains("Unverified draft")));

        await SetAsync(c, FacilityPolicyKeys.PbmEvaluateOrders, true);
        var result = await Indications(c).EvaluateAsync(f.Patient.Id, [f.RedCells.Id], _factory.Clock.UtcNow, null);
        Assert.Equal(PbmStatus.NoCriteria, Assert.Single(result).Status);
    }

    [Fact]
    public async Task LoadDrafts_Twice_SkipsExisting_AndNeverOverwritesAnEditedRow()
    {
        await using var c = _factory.Create();
        var admin = Admin(c);
        await admin.LoadDraftsAsync();
        var hb7 = await c.TransfusionIndicationCriteria.SingleAsync(r => r.Code == "DRAFT-HB7");
        hb7.Threshold = 6.5m;
        await c.SaveChangesAsync();

        var again = await admin.LoadDraftsAsync();

        Assert.Empty(again.Added);
        Assert.Equal(PbmDraftCriteria.All.Count, again.Skipped.Count);
        Assert.Equal(6.5m, (await c.TransfusionIndicationCriteria.SingleAsync(r => r.Code == "DRAFT-HB7")).Threshold);
    }

    [Fact]
    public async Task Draft_CannotBeActivated_UntilRenamed()
    {
        await using var c = _factory.Create();
        var admin = Admin(c);
        await admin.LoadDraftsAsync();
        var hb7 = await c.TransfusionIndicationCriteria.SingleAsync(r => r.Code == "DRAFT-HB7");

        var refused = await admin.SetActiveAsync(hb7.Id, true);
        Assert.False(refused.Succeeded);
        Assert.Contains(refused.Evaluation!.HardStops, h => h.Code == TransfusionIndicationRule.DraftCode);
        Assert.False((await c.TransfusionIndicationCriteria.AsNoTracking().SingleAsync(r => r.Id == hb7.Id)).IsActive);

        var renamed = await admin.UpdateAsync(hb7.Id, new SaveTransfusionIndicationCriterionRequest(
            "HB7", "Hemoglobin below 7 g/dL, hospitalized stable adult", hb7.ComponentClass, "HGB",
            IndicationComparison.LessThan, 7m, "g/dL", 24, 10));
        Assert.True(renamed.Succeeded, renamed.Error);
        var activated = await admin.SetActiveAsync(hb7.Id, true);
        Assert.True(activated.Succeeded, activated.Error);
    }

    private async Task<Fixture> SeedAsync(BloodBankDbContext c)
    {
        var patient = new Patient { MedicalRecordNumber = "MRN-PBM", LastName = "Blood", FirstName = "Pat", DateOfBirth = new DateOnly(1960, 1, 1), Sex = Sex.Male };
        var location = new OrderingLocation { Code = "PBM-WARD", Name = "Ward 4", IsActive = true };
        var redCells = new ProductType { ProductCode = "RBC-PBM", Name = "Red cells", ComponentClass = ComponentClass.RedBloodCells };
        c.AddRange(patient, location, redCells);
        await c.SaveChangesAsync();
        var encounter = new Encounter
        {
            PatientId = patient.Id, VisitNumber = "V-PBM", EncounterType = EncounterType.Inpatient,
            Status = EncounterStatus.Active, AdmitUtc = _factory.Clock.UtcNow.AddDays(-2)
        };
        c.Encounters.Add(encounter);
        await c.SaveChangesAsync();
        var specimen = new Specimen
        {
            AccessionNumber = "ACC-PBM", PatientId = patient.Id, EncounterId = encounter.Id, SpecimenType = "EDTA",
            CollectedUtc = _factory.Clock.UtcNow.AddHours(-5), Status = SpecimenStatus.Accepted
        };
        c.Specimens.Add(specimen);
        await c.SaveChangesAsync();
        return new Fixture(patient, encounter, location, specimen, redCells);
    }

    private static async Task AddCriteriaAsync(BloodBankDbContext c)
    {
        c.TransfusionIndicationCriteria.AddRange(
            new TransfusionIndicationCriterion
            {
                Code = "LOWX", Name = "Fixture low value", ComponentClass = ComponentClass.RedBloodCells,
                LabTestCode = "LABX", Comparison = IndicationComparison.LessThan, Threshold = 7m, Unit = "u", LookbackHours = 24, IsActive = true
            },
            new TransfusionIndicationCriterion
            {
                Code = "BLEEDX", Name = "Fixture attestation", ComponentClass = ComponentClass.RedBloodCells, IsActive = true, SortOrder = 20
            });
        await c.SaveChangesAsync();
    }

    private static async Task AddResultAsync(BloodBankDbContext c, Fixture f, string code, string value, DateTime verifiedUtc,
        ResultStatus status = ResultStatus.Verified)
    {
        c.TestResults.Add(new TestResult
        {
            SpecimenId = f.Specimen.Id, PatientId = f.Patient.Id, TestCode = code, Value = value, Units = "u",
            Status = status, EnteredUtc = verifiedUtc, VerifiedUtc = status == ResultStatus.Verified ? verifiedUtc : null
        });
        await c.SaveChangesAsync();
    }

    private static async Task SetAsync(BloodBankDbContext c, string key, bool on)
    {
        var row = await c.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (row is null)
        {
            c.SystemSettings.Add(new SystemSetting { Key = key, Value = on ? "true" : "false" });
        }
        else
        {
            row.Value = on ? "true" : "false";
        }

        await c.SaveChangesAsync();
    }
}
