using BloodBankLIS.Application.Compatibility;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Application.Results;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;
using BloodBankLIS.HL7.Messaging;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-519: numeric results are checked against the test's limits and flagged; titers are stored with their
// readings, endpoint, and score; history shows the flag and the titer trend; the EHR message carries both.
public class NumericTiterResultTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private ResultService Results(BloodBankDbContext c) =>
        new(new EfRepository<TestResult>(c), new EfRepository<Specimen>(c), new EfRepository<PatientBloodTypeHistory>(c),
            c, _factory.Clock, _factory.CurrentUser, new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            new EfRepository<TestDefinition>(c), new EfRepository<SubtestDefinition>(c), new EfRepository<Order>(c), new EfRepository<OrderLine>(c),
            new InventoryRepository(c), Compatibility(c), new EfRepository<AntibodyHistory>(c),
            new EfRepository<AntigenProfile>(c), new EfRepository<BloodAttributeDefinition>(c),
            new EfRepository<UnitBloodAttribute>(c), new EfRepository<SpecimenTypeDefinition>(c));

    private CompatibilityService Compatibility(BloodBankDbContext c) =>
        new(new InventoryRepository(c), new EfRepository<Crossmatch>(c), new EfRepository<Allocation>(c),
            new EfRepository<Patient>(c), new EfRepository<Specimen>(c), new EfRepository<ProductType>(c),
            new EfRepository<PatientBloodTypeHistory>(c),
            new BloodAttributeCompatLoader(new EfRepository<AntibodyHistory>(c), new EfRepository<AntigenProfile>(c),
                new EfRepository<UnitBloodAttribute>(c), new EfRepository<BloodAttributeDefinition>(c)),
            new AntibodyScreenCompatLoader(new EfRepository<TestResult>(c), new EfRepository<TestDefinition>(c), new EfRepository<AntibodyHistory>(c)),
            c, _factory.Clock, _factory.CurrentUser);

    private OrderService Orders(BloodBankDbContext c) =>
        new(new EfRepository<Order>(c), new EfRepository<OrderLine>(c), new EfRepository<OrderSpecimen>(c),
            new EfRepository<Encounter>(c), new EfRepository<OrderingLocation>(c), new EfRepository<Patient>(c),
            new EfRepository<Specimen>(c), new EfRepository<OrderingProvider>(c), new EfRepository<ProductType>(c),
            new EfRepository<TestDefinition>(c), new EfRepository<TestGrouper>(c), _factory.Clock, c);

    private PatientTestHistoryService History(BloodBankDbContext c) =>
        new(new EfRepository<TestResult>(c), new EfRepository<Specimen>(c), new EfRepository<Order>(c), new EfRepository<TestDefinition>(c));

    private sealed record Fixture(Patient Patient, Specimen Specimen, PatientOrderDto Order);

    private async Task<Fixture> SeedAsync(BloodBankDbContext c)
    {
        var patient = new Patient { MedicalRecordNumber = "MRN-NT1", LastName = "Titer", FirstName = "Pat", DateOfBirth = new DateOnly(1990, 1, 1), Sex = Sex.Female };
        var location = new OrderingLocation { Code = "LOC-NT", Name = "Lab", IsActive = true };
        c.AddRange(patient, location);
        await c.SaveChangesAsync();
        var encounter = new Encounter { PatientId = patient.Id, VisitNumber = "VIS-NT", Status = EncounterStatus.Active, AdmitUtc = _factory.Clock.UtcNow.AddDays(-1) };
        c.Encounters.Add(encounter);
        await c.SaveChangesAsync();
        var specimen = new Specimen
        {
            AccessionNumber = "ACC-NT1", PatientId = patient.Id, EncounterId = encounter.Id, SpecimenType = "EDTA",
            CollectedUtc = _factory.Clock.UtcNow.AddHours(-2), ExpiresUtc = _factory.Clock.UtcNow.AddDays(60), Status = SpecimenStatus.Accepted
        };
        c.Specimens.Add(specimen);
        c.TestDefinitions.AddRange(
            new TestDefinition
            {
                Code = "FMHQ", Name = "Fetal bleed quantitation", ResultValueType = ResultValueType.Numeric, ResultUnits = "%",
                DecimalPlaces = 2, ReportableLow = 0, ReportableHigh = 100, ReferenceLow = 0, ReferenceHigh = 0.3m, CriticalHigh = 5,
                IsActive = true, IsDraft = false, EffectiveUtc = _factory.Clock.UtcNow
            },
            new TestDefinition
            {
                Code = "ABTITER", Name = "Antibody titer", Category = TestCategory.AntibodyTiter, ResultValueType = ResultValueType.Titer,
                TiterStartDilution = 1, TiterEndDilution = 2048, TiterPhaseCode = "AHG", TiterEndpointMinGrade = "1+", TiterScored = true,
                IsActive = true, IsDraft = false, EffectiveUtc = _factory.Clock.UtcNow
            });
        await c.SaveChangesAsync();

        var created = await Orders(c).CreateAsync(patient.Id, new CreateOrderRequest(
            encounter.Id, location.Id, "ORD-NT1",
            [new OrderLineInputDto(OrderCategory.Test, "FMHQ", null), new OrderLineInputDto(OrderCategory.Test, "ABTITER", null)],
            OrderPriority.Routine, _factory.Clock.UtcNow, null, OrderSource.Manual, null, null, SpecimenId: specimen.Id));
        Assert.True(created.Succeeded, created.Error);

        // TEST-BB-631: a needed-by time is kept with the order; one before the order was placed is refused.
        var tooEarly = await Orders(c).CreateAsync(patient.Id, new CreateOrderRequest(
            encounter.Id, location.Id, "ORD-NT-EARLY", [new OrderLineInputDto(OrderCategory.Test, "FMHQ", null)],
            OrderPriority.Routine, _factory.Clock.UtcNow, null, OrderSource.Manual, null, null, NeededByUtc: _factory.Clock.UtcNow.AddHours(-2)));
        Assert.Contains("before the order was placed", tooEarly.Error);
        var timed = await Orders(c).CreateAsync(patient.Id, new CreateOrderRequest(
            encounter.Id, location.Id, "ORD-NT-TIMED", [new OrderLineInputDto(OrderCategory.Test, "FMHQ", null)],
            OrderPriority.Timed, _factory.Clock.UtcNow, null, OrderSource.Manual, null, null, NeededByUtc: _factory.Clock.UtcNow.AddHours(3)));
        Assert.True(timed.Succeeded, timed.Error);
        Assert.Equal(_factory.Clock.UtcNow.AddHours(3), timed.Value!.NeededByUtc);
        Assert.Equal(_factory.Clock.UtcNow.AddHours(3), (await Orders(c).ListByPatientAsync(patient.Id)).Single(o => o.OrderNumber == "ORD-NT-TIMED").NeededByUtc);
        return new Fixture(patient, specimen, Assert.Single(await Orders(c).ListByPatientAsync(patient.Id), o => o.OrderNumber == "ORD-NT1"));
    }

    private static SaveTestResultRequest Save(Fixture f, string code, string? value, bool complete = false,
        IReadOnlyList<TiterReading>? readings = null, string? correction = null) =>
        new(f.Specimen.Id, f.Order.Id, f.Order.Lines.Single(l => l.TestCode == code).Id, code, value, null, null, null, null, null,
            MarkComplete: complete, CorrectionReason: correction, UnitNumber: null, CrossmatchMethod: null, CrossmatchResult: null,
            AntibodyScreenNegative: null, TiterReadings: readings);

    // Each save in its own context, as each request has its own in the application.
    private async Task<BloodBankLIS.Application.Common.EvaluationResult<TestResult>> SaveAsync(SaveTestResultRequest request)
    {
        await using var c = _factory.Create();
        return await Results(c).SaveTestResultAsync(request);
    }

    private static IReadOnlyList<RuleResult> Warnings(BloodBankLIS.Application.Common.EvaluationResult<TestResult> r) =>
        r.Evaluation?.Warnings ?? [];

    private static TiterReading[] Read(params string[] grades) => grades.Select((g, i) => new TiterReading(1 << i, g)).ToArray();

    // TEST-BB-633: an open order's needed-by time is changed and cleared, each audited with the earlier time; a time before
    // the order and a cancelled order are refused.
    [Fact]
    public async Task NeededBy_IsChangedOrCleared_WithTheEarlierTimeKept()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);
        var orders = new OrderService(new EfRepository<Order>(c), new EfRepository<OrderLine>(c), new EfRepository<OrderSpecimen>(c),
            new EfRepository<Encounter>(c), new EfRepository<OrderingLocation>(c), new EfRepository<Patient>(c),
            new EfRepository<Specimen>(c), new EfRepository<OrderingProvider>(c), new EfRepository<ProductType>(c),
            new EfRepository<TestDefinition>(c), new EfRepository<TestGrouper>(c), _factory.Clock, c,
            audit: new AuditWriter(c, _factory.Clock, _factory.CurrentUser));
        var now = _factory.Clock.UtcNow;

        Assert.Contains("before the order was placed",
            (await orders.SetNeededByAsync(f.Patient.Id, f.Order.Id, new SetOrderNeededByRequest(f.Order.OrderedUtc.AddHours(-3)))).Error);
        Assert.True((await orders.SetNeededByAsync(f.Patient.Id, f.Order.Id, new SetOrderNeededByRequest(now.AddHours(2)))).Succeeded);
        var moved = await orders.SetNeededByAsync(f.Patient.Id, f.Order.Id, new SetOrderNeededByRequest(now.AddHours(5)));
        Assert.True(moved.Succeeded, moved.Error);
        Assert.Equal(now.AddHours(5), (await c.Orders.AsNoTracking().SingleAsync(o => o.Id == f.Order.Id)).NeededByUtc);
        var changes = await c.AuditEvents.Where(a => a.EntityType == nameof(Order) && a.EntityId == f.Order.Id && a.Reason == "Needed-by time changed.").ToListAsync();
        Assert.Equal(2, changes.Count);

        Assert.True((await orders.SetNeededByAsync(f.Patient.Id, f.Order.Id, new SetOrderNeededByRequest(null))).Succeeded);
        Assert.Null((await c.Orders.AsNoTracking().SingleAsync(o => o.Id == f.Order.Id)).NeededByUtc);
        Assert.True(await c.AuditEvents.AnyAsync(a => a.EntityId == f.Order.Id && a.Reason == "Needed-by time cleared."));
        Assert.Contains("not found", (await orders.SetNeededByAsync(f.Patient.Id, -1, new SetOrderNeededByRequest(null))).Error);

        var row = await c.Orders.SingleAsync(o => o.Id == f.Order.Id);
        row.Status = OrderStatus.Cancelled;
        await c.SaveChangesAsync();
        Assert.Contains("Cancelled", (await orders.SetNeededByAsync(f.Patient.Id, f.Order.Id, new SetOrderNeededByRequest(now.AddHours(1)))).Error);
    }

    [Fact]
    public async Task Numeric_IsChecked_Flagged_AndSentWithUnitsAndFlag()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);

        var tooHigh = await SaveAsync(Save(f, "FMHQ", "120"));
        Assert.False(tooHigh.Succeeded);
        Assert.Contains(NumericResultRule.ReportableCode, tooHigh.Error);
        Assert.Contains(NumericResultRule.DecimalsCode, (await SaveAsync(Save(f, "FMHQ", "0.456"))).Error);

        var high = await SaveAsync(Save(f, "FMHQ", "0.45"));
        Assert.True(high.Succeeded, high.Error);
        Assert.Equal(("%", "H"), (high.Value!.Units, high.Value.AbnormalFlag));
        Assert.DoesNotContain(Warnings(high), w => w.Code == NumericResultRule.CriticalCode);

        var critical = await SaveAsync(Save(f, "FMHQ", "6.10", complete: true));
        Assert.True(critical.Succeeded, critical.Error);
        Assert.Equal("HH", critical.Value!.AbnormalFlag);
        Assert.Contains(Warnings(critical), w => w.Code == NumericResultRule.CriticalCode);

        var row = (await History(c).ListByPatientAsync(f.Patient.Id)).Single(r => r.TestCode == "FMHQ" && r.IsCurrent);
        Assert.Equal(("6.10 %", "HH"), (ResultValueText.For(row.Value, row.Units), row.AbnormalFlag));

        var oru = Hl7OruBuilder.Build(f.Patient, critical.Value, "CTRL-1", _factory.Clock.UtcNow);
        var fields = oru.Split('\r').Single(s => s.StartsWith("OBX")).Split('|');
        Assert.Equal("NM", fields[2]);
        Assert.Equal("6.10", fields[5]);
        Assert.Equal("%", fields[6]);
        Assert.Equal("HH", fields[8]);
    }

    [Fact]
    public async Task Titer_IsStoredWithEndpointAndScore_AndACorrectionRecomputes()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);

        var missing = await SaveAsync(Save(f, "ABTITER", null, readings: [new TiterReading(1, "3+"), new TiterReading(4, "1+")]));
        Assert.False(missing.Succeeded);
        Assert.Contains(TiterRule.MissingCode, missing.Error);

        var first = await SaveAsync(Save(f, "ABTITER", null, complete: true, readings: Read("3+", "2+", "1+", "1+", "0")));
        Assert.True(first.Succeeded, first.Error);
        Assert.Equal("Titer 1:8 (score 28)", first.Value!.Interpretation);
        Assert.True(TiterResultValue.TryParse(first.Value.Value, out var stored));
        Assert.Equal(5, stored!.Readings.Count);
        Assert.Equal("1:8", ResultValueText.For(first.Value.Value));

        var corrected = await SaveAsync(Save(f, "ABTITER", null, complete: true,
            readings: Read("4+", "3+", "2+", "2+", "1+", "1+", "0"), correction: "Dilution 1:16 misread"));
        Assert.True(corrected.Succeeded, corrected.Error);
        Assert.Equal("Titer 1:32 (score 48)", corrected.Value!.Interpretation);

        var oru = Hl7OruBuilder.Build(f.Patient, corrected.Value, "CTRL-2", _factory.Clock.UtcNow);
        var obx = oru.Split('\r').Single(s => s.StartsWith("OBX")).Split('|');
        Assert.Equal(("ST", "1:32"), (obx[2], obx[5]));

        var pattern = await SaveAsync(Save(f, "ABTITER", null, readings: Read("2+", "0", "1+"), correction: "Retested"));
        Assert.True(pattern.Succeeded, pattern.Error);
        Assert.Contains(Warnings(pattern), w => w.Code == TiterRule.PatternCode);
    }

    [Fact]
    public async Task History_TrendsTitersOfTheSameTest_OldestFirst()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);
        var settings = new TiterSettings(1, 2048, "AHG", "1+", false);
        var eight = TiterRule.Evaluate(settings, Read("3+", "2+", "1+", "1+", "0")).Value!;
        var thirtyTwo = TiterRule.Evaluate(settings, Read("4+", "3+", "2+", "2+", "1+", "1+", "0")).Value!;
        c.TestResults.AddRange(
            new TestResult
            {
                SpecimenId = f.Specimen.Id, PatientId = f.Patient.Id, TestCode = "ABTITER", Version = 1, Value = eight.Serialize(),
                Interpretation = eight.Interpretation, Status = ResultStatus.Verified, VerifiedUtc = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc)
            },
            new TestResult
            {
                SpecimenId = f.Specimen.Id, PatientId = f.Patient.Id, TestCode = "ABTITER", Version = 1, Value = thirtyTwo.Serialize(),
                Interpretation = thirtyTwo.Interpretation, Status = ResultStatus.Verified, VerifiedUtc = new DateTime(2026, 10, 22, 9, 0, 0, DateTimeKind.Utc)
            });
        await c.SaveChangesAsync();

        var rows = (await History(c).ListByPatientAsync(f.Patient.Id)).Where(r => r.TestCode == "ABTITER").ToList();
        Assert.Equal("2 dilutions higher than 1:8 on 2026-10-01", rows.Single(r => r.Value == thirtyTwo.Serialize()).Trend);
        Assert.Null(rows.Single(r => r.Value == eight.Serialize()).Trend);
    }

    // TEST-BB-522: a numeric result queued for the EHR carries the test's reference range in OBX-7, beside units and flag.
    [Fact]
    public async Task QueuedNumericResult_CarriesTheReferenceRange()
    {
        long resultId;
        await using (var c = _factory.Create())
        {
            var f = await SeedAsync(c);
            var saved = await SaveAsync(Save(f, "FMHQ", "0.45", complete: true));
            Assert.True(saved.Succeeded, saved.Error);
            resultId = saved.Value!.Id;
        }

        await using var context = _factory.Create();
        var outbound = new BloodBankLIS.HL7.Messaging.Hl7OutboundService(
            new EfRepository<TestResult>(context), new EfRepository<Patient>(context), new EfRepository<Hl7MessageLog>(context), context,
            _factory.Clock, endpoints: new EfRepository<InterfaceEndpoint>(context), testDefinitions: new EfRepository<TestDefinition>(context));
        var queued = await outbound.QueueResultMessageAsync(resultId);
        Assert.True(queued.Succeeded, queued.Error);
        var obx = queued.Value!.RawMessage.Split('\r').Single(s => s.StartsWith("OBX")).Split('|');
        Assert.Equal(("NM", "0.45", "%", "0.00-0.30", "H"), (obx[2], obx[5], obx[6], obx[7], obx[8]));

        // TEST-BB-523: no LOINC code configured: OBX-3 is the local code alone, as before.
        Assert.Equal("FMHQ", obx[3]);

        var definition = await context.TestDefinitions.SingleAsync(t => t.Code == "FMHQ");
        definition.LoincCode = "882-1";
        await context.SaveChangesAsync();
        var coded = await outbound.QueueResultMessageAsync(resultId);
        var codedObx = coded.Value!.RawMessage.Split((char)13).Single(s => s.StartsWith("OBX")).Split('|');
        Assert.Equal("FMHQ^Fetal bleed quantitation^L^882-1^Fetal bleed quantitation^LN", codedObx[3]);

        Assert.Equal(">=0.1", new NumericLimits(ReferenceLow: 0.1m).Hl7ReferenceRange());
        Assert.Equal("<=5", new NumericLimits(ReferenceHigh: 5m).Hl7ReferenceRange());
        Assert.Null(new NumericLimits().Hl7ReferenceRange());
    }
}
