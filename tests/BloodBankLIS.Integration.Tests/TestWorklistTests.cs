using BloodBankLIS.Application.Compatibility;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Application.Results;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
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
            new EfRepository<Specimen>(c), new EfRepository<Patient>(c), new EfRepository<TestResult>(c),
            new EfRepository<TestDefinition>(c), new EfRepository<SpecimenTypeDefinition>(c), _factory.Clock);

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

    [Fact]
    public async Task XmSave_WithUnitNumber_CreatesCrossmatchRecord()
    {
        await using var c = _factory.Create();
        var seed = await SeedOrderWithTestAsync(c, testCode: "XM");

        var productType = new ProductType
        {
            ProductCode = "RBC-TWL",
            Name = "RBC",
            ComponentClass = ComponentClass.RedBloodCells,
            RequiresCrossmatch = true
        };
        c.ProductTypes.Add(productType);
        await c.SaveChangesAsync();

        var unit = new BloodUnit
        {
            UnitNumber = "W0009990000001",
            ProductTypeId = productType.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            Status = UnitStatus.Available,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(10)
        };
        c.BloodUnits.Add(unit);
        await c.SaveChangesAsync();

        var save = await Results(c).SaveTestResultAsync(new SaveTestResultRequest(
            seed.specimen!.Id, seed.order.Id, seed.line.Id, "XM", null, null, null, null, null, null,
            MarkComplete: false, CorrectionReason: null, UnitNumber: unit.UnitNumber,
            CrossmatchMethod: CrossmatchMethod.Serologic, CrossmatchResult: CrossmatchResult.Compatible,
            AntibodyScreenNegative: true));
        Assert.True(save.Succeeded);
        Assert.Contains(unit.UnitNumber, save.Value!.Interpretation!);

        var xm = await c.Crossmatches.FirstOrDefaultAsync(x => x.PatientId == seed.patient.Id && x.BloodProductId == unit.Id);
        Assert.NotNull(xm);
        Assert.Equal(CrossmatchResult.Compatible, xm!.Result);
    }
}
