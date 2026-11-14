using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Application.Billing;
using BloodBankLIS.Application.Compatibility;
using BloodBankLIS.Application.Inventory;
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

public class AutoThawProductAdminServiceTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public AutoThawProductAdminServiceTests(SqliteContextFactory factory) => _factory = factory;

    private AutoThawProductAdminService Admin(BloodBankDbContext c, IPermissionEvaluator? permissions = null)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new AutoThawProductAdminService(
            new EfRepository<AutoThawProduct>(c),
            new EfRepository<IsbtProductCode>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser, env),
            new ConfigurationHistoryWriter(c, _factory.Clock, _factory.CurrentUser, env),
            permissions);
    }

    [Fact]
    public async Task Create_AddsIsbtProductCode()
    {
        await using var c = _factory.Create();
        await SeedCatalogAsync(c, "E0707", "FRESH FROZEN PLASMA|CPDA-1/XX/<=-18C");
        var created = await Admin(c).CreateAsync(new SaveAutoThawProductRequest("E0707"));
        Assert.True(created.Succeeded, created.Error);
        Assert.Equal("E0707", created.Value!.ProductCode);
        Assert.Contains("FRESH FROZEN PLASMA", created.Value.Description);
        Assert.True(created.Value.IsActive);
    }

    [Fact]
    public async Task Create_UnknownCode_IsBlocked()
    {
        await using var c = _factory.Create();
        var created = await Admin(c).CreateAsync(new SaveAutoThawProductRequest("ZZZZZ"));
        Assert.False(created.Succeeded);
        Assert.Contains(created.Evaluation!.HardStops, r => r.Code == "AUTOTHAW.PRODUCT.MISSING");
    }

    [Fact]
    public async Task Create_DuplicateCode_IsBlocked()
    {
        await using var c = _factory.Create();
        var code = await SeedCatalogAsync(c, "E0869", "FRESH FROZEN PLASMA|APHERESIS/XX/<=-18C");
        var admin = Admin(c);
        var first = await admin.CreateAsync(new SaveAutoThawProductRequest(code));
        Assert.True(first.Succeeded, first.Error);

        var second = await admin.CreateAsync(new SaveAutoThawProductRequest(code));
        Assert.False(second.Succeeded);
        Assert.Contains(second.Evaluation!.HardStops, r => r.Code == "AUTOTHAW.PRODUCT.DUPLICATE");
        Assert.Equal(1, await c.AutoThawProducts.CountAsync(r => r.ProductCode == code));
    }

    private static async Task<string> SeedCatalogAsync(BloodBankDbContext c, string code, string description)
    {
        if (!await c.IsbtProductCodes.AnyAsync(p => p.ProductDescriptionCode == code))
        {
            c.IsbtProductCodes.Add(new IsbtProductCode
            {
                ProductDescriptionCode = code,
                Description = description,
                ComponentClass = nameof(ComponentClass.Plasma),
                AttributesJson = "[]",
                StandardVersion = "TEST",
                IsPlaceholder = true
            });
            await c.SaveChangesAsync();
        }

        return code;
    }
}

public class AutoThawServiceTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public AutoThawServiceTests(SqliteContextFactory factory) => _factory = factory;

    private AutoThawProductAdminService Admin(BloodBankDbContext c)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new AutoThawProductAdminService(
            new EfRepository<AutoThawProduct>(c),
            new EfRepository<IsbtProductCode>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser, env),
            new ConfigurationHistoryWriter(c, _factory.Clock, _factory.CurrentUser, env));
    }

    private BillingService Billing(BloodBankDbContext c, CapturingPublisher publisher) =>
        new(new EfRepository<BillingEvent>(c), new EfRepository<ChargeRule>(c), new EfRepository<ChargeCode>(c),
            new EfRepository<TestServiceBilling>(c), new EfRepository<ProductBilling>(c),
            new EfRepository<TestResult>(c), new EfRepository<Issue>(c), new EfRepository<BloodUnit>(c),
            new EfRepository<ProductType>(c), c, _factory.Clock, _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            publisher,
            actionBillings: new EfRepository<ActionBilling>(c),
            orderActions: new EfRepository<OrderAction>(c),
            actionDefinitions: new EfRepository<ActionDefinition>(c),
            orders: new EfRepository<Order>(c));

    private AutoThawService Thaw(BloodBankDbContext c, CapturingPublisher? publisher = null) =>
        new(new EfRepository<AutoThawProduct>(c),
            new EfRepository<AutoThawRequest>(c),
            new EfRepository<Allocation>(c),
            new EfRepository<BloodUnit>(c),
            new EfRepository<ProductType>(c),
            new EfRepository<OrderAction>(c),
            new EfRepository<ActionDefinition>(c),
            new EfRepository<ActionBilling>(c),
            Billing(c, publisher ?? new CapturingPublisher()),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            modifications: new EfRepository<UnitModification>(c));

    private PatientAllocationService Allocations(BloodBankDbContext c) =>
        new(Compatibility(c),
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
            BloodAttributes(c),
            AntibodyScreen(c),
            new FixedPermissionEvaluator(1),
            _factory.Clock,
            _factory.CurrentUser,
            c,
            orderRepo: new EfRepository<Order>(c),
            orderLines: new EfRepository<OrderLine>(c),
            autoThaw: Thaw(c));

    private CompatibilityService Compatibility(BloodBankDbContext c) =>
        new(new InventoryRepository(c),
            new EfRepository<Crossmatch>(c),
            new EfRepository<Allocation>(c),
            new EfRepository<Patient>(c),
            new EfRepository<Specimen>(c),
            new EfRepository<ProductType>(c),
            new EfRepository<PatientBloodTypeHistory>(c),
            BloodAttributes(c),
            AntibodyScreen(c),
            c,
            _factory.Clock,
            _factory.CurrentUser);

    private OrderService Orders(BloodBankDbContext c) =>
        new(new EfRepository<Order>(c), new EfRepository<OrderLine>(c), new EfRepository<OrderSpecimen>(c),
            new EfRepository<Encounter>(c), new EfRepository<OrderingLocation>(c), new EfRepository<Patient>(c),
            new EfRepository<Specimen>(c), new EfRepository<OrderingProvider>(c), new EfRepository<ProductType>(c),
            new EfRepository<TestDefinition>(c), new EfRepository<TestGrouper>(c), _factory.Clock, c);

    private static BloodAttributeCompatLoader BloodAttributes(BloodBankDbContext c) =>
        new(new EfRepository<AntibodyHistory>(c),
            new EfRepository<AntigenProfile>(c),
            new EfRepository<UnitBloodAttribute>(c),
            new EfRepository<BloodAttributeDefinition>(c));

    private static AntibodyScreenCompatLoader AntibodyScreen(BloodBankDbContext c) =>
        new(new EfRepository<TestResult>(c),
            new EfRepository<TestDefinition>(c),
            new EfRepository<AntibodyHistory>(c));

    [Fact]
    public async Task EligibleAllocate_FlagsThePrompt_AndIneligibleDoesNot()
    {
        await using var c = _factory.Create();
        var eligible = await SeedReservedScenarioAsync(c, listed: true, alreadyAllocated: false);
        var flagged = await Allocations(c).AllocateAsync(eligible.PatientId, new AllocatePatientUnitRequest(
            eligible.UnitId, OrderId: eligible.OrderId));
        Assert.True(flagged.Succeeded, flagged.Error);
        Assert.True(flagged.Value!.AutoThawEligible);

        var plain = await SeedReservedScenarioAsync(c, listed: false, alreadyAllocated: false);
        var quiet = await Allocations(c).AllocateAsync(plain.PatientId, new AllocatePatientUnitRequest(
            plain.UnitId, OrderId: plain.OrderId));
        Assert.True(quiet.Succeeded, quiet.Error);
        Assert.False(quiet.Value!.AutoThawEligible);

        var otherCode = await SeedReservedScenarioAsync(c, listed: true, alreadyAllocated: false, unitCode: "E0336");
        var mismatch = await Allocations(c).AllocateAsync(otherCode.PatientId, new AllocatePatientUnitRequest(
            otherCode.UnitId, OrderId: otherCode.OrderId));
        Assert.True(mismatch.Succeeded, mismatch.Error);
        Assert.False(mismatch.Value!.AutoThawEligible);

        var refused = await Thaw(c).ApplyAsync(plain.PatientId, quiet.Value.Allocation.AllocationId, _factory.Clock.UtcNow);
        Assert.False(refused.Succeeded);
    }

    [Fact]
    public async Task Yes_UpdatesExpirationOnly_AndSendsThawBilling()
    {
        await using var c = _factory.Create();
        var seeded = await SeedReservedScenarioAsync(c, listed: true, alreadyAllocated: true);
        var publisher = new CapturingPublisher();
        var thawUtc = _factory.Clock.UtcNow.AddHours(-2);
        var beforeCount = await c.BloodUnits.CountAsync();
        var before = await c.BloodUnits.SingleAsync(u => u.Id == seeded.UnitId);
        var modificationsBefore = await c.UnitModifications.CountAsync();

        var result = await Thaw(c, publisher).ApplyAsync(seeded.PatientId, seeded.AllocationId, thawUtc);

        Assert.True(result.Succeeded, result.Error);
        var after = await c.BloodUnits.SingleAsync(u => u.Id == seeded.UnitId);
        Assert.Equal(beforeCount, await c.BloodUnits.CountAsync());
        Assert.Equal(before.ProductTypeId, after.ProductTypeId);
        Assert.Equal(before.UnitNumber, after.UnitNumber);
        Assert.Equal(thawUtc.AddHours(24), after.ExpiresUtc);
        Assert.Equal(1, await c.BillingEvents.CountAsync(e => e.BillingCode == "BB-THAW" && e.PatientId == seeded.PatientId));
        Assert.Single(publisher.Published);
        Assert.Equal(modificationsBefore, await c.UnitModifications.CountAsync());
    }

    [Fact]
    public async Task Thaw_UsesTheCodesShelfLife_AndShelfLifeChangesNeedAReason()
    {
        // TEST-BB-565
        await using var c = _factory.Create();
        var seeded = await SeedReservedScenarioAsync(c, listed: true, alreadyAllocated: true);
        var row = await c.AutoThawProducts.SingleAsync(p => p.ProductCode == seeded.ProductCode);

        var noReason = await Admin(c).SetShelfLifeAsync(row.Id, new SetAutoThawShelfLifeRequest(6, " "));
        Assert.False(noReason.Succeeded);
        var tooLong = await Admin(c).SetShelfLifeAsync(row.Id, new SetAutoThawShelfLifeRequest(1000, "SOP"));
        Assert.False(tooLong.Succeeded);
        var changed = await Admin(c).SetShelfLifeAsync(row.Id, new SetAutoThawShelfLifeRequest(6, "Cryo SOP rev 4"));
        Assert.True(changed.Succeeded, changed.Error);
        Assert.Equal(6, changed.Value!.ThawedShelfLifeHours);
        Assert.True(await c.AuditEvents.AnyAsync(a =>
            a.EntityType == nameof(AutoThawProduct) && a.EntityId == row.Id && a.Reason == "Cryo SOP rev 4"));

        var rows = await Allocations(c).ListActiveAsync(seeded.PatientId);
        Assert.Equal(6, rows.Single(r => r.AllocationId == seeded.AllocationId).AutoThawShelfLifeHours);

        var thawUtc = _factory.Clock.UtcNow.AddHours(-1);
        var result = await Thaw(c).ApplyAsync(seeded.PatientId, seeded.AllocationId, thawUtc);
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(thawUtc.AddHours(6), (await c.BloodUnits.SingleAsync(u => u.Id == seeded.UnitId)).ExpiresUtc);
    }

    // TEST-BB-858 (OCD-068, decided 2026-11-10): open-system and pooled shelf lives are set with the closed one, with a
    // reason, blank meaning not set; out of range is refused. An open-system thaw is refused until its shelf life is set,
    // then uses it, and the thaw records what applied. A pool (the result of a pooling modification) is recognized
    // without asking: refused until the pooled shelf life is set, then the shorter of the two when thawed open.
    [Fact]
    public async Task Thaw_OpenSystemAndPooledShelfLives()
    {
        await using var c = _factory.Create();
        var seeded = await SeedReservedScenarioAsync(c, listed: true, alreadyAllocated: true);
        var row = await c.AutoThawProducts.SingleAsync(p => p.ProductCode == seeded.ProductCode);
        var thawUtc = _factory.Clock.UtcNow.AddHours(-1);

        var notSet = await Thaw(c).ApplyAsync(seeded.PatientId, seeded.AllocationId, thawUtc, openSystem: true);
        Assert.False(notSet.Succeeded);
        Assert.Contains("No open-system thawed shelf life is set", notSet.Error);
        Assert.False(await c.AutoThawRequests.AnyAsync(r => r.AllocationId == seeded.AllocationId && r.Status == AutoThawRequestStatus.Completed));

        Assert.False((await Admin(c).SetShelfLifeAsync(row.Id, new SetAutoThawShelfLifeRequest(24, "SOP", OpenSystemShelfLifeHours: 0))).Succeeded);
        var set = await Admin(c).SetShelfLifeAsync(row.Id, new SetAutoThawShelfLifeRequest(24, "Plasma SOP rev 5", OpenSystemShelfLifeHours: 4, PooledShelfLifeHours: 6));
        Assert.True(set.Succeeded, set.Error);
        Assert.Equal((4, 6), (set.Value!.OpenSystemShelfLifeHours, set.Value.PooledShelfLifeHours));

        var thawed = await Thaw(c).ApplyAsync(seeded.PatientId, seeded.AllocationId, thawUtc, openSystem: true);
        Assert.True(thawed.Succeeded, thawed.Error);
        Assert.Equal(thawUtc.AddHours(4), (await c.BloodUnits.SingleAsync(u => u.Id == seeded.UnitId)).ExpiresUtc);
        var recorded = await c.AutoThawRequests.SingleAsync(r => r.AllocationId == seeded.AllocationId);
        Assert.Equal((true, false, (int?)4), (recorded.OpenSystem, recorded.Pooled, recorded.ShelfLifeHoursApplied));

        // A pooled unit of another listed code.
        var pool = await SeedReservedScenarioAsync(c, listed: true, alreadyAllocated: true);
        var poolRow = await c.AutoThawProducts.SingleAsync(p => p.ProductCode == pool.ProductCode);
        var poolUnit = await c.BloodUnits.SingleAsync(u => u.Id == pool.UnitId);
        var expiration = new ExpirationModificationCode
        {
            Code = $"POOL-{pool.UnitId}", OffsetAmount = 4, OffsetUnit = BloodBankLIS.Domain.ValueObjects.ExpirationOffsetUnit.Hours,
            RelativeTo = ExpirationRelativeTo.ModificationDateTime, IsActive = true
        };
        c.ExpirationModificationCodes.Add(expiration);
        await c.SaveChangesAsync();
        var rule = new ModificationRule
        {
            ModificationCode = $"POOL-{pool.UnitId}", SourceProductTypeId = poolUnit.ProductTypeId, TargetProductTypeId = poolUnit.ProductTypeId,
            ModificationType = ModificationType.Pool, ExpirationModificationCodeId = expiration.Id, IsActive = true
        };
        c.ModificationRules.Add(rule);
        await c.SaveChangesAsync();
        var pooling = new UnitModification
        {
            ModificationRuleId = rule.Id, ModificationType = ModificationType.Pool, ExpirationOffsetCodeApplied = "4H",
            ResultExpiresUtc = poolUnit.ExpiresUtc, Reason = "Pooled", PerformedBy = "tech", PerformedUtc = thawUtc.AddHours(-2)
        };
        c.UnitModifications.Add(pooling);
        await c.SaveChangesAsync();
        poolUnit.DerivedFromModificationId = pooling.Id;
        await c.SaveChangesAsync();

        // TEST-BB-903: the allocation row says the unit is a pool and carries the code's times, so the prompt refuses
        // before Yes and, once set, states the time that will apply.
        var poolListed = (await Allocations(c).ListActiveAsync(pool.PatientId)).Single(r => r.AllocationId == pool.AllocationId);
        Assert.True(poolListed.AutoThawPooled);
        Assert.Null(poolListed.AutoThawPooledShelfLifeHours);
        Assert.Contains("No pooled thawed shelf life is set", AutoThawPreview.For(poolListed, openSystem: false).Refusal);

        var poolNotSet = await Thaw(c).ApplyAsync(pool.PatientId, pool.AllocationId, thawUtc);
        Assert.Contains("No pooled thawed shelf life is set", poolNotSet.Error);
        Assert.True((await Admin(c).SetShelfLifeAsync(poolRow.Id, new SetAutoThawShelfLifeRequest(24, "Pool SOP", OpenSystemShelfLifeHours: 4, PooledShelfLifeHours: 6))).Succeeded);
        var poolSet = (await Allocations(c).ListActiveAsync(pool.PatientId)).Single(r => r.AllocationId == pool.AllocationId);
        Assert.Equal(((int?)6, (string?)null), AutoThawPreview.For(poolSet, openSystem: false));
        Assert.Equal(((int?)4, (string?)null), AutoThawPreview.For(poolSet, openSystem: true));
        var poolThawed = await Thaw(c).ApplyAsync(pool.PatientId, pool.AllocationId, thawUtc, openSystem: true);
        Assert.True(poolThawed.Succeeded, poolThawed.Error);
        Assert.Equal(thawUtc.AddHours(4), (await c.BloodUnits.SingleAsync(u => u.Id == pool.UnitId)).ExpiresUtc);
        Assert.True((await c.AutoThawRequests.SingleAsync(r => r.AllocationId == pool.AllocationId)).Pooled);
    }

    [Fact]
    public async Task Thaw_NeverExtendsPastTheFrozenExpiration()
    {
        // TEST-BB-565
        await using var c = _factory.Create();
        var seeded = await SeedReservedScenarioAsync(c, listed: true, alreadyAllocated: true);
        var unit = await c.BloodUnits.SingleAsync(u => u.Id == seeded.UnitId);
        var frozenExpires = _factory.Clock.UtcNow.AddHours(5);
        unit.ExpiresUtc = frozenExpires;
        await c.SaveChangesAsync();

        var result = await Thaw(c).ApplyAsync(seeded.PatientId, seeded.AllocationId, _factory.Clock.UtcNow.AddMinutes(-10));
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(frozenExpires, (await c.BloodUnits.SingleAsync(u => u.Id == seeded.UnitId)).ExpiresUtc);
    }

    [Fact]
    public async Task No_LeavesExpiration_AndLaterActionMatchesYes()
    {
        await using var c = _factory.Create();
        var seeded = await SeedReservedScenarioAsync(c, listed: true, alreadyAllocated: true);
        var original = (await c.BloodUnits.SingleAsync(u => u.Id == seeded.UnitId)).ExpiresUtc;
        var publisher = new CapturingPublisher();
        var service = Thaw(c, publisher);

        var declined = await service.DeclineAsync(seeded.PatientId, seeded.AllocationId);
        Assert.True(declined.Succeeded, declined.Error);
        Assert.Equal(original, (await c.BloodUnits.SingleAsync(u => u.Id == seeded.UnitId)).ExpiresUtc);

        var rows = await Allocations(c).ListActiveAsync(seeded.PatientId);
        Assert.Contains(rows, r => r.AllocationId == seeded.AllocationId && r.PendingAutoThaw);

        var thawUtc = _factory.Clock.UtcNow.AddMinutes(-30);
        var applied = await service.ApplyAsync(seeded.PatientId, seeded.AllocationId, thawUtc);
        Assert.True(applied.Succeeded, applied.Error);
        Assert.Equal(thawUtc.AddHours(24), (await c.BloodUnits.SingleAsync(u => u.Id == seeded.UnitId)).ExpiresUtc);
        Assert.Equal(seeded.ProductCode, applied.Value!.ProductCode);
        Assert.Equal(5, applied.Value.ProductCode.Length);
        Assert.Equal(1, await c.BillingEvents.CountAsync(e => e.BillingCode == "BB-THAW" && e.PatientId == seeded.PatientId));
        Assert.Single(publisher.Published);

        var after = await Allocations(c).ListActiveAsync(seeded.PatientId);
        Assert.Contains(after, r => r.AllocationId == seeded.AllocationId && !r.PendingAutoThaw);
    }

    [Fact]
    public async Task DivisionSuffix_MatchesTheListedFiveCharacterCode()
    {
        await using var c = _factory.Create();
        var seeded = await SeedReservedScenarioAsync(
            c, listed: true, alreadyAllocated: false, unitCode: null, codeDataOnly: true);
        var flagged = await Allocations(c).AllocateAsync(seeded.PatientId, new AllocatePatientUnitRequest(
            seeded.UnitId, OrderId: seeded.OrderId));
        Assert.True(flagged.Succeeded, flagged.Error);
        Assert.True(flagged.Value!.AutoThawEligible);
    }

    [Fact]
    public async Task ProductTypeIsbtCode_OffersThawAfterUnitIsAssigned()
    {
        await using var c = _factory.Create();
        var seeded = await SeedReservedScenarioAsync(
            c, listed: true, alreadyAllocated: false, productTypeCodeOnly: true);
        var flagged = await Allocations(c).AllocateAsync(seeded.PatientId, new AllocatePatientUnitRequest(
            seeded.UnitId, OrderId: seeded.OrderId));
        Assert.True(flagged.Succeeded, flagged.Error);
        Assert.True(flagged.Value!.AutoThawEligible);
        Assert.Equal(seeded.ProductCode, flagged.Value.Allocation.AutoThawProductCode);

        var unit = await c.BloodUnits.SingleAsync(u => u.Id == seeded.UnitId);
        Assert.Equal(UnitStatus.Assigned, unit.Status);
        Assert.Null(unit.ProductDescriptionCode);

        var rows = await Allocations(c).ListActiveAsync(seeded.PatientId);
        Assert.Contains(rows, r => r.AllocationId == flagged.Value.Allocation.AllocationId
            && r.AutoThawProductCode == seeded.ProductCode
            && r.DisplayStatus == ProductAllocationDisplayStatus.ReadyForIssue);

        var applied = await Thaw(c).ApplyAsync(
            seeded.PatientId, flagged.Value.Allocation.AllocationId, _factory.Clock.UtcNow.AddMinutes(-5));
        Assert.True(applied.Succeeded, applied.Error);
        Assert.Equal(seeded.ProductCode, applied.Value!.ProductCode);
    }

    private async Task<SeededPlasma> SeedReservedScenarioAsync(
        BloodBankDbContext c,
        bool listed,
        bool alreadyAllocated,
        string? unitCode = null,
        bool codeDataOnly = false,
        bool productTypeCodeOnly = false)
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var productCode = ("E" + token[..4]).ToUpperInvariant();
        var product = new ProductType
        {
            ProductCode = $"FFP-{token}",
            Name = "Fresh Frozen Plasma",
            ComponentClass = ComponentClass.Plasma,
            RequiresCrossmatch = false,
            Isbt128ProductCode = productTypeCodeOnly ? productCode : null,
            IsActive = true
        };
        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-{token}",
            LastName = "Plasma",
            FirstName = "Pat",
            DateOfBirth = new DateOnly(1980, 4, 2),
            Sex = Sex.Female
        };
        var location = new OrderingLocation { Code = $"LOC-{token}", Name = "OR", IsActive = true };
        c.ProductTypes.Add(product);
        c.Patients.Add(patient);
        c.OrderingLocations.Add(location);
        await c.SaveChangesAsync();

        c.PatientBloodTypeHistory.Add(new PatientBloodTypeHistory
        {
            PatientId = patient.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            IsCurrent = true
        });
        c.Encounters.Add(new Encounter
        {
            PatientId = patient.Id,
            VisitNumber = $"VIS-{token}",
            EncounterType = EncounterType.Inpatient,
            Status = EncounterStatus.Active,
            AdmitUtc = _factory.Clock.UtcNow.AddHours(-3)
        });
        await c.SaveChangesAsync();
        var encounter = await c.Encounters.SingleAsync(e => e.PatientId == patient.Id);

        if (listed)
        {
            c.AutoThawProducts.Add(new AutoThawProduct { ProductCode = productCode, IsActive = true });
        }

        await EnsureThawBillingAsync(c);

        var order = new Order
        {
            OrderNumber = $"ORD-{token}",
            PatientId = patient.Id,
            EncounterId = encounter.Id,
            OrderingLocationId = location.Id,
            OrderCategory = OrderCategory.Product,
            OrderName = "Fresh Frozen Plasma",
            OrderType = OrderType.Other,
            ProductTypeId = product.Id,
            Status = OrderStatus.InProcess,
            OrderedUtc = _factory.Clock.UtcNow.AddHours(-1)
        };
        var stamped = unitCode ?? productCode;
        var unit = new BloodUnit
        {
            UnitNumber = $"F{token}",
            ProductTypeId = product.Id,
            ProductDescriptionCode = productTypeCodeOnly || codeDataOnly ? null : stamped,
            ProductCodeData = !productTypeCodeOnly && codeDataOnly ? productCode + "V00" : null,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            Status = alreadyAllocated ? UnitStatus.Assigned : UnitStatus.Available,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(200)
        };
        c.Orders.Add(order);
        c.BloodUnits.Add(unit);
        await c.SaveChangesAsync();

        c.OrderLines.Add(new OrderLine
        {
            OrderId = order.Id,
            LineNumber = 1,
            LineCategory = OrderCategory.Product,
            LineName = "Fresh Frozen Plasma",
            ProductTypeId = product.Id,
            OrderType = OrderType.Other
        });

        long allocationId = 0;
        if (alreadyAllocated)
        {
            var allocation = new Allocation
            {
                BloodProductId = unit.Id,
                PatientId = patient.Id,
                EncounterId = encounter.Id,
                OrderId = order.Id,
                Status = AllocationStatus.Reserved,
                AllocatedUtc = _factory.Clock.UtcNow,
                AllocatedBy = "tech-test"
            };
            c.Allocations.Add(allocation);
            await c.SaveChangesAsync();
            allocationId = allocation.Id;
        }
        else
        {
            await c.SaveChangesAsync();
        }

        return new SeededPlasma(patient.Id, unit.Id, order.Id, allocationId, productCode);
    }

    private async Task EnsureThawBillingAsync(BloodBankDbContext c)
    {
        var action = await c.ActionDefinitions.FirstOrDefaultAsync(a => a.Code == "THAW");
        if (action is null)
        {
            action = new ActionDefinition
            {
                Code = "THAW",
                Name = "Thaw",
                Category = ActionCategory.Modification,
                ModificationType = ModificationType.Thaw,
                Description = "Thaw an allocated frozen product.",
                IsActive = true
            };
            c.ActionDefinitions.Add(action);
            await c.SaveChangesAsync();
        }

        var charge = await c.ChargeCodes.FirstOrDefaultAsync(ch => ch.Code == "BB-THAW");
        if (charge is null)
        {
            charge = new ChargeCode
            {
                Code = "BB-THAW",
                Description = "Thawing of plasma or cryoprecipitate",
                DefaultAmount = 45m,
                CptCode = "86927",
                RevenueCode = "0301",
                IsActive = true
            };
            c.ChargeCodes.Add(charge);
            await c.SaveChangesAsync();
        }

        if (!await c.ActionBillings.AnyAsync(b => b.ActionDefinitionId == action.Id && b.ChargeCodeId == charge.Id))
        {
            c.ActionBillings.Add(new ActionBilling
            {
                ActionDefinitionId = action.Id,
                ChargeCodeId = charge.Id,
                Description = "Thaw an allocated frozen product.",
                Trigger = BillingTriggerType.ActionCompleted,
                IsActive = true
            });
            await c.SaveChangesAsync();
        }
    }

    private sealed record SeededPlasma(long PatientId, long UnitId, long OrderId, long AllocationId, string ProductCode);
}
