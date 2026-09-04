using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Application.Compatibility;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Application.Results;
using BloodBankLIS.Application.Rules;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

/// <summary>
/// End-to-end coverage for the configurable order and test rules engine, driven through
/// the two reference rules: neonatal type and screen substitution at order entry, and
/// Weak D reflex on an Rh negative ABO/Rh interpretation at result verification.
/// </summary>
public class RulesEngineTests : IClassFixture<SqliteContextFactory>
{
    private const string NeonatalCondition = "patient.ageDays < 1 AND order.hasTest('TNS')";
    private const string NeonatalAction = "cancelTest('TNS'); addTest('TSNEO')";

    private const string WeakDCondition =
        "test.code = 'ABORH' AND test.interpretation IN ('A Negative','B Negative','O Negative','AB Negative')";

    private const string WeakDAction = "addTest('WEAKD')";

    private readonly SqliteContextFactory _factory;

    public RulesEngineTests(SqliteContextFactory factory) => _factory = factory;

    private RuleEngineService Engine(BloodBankDbContext c) =>
        new(new EfRepository<RuleDefinition>(c), new EfRepository<RuleExecutionLog>(c),
            new EfRepository<Patient>(c), new EfRepository<PatientBloodTypeHistory>(c),
            new EfRepository<Order>(c), new EfRepository<OrderLine>(c),
            new EfRepository<OrderSpecimen>(c), new EfRepository<Specimen>(c),
            new EfRepository<TestDefinition>(c), new EfRepository<TestGrouper>(c),
            new EfRepository<ProductType>(c), _factory.Clock,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser));

    private OrderService Orders(BloodBankDbContext c) =>
        new(new EfRepository<Order>(c), new EfRepository<OrderLine>(c), new EfRepository<OrderSpecimen>(c),
            new EfRepository<Encounter>(c), new EfRepository<OrderingLocation>(c), new EfRepository<Patient>(c),
            new EfRepository<Specimen>(c), new EfRepository<OrderingProvider>(c), new EfRepository<ProductType>(c),
            new EfRepository<TestDefinition>(c), new EfRepository<TestGrouper>(c), _factory.Clock, c,
            Engine(c));

    private static ICurrentUser Verifier => new TestCurrentUser("tech-verify", "WORKSTATION-2");

    private ResultService Results(BloodBankDbContext c, ICurrentUser? user = null)
    {
        var current = user ?? _factory.CurrentUser;
        return new(new EfRepository<TestResult>(c), new EfRepository<Specimen>(c), new EfRepository<PatientBloodTypeHistory>(c),
            c, _factory.Clock, current, new AuditWriter(c, _factory.Clock, current),
            new EfRepository<TestDefinition>(c), new EfRepository<SubtestDefinition>(c),
            new EfRepository<Order>(c), new EfRepository<OrderLine>(c),
            new InventoryRepository(c), Compatibility(c), new EfRepository<AntibodyHistory>(c),
            new EfRepository<AntigenProfile>(c), new EfRepository<BloodAttributeDefinition>(c),
            new EfRepository<UnitBloodAttribute>(c), new EfRepository<SpecimenTypeDefinition>(c),
            reflexRules: new EfRepository<ReflexRule>(c),
            ruleEngine: Engine(c));
    }

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

    private RuleDefinitionAdminService RuleAdmin(BloodBankDbContext c)
    {
        var env = new StaticEnvironmentInfo("Development", false);
        return new RuleDefinitionAdminService(
            new EfRepository<RuleDefinition>(c),
            new EfRepository<TestDefinition>(c),
            new EfRepository<TestGrouper>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser, env),
            new ConfigurationHistoryWriter(c, _factory.Clock, _factory.CurrentUser, env));
    }

    // ---- Fixtures ----

    /// <summary>
    /// The class fixture shares one database across the whole class, so each test starts
    /// from a clean rule set. Otherwise a rule seeded by one test would fire in the next.
    /// </summary>
    private async Task PrepareAsync(BloodBankDbContext c)
    {
        c.RuleExecutionLogs.RemoveRange(await c.RuleExecutionLogs.ToListAsync());
        c.RuleDefinitions.RemoveRange(await c.RuleDefinitions.ToListAsync());
        await c.SaveChangesAsync();
        await SeedCatalogAsync(c);
    }

    private async Task SeedCatalogAsync(BloodBankDbContext c)
    {
        if (await c.TestDefinitions.AnyAsync(t => t.Code == "ABORH"))
        {
            return;
        }

        var now = _factory.Clock.UtcNow;
        c.TestDefinitions.AddRange(
            new TestDefinition
            {
                Code = "ABORH",
                Name = "ABO/Rh Type",
                Category = TestCategory.AboRh,
                ResultValueType = ResultValueType.AboRh,
                IsActive = true,
                IsDraft = false,
                EffectiveUtc = now
            },
            new TestDefinition
            {
                Code = "ABSC",
                Name = "Antibody Screen",
                Category = TestCategory.AntibodyScreen,
                ResultValueType = ResultValueType.Coded,
                AllowedResultValues = "Negative\nPositive",
                IsActive = true,
                IsDraft = false,
                EffectiveUtc = now
            },
            new TestDefinition
            {
                Code = "WEAKD",
                Name = "Weak D Test",
                Category = TestCategory.AboRh,
                ResultValueType = ResultValueType.Coded,
                AllowedResultValues = "Negative\nPositive",
                IsActive = true,
                IsDraft = false,
                EffectiveUtc = now
            },
            new TestDefinition
            {
                Code = "TSNEO",
                Name = "Neonatal Type and Screen",
                Category = TestCategory.AboRh,
                ResultValueType = ResultValueType.Coded,
                AllowedResultValues = "Negative\nPositive",
                IsActive = true,
                IsDraft = false,
                EffectiveUtc = now
            });

        c.TestGroupers.Add(new TestGrouper
        {
            Code = "TNS",
            Name = "Type and Screen",
            MemberTestsJson = TestGrouperMembers.ToJson([
                new TestGrouperMember("ABORH", 1),
                new TestGrouperMember("ABSC", 2)
            ]),
            IsActive = true,
            IsDraft = false,
            EffectiveUtc = now,
            Version = 1
        });

        await c.SaveChangesAsync();
    }

    private async Task<RuleDefinition> SeedRuleAsync(
        BloodBankDbContext c,
        RuleLevel level,
        string condition,
        string action,
        bool stopOnMatch = false)
    {
        var rule = new RuleDefinition
        {
            Code = $"RULE-{Guid.NewGuid():N}"[..20].ToUpperInvariant(),
            Name = "Test rule",
            Level = level,
            Priority = 100,
            StopOnMatch = stopOnMatch,
            ConditionExpression = condition,
            ActionExpression = action,
            IsActive = true,
            IsDraft = false,
            EffectiveUtc = _factory.Clock.UtcNow,
            Version = 1
        };

        c.RuleDefinitions.Add(rule);
        await c.SaveChangesAsync();
        return rule;
    }

    private async Task<(Patient patient, Encounter encounter, OrderingLocation location, Specimen specimen)>
        SeedPatientAsync(BloodBankDbContext c, int ageInDays)
    {
        var now = _factory.Clock.UtcNow;
        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-{Guid.NewGuid():N}",
            LastName = "Rules",
            FirstName = "Patient",
            DateOfBirth = DateOnly.FromDateTime(now).AddDays(-ageInDays),
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
            AdmitUtc = now.AddHours(-4)
        };
        c.Encounters.Add(encounter);
        await c.SaveChangesAsync();

        var specimen = new Specimen
        {
            AccessionNumber = $"ACC-{Guid.NewGuid():N}",
            PatientId = patient.Id,
            EncounterId = encounter.Id,
            SpecimenType = "EDTA",
            CollectedUtc = now.AddHours(-2),
            ExpiresUtc = now.AddDays(2),
            Status = SpecimenStatus.Accepted
        };
        c.Specimens.Add(specimen);
        await c.SaveChangesAsync();

        return (patient, encounter, location, specimen);
    }

    private async Task<Order> CreateOrderAsync(
        BloodBankDbContext c,
        Patient patient,
        Encounter encounter,
        OrderingLocation location,
        Specimen specimen,
        params OrderLineInputDto[] lines)
    {
        var created = await Orders(c).CreateAsync(patient.Id, new CreateOrderRequest(
            encounter.Id, location.Id, $"ORD-{Guid.NewGuid():N}",
            lines,
            OrderPriority.Routine, _factory.Clock.UtcNow, null, OrderSource.Manual, null, null,
            SpecimenId: specimen.Id));

        Assert.True(created.Succeeded, created.Error);
        return created.Value!;
    }

    private static Task<List<OrderLine>> ActiveLinesAsync(BloodBankDbContext c, long orderId) =>
        c.OrderLines.Where(l => l.OrderId == orderId && l.IsActive).OrderBy(l => l.LineNumber).ToListAsync();

    // ---- Order level ----

    [Fact]
    public async Task NeonatalRule_SwapsTypeAndScreenForTheNeonatalTest()
    {
        await using var c = _factory.Create();
        await PrepareAsync(c);
        await SeedRuleAsync(c, RuleLevel.Order, NeonatalCondition, NeonatalAction);
        var (patient, encounter, location, specimen) = await SeedPatientAsync(c, ageInDays: 0);

        var order = await CreateOrderAsync(c, patient, encounter, location, specimen,
            new OrderLineInputDto(OrderCategory.Test, "TNS", null));

        var lines = await ActiveLinesAsync(c, order.Id);
        Assert.Equal("TSNEO", Assert.Single(lines).TestCode);
    }

    [Fact]
    public async Task NeonatalRule_DoesNotFireForAnOlderPatient()
    {
        await using var c = _factory.Create();
        await PrepareAsync(c);
        await SeedRuleAsync(c, RuleLevel.Order, NeonatalCondition, NeonatalAction);
        var (patient, encounter, location, specimen) = await SeedPatientAsync(c, ageInDays: 2);

        var order = await CreateOrderAsync(c, patient, encounter, location, specimen,
            new OrderLineInputDto(OrderCategory.Test, "TNS", null));

        var lines = await ActiveLinesAsync(c, order.Id);
        Assert.Equal(new[] { "ABORH", "ABSC" }, lines.Select(l => l.TestCode).Order().ToArray());
    }

    [Fact]
    public async Task NeonatalRule_WritesAnExecutionLogExplainingTheChange()
    {
        await using var c = _factory.Create();
        await PrepareAsync(c);
        var rule = await SeedRuleAsync(c, RuleLevel.Order, NeonatalCondition, NeonatalAction);
        var (patient, encounter, location, specimen) = await SeedPatientAsync(c, ageInDays: 0);

        var order = await CreateOrderAsync(c, patient, encounter, location, specimen,
            new OrderLineInputDto(OrderCategory.Test, "TNS", null));

        var log = Assert.Single(await c.RuleExecutionLogs.Where(l => l.OrderId == order.Id).ToListAsync());
        Assert.Equal(rule.Code, log.RuleCode);
        Assert.Equal(RuleLevel.Order, log.Level);
        Assert.Equal(patient.Id, log.PatientId);
        Assert.Contains("cancelTest('TNS')", log.ActionsJson);
        Assert.Contains("addTest('TSNEO')", log.ActionsJson);
    }

    [Fact]
    public async Task OrderRule_CanBlockAnOrder()
    {
        await using var c = _factory.Create();
        await PrepareAsync(c);
        await SeedRuleAsync(c, RuleLevel.Order, "order.specimenType = 'EDTA'", "block('Specimen type not allowed')");
        var (patient, encounter, location, specimen) = await SeedPatientAsync(c, ageInDays: 30);

        var created = await Orders(c).CreateAsync(patient.Id, new CreateOrderRequest(
            encounter.Id, location.Id, $"ORD-{Guid.NewGuid():N}",
            [new OrderLineInputDto(OrderCategory.Test, "ABSC", null)],
            OrderPriority.Routine, _factory.Clock.UtcNow, null, OrderSource.Manual, null, null,
            SpecimenId: specimen.Id));

        Assert.False(created.Succeeded);
        Assert.Equal("Specimen type not allowed", created.Error);
        Assert.Empty(await c.Orders.Where(o => o.PatientId == patient.Id).ToListAsync());
    }

    [Fact]
    public async Task OrderRule_CanWarnWithoutBlocking()
    {
        await using var c = _factory.Create();
        await PrepareAsync(c);
        await SeedRuleAsync(c, RuleLevel.Order, "patient.ageDays < 1", "warn('Neonatal protocol applies')");
        var (patient, encounter, location, specimen) = await SeedPatientAsync(c, ageInDays: 0);

        var created = await Orders(c).CreateAsync(patient.Id, new CreateOrderRequest(
            encounter.Id, location.Id, $"ORD-{Guid.NewGuid():N}",
            [new OrderLineInputDto(OrderCategory.Test, "ABSC", null)],
            OrderPriority.Routine, _factory.Clock.UtcNow, null, OrderSource.Manual, null, null,
            SpecimenId: specimen.Id));

        Assert.True(created.Succeeded, created.Error);
        Assert.Contains(created.Warnings, w => w.Message == "Neonatal protocol applies");
    }

    [Fact]
    public async Task OrderRule_DoesNotAddATestThatIsAlreadyOrdered()
    {
        await using var c = _factory.Create();
        await PrepareAsync(c);
        await SeedRuleAsync(c, RuleLevel.Order, "order.hasTest('ABSC')", "addTest('WEAKD')");
        var (patient, encounter, location, specimen) = await SeedPatientAsync(c, ageInDays: 30);

        var order = await CreateOrderAsync(c, patient, encounter, location, specimen,
            new OrderLineInputDto(OrderCategory.Test, "ABSC", null),
            new OrderLineInputDto(OrderCategory.Test, "WEAKD", null));

        var lines = await ActiveLinesAsync(c, order.Id);
        Assert.Equal(1, lines.Count(l => l.TestCode == "WEAKD"));
    }

    [Fact]
    public async Task OrderRule_SkipsATestMissingFromTheCatalog()
    {
        await using var c = _factory.Create();
        await PrepareAsync(c);
        await SeedRuleAsync(c, RuleLevel.Order, "order.hasTest('ABSC')", "addTest('NOSUCHTEST')");
        var (patient, encounter, location, specimen) = await SeedPatientAsync(c, ageInDays: 30);

        var order = await CreateOrderAsync(c, patient, encounter, location, specimen,
            new OrderLineInputDto(OrderCategory.Test, "ABSC", null));

        var lines = await ActiveLinesAsync(c, order.Id);
        Assert.Equal("ABSC", Assert.Single(lines).TestCode);

        var log = Assert.Single(await c.RuleExecutionLogs.Where(l => l.OrderId == order.Id).ToListAsync());
        Assert.Contains("not in the active catalog", log.Notes);
    }

    // ---- Test level ----

    private async Task<(Order order, OrderLine line, Specimen specimen)> SeedAboRhOrderAsync(
        BloodBankDbContext c,
        Patient patient,
        Encounter encounter,
        OrderingLocation location,
        Specimen specimen)
    {
        var order = await CreateOrderAsync(c, patient, encounter, location, specimen,
            new OrderLineInputDto(OrderCategory.Test, "ABORH", null));
        var line = Assert.Single(await ActiveLinesAsync(c, order.Id));
        return (order, line, specimen);
    }

    private async Task<EvaluationResultProbe> VerifyAboRhAsync(
        BloodBankDbContext c,
        Order order,
        OrderLine line,
        Specimen specimen,
        AboGroup abo,
        RhType rh)
    {
        var save = await Results(c).SaveTestResultAsync(new SaveTestResultRequest(
            specimen.Id, order.Id, line.Id, "ABORH", null, null, null, abo, rh, null,
            MarkComplete: true, CorrectionReason: null, UnitNumber: null, CrossmatchMethod: null,
            CrossmatchResult: null, AntibodyScreenNegative: null));

        Assert.True(save.Succeeded, save.Error);
        Assert.Equal(ResultStatus.Entered, save.Value!.Status);

        var verified = await Results(c, Verifier).VerifyResultAsync(save.Value.Id);
        Assert.True(verified.Succeeded, verified.Error);
        return new EvaluationResultProbe(verified.Value!);
    }

    private sealed record EvaluationResultProbe(TestResult Result);

    [Fact]
    public async Task WeakDRule_AddsWeakDForAnRhNegativeType()
    {
        await using var c = _factory.Create();
        await PrepareAsync(c);
        await SeedRuleAsync(c, RuleLevel.Test, WeakDCondition, WeakDAction);
        var (patient, encounter, location, specimen) = await SeedPatientAsync(c, ageInDays: 30);
        var (order, line, _) = await SeedAboRhOrderAsync(c, patient, encounter, location, specimen);

        await VerifyAboRhAsync(c, order, line, specimen, AboGroup.O, RhType.Negative);

        var lines = await ActiveLinesAsync(c, order.Id);
        Assert.Contains(lines, l => l.TestCode == "WEAKD");
        Assert.Equal(ResultStatus.Pending, lines.Single(l => l.TestCode == "WEAKD").ResultStatus);
    }

    [Theory]
    [InlineData(AboGroup.A)]
    [InlineData(AboGroup.B)]
    [InlineData(AboGroup.AB)]
    public async Task WeakDRule_AddsWeakDForEveryRhNegativeAboGroup(AboGroup abo)
    {
        await using var c = _factory.Create();
        await PrepareAsync(c);
        await SeedRuleAsync(c, RuleLevel.Test, WeakDCondition, WeakDAction);
        var (patient, encounter, location, specimen) = await SeedPatientAsync(c, ageInDays: 30);
        var (order, line, _) = await SeedAboRhOrderAsync(c, patient, encounter, location, specimen);

        await VerifyAboRhAsync(c, order, line, specimen, abo, RhType.Negative);

        Assert.Contains(await ActiveLinesAsync(c, order.Id), l => l.TestCode == "WEAKD");
    }

    [Fact]
    public async Task WeakDRule_DoesNotFireForAnRhPositiveType()
    {
        await using var c = _factory.Create();
        await PrepareAsync(c);
        await SeedRuleAsync(c, RuleLevel.Test, WeakDCondition, WeakDAction);
        var (patient, encounter, location, specimen) = await SeedPatientAsync(c, ageInDays: 30);
        var (order, line, _) = await SeedAboRhOrderAsync(c, patient, encounter, location, specimen);

        await VerifyAboRhAsync(c, order, line, specimen, AboGroup.O, RhType.Positive);

        Assert.DoesNotContain(await ActiveLinesAsync(c, order.Id), l => l.TestCode == "WEAKD");
    }

    [Fact]
    public async Task WeakDRule_DoesNotDuplicateAWeakDAlreadyOnTheOrder()
    {
        await using var c = _factory.Create();
        await PrepareAsync(c);
        await SeedRuleAsync(c, RuleLevel.Test, WeakDCondition, WeakDAction);
        var (patient, encounter, location, specimen) = await SeedPatientAsync(c, ageInDays: 30);

        var order = await CreateOrderAsync(c, patient, encounter, location, specimen,
            new OrderLineInputDto(OrderCategory.Test, "ABORH", null),
            new OrderLineInputDto(OrderCategory.Test, "WEAKD", null));
        var line = (await ActiveLinesAsync(c, order.Id)).Single(l => l.TestCode == "ABORH");

        await VerifyAboRhAsync(c, order, line, specimen, AboGroup.O, RhType.Negative);

        var lines = await ActiveLinesAsync(c, order.Id);
        Assert.Equal(1, lines.Count(l => l.TestCode == "WEAKD"));
    }

    [Fact]
    public async Task TestRule_FiresOnlyOncePerResult()
    {
        await using var c = _factory.Create();
        await PrepareAsync(c);
        await SeedRuleAsync(c, RuleLevel.Test, WeakDCondition, WeakDAction);
        var (patient, encounter, location, specimen) = await SeedPatientAsync(c, ageInDays: 30);
        var (order, line, _) = await SeedAboRhOrderAsync(c, patient, encounter, location, specimen);

        var probe = await VerifyAboRhAsync(c, order, line, specimen, AboGroup.O, RhType.Negative);

        // Re-running the engine over the same verified result must not add a second line.
        await Engine(c).ApplyTestRulesAsync(probe.Result);
        await c.SaveChangesAsync();

        var lines = await ActiveLinesAsync(c, order.Id);
        Assert.Equal(1, lines.Count(l => l.TestCode == "WEAKD"));
        Assert.Single(await c.RuleExecutionLogs.Where(l => l.TestResultId == probe.Result.Id).ToListAsync());
    }

    [Fact]
    public async Task TestRule_CanMatchOnPatientAndOrderAttributesToo()
    {
        await using var c = _factory.Create();
        await PrepareAsync(c);
        await SeedRuleAsync(c, RuleLevel.Test,
            "test.code = 'ABORH' AND patient.ageDays < 1 AND order.specimenType = 'EDTA'",
            WeakDAction);
        var (patient, encounter, location, specimen) = await SeedPatientAsync(c, ageInDays: 0);
        var (order, line, _) = await SeedAboRhOrderAsync(c, patient, encounter, location, specimen);

        await VerifyAboRhAsync(c, order, line, specimen, AboGroup.O, RhType.Positive);

        Assert.Contains(await ActiveLinesAsync(c, order.Id), l => l.TestCode == "WEAKD");
    }

    [Fact]
    public async Task InactiveRule_IsNotEvaluated()
    {
        await using var c = _factory.Create();
        await PrepareAsync(c);
        var rule = await SeedRuleAsync(c, RuleLevel.Test, WeakDCondition, WeakDAction);
        rule.IsActive = false;
        await c.SaveChangesAsync();

        var (patient, encounter, location, specimen) = await SeedPatientAsync(c, ageInDays: 30);
        var (order, line, _) = await SeedAboRhOrderAsync(c, patient, encounter, location, specimen);

        await VerifyAboRhAsync(c, order, line, specimen, AboGroup.O, RhType.Negative);

        Assert.DoesNotContain(await ActiveLinesAsync(c, order.Id), l => l.TestCode == "WEAKD");
    }

    // ---- Admin ----

    [Fact]
    public async Task RuleAdmin_CreateThenActivate_RoundTrips()
    {
        await using var c = _factory.Create();
        await PrepareAsync(c);
        var svc = RuleAdmin(c);

        var code = $"WEAKD-{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        var created = await svc.CreateAsync(new SaveRuleDefinitionRequest(
            code, "Weak D on Rh negative", null, RuleLevel.Test, 100, false,
            WeakDCondition, WeakDAction, null));

        Assert.True(created.Succeeded, Describe(created));
        Assert.True(created.Value!.IsDraft);
        Assert.False(created.Value.IsActive);

        var activated = await svc.ActivateAsync(created.Value.Id, "Validated for go-live");
        Assert.True(activated.Succeeded, Describe(activated));
        Assert.True(activated.Value!.IsActive);
        Assert.False(activated.Value.IsDraft);

        var history = await c.ConfigurationChangeHistory
            .Where(h => h.EntityType == nameof(RuleDefinition) && h.EntityId == created.Value.Id)
            .ToListAsync();
        Assert.Contains(history, h => h.Action == ConfigChangeAction.Create);
        Assert.Contains(history, h => h.Action == ConfigChangeAction.Activate);
    }

    [Fact]
    public async Task RuleAdmin_RejectsAConditionTheLevelCannotSupply()
    {
        await using var c = _factory.Create();
        await PrepareAsync(c);

        var created = await RuleAdmin(c).CreateAsync(new SaveRuleDefinitionRequest(
            $"BAD-{Guid.NewGuid():N}"[..12].ToUpperInvariant(), "Bad rule", null, RuleLevel.Order, 100, false,
            "test.interpretation = 'A Negative'", "addTest('WEAKD')", null));

        Assert.False(created.Succeeded);
        Assert.Contains(created.Evaluation!.HardStops, h => h.Code == "RULE.CONDITION.ATTRIBUTE");
    }

    [Fact]
    public async Task RuleAdmin_Validate_ReportsSyntaxAndUnknownTestWarnings()
    {
        await using var c = _factory.Create();
        await PrepareAsync(c);
        var svc = RuleAdmin(c);

        var good = await svc.ValidateAsync(new ValidateRuleRequest(RuleLevel.Test, WeakDCondition, WeakDAction));
        Assert.True(good.IsValid);
        Assert.Empty(good.Warnings);
        Assert.Equal(new[] { "addTest('WEAKD')" }, good.ParsedActions.ToArray());

        var unknownTest = await svc.ValidateAsync(new ValidateRuleRequest(
            RuleLevel.Test, WeakDCondition, "addTest('NOPE')"));
        Assert.True(unknownTest.IsValid);
        Assert.Contains(unknownTest.Warnings, w => w.Code == "RULE.TEST.UNKNOWN");

        var broken = await svc.ValidateAsync(new ValidateRuleRequest(
            RuleLevel.Test, "test.code = ", WeakDAction));
        Assert.False(broken.IsValid);
        Assert.Contains(broken.HardStops, h => h.Code == "RULE.CONDITION.SYNTAX");
    }

    [Fact]
    public async Task RuleAdmin_ActiveEditRequiresAChangeReason()
    {
        await using var c = _factory.Create();
        await PrepareAsync(c);
        var svc = RuleAdmin(c);

        var code = $"EDIT-{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        var created = await svc.CreateAsync(new SaveRuleDefinitionRequest(
            code, "Editable", null, RuleLevel.Test, 100, false, WeakDCondition, WeakDAction, null));
        await svc.ActivateAsync(created.Value!.Id, "go-live");

        var noReason = await svc.UpdateAsync(created.Value.Id, new SaveRuleDefinitionRequest(
            code, "Editable renamed", null, RuleLevel.Test, 100, false, WeakDCondition, WeakDAction, null));
        Assert.False(noReason.Succeeded);

        var withReason = await svc.UpdateAsync(created.Value.Id, new SaveRuleDefinitionRequest(
            code, "Editable renamed", null, RuleLevel.Test, 100, false, WeakDCondition, WeakDAction, "typo"));
        Assert.True(withReason.Succeeded, Describe(withReason));
        Assert.Equal(2, withReason.Value!.Version);
    }

    private static string Describe<T>(BloodBankLIS.Application.Common.EvaluationResult<T> result) =>
        result.Error ?? string.Join("; ", result.Evaluation?.HardStops.Select(h => $"{h.Code}: {h.Message}") ?? []);
}
