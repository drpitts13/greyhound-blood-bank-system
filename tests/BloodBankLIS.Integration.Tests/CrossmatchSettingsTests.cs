using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Application.Billing;
using BloodBankLIS.Application.Compatibility;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Application.Results;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

/// <summary>TEST-BB-035 — Crossmatch settings, attach timing, order override, and EXM auto-result.</summary>
[Collection(nameof(CrossmatchSettingsTests))]
public class CrossmatchSettingsTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public CrossmatchSettingsTests(SqliteContextFactory factory) => _factory = factory;

    [Fact]
    public async Task Admin_Update_WritesHistory()
    {
        await using var c = _factory.Create();
        await SeedCatalogAsync(c);
        var svc = SettingsAdmin(c);

        var created = await svc.GetAsync();
        Assert.Equal("XM", created.NegativeAntibodyHistoryTestCode);

        var updated = await svc.UpdateAsync(new SaveCrossmatchSettingsRequest(
            "XM", "CXM", "EXM", 3, 2, 2, "Raise visit minimum"));
        Assert.True(updated.Succeeded, updated.Error);
        Assert.Equal(3, updated.Value!.ElectronicXmMinimumVisits);
        Assert.True(await c.ConfigurationChangeHistory.AnyAsync(h => h.EntityType == nameof(CrossmatchSettings)));
    }

    [Fact]
    public async Task ProductOrder_AttachesXm_AfterNegativeScreen()
    {
        await using var c = _factory.Create();
        var seed = await SeedProductAndScreenAsync(c, positiveScreen: false, antibodyHistory: false);
        var results = Results(c);
        var verify = await results.VerifyResultAsync(seed.ScreenResult.Id);
        Assert.True(verify.Succeeded, verify.Error);

        var lines = await c.OrderLines.Where(l => l.OrderId == seed.ProductOrder.Id && l.IsActive).ToListAsync();
        Assert.Contains(lines, l => l.TestCode == "XM");
    }

    [Fact]
    public async Task ProductOrder_AttachesCxm_AfterPositiveScreen()
    {
        await using var c = _factory.Create();
        var seed = await SeedProductAndScreenAsync(c, positiveScreen: true, antibodyHistory: false);
        var verify = await Results(c).VerifyResultAsync(seed.ScreenResult.Id);
        Assert.True(verify.Succeeded, verify.Error);

        var lines = await c.OrderLines.Where(l => l.OrderId == seed.ProductOrder.Id && l.IsActive).ToListAsync();
        Assert.Contains(lines, l => l.TestCode == "CXM");
    }

    [Fact]
    public async Task UpdateOrder_ComplexToSimple_RequiresOverrideAndPersists()
    {
        await using var c = _factory.Create();
        var seed = await SeedProductAndScreenAsync(c, positiveScreen: true, antibodyHistory: true);
        await Results(c).VerifyResultAsync(seed.ScreenResult.Id);

        var orders = Orders(c);
        var blocked = await orders.UpdateAsync(seed.Patient.Id, seed.ProductOrder.Id, new UpdateOrderRequest(
            seed.Encounter.Id,
            seed.Location.Id,
            [
                new OrderLineInputDto(OrderCategory.Product, null, seed.Product.Id),
                new OrderLineInputDto(OrderCategory.Test, "XM", null)
            ],
            OrderPriority.Routine,
            null));
        Assert.False(blocked.Succeeded);
        Assert.True(blocked.RequiresOverride);

        var ok = await orders.UpdateAsync(seed.Patient.Id, seed.ProductOrder.Id, new UpdateOrderRequest(
            seed.Encounter.Id,
            seed.Location.Id,
            [
                new OrderLineInputDto(OrderCategory.Product, null, seed.Product.Id),
                new OrderLineInputDto(OrderCategory.Test, "XM", null)
            ],
            OrderPriority.Routine,
            null,
            "Clinically indicated IS XM",
            "supervisor"));
        Assert.True(ok.Succeeded, ok.Error);
        Assert.True(await c.Overrides.AnyAsync(o =>
            o.RuleCode == AntibodyHistoryCrossmatchRule.RuleCode && o.ContextType == nameof(Order)));
    }

    // TEST-BB-316 (OCD-041): an order override started without an authorizer says so and records nothing.
    [Fact]
    public async Task UpdateOrder_OverrideReasonWithoutAuthorizer_SaysWhoIsMissing()
    {
        await using var c = _factory.Create();
        var seed = await SeedProductAndScreenAsync(c, positiveScreen: true, antibodyHistory: true);
        await Results(c).VerifyResultAsync(seed.ScreenResult.Id);

        var blocked = await Orders(c).UpdateAsync(seed.Patient.Id, seed.ProductOrder.Id, new UpdateOrderRequest(
            seed.Encounter.Id,
            seed.Location.Id,
            [
                new OrderLineInputDto(OrderCategory.Product, null, seed.Product.Id),
                new OrderLineInputDto(OrderCategory.Test, "XM", null)
            ],
            OrderPriority.Routine,
            null,
            "Clinically indicated IS XM",
            null));

        Assert.False(blocked.Succeeded);
        Assert.True(blocked.RequiresOverride);
        Assert.Contains(blocked.Evaluation!.Warnings, w => w.Code == OverrideFieldsRule.Code && w.Message.Contains("name who authorized it"));
        Assert.Contains(blocked.Evaluation.Warnings, w => w.Code == AntibodyHistoryCrossmatchRule.RuleCode);
        Assert.False(await c.Overrides.AnyAsync(o => o.ContextType == nameof(Order) && o.ContextId == seed.ProductOrder.Id));
    }

    [Fact]
    public async Task ProductOrder_Create_BeforeScreen_HasNoCrossmatchLine()
    {
        await using var c = _factory.Create();
        var seed = await SeedProductAndScreenAsync(c, positiveScreen: false, antibodyHistory: false, verifyScreen: false);
        var created = await Orders(c).CreateAsync(seed.Patient.Id, new CreateOrderRequest(
            seed.Encounter.Id,
            seed.Location.Id,
            $"RBC2-{Guid.NewGuid():N}"[..16],
            [new OrderLineInputDto(OrderCategory.Product, null, seed.Product.Id)],
            OrderPriority.Routine,
            _factory.Clock.UtcNow,
            null,
            OrderSource.Manual,
            null,
            null));
        Assert.True(created.Succeeded, created.Error);
        var lines = await c.OrderLines.Where(l => l.OrderId == created.Value!.Id && l.IsActive).ToListAsync();
        Assert.DoesNotContain(lines, l => OrderLineBuilder.IsCrossmatchTestCode(l.TestCode));
    }

    [Fact]
    public async Task ProductOrder_SkipsSerologic_WhenExmEligible()
    {
        await using var c = _factory.Create();
        var seed = await SeedExmEligibleAsync(c, screens: 2);
        await Attachment(c).AfterAntibodyScreenVerifiedAsync(seed.Patient.Id);

        var lines = await c.OrderLines.Where(l => l.OrderId == seed.ProductOrder.Id && l.IsActive).ToListAsync();
        Assert.DoesNotContain(lines, l => l.TestCode == "XM" || l.TestCode == "CXM");
    }

    [Fact]
    public async Task Allocate_ExmEligible_AutoResultsOnProductOrder()
    {
        await using var c = _factory.Create();
        var seed = await SeedExmEligibleAsync(c, screens: 2, withUnit: true);
        var beforeOrders = await c.Orders.CountAsync(o => o.PatientId == seed.Patient.Id);

        var alloc = await Allocations(c).AllocateAsync(seed.Patient.Id, new AllocatePatientUnitRequest(
            seed.Unit!.Id, seed.Encounter.Id, seed.Specimen.Id, seed.Location.Id, OrderId: seed.ProductOrder.Id));
        Assert.True(alloc.Succeeded, alloc.Error);
        Assert.Equal("EXM", alloc.Value!.CrossmatchTestCode);
        Assert.Equal(seed.ProductOrder.Id, alloc.Value.CrossmatchOrderId);
        Assert.Equal(beforeOrders, await c.Orders.CountAsync(o => o.PatientId == seed.Patient.Id));
        Assert.True(await c.TestResults.AnyAsync(r =>
            r.OrderId == seed.ProductOrder.Id && r.TestCode == "EXM" && r.Status == ResultStatus.Verified));
        Assert.True(await c.Crossmatches.AnyAsync(x =>
            x.PatientId == seed.Patient.Id
            && x.BloodProductId == seed.Unit.Id
            && x.Method == CrossmatchMethod.Electronic
            && x.Result == CrossmatchResult.Compatible));
    }

    [Fact]
    public async Task Allocate_ExmEligible_CapturesTestChargeOnVerify()
    {
        await using var c = _factory.Create();
        var seed = await SeedExmEligibleAsync(c, screens: 2, withUnit: true);
        var charge = new ChargeCode { Code = "BB-EXM", Description = "Electronic XM", DefaultAmount = 40m };
        c.ChargeCodes.Add(charge);
        await c.SaveChangesAsync();
        c.TestServiceBillings.Add(new TestServiceBilling
        {
            TestCode = "EXM",
            ChargeCodeId = charge.Id,
            Trigger = BillingTriggerType.TestVerified,
            IsActive = true
        });
        await c.SaveChangesAsync();

        var alloc = await Allocations(c).AllocateAsync(seed.Patient.Id, new AllocatePatientUnitRequest(
            seed.Unit!.Id, seed.Encounter.Id, seed.Specimen.Id, seed.Location.Id, OrderId: seed.ProductOrder.Id));
        Assert.True(alloc.Succeeded, alloc.Error);
        Assert.True(await c.BillingEvents.AnyAsync(e =>
            e.TriggerType == BillingTriggerType.TestVerified && e.BillingCode == "BB-EXM"));
    }

    [Fact]
    public async Task Allocate_CountsBelowMinimum_DoesNotAutoResultExm()
    {
        await using var c = _factory.Create();
        var seed = await SeedExmEligibleAsync(c, screens: 1, withUnit: true);
        var board = await Eligibility(c).AssessAsync(seed.Patient.Id);
        Assert.False(board!.Eligible);
        Assert.Contains(board.Criteria, cr =>
            cr.Code == ElectronicCrossmatchEligibilityRule.VisitsCode && !cr.Satisfied);

        var alloc = await Allocations(c).AllocateAsync(seed.Patient.Id, new AllocatePatientUnitRequest(
            seed.Unit!.Id, seed.Encounter.Id, seed.Specimen.Id, seed.Location.Id, OrderId: seed.ProductOrder.Id));
        Assert.True(alloc.Succeeded, alloc.Error);
        Assert.Equal("XM", alloc.Value!.CrossmatchTestCode);
        Assert.False(await c.TestResults.AnyAsync(r => r.TestCode == "EXM" && r.PatientId == seed.Patient.Id));
        Assert.Equal(2, await c.Orders.CountAsync(o => o.PatientId == seed.Patient.Id));
    }

    private CrossmatchSettingsAdminService SettingsAdmin(BloodBankDbContext c)
    {
        var env = new StaticEnvironmentInfo("Development", false);
        var audit = new AuditWriter(c, _factory.Clock, _factory.CurrentUser, env);
        var history = new ConfigurationHistoryWriter(c, _factory.Clock, _factory.CurrentUser, env);
        return new CrossmatchSettingsAdminService(
            new EfRepository<CrossmatchSettings>(c),
            new EfRepository<TestDefinition>(c),
            new FacilityPolicyService(new EfRepository<SystemSetting>(c)),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            audit,
            history);
    }

    private OrderService Orders(BloodBankDbContext c) =>
        new(new EfRepository<Order>(c), new EfRepository<OrderLine>(c), new EfRepository<OrderSpecimen>(c),
            new EfRepository<Encounter>(c), new EfRepository<OrderingLocation>(c), new EfRepository<Patient>(c),
            new EfRepository<Specimen>(c), new EfRepository<OrderingProvider>(c), new EfRepository<ProductType>(c),
            new EfRepository<TestDefinition>(c), new EfRepository<TestGrouper>(c), _factory.Clock, c,
            antibodyScreen: AntibodyScreen(c),
            exceptionDefinitions: new EfRepository<ExceptionDefinition>(c),
            overrides: new EfRepository<Override>(c),
            permissions: new FixedPermissionEvaluator(3),
            currentUser: _factory.CurrentUser,
            crossmatchAttachment: Attachment(c));

    // TEST-BB-1026: a unit reserved without a blood order (from Compatibility) joins the patient's one open product order
    // when its crossmatch is recorded; with no open order the message says to order the units on the chart (end-user
    // testing pass 17: "This allocation has no product order for the crossmatch").
    [Fact]
    public async Task Crossmatch_ForAReservationWithoutAnOrder_JoinsTheOpenOrderOrSaysWhatToDo()
    {
        await using var c = _factory.Create();
        var seed = await SeedProductAndScreenAsync(c, positiveScreen: false, antibodyHistory: false, verifyScreen: true);
        Allocation Reserve()
        {
            var unit = new BloodUnit
            {
                UnitNumber = $"U-{Guid.NewGuid():N}"[..16], ProductTypeId = seed.Product.Id, Abo = AboGroup.O, RhD = RhType.Positive,
                ExpiresUtc = _factory.Clock.UtcNow.AddDays(20), Status = UnitStatus.Allocated
            };
            c.BloodUnits.Add(unit);
            c.SaveChanges();
            var a = new Allocation
            {
                BloodProductId = unit.Id, PatientId = seed.Patient.Id, SpecimenId = seed.Specimen.Id,
                Status = AllocationStatus.Reserved, AllocatedUtc = _factory.Clock.UtcNow
            };
            c.Allocations.Add(a);
            c.SaveChanges();
            return a;
        }

        // A blood order with its crossmatch test line is Mixed (pass 18: a Mixed order was not found).
        (await c.Orders.FindAsync(seed.ProductOrder.Id))!.OrderCategory = OrderCategory.Mixed;
        await c.SaveChangesAsync();
        var joins = Reserve();
        var line = await Attachment(c).EnsureManualCrossmatchLineAsync(seed.Patient.Id, joins.Id, "XM", specimenId: seed.Specimen.Id);
        Assert.True(line.Succeeded, line.Error);
        Assert.Equal(seed.ProductOrder.Id, line.Value!.OrderId);
        Assert.Equal(seed.ProductOrder.Id, (await c.Allocations.AsNoTracking().SingleAsync(a => a.Id == joins.Id)).OrderId);

        (await c.Orders.FindAsync(seed.ProductOrder.Id))!.Status = OrderStatus.Cancelled;
        await c.SaveChangesAsync();
        var none = Reserve();
        var refused = await Attachment(c).EnsureManualCrossmatchLineAsync(seed.Patient.Id, none.Id, "XM", specimenId: seed.Specimen.Id);
        Assert.False(refused.Succeeded);
        Assert.Contains("reserved without a blood order, and the patient has no open blood order", refused.Error);
    }

    // TEST-BB-996: a unit whose compatible crossmatch has expired is crossmatched again on a current specimen linked to
    // the order (end-user testing, 2026-11-12: the repeat was refused as "already recorded"); a current compatible or any
    // incompatible result still stands; a specimen not linked to the order is refused by name, never swapped silently.
    [Fact]
    public async Task RepeatCrossmatch_AfterExpiry_OnALinkedSpecimen()
    {
        await using var c = _factory.Create();
        var seed = await SeedProductAndScreenAsync(c, positiveScreen: false, antibodyHistory: false, verifyScreen: true);
        var unit = new BloodUnit
        {
            UnitNumber = $"U-{Guid.NewGuid():N}"[..16],
            ProductTypeId = seed.Product.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(20),
            Status = UnitStatus.Allocated
        };
        c.BloodUnits.Add(unit);
        await c.SaveChangesAsync();
        var allocation = new Allocation
        {
            BloodProductId = unit.Id, PatientId = seed.Patient.Id, OrderId = seed.ProductOrder.Id, SpecimenId = seed.Specimen.Id,
            Status = AllocationStatus.Reserved, AllocatedUtc = _factory.Clock.UtcNow.AddDays(-4)
        };
        c.Allocations.Add(allocation);
        var earlier = new Crossmatch
        {
            BloodProductId = unit.Id, PatientId = seed.Patient.Id, SpecimenId = seed.Specimen.Id, Result = CrossmatchResult.Compatible,
            PerformedUtc = _factory.Clock.UtcNow.AddDays(-4), ExpiresUtc = _factory.Clock.UtcNow.AddHours(-1)
        };
        c.Crossmatches.Add(earlier);
        var other = new Specimen
        {
            AccessionNumber = $"ACC-NEW-{Guid.NewGuid():N}"[..20], PatientId = seed.Patient.Id, SpecimenType = "EDTA",
            CollectedUtc = _factory.Clock.UtcNow.AddHours(-1), ExpiresUtc = _factory.Clock.UtcNow.AddDays(3), Status = SpecimenStatus.Accepted
        };
        c.Specimens.Add(other);
        await c.SaveChangesAsync();

        var repeat = await Attachment(c).EnsureManualCrossmatchLineAsync(seed.Patient.Id, allocation.Id, "XM", specimenId: seed.Specimen.Id);
        Assert.True(repeat.Succeeded, repeat.Error);
        Assert.Equal(seed.Specimen.Id, repeat.Value!.SpecimenId);

        var unlinked = await Attachment(c).EnsureManualCrossmatchLineAsync(seed.Patient.Id, allocation.Id, "XM", specimenId: other.Id);
        Assert.False(unlinked.Succeeded);
        Assert.Contains($"Specimen {other.AccessionNumber} is not linked to order {seed.ProductOrder.OrderNumber}", unlinked.Error);

        earlier.ExpiresUtc = _factory.Clock.UtcNow.AddHours(5);
        await c.SaveChangesAsync();
        Assert.Contains("already recorded", (await Attachment(c).EnsureManualCrossmatchLineAsync(seed.Patient.Id, allocation.Id, "XM")).Error);

        earlier.Result = CrossmatchResult.Incompatible;
        earlier.ExpiresUtc = _factory.Clock.UtcNow.AddHours(-1);
        await c.SaveChangesAsync();
        Assert.Contains("already recorded", (await Attachment(c).EnsureManualCrossmatchLineAsync(seed.Patient.Id, allocation.Id, "XM")).Error);
    }

    private ResultService Results(BloodBankDbContext c) =>
        new(new EfRepository<TestResult>(c), new EfRepository<Specimen>(c), new EfRepository<PatientBloodTypeHistory>(c),
            c, _factory.Clock, _factory.CurrentUser, new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            new EfRepository<TestDefinition>(c), new EfRepository<SubtestDefinition>(c),
            new EfRepository<Order>(c), new EfRepository<OrderLine>(c),
            new InventoryRepository(c), Compatibility(c),
            crossmatchAttachment: Attachment(c));

    private CrossmatchAttachmentService Attachment(BloodBankDbContext c)
    {
        var settings = new CrossmatchSettingsReader(new EfRepository<CrossmatchSettings>(c));
        var screen = AntibodyScreen(c);
        var eligibility = new ElectronicCrossmatchEligibilityService(
            new EfRepository<Patient>(c),
            new EfRepository<PatientBloodTypeHistory>(c),
            new EfRepository<AntibodyHistory>(c),
            new EfRepository<AntibodyIdentificationWorkup>(c),
            screen,
            new FacilityPolicyService(new EfRepository<SystemSetting>(c)),
            settings,
            _factory.Clock);
        return new CrossmatchAttachmentService(
            settings,
            eligibility,
            screen,
            Compatibility(c),
            new InventoryRepository(c),
            new EfRepository<Order>(c),
            new EfRepository<OrderLine>(c),
            new EfRepository<OrderSpecimen>(c),
            new EfRepository<ProductType>(c),
            new EfRepository<TestDefinition>(c),
            new EfRepository<TestResult>(c),
            new EfRepository<Allocation>(c),
            new EfRepository<Crossmatch>(c),
            new EfRepository<Specimen>(c),
            _factory.Clock,
            _factory.CurrentUser,
            c,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            billing: Billing(c));
    }

    private BillingService Billing(BloodBankDbContext c) =>
        new(new EfRepository<BillingEvent>(c), new EfRepository<ChargeRule>(c), new EfRepository<ChargeCode>(c),
            new EfRepository<TestServiceBilling>(c), new EfRepository<ProductBilling>(c),
            new EfRepository<TestResult>(c), new EfRepository<Issue>(c), new EfRepository<BloodUnit>(c),
            new EfRepository<ProductType>(c), c, _factory.Clock, _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            new NoopBillingPublisher());

    private sealed class NoopBillingPublisher : IBillingInterfacePublisher
    {
        public Task<long?> PublishChargeAsync(BillingEvent billingEvent, CancellationToken ct = default) =>
            Task.FromResult<long?>(null);
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
            AntibodyScreen(c),
            c, _factory.Clock, _factory.CurrentUser,
            crossmatchSettings: new CrossmatchSettingsReader(new EfRepository<CrossmatchSettings>(c)));

    private static AntibodyScreenCompatLoader AntibodyScreen(BloodBankDbContext c) =>
        new(
            new EfRepository<TestResult>(c),
            new EfRepository<TestDefinition>(c),
            new EfRepository<AntibodyHistory>(c),
            specimens: new EfRepository<Specimen>(c),
            orders: new EfRepository<Order>(c));

    private async Task SeedCatalogAsync(BloodBankDbContext c)
    {
        await EnsureTestAsync(c, "XM", "Crossmatch", TestCategory.Crossmatch, ResultValueType.Crossmatch);
        await EnsureTestAsync(c, "CXM", "Complex Crossmatch", TestCategory.Crossmatch, ResultValueType.ComplexCrossmatch);
        await EnsureTestAsync(c, "EXM", "Electronic Crossmatch", TestCategory.Crossmatch, ResultValueType.Crossmatch);
        await EnsureTestAsync(c, "ABSC", "Antibody Screen", TestCategory.AntibodyScreen, ResultValueType.Coded);
        await EnsureTestAsync(c, "ABORH", "ABO/Rh", TestCategory.AboRh, ResultValueType.AboRh);
        if (!await c.ExceptionDefinitions.AnyAsync(e => e.RuleCode == AntibodyHistoryCrossmatchRule.RuleCode))
        {
            c.ExceptionDefinitions.Add(new ExceptionDefinition
            {
                RuleCode = AntibodyHistoryCrossmatchRule.RuleCode,
                Name = "Simple XM with Ab history",
                MinSecurityLevel = 2,
                IsOverridable = true,
                IsActive = true
            });
        }

        var settings = await c.CrossmatchSettings.OrderByDescending(s => s.Version).FirstOrDefaultAsync();
        if (settings is null)
        {
            c.CrossmatchSettings.Add(CrossmatchSettings.CreateDefault());
        }
        else
        {
            settings.NegativeAntibodyHistoryTestCode = CrossmatchSettings.DefaultNegativeTestCode;
            settings.PositiveAntibodyHistoryTestCode = CrossmatchSettings.DefaultPositiveTestCode;
            settings.ElectronicCrossmatchTestCode = CrossmatchSettings.DefaultElectronicTestCode;
            settings.ElectronicXmMinimumVisits = CrossmatchSettings.DefaultElectronicMinimum;
            settings.ElectronicXmMinimumSpecimens = CrossmatchSettings.DefaultElectronicMinimum;
            settings.ElectronicXmMinimumTests = CrossmatchSettings.DefaultElectronicMinimum;
            settings.IsActive = true;
            settings.IsDraft = false;
        }

        await c.SaveChangesAsync();
    }

    private async Task EnsureTestAsync(
        BloodBankDbContext c,
        string code,
        string name,
        TestCategory category,
        ResultValueType valueType)
    {
        if (await c.TestDefinitions.AnyAsync(t => t.Code == code))
        {
            return;
        }

        c.TestDefinitions.Add(new TestDefinition
        {
            Code = code,
            Name = name,
            Category = category,
            ResultValueType = valueType,
            IsActive = true,
            IsDraft = false,
            AllowedResultValues = category == TestCategory.AntibodyScreen
                ? "Negative\nPositive"
                : "Compatible\nIncompatible",
            EffectiveUtc = _factory.Clock.UtcNow,
            Version = 1
        });
    }

    private PatientAllocationService Allocations(BloodBankDbContext c) =>
        new(
            Compatibility(c),
            Orders(c),
            new InventoryRepository(c),
            new EfRepository<Allocation>(c),
            new EfRepository<Crossmatch>(c),
            new EfRepository<Patient>(c),
            new EfRepository<ProductType>(c),
            new EfRepository<PatientBloodTypeHistory>(c),
            new EfRepository<TestDefinition>(c),
            new EfRepository<Encounter>(c),
            new EfRepository<OrderingLocation>(c),
            new EfRepository<Specimen>(c),
            new EfRepository<ExceptionDefinition>(c),
            new EfRepository<Override>(c),
            new BloodAttributeCompatLoader(
                new EfRepository<AntibodyHistory>(c),
                new EfRepository<AntigenProfile>(c),
                new EfRepository<UnitBloodAttribute>(c),
                new EfRepository<BloodAttributeDefinition>(c)),
            AntibodyScreen(c),
            new FixedPermissionEvaluator(3),
            _factory.Clock,
            _factory.CurrentUser,
            c,
            Attachment(c),
            Eligibility(c),
            new EfRepository<OrderLine>(c),
            orderRepo: new EfRepository<Order>(c),
            orderSpecimens: new EfRepository<OrderSpecimen>(c));

    private ElectronicCrossmatchEligibilityService Eligibility(BloodBankDbContext c) =>
        new(
            new EfRepository<Patient>(c),
            new EfRepository<PatientBloodTypeHistory>(c),
            new EfRepository<AntibodyHistory>(c),
            new EfRepository<AntibodyIdentificationWorkup>(c),
            AntibodyScreen(c),
            new FacilityPolicyService(new EfRepository<SystemSetting>(c)),
            new CrossmatchSettingsReader(new EfRepository<CrossmatchSettings>(c)),
            _factory.Clock);

    private async Task<Seed> SeedProductAndScreenAsync(
        BloodBankDbContext c,
        bool positiveScreen,
        bool antibodyHistory,
        bool verifyScreen = false)
    {
        await SeedCatalogAsync(c);
        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-{Guid.NewGuid():N}",
            LastName = "Xm",
            FirstName = "Attach",
            DateOfBirth = new DateOnly(1980, 1, 1)
        };
        c.Patients.Add(patient);
        var location = new OrderingLocation { Code = $"LOC-{Guid.NewGuid():N}"[..12], Name = "BB", IsActive = true };
        c.OrderingLocations.Add(location);
        var product = new ProductType
        {
            ProductCode = $"RBC-{Guid.NewGuid():N}"[..12],
            Name = "RBC",
            ComponentClass = ComponentClass.RedBloodCells,
            RequiresCrossmatch = true
        };
        c.ProductTypes.Add(product);
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

        var tns = new Order
        {
            PatientId = patient.Id,
            EncounterId = encounter.Id,
            OrderingLocationId = location.Id,
            OrderNumber = $"TNS-{Guid.NewGuid():N}"[..16],
            OrderCategory = OrderCategory.Test,
            OrderName = "ABSC",
            Status = OrderStatus.New,
            OrderedUtc = _factory.Clock.UtcNow
        };
        var productOrder = new Order
        {
            PatientId = patient.Id,
            EncounterId = encounter.Id,
            OrderingLocationId = location.Id,
            OrderNumber = $"RBC-{Guid.NewGuid():N}"[..16],
            OrderCategory = OrderCategory.Product,
            OrderName = "RBC",
            ProductTypeId = product.Id,
            Status = OrderStatus.New,
            OrderedUtc = _factory.Clock.UtcNow
        };
        c.Orders.AddRange(tns, productOrder);
        await c.SaveChangesAsync();

        c.OrderLines.AddRange(
            new OrderLine
            {
                OrderId = tns.Id, LineNumber = 1, LineCategory = OrderCategory.Test,
                LineName = "Antibody Screen", TestCode = "ABSC", OrderType = OrderType.TypeAndScreen, IsActive = true
            },
            new OrderLine
            {
                OrderId = productOrder.Id, LineNumber = 1, LineCategory = OrderCategory.Product,
                LineName = "RBC", ProductTypeId = product.Id, IsActive = true
            });
        c.OrderSpecimens.AddRange(
            new OrderSpecimen { OrderId = tns.Id, SpecimenId = specimen.Id, IsPrimary = true },
            new OrderSpecimen { OrderId = productOrder.Id, SpecimenId = specimen.Id, IsPrimary = true });
        c.TestResults.Add(new TestResult
        {
            PatientId = patient.Id,
            SpecimenId = specimen.Id,
            OrderId = tns.Id,
            TestCode = "ABORH",
            Value = "O Pos",
            Status = ResultStatus.Verified,
            Source = ResultSource.Manual,
            EnteredBy = _factory.CurrentUser.UserName,
            EnteredUtc = _factory.Clock.UtcNow,
            VerifiedBy = "tech-verify",
            VerifiedUtc = _factory.Clock.UtcNow
        });

        if (antibodyHistory)
        {
            c.AntibodyHistory.Add(new AntibodyHistory
            {
                PatientId = patient.Id,
                AntibodySpecificity = "K",
                IsActive = true
            });
        }

        var screen = new TestResult
        {
            PatientId = patient.Id,
            SpecimenId = specimen.Id,
            OrderId = tns.Id,
            TestCode = "ABSC",
            Value = positiveScreen ? "Positive" : "Negative",
            Interpretation = positiveScreen ? "Positive" : "Negative",
            Status = ResultStatus.Entered,
            Source = ResultSource.Manual,
            EnteredBy = _factory.CurrentUser.UserName,
            EnteredUtc = _factory.Clock.UtcNow
        };
        c.TestResults.Add(screen);
        await c.SaveChangesAsync();

        if (verifyScreen)
        {
            screen.Status = ResultStatus.Verified;
            screen.VerifiedBy = "tech-verify";
            screen.VerifiedUtc = _factory.Clock.UtcNow;
            await c.SaveChangesAsync();
        }

        return new Seed(patient, encounter, location, product, productOrder, screen, specimen, null);
    }

    private async Task<Seed> SeedExmEligibleAsync(BloodBankDbContext c, int screens, bool withUnit = false)
    {
        var seed = await SeedProductAndScreenAsync(c, positiveScreen: false, antibodyHistory: false, verifyScreen: true);
        c.PatientBloodTypeHistory.AddRange(
            new PatientBloodTypeHistory
            {
                PatientId = seed.Patient.Id,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                Source = BloodTypeSource.TestResult,
                IsCurrent = false
            },
            new PatientBloodTypeHistory
            {
                PatientId = seed.Patient.Id,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                Source = BloodTypeSource.TestResult,
                IsCurrent = true
            });

        if (screens >= 2)
        {
            var priorVisit = new Encounter
            {
                PatientId = seed.Patient.Id,
                VisitNumber = $"VIS-{Guid.NewGuid():N}",
                EncounterType = EncounterType.Inpatient,
                Status = EncounterStatus.Discharged,
                AdmitUtc = _factory.Clock.UtcNow.AddDays(-10),
                DischargeUtc = _factory.Clock.UtcNow.AddDays(-8)
            };
            c.Encounters.Add(priorVisit);
            await c.SaveChangesAsync();

            var priorSpecimen = new Specimen
            {
                AccessionNumber = $"ACC-{Guid.NewGuid():N}",
                PatientId = seed.Patient.Id,
                EncounterId = priorVisit.Id,
                SpecimenType = "EDTA",
                CollectedUtc = _factory.Clock.UtcNow.AddDays(-9),
                ExpiresUtc = _factory.Clock.UtcNow.AddDays(-6),
                Status = SpecimenStatus.Accepted
            };
            c.Specimens.Add(priorSpecimen);
            await c.SaveChangesAsync();

            var priorOrder = new Order
            {
                PatientId = seed.Patient.Id,
                EncounterId = priorVisit.Id,
                OrderingLocationId = seed.Location.Id,
                OrderNumber = $"TNS2-{Guid.NewGuid():N}"[..16],
                OrderCategory = OrderCategory.Test,
                OrderName = "ABSC",
                Status = OrderStatus.Completed,
                OrderedUtc = _factory.Clock.UtcNow.AddDays(-9)
            };
            c.Orders.Add(priorOrder);
            await c.SaveChangesAsync();

            c.OrderLines.Add(new OrderLine
            {
                OrderId = priorOrder.Id, LineNumber = 1, LineCategory = OrderCategory.Test,
                LineName = "Antibody Screen", TestCode = "ABSC", OrderType = OrderType.TypeAndScreen, IsActive = true
            });
            c.TestResults.Add(new TestResult
            {
                PatientId = seed.Patient.Id,
                SpecimenId = priorSpecimen.Id,
                OrderId = priorOrder.Id,
                TestCode = "ABSC",
                Value = "Negative",
                Interpretation = "Negative",
                Status = ResultStatus.Verified,
                Source = ResultSource.Manual,
                EnteredBy = _factory.CurrentUser.UserName,
                EnteredUtc = _factory.Clock.UtcNow.AddDays(-9),
                VerifiedBy = "tech-verify",
                VerifiedUtc = _factory.Clock.UtcNow.AddDays(-9)
            });
        }

        BloodUnit? unit = null;
        if (withUnit)
        {
            unit = new BloodUnit
            {
                UnitNumber = $"U-{Guid.NewGuid():N}"[..16],
                ProductTypeId = seed.Product.Id,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                ExpiresUtc = _factory.Clock.UtcNow.AddDays(20),
                Status = UnitStatus.Available
            };
            c.BloodUnits.Add(unit);
        }

        var exm = await c.SystemSettings.FirstOrDefaultAsync(s => s.Key == FacilityPolicyKeys.AllowElectronicCrossmatch);
        if (exm is null)
        {
            c.SystemSettings.Add(new SystemSetting
            {
                Key = FacilityPolicyKeys.AllowElectronicCrossmatch,
                Value = "true",
                Category = "Issue"
            });
        }
        else
        {
            exm.Value = "true";
        }

        await c.SaveChangesAsync();
        return seed with { Unit = unit };
    }

    private sealed record Seed(
        Patient Patient,
        Encounter Encounter,
        OrderingLocation Location,
        ProductType Product,
        Order ProductOrder,
        TestResult ScreenResult,
        Specimen Specimen,
        BloodUnit? Unit);
}
