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
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser));

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

        var result = await Thaw(c, publisher).ApplyAsync(seeded.PatientId, seeded.AllocationId, thawUtc);

        Assert.True(result.Succeeded, result.Error);
        var after = await c.BloodUnits.SingleAsync(u => u.Id == seeded.UnitId);
        Assert.Equal(beforeCount, await c.BloodUnits.CountAsync());
        Assert.Equal(before.ProductTypeId, after.ProductTypeId);
        Assert.Equal(before.UnitNumber, after.UnitNumber);
        Assert.Equal(thawUtc.AddHours(24), after.ExpiresUtc);
        Assert.Equal(1, await c.BillingEvents.CountAsync(e => e.BillingCode == "BB-THAW" && e.PatientId == seeded.PatientId));
        Assert.Single(publisher.Published);
        Assert.Equal(0, await c.UnitModifications.CountAsync());
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

    private async Task<SeededPlasma> SeedReservedScenarioAsync(
        BloodBankDbContext c,
        bool listed,
        bool alreadyAllocated,
        string? unitCode = null,
        bool codeDataOnly = false)
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var productCode = ("E" + token[..4]).ToUpperInvariant();
        var product = new ProductType
        {
            ProductCode = $"FFP-{token}",
            Name = "Fresh Frozen Plasma",
            ComponentClass = ComponentClass.Plasma,
            RequiresCrossmatch = false,
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
            ProductDescriptionCode = codeDataOnly ? null : stamped,
            ProductCodeData = codeDataOnly ? productCode + "V00" : null,
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
