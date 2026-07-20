using BloodBankLIS.Application.Admin;
using BloodBankLIS.Application.Compatibility;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Application.Results;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class ReflexRulesTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public ReflexRulesTests(SqliteContextFactory factory) => _factory = factory;

    private ResultService Results(BloodBankDbContext c) =>
        new(new EfRepository<TestResult>(c), new EfRepository<Specimen>(c), new EfRepository<PatientBloodTypeHistory>(c),
            c, _factory.Clock, _factory.CurrentUser, new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            new EfRepository<TestDefinition>(c), new EfRepository<SubtestDefinition>(c), new EfRepository<Order>(c), new EfRepository<OrderLine>(c),
            new InventoryRepository(c), Compatibility(c), new EfRepository<AntibodyHistory>(c),
            new EfRepository<AntigenProfile>(c), new EfRepository<BloodAttributeDefinition>(c),
            new EfRepository<UnitBloodAttribute>(c), new EfRepository<SpecimenTypeDefinition>(c),
            reflexRules: new EfRepository<ReflexRule>(c));

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

    private TestWorklistService Worklist(BloodBankDbContext c) =>
        new(new EfRepository<Order>(c), new EfRepository<OrderLine>(c), new EfRepository<OrderSpecimen>(c),
            new EfRepository<Specimen>(c), new EfRepository<Patient>(c), new EfRepository<TestResult>(c),
            new EfRepository<TestDefinition>(c), new EfRepository<SpecimenTypeDefinition>(c), _factory.Clock);

    private ReflexRuleAdminService ReflexAdmin(BloodBankDbContext c)
    {
        var audit = new AuditWriter(c, _factory.Clock, _factory.CurrentUser, new StaticEnvironmentInfo("Development", false));
        var history = new ConfigurationHistoryWriter(c, _factory.Clock, _factory.CurrentUser, new StaticEnvironmentInfo("Development", false));
        return new ReflexRuleAdminService(
            new EfRepository<ReflexRule>(c),
            new EfRepository<TestDefinition>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            audit,
            history);
    }

    private async Task<(Patient patient, Specimen specimen, PatientOrderDto order, OrderLineDto abscLine)> SeedAbscOrderAsync(BloodBankDbContext c)
    {
        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-{Guid.NewGuid():N}",
            LastName = "Reflex",
            FirstName = "Patient",
            DateOfBirth = new DateOnly(1988, 5, 5),
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

        var specimen = new Specimen
        {
            AccessionNumber = $"ACC-{Guid.NewGuid():N}",
            PatientId = patient.Id,
            EncounterId = encounter.Id,
            SpecimenType = "EDTA",
            CollectedUtc = _factory.Clock.UtcNow.AddHours(-2),
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(2),
            Status = SpecimenStatus.Accepted
        };
        c.Specimens.Add(specimen);

        c.TestDefinitions.AddRange(
            new TestDefinition
            {
                Code = "ABSC",
                Name = "Antibody Screen",
                Category = TestCategory.AntibodyScreen,
                ResultValueType = ResultValueType.Coded,
                AllowedResultValues = "Negative\nPositive",
                IsActive = true,
                IsDraft = false,
                EffectiveUtc = _factory.Clock.UtcNow
            },
            new TestDefinition
            {
                Code = "ABID",
                Name = "Antibody Identification",
                Category = TestCategory.AntibodyIdentification,
                ResultValueType = ResultValueType.FreeText,
                IsActive = true,
                IsDraft = false,
                EffectiveUtc = _factory.Clock.UtcNow
            });
        await c.SaveChangesAsync();

        var created = await Orders(c).CreateAsync(patient.Id, new CreateOrderRequest(
            encounter.Id, location.Id, $"ORD-{Guid.NewGuid():N}",
            [new OrderLineInputDto(OrderCategory.Test, "ABSC", null)],
            OrderPriority.Routine, _factory.Clock.UtcNow, null, OrderSource.Manual, null, null,
            SpecimenId: specimen.Id));
        Assert.True(created.Succeeded);

        var list = await Orders(c).ListByPatientAsync(patient.Id);
        var order = Assert.Single(list);
        var line = Assert.Single(order.Lines);
        return (patient, specimen, order, line);
    }

    private async Task SeedAbscPositiveRuleAsync(BloodBankDbContext c)
    {
        c.ReflexRules.Add(new ReflexRule
        {
            Code = "ABSC-POS-ABID",
            Name = "Positive antibody screen reflexes antibody identification",
            TriggerTestCode = "ABSC",
            TriggerResultValue = "Positive",
            ReflexTestCode = "ABID",
            IsActive = true,
            IsDraft = false,
            EffectiveUtc = _factory.Clock.UtcNow,
            Version = 1
        });
        await c.SaveChangesAsync();
    }

    [Fact]
    public async Task VerifyPositiveAbsc_AddsAbidLine_AndShowsOnWorklist()
    {
        await using var c = _factory.Create();
        var (patient, specimen, order, line) = await SeedAbscOrderAsync(c);
        await SeedAbscPositiveRuleAsync(c);

        var save = await Results(c).SaveTestResultAsync(new SaveTestResultRequest(
            specimen.Id, order.Id, line.Id, "ABSC", "Positive", null, null, null, null, null,
            MarkComplete: true, CorrectionReason: null, UnitNumber: null, CrossmatchMethod: null,
            CrossmatchResult: null, AntibodyScreenNegative: null));
        Assert.True(save.Succeeded);
        Assert.Equal(ResultStatus.Verified, save.Value!.Status);

        var lines = await c.OrderLines.Where(l => l.OrderId == order.Id && l.IsActive).OrderBy(l => l.LineNumber).ToListAsync();
        Assert.Equal(2, lines.Count);
        Assert.Contains(lines, l => l.TestCode == "ABID");
        Assert.Equal(OrderType.AntibodyIdentification, lines.Single(l => l.TestCode == "ABID").OrderType);

        var pending = await Worklist(c).ListForPatientAsync(patient.Id, TestWorklistFilter.Pending);
        Assert.Contains(pending, i => i.TestCode == "ABID" && i.SpecimenId == specimen.Id);
    }

    [Fact]
    public async Task VerifyNegativeAbsc_DoesNotAddAbid()
    {
        await using var c = _factory.Create();
        var (_, specimen, order, line) = await SeedAbscOrderAsync(c);
        await SeedAbscPositiveRuleAsync(c);

        var save = await Results(c).SaveTestResultAsync(new SaveTestResultRequest(
            specimen.Id, order.Id, line.Id, "ABSC", "Negative", null, null, null, null, null,
            MarkComplete: true, CorrectionReason: null, UnitNumber: null, CrossmatchMethod: null,
            CrossmatchResult: null, AntibodyScreenNegative: null));
        Assert.True(save.Succeeded);

        var lines = await c.OrderLines.Where(l => l.OrderId == order.Id && l.IsActive).ToListAsync();
        Assert.Single(lines);
        Assert.DoesNotContain(lines, l => l.TestCode == "ABID");
    }

    [Fact]
    public async Task VerifyPositiveAbsc_WhenAbidAlreadyOrdered_DoesNotDuplicate()
    {
        await using var c = _factory.Create();
        var (_, specimen, order, line) = await SeedAbscOrderAsync(c);
        await SeedAbscPositiveRuleAsync(c);

        c.OrderLines.Add(new OrderLine
        {
            OrderId = order.Id,
            LineNumber = 2,
            LineCategory = OrderCategory.Test,
            LineName = "Antibody Identification",
            TestCode = "ABID",
            OrderType = OrderType.AntibodyIdentification,
            ResultStatus = ResultStatus.Pending,
            IsActive = true
        });
        await c.SaveChangesAsync();

        var save = await Results(c).SaveTestResultAsync(new SaveTestResultRequest(
            specimen.Id, order.Id, line.Id, "ABSC", "Positive", null, null, null, null, null,
            MarkComplete: true, CorrectionReason: null, UnitNumber: null, CrossmatchMethod: null,
            CrossmatchResult: null, AntibodyScreenNegative: null));
        Assert.True(save.Succeeded);

        var abidCount = await c.OrderLines.CountAsync(l => l.OrderId == order.Id && l.IsActive && l.TestCode == "ABID");
        Assert.Equal(1, abidCount);
    }

    [Fact]
    public async Task ReflexAdmin_CreateAndActivate_Succeeds()
    {
        await using var c = _factory.Create();
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var trigger = $"T{suffix}";
        var reflex = $"R{suffix}";
        c.TestDefinitions.AddRange(
            new TestDefinition { Code = trigger, Name = "Trigger", IsActive = true, IsDraft = false },
            new TestDefinition { Code = reflex, Name = "Reflex", IsActive = true, IsDraft = false });
        await c.SaveChangesAsync();

        var svc = ReflexAdmin(c);
        var created = await svc.CreateAsync(new SaveReflexRuleRequest(
            $"RULE-{suffix}",
            "Trigger to reflex",
            trigger,
            "Positive",
            reflex,
            null));
        Assert.True(created.Succeeded, created.Error ?? string.Join("; ", created.Evaluation?.HardStops.Select(h => h.Message) ?? []));

        var activated = await svc.ActivateAsync(created.Value!.Id, "seed");
        Assert.True(activated.Succeeded, activated.Error ?? string.Join("; ", activated.Evaluation?.HardStops.Select(h => h.Message) ?? []));
        Assert.True(activated.Value!.IsActive);
        Assert.Equal(trigger, activated.Value.TriggerTestCode);
        Assert.Equal(reflex, activated.Value.ReflexTestCode);
    }
}
