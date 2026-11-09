using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Compatibility;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Application.Results;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class TestWorklistTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public TestWorklistTests(SqliteContextFactory factory) => _factory = factory;

    private TestWorklistService Worklist(BloodBankDbContext c) =>
        new(new EfRepository<Order>(c), new EfRepository<OrderLine>(c), new EfRepository<OrderSpecimen>(c),
            new EfRepository<Specimen>(c), new EfRepository<Patient>(c),
            new EfRepository<PatientBloodTypeHistory>(c), new EfRepository<AntibodyHistory>(c),
            new EfRepository<TestResult>(c),
            new EfRepository<TestDefinition>(c), new EfRepository<SpecimenTypeDefinition>(c), _factory.Clock,
            eligibility: Eligibility(c),
            workups: new EfRepository<AntibodyIdentificationWorkup>(c),
            policy: new BloodBankLIS.Application.Compliance.FacilityPolicyService(new EfRepository<SystemSetting>(c)));

    private ElectronicCrossmatchEligibilityService Eligibility(BloodBankDbContext c) =>
        new(new EfRepository<Patient>(c),
            new EfRepository<PatientBloodTypeHistory>(c),
            new EfRepository<AntibodyHistory>(c),
            new EfRepository<AntibodyIdentificationWorkup>(c),
            new AntibodyScreenCompatLoader(
                new EfRepository<TestResult>(c),
                new EfRepository<TestDefinition>(c),
                new EfRepository<AntibodyHistory>(c)),
            new BloodBankLIS.Application.Compliance.FacilityPolicyService(new EfRepository<SystemSetting>(c)));

    private ResultService Results(BloodBankDbContext c, bool requireOrderSpecimen = false, bool withPhases = false) =>
        new(new EfRepository<TestResult>(c), new EfRepository<Specimen>(c), new EfRepository<PatientBloodTypeHistory>(c),
            c, _factory.Clock, _factory.CurrentUser, new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            new EfRepository<TestDefinition>(c), new EfRepository<SubtestDefinition>(c), new EfRepository<Order>(c), new EfRepository<OrderLine>(c),
            new InventoryRepository(c), Compatibility(c), new EfRepository<AntibodyHistory>(c),
            new EfRepository<AntigenProfile>(c), new EfRepository<BloodAttributeDefinition>(c),
            new EfRepository<UnitBloodAttribute>(c), new EfRepository<SpecimenTypeDefinition>(c),
            allocations: new EfRepository<Allocation>(c),
            orderSpecimens: requireOrderSpecimen ? new EfRepository<OrderSpecimen>(c) : null,
            phaseDefinitions: withPhases ? new EfRepository<PhaseDefinition>(c) : null);

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

    private OrderService Orders(BloodBankDbContext c) =>
        new(new EfRepository<Order>(c), new EfRepository<OrderLine>(c), new EfRepository<OrderSpecimen>(c),
            new EfRepository<Encounter>(c), new EfRepository<OrderingLocation>(c), new EfRepository<Patient>(c),
            new EfRepository<Specimen>(c), new EfRepository<OrderingProvider>(c), new EfRepository<ProductType>(c),
            new EfRepository<TestDefinition>(c), new EfRepository<TestGrouper>(c), _factory.Clock, c);

    private async Task<(Patient patient, Encounter encounter, OrderingLocation location, Specimen? specimen, PatientOrderDto order, OrderLineDto line)> SeedOrderWithTestAsync(
        BloodBankDbContext c, string testCode = "ABSC", bool linkSpecimen = true, SpecimenStatus specimenStatus = SpecimenStatus.Accepted)
    {
        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-{Guid.NewGuid():N}",
            LastName = "Worklist",
            FirstName = "Patient",
            DateOfBirth = new DateOnly(1990, 3, 4),
            Sex = Sex.Female
        };
        c.Patients.Add(patient);
        var location = new OrderingLocation { Code = $"LOC-{Guid.NewGuid():N}", Name = "Lab", IsActive = true };
        c.OrderingLocations.Add(location);
        await c.SaveChangesAsync();

        var encounter = new Encounter
        {
            PatientId = patient.Id,
            VisitNumber = $"VIS-{Guid.NewGuid():N}",
            EncounterType = EncounterType.Inpatient,
            Status = EncounterStatus.Active,
            AdmitUtc = _factory.Clock.UtcNow.AddDays(-1)
        };
        c.Encounters.Add(encounter);
        await c.SaveChangesAsync();

        Specimen? specimen = null;
        if (linkSpecimen)
        {
            specimen = new Specimen
            {
                AccessionNumber = $"ACC-{Guid.NewGuid():N}",
                PatientId = patient.Id,
                EncounterId = encounter.Id,
                SpecimenType = "EDTA",
                CollectedUtc = _factory.Clock.UtcNow.AddHours(-2),
                ExpiresUtc = _factory.Clock.UtcNow.AddDays(2),
                Status = specimenStatus
            };
            c.Specimens.Add(specimen);
            await c.SaveChangesAsync();
        }

        c.TestDefinitions.Add(new TestDefinition
        {
            Code = testCode,
            Name = testCode,
            Category = string.Equals(testCode, "XM", StringComparison.OrdinalIgnoreCase) ? TestCategory.Crossmatch : TestCategory.Other,
            ResultValueType = ResultValueType.Coded,
            AllowedResultValues = string.Equals(testCode, "XM", StringComparison.OrdinalIgnoreCase)
                ? "Compatible\nIncompatible"
                : "Negative\nPositive",
            IsActive = true,
            IsDraft = false,
            EffectiveUtc = _factory.Clock.UtcNow
        });
        await c.SaveChangesAsync();

        var created = await Orders(c).CreateAsync(patient.Id, new CreateOrderRequest(
            encounter.Id, location.Id, $"ORD-{Guid.NewGuid():N}",
            [new OrderLineInputDto(OrderCategory.Test, testCode, null)],
            OrderPriority.Routine, _factory.Clock.UtcNow, null, OrderSource.Manual, null, null));
        Assert.True(created.Succeeded);

        var list = await Orders(c).ListByPatientAsync(patient.Id);
        var order = Assert.Single(list);
        var line = Assert.Single(order.Lines);
        return (patient, encounter, location, specimen, order, line);
    }

    [Fact]
    public async Task PendingWorklist_ListsRushPriorityAheadOfNewerRoutine()
    {
        await using var c = _factory.Create();
        var (patient, encounter, location, _, _, _) = await SeedOrderWithTestAsync(c, testCode: "ABSC");

        var routine = await Orders(c).CreateAsync(patient.Id, new CreateOrderRequest(
            encounter.Id, location.Id, $"ORD-ROUTINE-{Guid.NewGuid():N}",
            [new OrderLineInputDto(OrderCategory.Test, "ABSC", null)],
            OrderPriority.Routine, _factory.Clock.UtcNow, null, OrderSource.Manual, null, null));
        Assert.True(routine.Succeeded);

        var statNumber = $"ORD-STAT-{Guid.NewGuid():N}";
        var stat = await Orders(c).CreateAsync(patient.Id, new CreateOrderRequest(
            encounter.Id, location.Id, statNumber,
            [new OrderLineInputDto(OrderCategory.Test, "ABSC", null)],
            OrderPriority.Stat, _factory.Clock.UtcNow.AddHours(-5), null, OrderSource.Manual, null, null));
        Assert.True(stat.Succeeded);

        var pending = await Worklist(c).ListForPatientAsync(patient.Id, TestWorklistFilter.Pending);

        Assert.Equal(statNumber, pending[0].OrderNumber);
        Assert.Equal(OrderPriority.Stat, pending[0].Priority);
        Assert.Contains(pending.Skip(1), item => item.Priority == OrderPriority.Routine);
    }

    [Fact]
    public async Task LinkSpecimen_EnablesWorklistForOrderTests()
    {
        await using var c = _factory.Create();
        var (patient, _, _, _, order, line) = await SeedOrderWithTestAsync(c, linkSpecimen: false);

        var blocked = await Worklist(c).ListForPatientAsync(patient.Id, TestWorklistFilter.Pending);
        var blockedItem = Assert.Single(blocked);
        Assert.False(blockedItem.CanEnterResults);

        var specimen = new Specimen
        {
            AccessionNumber = $"ACC-{Guid.NewGuid():N}",
            PatientId = patient.Id,
            EncounterId = order.EncounterId,
            SpecimenType = "EDTA",
            CollectedUtc = _factory.Clock.UtcNow.AddHours(-1),
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(2),
            Status = SpecimenStatus.Accepted
        };
        c.Specimens.Add(specimen);
        await c.SaveChangesAsync();

        var link = await Orders(c).LinkSpecimenAsync(patient.Id, order.Id, new LinkOrderSpecimenRequest(specimen.Id));
        Assert.True(link.Succeeded);
        Assert.Equal(specimen.Id, link.Value!.SpecimenId);
        Assert.Equal(specimen.AccessionNumber, link.Value.AccessionNumber);

        var enabled = await Worklist(c).ListForPatientAsync(patient.Id, TestWorklistFilter.Pending);
        var enabledItem = Assert.Single(enabled);
        Assert.True(enabledItem.CanEnterResults);
        Assert.Equal(specimen.Id, enabledItem.SpecimenId);

        var specimenWorklist = await Worklist(c).ListForSpecimenAsync(specimen.Id, TestWorklistFilter.Pending);
        var inherited = Assert.Single(specimenWorklist);
        Assert.Equal(line.TestCode, inherited.TestCode);
        Assert.Equal(order.OrderNumber, inherited.OrderNumber);
    }

    [Fact]
    public async Task Cancel_RemovesPendingTestFromTheChart()
    {
        await using var c = _factory.Create();
        var (patient, _, _, _, order, line) = await SeedOrderWithTestAsync(c, linkSpecimen: false);

        var cancelled = await Orders(c).CancelAsync(patient.Id, order.Id, new CancelOrderRequest("No longer needed"));
        Assert.True(cancelled.Succeeded, cancelled.Error);

        Assert.Empty(await Worklist(c).ListForPatientAsync(patient.Id, TestWorklistFilter.Pending));
        Assert.Empty(await Worklist(c).ListForPatientAsync(patient.Id, TestWorklistFilter.All));
        var saved = await c.OrderLines.AsNoTracking().SingleAsync(l => l.Id == line.Id);
        Assert.False(saved.IsActive);
        Assert.Equal(FulfillmentStatus.Cancelled, saved.FulfillmentStatus);
    }

    [Fact]
    public async Task Save_RequiresTheSpecimenToBeLinkedToTheOrder()
    {
        await using var c = _factory.Create();
        var seed = await SeedOrderWithTestAsync(c);
        var other = new Specimen
        {
            AccessionNumber = $"ACC-{Guid.NewGuid():N}",
            PatientId = seed.patient.Id,
            EncounterId = seed.encounter.Id,
            SpecimenType = "EDTA",
            CollectedUtc = _factory.Clock.UtcNow.AddHours(-1),
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(2),
            Status = SpecimenStatus.Accepted
        };
        c.Specimens.Add(other);
        await c.SaveChangesAsync();

        var results = Results(c, requireOrderSpecimen: true);
        var blocked = await results.SaveTestResultAsync(new SaveTestResultRequest(
            other.Id, seed.order.Id, seed.line.Id, seed.line.TestCode!, "Negative", null, null, null, null, null,
            MarkComplete: false, CorrectionReason: null, UnitNumber: null, CrossmatchMethod: null,
            CrossmatchResult: null, AntibodyScreenNegative: null));
        Assert.False(blocked.Succeeded);
        Assert.Contains("Link a specimen", blocked.Error!);

        var allowed = await results.SaveTestResultAsync(new SaveTestResultRequest(
            seed.specimen!.Id, seed.order.Id, seed.line.Id, seed.line.TestCode!, "Negative", null, null, null, null, null,
            MarkComplete: false, CorrectionReason: null, UnitNumber: null, CrossmatchMethod: null,
            CrossmatchResult: null, AntibodyScreenNegative: null));
        Assert.True(allowed.Succeeded, allowed.Error);
    }

    [Fact]
    public async Task PendingWorklist_IncludesOrderWithoutSpecimen_AsBlocked()
    {
        await using var c = _factory.Create();
        var (patient, _, _, _, order, line) = await SeedOrderWithTestAsync(c, linkSpecimen: false);

        var items = await Worklist(c).ListForPatientAsync(patient.Id, TestWorklistFilter.Pending);
        var item = Assert.Single(items);
        Assert.Equal(line.Id, item.OrderLineId);
        Assert.False(item.CanEnterResults);
        Assert.Contains("specimen", item.BlockReason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Save_BlockedWhenSpecimenNotAccepted()
    {
        await using var c = _factory.Create();
        var seed = await SeedOrderWithTestAsync(c, specimenStatus: SpecimenStatus.Rejected);

        var save = await Results(c).SaveTestResultAsync(new SaveTestResultRequest(
            seed.specimen!.Id, seed.order.Id, seed.line.Id, seed.line.TestCode!, "Negative", null, null, null, null, null,
            MarkComplete: false, CorrectionReason: null, UnitNumber: null, CrossmatchMethod: null,
            CrossmatchResult: null, AntibodyScreenNegative: null));
        Assert.False(save.Succeeded);
        Assert.Contains("Accepted", save.Error!);
    }

    [Fact]
    public async Task SaveIncomplete_SetsEntered_AndUpdatesOrderLine()
    {
        await using var c = _factory.Create();
        var seed = await SeedOrderWithTestAsync(c);

        var save = await Results(c).SaveTestResultAsync(new SaveTestResultRequest(
            seed.specimen!.Id, seed.order.Id, seed.line.Id, seed.line.TestCode!, "Negative", null, null, null, null, null,
            MarkComplete: false, CorrectionReason: null, UnitNumber: null, CrossmatchMethod: null,
            CrossmatchResult: null, AntibodyScreenNegative: null));
        Assert.True(save.Succeeded);
        Assert.Equal(ResultStatus.Entered, save.Value!.Status);

        var updatedLine = await c.OrderLines.FindAsync(seed.line.Id);
        Assert.Equal(ResultStatus.Entered, updatedLine!.ResultStatus);

        var pending = await Worklist(c).ListForPatientAsync(seed.patient.Id, TestWorklistFilter.Pending);
        Assert.Single(pending);
        Assert.Equal(ResultSource.Manual, pending[0].CurrentResultSource);
    }

    [Fact]
    public async Task PendingWorklist_IncludesInstrumentSource()
    {
        await using var c = _factory.Create();
        var seed = await SeedOrderWithTestAsync(c);

        var entered = await Results(c).EnterResultAsync(new EnterResultRequest(
            seed.specimen!.Id,
            seed.line.TestCode!,
            "Positive",
            seed.order.Id,
            Source: ResultSource.Instrument,
            SourceReference: "Analyzer-RA17"));
        Assert.True(entered.Succeeded, entered.Error);
        Assert.Equal(ResultStatus.PendingVerification, entered.Value!.Status);

        var pending = await Worklist(c).ListForPatientAsync(seed.patient.Id, TestWorklistFilter.Pending);
        Assert.Single(pending);
        Assert.Equal(ResultSource.Instrument, pending[0].CurrentResultSource);
        Assert.Equal(ResultStatus.PendingVerification, pending[0].CurrentResultStatus);
    }

    [Fact]
    public async Task PendingWorklist_InterfaceResult_CanVerifyWithoutReentry()
    {
        await using var c = _factory.Create();
        var seed = await SeedOrderWithTestAsync(c);

        var posted = await Results(c).EnterFromInterfaceAsync(
            seed.specimen!.Id,
            seed.order.Id,
            seed.line.TestCode!,
            "Negative",
            units: null,
            interpretation: null,
            sourceReference: "CTRL-WL-ORU");
        Assert.True(posted.Succeeded, posted.Error);
        Assert.Equal(ResultStatus.PendingVerification, posted.Value!.Status);
        Assert.Equal(ResultSource.Interface, posted.Value.Source);

        var pending = Assert.Single(await Worklist(c).ListForPatientAsync(seed.patient.Id, TestWorklistFilter.Pending));
        Assert.Equal(ResultSource.Interface, pending.CurrentResultSource);
        Assert.Equal("Negative", pending.CurrentResultValue);
        Assert.Equal(posted.Value.Id, pending.CurrentResultId);
        Assert.Equal("Verify", pending.BenchActionLabel);
        Assert.True(pending.HasPostedInterfaceOrInstrumentValue);

        var postedRow = await c.TestResults.SingleAsync(r => r.Id == posted.Value.Id);
        postedRow.EnteredBy = "hl7";
        await c.SaveChangesAsync();

        var verified = await Results(c).VerifyResultAsync(posted.Value.Id);
        Assert.True(verified.Succeeded, verified.Error);
        Assert.Equal(ResultStatus.Verified, verified.Value!.Status);
        Assert.Equal("Negative", verified.Value.Value);
    }

    [Fact]
    public async Task PendingWorklist_PostedInterface_SurfacesResultAndVerifyAction()
    {
        await using var c = _factory.Create();
        var seed = await SeedOrderWithTestAsync(c);

        var posted = await Results(c).EnterFromInterfaceAsync(
            seed.specimen!.Id,
            seed.order.Id,
            seed.line.TestCode!,
            "Negative",
            units: null,
            interpretation: null,
            sourceReference: "CTRL-WL-VERIFY");
        Assert.True(posted.Succeeded, posted.Error);

        var item = Assert.Single(await Worklist(c).ListForPatientAsync(seed.patient.Id, TestWorklistFilter.Pending));
        Assert.Equal("Negative", item.CurrentResultValue);
        Assert.Equal("Verify", item.BenchActionLabel);
        Assert.True(item.HasPostedInterfaceOrInstrumentValue);
        Assert.Equal(posted.Value!.EnteredBy, item.CurrentResultEnteredBy);
        Assert.False(item.IsAboSelfVerifyBlocked(item.CurrentResultEnteredBy));
    }

    [Fact]
    public async Task PendingWorklist_SurfacesBloodTypeAntibodyHistoryAndSpecimenExpiry()
    {
        await using var c = _factory.Create();
        var seed = await SeedOrderWithTestAsync(c);
        seed.specimen!.ExpiresUtc = _factory.Clock.UtcNow.AddHours(-1);
        c.PatientBloodTypeHistory.Add(new PatientBloodTypeHistory
        {
            PatientId = seed.patient.Id,
            Abo = AboGroup.A,
            RhD = RhType.Positive,
            Source = BloodTypeSource.TestResult,
            IsCurrent = true
        });
        c.AntibodyHistory.Add(new AntibodyHistory
        {
            PatientId = seed.patient.Id,
            AntibodySpecificity = "anti-K",
            Status = AntibodyStatus.Identified,
            IsActive = true
        });
        await c.SaveChangesAsync();

        var item = Assert.Single(await Worklist(c).ListForPatientAsync(seed.patient.Id, TestWorklistFilter.Pending));
        Assert.Equal("A+", item.CurrentBloodType);
        Assert.True(item.HasAntibodyHistory);
        Assert.Contains("anti-K", item.AntibodySummary);
        Assert.Equal(seed.specimen.ExpiresUtc, item.SpecimenExpiresUtc);
        Assert.True(item.SpecimenExpired);
        Assert.False(item.CanEnterResults);
        Assert.Contains("expired", item.BlockReason!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Blocked", item.BenchActionLabel);
    }

    [Fact]
    public async Task PendingWorklist_HeldOrder_DoesNotOfferEnter()
    {
        await using var c = _factory.Create();
        var seed = await SeedOrderWithTestAsync(c);
        var posted = await Results(c).EnterFromInterfaceAsync(
            seed.specimen!.Id,
            seed.order.Id,
            seed.line.TestCode!,
            "Negative",
            units: null,
            interpretation: null,
            sourceReference: "CTRL-WL-HOLD");
        Assert.True(posted.Succeeded, posted.Error);

        var order = await c.Orders.SingleAsync(o => o.Id == seed.order.Id);
        order.Status = OrderStatus.OnHold;
        await c.SaveChangesAsync();

        var item = Assert.Single(await Worklist(c).ListForPatientAsync(seed.patient.Id, TestWorklistFilter.Pending));
        Assert.False(item.CanEnterResults);
        Assert.Equal("Blocked", item.BenchActionLabel);
        Assert.Contains("on hold", item.BlockReason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            await Worklist(c).ListPendingGlobalAsync(),
            row => row.OrderId == seed.order.Id);

        var verify = await Results(c).VerifyResultAsync(posted.Value!.Id);
        Assert.False(verify.Succeeded);
        Assert.Contains("on hold", verify.Error, StringComparison.OrdinalIgnoreCase);

        var entered = await Results(c).EnterResultAsync(new EnterResultRequest(
            seed.specimen.Id,
            seed.line.TestCode!,
            "Positive",
            seed.order.Id));
        Assert.False(entered.Succeeded);
        Assert.Contains("on hold", entered.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PendingWorklist_ExpiredSpecimen_DoesNotOfferVerify()
    {
        await using var c = _factory.Create();
        var seed = await SeedOrderWithTestAsync(c);
        var posted = await Results(c).EnterFromInterfaceAsync(
            seed.specimen!.Id,
            seed.order.Id,
            seed.line.TestCode!,
            "Negative",
            units: null,
            interpretation: null,
            sourceReference: "CTRL-WL-EXPIRED");
        Assert.True(posted.Succeeded, posted.Error);

        seed.specimen.ExpiresUtc = _factory.Clock.UtcNow.AddHours(-1);
        await c.SaveChangesAsync();

        var item = Assert.Single(await Worklist(c).ListForPatientAsync(seed.patient.Id, TestWorklistFilter.Pending));
        Assert.True(item.HasPostedInterfaceOrInstrumentValue);
        Assert.False(item.CanEnterResults);
        Assert.Equal("Blocked", item.BenchActionLabel);
    }

    [Fact]
    public async Task PendingWorklist_SurfacesElectronicXmHoldAndOpenAntibodyId()
    {
        await using var c = _factory.Create();
        var seed = await SeedOrderWithTestAsync(c);
        c.PatientBloodTypeHistory.AddRange(
            new PatientBloodTypeHistory
            {
                PatientId = seed.patient.Id,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                Source = BloodTypeSource.HistoricalImport,
                IsCurrent = false
            },
            new PatientBloodTypeHistory
            {
                PatientId = seed.patient.Id,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                Source = BloodTypeSource.TestResult,
                IsCurrent = true
            });
        var workup = new AntibodyIdentificationWorkup
        {
            PatientId = seed.patient.Id,
            SpecimenId = seed.specimen!.Id,
            PrimaryLotId = 1,
            Status = AntibodyWorkupStatus.InProgress
        };
        c.AntibodyIdentificationWorkups.Add(workup);
        await c.SaveChangesAsync();

        var item = Assert.Single(await Worklist(c).ListForPatientAsync(seed.patient.Id, TestWorklistFilter.Pending));
        Assert.True(item.HasOpenAntibodyIdWorkup);
        Assert.Equal(workup.Id, item.OpenAntibodyIdWorkupId);
        Assert.False(item.ElectronicXmEligible);
        Assert.Contains("antibody-identification workup", item.ElectronicXmClinicalBlockReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PendingWorklist_SurfacesAboRhDeltaHold_OnEnteredAborh()
    {
        await using var c = _factory.Create();
        var seed = await SeedOrderWithTestAsync(c, testCode: "ABORH");
        c.PatientBloodTypeHistory.Add(new PatientBloodTypeHistory
        {
            PatientId = seed.patient.Id,
            Abo = AboGroup.A,
            RhD = RhType.Positive,
            Source = BloodTypeSource.TestResult,
            IsCurrent = true
        });
        await c.SaveChangesAsync();

        var save = await Results(c).EnterAboRhAsync(new EnterAboRhRequest(
            seed.specimen!.Id, AboGroup.O, RhType.Negative, OrderId: seed.order.Id));
        Assert.True(save.Succeeded);
        Assert.NotEqual(ResultStatus.Verified, save.Value!.Status);

        var item = Assert.Single(await Worklist(c).ListForPatientAsync(seed.patient.Id, TestWorklistFilter.Pending));
        Assert.True(item.AboRhDeltaHold);
        Assert.Contains("historical", item.AboRhDeltaDetail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("O-", item.AboRhDeltaDetail);
        Assert.Contains("A+", item.AboRhDeltaDetail);
    }

    [Fact]
    public async Task PendingWorklist_AboSelfVerifyBlock_FollowsFacilityPolicy()
    {
        await using var c = _factory.Create();
        var seed = await SeedOrderWithTestAsync(c, testCode: "ABORH");
        var save = await Results(c).EnterAboRhAsync(new EnterAboRhRequest(
            seed.specimen!.Id, AboGroup.O, RhType.Positive, OrderId: seed.order.Id));
        Assert.True(save.Succeeded, save.Error);

        // Unset policy defaults to blocking the enterer.
        var blocked = Assert.Single(await Worklist(c).ListForPatientAsync(seed.patient.Id, TestWorklistFilter.Pending));
        Assert.True(blocked.IsAboSelfVerifyBlocked(save.Value!.EnteredBy));

        var setting = new SystemSetting { Key = FacilityPolicyKeys.BlockAboSelfVerify, Value = "false", Category = "Result" };
        c.SystemSettings.Add(setting);
        await c.SaveChangesAsync();
        try
        {
            var allowed = Assert.Single(await Worklist(c).ListForPatientAsync(seed.patient.Id, TestWorklistFilter.Pending));
            Assert.False(allowed.IsAboSelfVerifyBlocked(save.Value.EnteredBy));
        }
        finally
        {
            c.SystemSettings.Remove(setting);
            await c.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task SaveComplete_VerifiesResult_AndMovesToCompletedFilter()
    {
        await using var c = _factory.Create();
        var seed = await SeedOrderWithTestAsync(c);

        var save = await Results(c).SaveTestResultAsync(new SaveTestResultRequest(
            seed.specimen!.Id, seed.order.Id, seed.line.Id, seed.line.TestCode!, "Negative", null, null, null, null, null,
            MarkComplete: true, CorrectionReason: null, UnitNumber: null, CrossmatchMethod: null,
            CrossmatchResult: null, AntibodyScreenNegative: null));
        Assert.True(save.Succeeded);
        Assert.Equal(ResultStatus.Verified, save.Value!.Status);

        var completed = await Worklist(c).ListForPatientAsync(seed.patient.Id, TestWorklistFilter.Completed);
        Assert.Single(completed);
        Assert.Empty(await Worklist(c).ListForPatientAsync(seed.patient.Id, TestWorklistFilter.Pending));
    }

    [Fact]
    public async Task ResaveVerified_RequiresReason_AndCreatesCorrectedVersion()
    {
        long resultId;
        long lineId;
        long orderId;
        long specimenId;

        await using (var c = _factory.Create())
        {
            var seed = await SeedOrderWithTestAsync(c);
            lineId = seed.line.Id;
            orderId = seed.order.Id;
            specimenId = seed.specimen!.Id;
            resultId = (await Results(c).SaveTestResultAsync(new SaveTestResultRequest(
                seed.specimen!.Id, seed.order.Id, seed.line.Id, seed.line.TestCode!, "Negative", null, null, null, null, null,
                MarkComplete: true, CorrectionReason: null, UnitNumber: null, CrossmatchMethod: null,
                CrossmatchResult: null, AntibodyScreenNegative: null))).Value!.Id;
        }

        await using var context = _factory.Create();
        var original = await context.TestResults.FindAsync(resultId);
        var orderLine = await context.OrderLines.FindAsync(lineId);
        var patientOrder = await context.Orders.FindAsync(orderId);
        var linkedSpecimen = await context.Specimens.FindAsync(specimenId);

        var noReason = await Results(context).SaveTestResultAsync(new SaveTestResultRequest(
            linkedSpecimen!.Id, patientOrder!.Id, orderLine!.Id, orderLine.TestCode!, "Positive", null, null, null, null, null,
            MarkComplete: false, CorrectionReason: null, UnitNumber: null, CrossmatchMethod: null,
            CrossmatchResult: null, AntibodyScreenNegative: null));
        Assert.False(noReason.Succeeded);

        var corrected = await Results(context).SaveTestResultAsync(new SaveTestResultRequest(
            linkedSpecimen.Id, patientOrder.Id, orderLine.Id, orderLine.TestCode!, "Positive", null, null, null, null, null,
            MarkComplete: false, CorrectionReason: "Repeat test positive", UnitNumber: null, CrossmatchMethod: null,
            CrossmatchResult: null, AntibodyScreenNegative: null));
        Assert.True(corrected.Succeeded);
        Assert.Equal(ResultStatus.Corrected, corrected.Value!.Status);

        await context.Entry(original!).ReloadAsync();
        Assert.NotNull(original.SupersededByResultId);
    }

    // TEST-BB-368: a pending result whose specimen expires soon names the deadline and is listed first.
    [Fact]
    public async Task PendingResult_WhoseSpecimenExpiresSoon_NamesTheDeadline_AndComesFirst()
    {
        await using var c = _factory.Create();
        var seed = await SeedOrderWithTestAsync(c);
        var pending = await Results(c).SaveTestResultAsync(new SaveTestResultRequest(
            seed.specimen!.Id, seed.order.Id, seed.line.Id, seed.line.TestCode!, "Negative", null, null, null, null, null,
            MarkComplete: false, CorrectionReason: null, UnitNumber: null, CrossmatchMethod: null,
            CrossmatchResult: null, AntibodyScreenNegative: null));
        Assert.True(pending.Succeeded, pending.Error);

        var other = await SeedOrderWithTestAsync(c);
        other.specimen!.ExpiresUtc = _factory.Clock.UtcNow.AddDays(3);
        seed.specimen.ExpiresUtc = _factory.Clock.UtcNow.AddHours(2);
        await c.SaveChangesAsync();

        var rows = (await Worklist(c).ListPendingGlobalAsync())
            .Where(r => r.OrderLineId == seed.line.Id || r.OrderLineId == other.line.Id)
            .ToList();
        Assert.Equal(seed.line.Id, rows[0].OrderLineId);
        Assert.StartsWith("Verify before", rows[0].VerificationDeadlineNotice);
        Assert.Null(rows[1].VerificationDeadlineNotice);
    }

    // TEST-BB-371: the patient chart's Tests tab gets the same verify-before deadline as the worklist.
    [Fact]
    public async Task PatientTestsTab_CarriesTheVerificationDeadline()
    {
        await using var c = _factory.Create();
        var seed = await SeedOrderWithTestAsync(c);
        Assert.True((await Results(c).SaveTestResultAsync(new SaveTestResultRequest(
            seed.specimen!.Id, seed.order.Id, seed.line.Id, seed.line.TestCode!, "Negative", null, null, null, null, null,
            MarkComplete: false, CorrectionReason: null, UnitNumber: null, CrossmatchMethod: null,
            CrossmatchResult: null, AntibodyScreenNegative: null))).Succeeded);
        seed.specimen.ExpiresUtc = _factory.Clock.UtcNow.AddHours(5);
        await c.SaveChangesAsync();

        var row = Assert.Single(await Worklist(c).ListForPatientAsync(seed.patient.Id, TestWorklistFilter.Pending));
        Assert.StartsWith("Verify before", row.VerificationDeadlineNotice);

        seed.specimen.ExpiresUtc = _factory.Clock.UtcNow.AddDays(3);
        await c.SaveChangesAsync();
        Assert.Null(Assert.Single(await Worklist(c).ListForPatientAsync(seed.patient.Id, TestWorklistFilter.Pending)).VerificationDeadlineNotice);
    }

    // TEST-BB-370: the shift handoff counts results that must be verified before their specimen expires.
    [Fact]
    public async Task ShiftHandoff_CountsResultsToVerifyBeforeSpecimenExpiry()
    {
        await using var c = _factory.Create();
        var seed = await SeedOrderWithTestAsync(c);
        Assert.True((await Results(c).SaveTestResultAsync(new SaveTestResultRequest(
            seed.specimen!.Id, seed.order.Id, seed.line.Id, seed.line.TestCode!, "Negative", null, null, null, null, null,
            MarkComplete: false, CorrectionReason: null, UnitNumber: null, CrossmatchMethod: null,
            CrossmatchResult: null, AntibodyScreenNegative: null))).Succeeded);
        seed.specimen.ExpiresUtc = _factory.Clock.UtcNow.AddHours(2);
        await c.SaveChangesAsync();

        var handoff = await new BloodBankLIS.Application.Compliance.ShiftHandoffService(
                new EfRepository<ShiftHandoff>(c), c, _factory.Clock, _factory.CurrentUser,
                tests: Worklist(c), audit: new AuditWriter(c, _factory.Clock, _factory.CurrentUser))
            .CreateAsync(new BloodBankLIS.Application.Compliance.CreateShiftHandoffRequest("Night shift: one screen to verify"));

        Assert.True(handoff.Succeeded, handoff.Error);
        Assert.True(handoff.Value!.PendingTestsVerifyBeforeExpiry >= 1);
        Assert.True(handoff.Value.PendingTests >= handoff.Value.PendingTestsVerifyBeforeExpiry);
        Assert.Equal(handoff.Value.PendingTestsVerifyBeforeExpiry,
            BloodBankLIS.Application.Compliance.ShiftHandoffDto.From(handoff.Value).PendingTestsVerifyBeforeExpiry);
        // TEST-BB-429: the entered result is also counted as waiting for verification.
        Assert.True(handoff.Value.ResultsAwaitingVerification >= 1);
        Assert.Contains("to verify", BloodBankLIS.Application.Compliance.ShiftHandoffDto.From(handoff.Value).QueueSummary);
    }

    // TEST-BB-451: rejecting a tube names the open orders that now need a new specimen, and the bench shows their lines
    // as Recollect with the reason, offering Accession.
    [Fact]
    public async Task RejectedTube_NamesItsWaitingOrders_AndTheBenchAsksForRecollection()
    {
        await using var c = _factory.Create();
        var seed = await SeedOrderWithTestAsync(c, testCode: "DAT");
        var specimens = new BloodBankLIS.Application.Specimens.SpecimenService(
            new EfRepository<Specimen>(c), new EfRepository<Patient>(c), new EfRepository<SpecimenTypeDefinition>(c),
            c, _factory.Clock, currentUser: _factory.CurrentUser,
            orderSpecimens: new EfRepository<OrderSpecimen>(c), orders: new EfRepository<Order>(c));

        var rejected = await specimens.RejectAsync(seed.specimen!.Id, "Hemolyzed", null);
        Assert.True(rejected.Succeeded, rejected.Error);
        var warning = Assert.Single(rejected.Warnings!, w => w.Code == SpecimenRecollectionRule.Code);
        Assert.Contains(seed.order.OrderNumber, warning.Message);

        var row = Assert.Single(await Worklist(c).ListForPatientAsync(seed.patient.Id), r => r.OrderLineId == seed.line.Id);
        Assert.False(row.CanEnterResults);
        Assert.True(row.NeedsRecollection);
        Assert.True(row.NeedsSpecimenLink);
        Assert.Equal("Recollect", row.BenchActionLabel);
        Assert.Contains("was rejected (Hemolyzed)", row.BlockReason);
        Assert.Contains("Collect a new specimen", row.BlockReason);

        // A tube with no open orders is rejected without the recollection warning.
        var spare = new Specimen
        {
            AccessionNumber = $"ACC-{Guid.NewGuid():N}", PatientId = seed.patient.Id, EncounterId = seed.encounter.Id, SpecimenType = "EDTA",
            CollectedUtc = _factory.Clock.UtcNow.AddHours(-1), ExpiresUtc = _factory.Clock.UtcNow.AddDays(2), Status = SpecimenStatus.Accepted
        };
        c.Specimens.Add(spare);
        await c.SaveChangesAsync();
        var spareRejected = await specimens.RejectAsync(spare.Id, "Clotted", null);
        Assert.True(spareRejected.Succeeded, spareRejected.Error);
        Assert.DoesNotContain(spareRejected.Warnings ?? [], w => w.Code == SpecimenRecollectionRule.Code);
    }

    // TEST-BB-444 (OCD-058): a reaction workup line on an expired tube can be resulted and verified; an ordinary order
    // on the same expired tube stays blocked.
    [Fact]
    public async Task ExpiredTube_TakesReactionWorkupResults_ButNotOrdinaryOnes()
    {
        await using var c = _factory.Create();
        var workup = await SeedOrderWithTestAsync(c, testCode: "DAT");
        var ordinary = await SeedOrderWithTestAsync(c, testCode: "DAT");
        var workupOrder = await c.Orders.SingleAsync(o => o.Id == workup.order.Id);
        workupOrder.OrderType = OrderType.TransfusionReactionWorkup;
        foreach (var tube in new[] { workup.specimen!, ordinary.specimen! })
        {
            var tracked = await c.Specimens.SingleAsync(sp => sp.Id == tube.Id);
            tracked.ExpiresUtc = _factory.Clock.UtcNow.AddHours(-1);
            tracked.Status = SpecimenStatus.Expired;
        }

        await c.SaveChangesAsync();

        var workupRow = Assert.Single(await Worklist(c).ListForSpecimenAsync(workup.specimen!.Id), r => r.OrderLineId == workup.line.Id);
        Assert.True(workupRow.CanEnterResults, workupRow.BlockReason);
        var ordinaryRow = Assert.Single(await Worklist(c).ListForSpecimenAsync(ordinary.specimen!.Id), r => r.OrderLineId == ordinary.line.Id);
        Assert.False(ordinaryRow.CanEnterResults);
        // TEST-BB-450: the bench marks the workup line and links to the investigation; the ordinary line is unmarked.
        Assert.True(workupRow.ReactionWorkup);
        Assert.Equal($"/reactions?patientId={workup.patient.Id}", workupRow.ReactionHref);
        Assert.False(ordinaryRow.ReactionWorkup);

        var saved = await Results(c).SaveTestResultAsync(new SaveTestResultRequest(
            workup.specimen.Id, workup.order.Id, workup.line.Id, "DAT", "Negative", null, null, null, null, null,
            MarkComplete: false, CorrectionReason: null, UnitNumber: null, CrossmatchMethod: null,
            CrossmatchResult: null, AntibodyScreenNegative: null));
        Assert.True(saved.Succeeded, saved.Error);
        var refused = await Results(c).SaveTestResultAsync(new SaveTestResultRequest(
            ordinary.specimen.Id, ordinary.order.Id, ordinary.line.Id, "DAT", "Negative", null, null, null, null, null,
            MarkComplete: false, CorrectionReason: null, UnitNumber: null, CrossmatchMethod: null,
            CrossmatchResult: null, AntibodyScreenNegative: null));
        Assert.False(refused.Succeeded);
        Assert.Contains("Expired", refused.Error);
    }

    // TEST-BB-433: worklist rows carry the tube's label barcode, so a label scan on the bench finds them.
    [Fact]
    public async Task WorklistRow_CarriesTheLabelBarcode_AndALabelScanFindsIt()
    {
        await using var c = _factory.Create();
        var seed = await SeedOrderWithTestAsync(c);
        var label = $"LBL-{seed.line.Id}";
        seed.specimen!.Barcode = label;
        await c.SaveChangesAsync();

        var rows = await Worklist(c).ListPendingGlobalAsync();
        var row = Assert.Single(rows, r => r.OrderLineId == seed.line.Id);
        Assert.Equal(label, row.SpecimenBarcode);
        var onTube = TestWorklistView.ForTube(rows, label.ToLowerInvariant());
        Assert.Contains(onTube, r => r.OrderLineId == seed.line.Id);
        Assert.All(onTube, r => Assert.Equal(seed.specimen.Id, r.SpecimenId));
        Assert.Equal(seed.specimen.AccessionNumber, TestWorklistView.TubeLabel(rows, label));
    }

    // TEST-BB-431: after downtime, results entered from paper and waiting for verification are counted on the snapshot.
    [Fact]
    public async Task DowntimeSnapshot_CountsResultsToVerify_AndThoseDueBeforeSpecimenExpiry()
    {
        await using var c = _factory.Create();
        var seed = await SeedOrderWithTestAsync(c);
        Assert.True((await Results(c).SaveTestResultAsync(new SaveTestResultRequest(
            seed.specimen!.Id, seed.order.Id, seed.line.Id, seed.line.TestCode!, "Negative", null, null, null, null, null,
            MarkComplete: false, CorrectionReason: null, UnitNumber: null, CrossmatchMethod: null,
            CrossmatchResult: null, AntibodyScreenNegative: null))).Succeeded);
        seed.specimen.ExpiresUtc = _factory.Clock.UtcNow.AddHours(2);
        await c.SaveChangesAsync();

        var snapshot = await new BloodBankLIS.Application.Compliance.DowntimeReconciliationService(
                new EfRepository<InterfaceErrorQueueItem>(c), new EfRepository<Hl7MessageLog>(c), new EfRepository<Issue>(c),
                new AuditQuery(c), _factory.Clock, _factory.CurrentUser,
                new FixedPermissionEvaluator(1, PermissionCodes.AuditRead), tests: Worklist(c))
            .GetSnapshotAsync();

        Assert.True(snapshot.Succeeded, snapshot.Error);
        Assert.True(snapshot.Value!.ResultsAwaitingVerification >= 1);
        Assert.True(snapshot.Value.ResultsVerifyBeforeExpiry >= 1);
        Assert.True(snapshot.Value.ResultsAwaitingVerification >= snapshot.Value.ResultsVerifyBeforeExpiry);
        // Without the worklist (older wiring) the new counts stay zero rather than failing.
        Assert.Equal(0, (await new BloodBankLIS.Application.Compliance.DowntimeReconciliationService(
                new EfRepository<InterfaceErrorQueueItem>(c), new EfRepository<Hl7MessageLog>(c), new EfRepository<Issue>(c),
                new AuditQuery(c), _factory.Clock, _factory.CurrentUser)
            .GetSnapshotAsync()).Value!.ResultsAwaitingVerification);
    }

    [Fact]
    public async Task PendingCorrection_OnAnExpiredSpecimen_CanStillBeWorkedFromTheWorklist()
    {
        await using var c = _factory.Create();
        var seed = await SeedOrderWithTestAsync(c);
        Assert.True((await Results(c).SaveTestResultAsync(new SaveTestResultRequest(
            seed.specimen!.Id, seed.order.Id, seed.line.Id, seed.line.TestCode!, "Negative", null, null, null, null, null,
            MarkComplete: true, CorrectionReason: null, UnitNumber: null, CrossmatchMethod: null,
            CrossmatchResult: null, AntibodyScreenNegative: null))).Succeeded);
        Assert.True((await Results(c).SaveTestResultAsync(new SaveTestResultRequest(
            seed.specimen.Id, seed.order.Id, seed.line.Id, seed.line.TestCode!, "Positive", null, null, null, null, null,
            MarkComplete: false, CorrectionReason: "Transcription error", UnitNumber: null, CrossmatchMethod: null,
            CrossmatchResult: null, AntibodyScreenNegative: null))).Succeeded);

        seed.specimen.ExpiresUtc = _factory.Clock.UtcNow.AddMinutes(-1);
        seed.specimen.Status = SpecimenStatus.Expired;
        await c.SaveChangesAsync();

        var row = Assert.Single(await Worklist(c).ListForPatientAsync(seed.patient.Id, TestWorklistFilter.Pending));
        Assert.Equal(ResultStatus.Corrected, row.CurrentResultStatus);
        Assert.True(row.CanEnterResults, row.BlockReason);
    }

    // TEST-BB-367 (OCD-051): the worklist correction of a verified result works after the specimen expires.
    [Fact]
    public async Task WorklistCorrection_AfterTheSpecimenExpires_CreatesTheCorrectedVersion()
    {
        await using var c = _factory.Create();
        var seed = await SeedOrderWithTestAsync(c);
        var verified = await Results(c).SaveTestResultAsync(new SaveTestResultRequest(
            seed.specimen!.Id, seed.order.Id, seed.line.Id, seed.line.TestCode!, "Negative", null, null, null, null, null,
            MarkComplete: true, CorrectionReason: null, UnitNumber: null, CrossmatchMethod: null,
            CrossmatchResult: null, AntibodyScreenNegative: null));
        Assert.True(verified.Succeeded, verified.Error);

        seed.specimen.ExpiresUtc = _factory.Clock.UtcNow.AddMinutes(-1);
        seed.specimen.Status = SpecimenStatus.Expired;
        await c.SaveChangesAsync();

        var corrected = await Results(c).SaveTestResultAsync(new SaveTestResultRequest(
            seed.specimen.Id, seed.order.Id, seed.line.Id, seed.line.TestCode!, "Positive", null, null, null, null, null,
            MarkComplete: false, CorrectionReason: "Transcription error", UnitNumber: null, CrossmatchMethod: null,
            CrossmatchResult: null, AntibodyScreenNegative: null));
        Assert.True(corrected.Succeeded, corrected.Error);
        Assert.Equal(ResultStatus.Corrected, corrected.Value!.Status);
    }

    [Fact]
    public async Task CorrectionReason_DoesNotLetAFirstResultPost_OnAnExpiredSpecimen()
    {
        await using var c = _factory.Create();
        var seed = await SeedOrderWithTestAsync(c);
        seed.specimen!.ExpiresUtc = _factory.Clock.UtcNow.AddMinutes(-1);
        await c.SaveChangesAsync();

        var save = await Results(c).SaveTestResultAsync(new SaveTestResultRequest(
            seed.specimen.Id, seed.order.Id, seed.line.Id, seed.line.TestCode!, "Negative", null, null, null, null, null,
            MarkComplete: false, CorrectionReason: "Not really a correction", UnitNumber: null, CrossmatchMethod: null,
            CrossmatchResult: null, AntibodyScreenNegative: null));
        Assert.False(save.Succeeded);
        Assert.Contains("has expired", save.Error);
        Assert.False(await c.TestResults.AnyAsync(r => r.SpecimenId == seed.specimen.Id));
    }

    [Fact]
    public async Task XmSave_WithUnitNumber_CreatesCrossmatchRecord()
    {
        await using var c = _factory.Create();
        var seed = await SeedOrderWithTestAsync(c, testCode: "XM");
        var unit = await AddCrossmatchUnitAsync(c, "RBC-TWL", "W0009990000001");
        await ReserveAsync(c, unit, seed.patient.Id, AllocationStatus.Reserved);

        var save = await SaveCrossmatchAsync(c, seed, unit.UnitNumber);
        Assert.True(save.Succeeded);
        Assert.Contains(unit.UnitNumber, save.Value!.Interpretation!);

        var xm = await c.Crossmatches.FirstOrDefaultAsync(x => x.PatientId == seed.patient.Id && x.BloodProductId == unit.Id);
        Assert.NotNull(xm);
        Assert.Equal(CrossmatchResult.Compatible, xm!.Result);
    }

    /// <summary>
    /// The unit dropdown only offers reserved units, but the API must refuse an unreserved unit on its
    /// own so a stale or hand-crafted request cannot crossmatch another patient's blood.
    /// </summary>
    [Fact]
    public async Task XmSave_RejectsUnitNotReservedForPatient()
    {
        await using var c = _factory.Create();
        var seed = await SeedOrderWithTestAsync(c, testCode: "XM");
        var unit = await AddCrossmatchUnitAsync(c, "RBC-TWL-UNRESERVED", "W0009990000002");

        var save = await SaveCrossmatchAsync(c, seed, unit.UnitNumber);

        Assert.False(save.Succeeded);
        Assert.Contains("not reserved", save.Error!, StringComparison.OrdinalIgnoreCase);

        // The request must be rejected before anything is written, not left half-recorded.
        Assert.Empty(await c.Crossmatches.Where(x => x.BloodProductId == unit.Id).ToListAsync());
        Assert.Empty(await c.TestResults.Where(r => r.OrderId == seed.order.Id && r.TestCode == "XM").ToListAsync());
    }

    /// <summary>
    /// A unit reserved for someone else is the hazard the check exists to prevent.
    /// </summary>
    [Fact]
    public async Task XmSave_RejectsUnitReservedForAnotherPatient()
    {
        await using var c = _factory.Create();
        var seed = await SeedOrderWithTestAsync(c, testCode: "XM");
        var other = await SeedOrderWithTestAsync(c, testCode: "XM");
        var unit = await AddCrossmatchUnitAsync(c, "RBC-TWL-OTHER", "W0009990000004");
        await ReserveAsync(c, unit, other.patient.Id, AllocationStatus.Reserved);

        var save = await SaveCrossmatchAsync(c, seed, unit.UnitNumber);

        Assert.False(save.Succeeded);
        Assert.Contains("not reserved", save.Error!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A crossmatch still needs correcting after the unit is issued, at which point the reservation is
    /// consumed rather than reserved.
    /// </summary>
    [Fact]
    public async Task XmSave_AllowsUnitWhoseReservationWasConsumed()
    {
        await using var c = _factory.Create();
        var seed = await SeedOrderWithTestAsync(c, testCode: "XM");
        var unit = await AddCrossmatchUnitAsync(c, "RBC-TWL-CONSUMED", "W0009990000003");
        await ReserveAsync(c, unit, seed.patient.Id, AllocationStatus.Consumed);

        var save = await SaveCrossmatchAsync(c, seed, unit.UnitNumber);

        Assert.True(save.Succeeded);
    }

    [Fact]
    public async Task XmSave_RequiresCellReactionsDefinedOnTheTest()
    {
        await using var c = _factory.Create();
        var code = $"XC{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var seed = await SeedOrderWithTestAsync(c, testCode: code);
        var definition = await c.TestDefinitions.FirstAsync(t => t.Code == code);
        definition.Category = TestCategory.Crossmatch;
        definition.ResultValueType = ResultValueType.Crossmatch;
        definition.PanelSubtestsJson =
            """[{"subtestCode":"IS","required":true,"sortOrder":1},{"subtestCode":"CC","required":false,"sortOrder":2}]""";
        await c.SaveChangesAsync();

        var unit = await AddCrossmatchUnitAsync(c, $"P{Guid.NewGuid():N}"[..12], $"U-{code}");
        await ReserveAsync(c, unit, seed.patient.Id, AllocationStatus.Reserved);

        var missing = await Results(c).SaveTestResultAsync(new SaveTestResultRequest(
            seed.specimen!.Id, seed.order.Id, seed.line.Id, code, null, null, null, null, null, null,
            MarkComplete: false, CorrectionReason: null, UnitNumber: unit.UnitNumber,
            CrossmatchMethod: CrossmatchMethod.ImmediateSpin, CrossmatchResult: CrossmatchResult.Compatible,
            AntibodyScreenNegative: true));
        Assert.False(missing.Succeeded);
        Assert.Contains("IS", missing.Error!);

        var saved = await Results(c).SaveTestResultAsync(new SaveTestResultRequest(
            seed.specimen.Id, seed.order.Id, seed.line.Id, code, null, null, null, null, null,
            new Dictionary<string, string> { ["IS"] = "0" },
            MarkComplete: false, CorrectionReason: null, UnitNumber: unit.UnitNumber,
            CrossmatchMethod: CrossmatchMethod.ImmediateSpin, CrossmatchResult: CrossmatchResult.Compatible,
            AntibodyScreenNegative: true));
        Assert.True(saved.Succeeded, saved.Error);
        Assert.Contains("IS", saved.Value!.Value);
    }

    // TEST-BB-688: a crossmatch with a positive counted reaction cannot be saved as Compatible (nothing recorded); as
    // Incompatible it saves, and a positive check cell does not count.
    [Fact]
    public async Task XmSave_CompatibleWithPositiveReaction_IsRefused()
    {
        await using var c = _factory.Create();
        var code = $"XP{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var seed = await SeedOrderWithTestAsync(c, testCode: code);
        var definition = await c.TestDefinitions.FirstAsync(t => t.Code == code);
        definition.Category = TestCategory.Crossmatch;
        definition.ResultValueType = ResultValueType.Crossmatch;
        definition.PanelSubtestsJson =
            """[{"subtestCode":"IS","required":true,"sortOrder":1},{"subtestCode":"CC","required":false,"sortOrder":2}]""";
        await c.SaveChangesAsync();

        // The catalog as seeded: the readings and the check-cell phase (whose code the check-cells reading shares).
        var now = DateTime.UtcNow;
        if (!await c.SubtestDefinitions.AnyAsync(s => s.Code == "IS"))
        {
            c.SubtestDefinitions.Add(new SubtestDefinition { Code = "IS", Name = "Immediate spin", IsActive = true, IsDraft = false, EffectiveUtc = now, Version = 1 });
        }

        if (!await c.SubtestDefinitions.AnyAsync(s => s.Code == "CC"))
        {
            c.SubtestDefinitions.Add(new SubtestDefinition { Code = "CC", Name = "Check cells", IsActive = true, IsDraft = false, EffectiveUtc = now, Version = 1 });
        }

        if (!await c.PhaseDefinitions.AnyAsync(p => p.Code == "CC"))
        {
            c.PhaseDefinitions.Add(new PhaseDefinition
            {
                Code = "CC", Name = "Check cells", SortOrder = 4, IncludeInInterpretation = false, IsCheckCell = true,
                ValidatesPhaseCode = "AHG", IsActive = true, IsDraft = false, EffectiveUtc = now, Version = 1
            });
        }

        await c.SaveChangesAsync();

        var unit = await AddCrossmatchUnitAsync(c, $"P{Guid.NewGuid():N}"[..12], $"U-{code}");
        await ReserveAsync(c, unit, seed.patient.Id, AllocationStatus.Reserved);

        async Task<EvaluationResult<TestResult>> Save(string isGrade, string? checkCells, CrossmatchResult result)
        {
            await using var each = _factory.Create();
            var reactions = new Dictionary<string, string> { ["IS"] = isGrade };
            if (checkCells is not null)
            {
                reactions["CC"] = checkCells;
            }

            return await Results(each, withPhases: true).SaveTestResultAsync(new SaveTestResultRequest(
                seed.specimen!.Id, seed.order.Id, seed.line.Id, code, null, null, null, null, null, reactions,
                MarkComplete: false, CorrectionReason: null, UnitNumber: unit.UnitNumber,
                CrossmatchMethod: CrossmatchMethod.ImmediateSpin, CrossmatchResult: result,
                AntibodyScreenNegative: true));
        }

        var refused = await Save("2+", null, CrossmatchResult.Compatible);
        Assert.False(refused.Succeeded);
        Assert.Contains("positive reaction (Immediate spin 2+)", refused.Error!);
        Assert.Empty(await c.Crossmatches.Where(x => x.BloodProductId == unit.Id).ToListAsync());

        var checkCellsPositive = await Save("0", "2+", CrossmatchResult.Compatible);
        Assert.True(checkCellsPositive.Succeeded, checkCellsPositive.Error);

        var incompatible = await Save("2+", null, CrossmatchResult.Incompatible);
        Assert.True(incompatible.Succeeded, incompatible.Error);
    }

    private async Task<BloodUnit> AddCrossmatchUnitAsync(BloodBankDbContext c, string productCode, string unitNumber)
    {
        var productType = new ProductType
        {
            ProductCode = productCode,
            Name = "RBC",
            ComponentClass = ComponentClass.RedBloodCells,
            RequiresCrossmatch = true
        };
        c.ProductTypes.Add(productType);
        await c.SaveChangesAsync();

        var unit = new BloodUnit
        {
            UnitNumber = unitNumber,
            ProductTypeId = productType.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            Status = UnitStatus.Available,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(10)
        };
        c.BloodUnits.Add(unit);
        await c.SaveChangesAsync();
        return unit;
    }

    private Task<EvaluationResult<TestResult>> SaveCrossmatchAsync(
        BloodBankDbContext c,
        (Patient patient, Encounter encounter, OrderingLocation location, Specimen? specimen, PatientOrderDto order, OrderLineDto line) seed,
        string unitNumber) =>
        Results(c).SaveTestResultAsync(new SaveTestResultRequest(
            seed.specimen!.Id, seed.order.Id, seed.line.Id, "XM", null, null, null, null, null, null,
            MarkComplete: false, CorrectionReason: null, UnitNumber: unitNumber,
            CrossmatchMethod: CrossmatchMethod.Serologic, CrossmatchResult: CrossmatchResult.Compatible,
            AntibodyScreenNegative: true));

    private async Task ReserveAsync(BloodBankDbContext c, BloodUnit unit, long patientId, AllocationStatus status)
    {
        c.Allocations.Add(new Allocation
        {
            BloodProductId = unit.Id,
            PatientId = patientId,
            Status = status,
            AllocatedUtc = _factory.Clock.UtcNow,
            AllocatedBy = "tech"
        });
        await c.SaveChangesAsync();
    }
}
