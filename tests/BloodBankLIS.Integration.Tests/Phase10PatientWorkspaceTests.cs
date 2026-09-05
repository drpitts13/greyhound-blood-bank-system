using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class Phase10PatientWorkspaceTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public Phase10PatientWorkspaceTests(SqliteContextFactory factory) => _factory = factory;

    private static EncounterService Encounters(BloodBankDbContext c)
    {
        var clock = new FixedClock(DateTime.UtcNow);
        var providers = new OrderingProviderService(new EfRepository<OrderingProvider>(c), c);
        return new(new EfRepository<Encounter>(c), new EfRepository<Patient>(c), new EfRepository<OrderingProvider>(c), providers, c, clock);
    }

    private OrderService Orders(BloodBankDbContext c) =>
        new(new EfRepository<Order>(c), new EfRepository<OrderLine>(c), new EfRepository<OrderSpecimen>(c),
            new EfRepository<Encounter>(c), new EfRepository<OrderingLocation>(c), new EfRepository<Patient>(c),
            new EfRepository<Specimen>(c), new EfRepository<OrderingProvider>(c), new EfRepository<ProductType>(c),
            new EfRepository<TestDefinition>(c), new EfRepository<TestGrouper>(c), new FixedClock(DateTime.UtcNow), c,
            ruleEngine: null,
            audit: new AuditWriter(c, _factory.Clock, _factory.CurrentUser));

    private static PatientProductHistoryService History(BloodBankDbContext c) =>
        new(new EfRepository<Allocation>(c), new EfRepository<Crossmatch>(c), new EfRepository<Issue>(c),
            new EfRepository<Return>(c), new EfRepository<TransfusionEvent>(c), new EfRepository<BloodUnit>(c),
            new EfRepository<ProductType>(c), new EfRepository<Encounter>(c), new EfRepository<Order>(c),
            new EfRepository<Specimen>(c), new EfRepository<PatientBloodTypeHistory>(c));

    private async Task<(Patient patient, OrderingLocation location)> SeedPatientAsync(BloodBankDbContext c)
    {
        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-{Guid.NewGuid():N}",
            LastName = "Test",
            FirstName = "Patient",
            DateOfBirth = new DateOnly(1990, 1, 1),
            Sex = Sex.Female
        };
        c.Patients.Add(patient);
        var loc = new OrderingLocation { Code = $"LOC-{Guid.NewGuid():N}", Name = "Test OR", IsActive = true };
        c.OrderingLocations.Add(loc);
        await c.SaveChangesAsync();
        return (patient, loc);
    }

    [Fact]
    public async Task CreateEncounter_AndOrder_WithSpecimen_Succeeds()
    {
        await using var c = _factory.Create();
        var (patient, loc) = await SeedPatientAsync(c);
        var enc = await Encounters(c).CreateAsync(patient.Id, new CreateEncounterRequest(
            "VIS-TEST", EncounterType.Inpatient, EncounterStatus.Active, DateTime.UtcNow, null,
            null, null, "4W", "4W", null, null, null, null));
        Assert.True(enc.Succeeded);

        var specimen = new Specimen
        {
            AccessionNumber = $"ACC-{Guid.NewGuid():N}",
            PatientId = patient.Id,
            EncounterId = enc.Value!.Id,
            SpecimenType = "EDTA",
            CollectedUtc = DateTime.UtcNow.AddHours(-1),
            ExpiresUtc = DateTime.UtcNow.AddDays(3),
            Status = SpecimenStatus.Accepted
        };
        c.Specimens.Add(specimen);
        await c.SaveChangesAsync();

        var provider = new OrderingProvider { ProviderId = "P1", Name = "Dr. Test", IsActive = true };
        c.OrderingProviders.Add(provider);
        await c.SaveChangesAsync();

        var order = await Orders(c).CreateAsync(patient.Id, new CreateOrderRequest(
            enc.Value.Id, loc.Id, $"ORD-{Guid.NewGuid():N}",
            [new OrderLineInputDto(OrderCategory.Test, "ABORH", null)],
            OrderPriority.Stat, DateTime.UtcNow, provider.Id, OrderSource.Manual, null, null));
        Assert.True(order.Succeeded);
        Assert.True(await c.AuditEvents.AnyAsync(a =>
            a.EventType == AuditEventType.OrderChange
            && a.EntityType == nameof(Order)
            && a.EntityId == order.Value!.Id
            && a.Reason == "Order created."));

        var list = await Orders(c).ListByPatientAsync(patient.Id);
        Assert.Single(list);
        Assert.Single(list[0].Lines);
        Assert.Equal("ABORH", list[0].Lines[0].TestCode);
        Assert.Equal(specimen.AccessionNumber, list[0].AccessionNumber);
    }

    [Fact]
    public async Task CreateOrder_WithoutEncounter_Fails()
    {
        await using var c = _factory.Create();
        var (patient, loc) = await SeedPatientAsync(c);

        var order = await Orders(c).CreateAsync(patient.Id, new CreateOrderRequest(
            0, loc.Id, $"ORD-{Guid.NewGuid():N}",
            [new OrderLineInputDto(OrderCategory.Test, "ABORH", null)],
            OrderPriority.Routine, DateTime.UtcNow, null, OrderSource.Manual, null, null));

        Assert.False(order.Succeeded);
        Assert.Contains("visit", order.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateOrder_WithMultipleTestsAndProducts_Succeeds()
    {
        await using var c = _factory.Create();
        var (patient, loc) = await SeedPatientAsync(c);
        var enc = await Encounters(c).CreateAsync(patient.Id, new CreateEncounterRequest(
            "VIS-MULTI", EncounterType.Inpatient, EncounterStatus.Active, DateTime.UtcNow, null,
            null, null, null, null, null, null, null, null));
        var rbc = new ProductType { ProductCode = "RBC-M", Name = "RBC", ComponentClass = ComponentClass.RedBloodCells, RequiresCrossmatch = false };
        c.ProductTypes.Add(rbc);
        await c.SaveChangesAsync();

        var order = await Orders(c).CreateAsync(patient.Id, new CreateOrderRequest(
            enc.Value!.Id, loc.Id, $"ORD-MULTI-{Guid.NewGuid():N}",
            [
                new OrderLineInputDto(OrderCategory.Test, "ABORH", null),
                new OrderLineInputDto(OrderCategory.Test, "ABSC", null),
                new OrderLineInputDto(OrderCategory.Product, null, rbc.Id)
            ],
            OrderPriority.Routine, DateTime.UtcNow, null, OrderSource.Manual, null, null));
        Assert.True(order.Succeeded);
        Assert.Equal(OrderCategory.Mixed, order.Value!.OrderCategory);

        var list = await Orders(c).ListByPatientAsync(patient.Id);
        Assert.Equal(3, list[0].Lines.Count);
    }

    [Fact]
    public async Task CreateOrder_WithTestGrouper_ExpandsMemberTests()
    {
        await using var c = _factory.Create();
        var (patient, loc) = await SeedPatientAsync(c);
        var enc = await Encounters(c).CreateAsync(patient.Id, new CreateEncounterRequest(
            "VIS-TNS", EncounterType.Inpatient, EncounterStatus.Active, DateTime.UtcNow, null,
            null, null, null, null, null, null, null, null));
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
            EffectiveUtc = DateTime.UtcNow,
            Version = 1
        });
        await c.SaveChangesAsync();

        var order = await Orders(c).CreateAsync(patient.Id, new CreateOrderRequest(
            enc.Value!.Id, loc.Id, $"ORD-TNS-{Guid.NewGuid():N}",
            [new OrderLineInputDto(OrderCategory.Test, "TNS", null)],
            OrderPriority.Routine, DateTime.UtcNow, null, OrderSource.Manual, null, null));
        Assert.True(order.Succeeded);

        var list = await Orders(c).ListByPatientAsync(patient.Id);
        var tests = list[0].Lines.Where(l => l.LineCategory == OrderCategory.Test).Select(l => l.TestCode).ToList();
        Assert.Equal(["ABORH", "ABSC"], tests);
    }

    [Fact]
    public async Task ProductHistory_IsPatientScoped()
    {
        await using var c = _factory.Create();
        var (p1, _) = await SeedPatientAsync(c);
        var (p2, _) = await SeedPatientAsync(c);
        var rbc = new ProductType { ProductCode = "RBC-T", Name = "RBC", ComponentClass = ComponentClass.RedBloodCells };
        c.ProductTypes.Add(rbc);
        await c.SaveChangesAsync();

        var unit = new BloodUnit
        {
            UnitNumber = $"U-{Guid.NewGuid():N}",
            ProductTypeId = rbc.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            ExpiresUtc = DateTime.UtcNow.AddDays(10),
            Status = UnitStatus.Issued
        };
        c.BloodUnits.Add(unit);
        await c.SaveChangesAsync();

        c.Issues.Add(new Issue
        {
            BloodProductId = unit.Id,
            PatientId = p1.Id,
            IssuedUtc = DateTime.UtcNow,
            IssuedBy = "tech",
            Status = IssueStatus.Issued
        });
        c.Issues.Add(new Issue
        {
            BloodProductId = unit.Id,
            PatientId = p2.Id,
            IssuedUtc = DateTime.UtcNow,
            IssuedBy = "tech",
            Status = IssueStatus.Issued
        });
        await c.SaveChangesAsync();

        var h1 = await History(c).ListByPatientAsync(p1.Id);
        var h2 = await History(c).ListByPatientAsync(p2.Id);
        Assert.Single(h1);
        Assert.Single(h2);
        Assert.Equal(PatientProductHistoryEventType.Issued, h1[0].EventType);
    }

    [Fact]
    public async Task UpdateOrder_ChangesEditableFields()
    {
        await using var c = _factory.Create();
        var (patient, loc) = await SeedPatientAsync(c);
        var enc = await Encounters(c).CreateAsync(patient.Id, new CreateEncounterRequest(
            "VIS-EDIT", EncounterType.Inpatient, EncounterStatus.Active, DateTime.UtcNow, null,
            null, null, null, null, null, null, null, null));
        Assert.True(enc.Succeeded);

        var order = await Orders(c).CreateAsync(patient.Id, new CreateOrderRequest(
            enc.Value!.Id, loc.Id, $"ORD-EDIT-{Guid.NewGuid():N}",
            [new OrderLineInputDto(OrderCategory.Test, "ABORH", null)],
            OrderPriority.Routine, DateTime.UtcNow, null, OrderSource.Manual, null, null));
        Assert.True(order.Succeeded);

        var updated = await Orders(c).UpdateAsync(patient.Id, order.Value!.Id, new UpdateOrderRequest(
            enc.Value.Id, loc.Id,
            [new OrderLineInputDto(OrderCategory.Test, "ABORH", null), new OrderLineInputDto(OrderCategory.Test, "ABSC", null)],
            OrderPriority.Stat, null));
        Assert.True(updated.Succeeded);
        Assert.Equal(OrderPriority.Stat, updated.Value!.Priority);

        var list = await Orders(c).ListByPatientAsync(patient.Id);
        Assert.Equal(2, list[0].Lines.Count);
    }

    [Fact]
    public async Task UpdateOrder_WhenCancelled_Fails()
    {
        await using var c = _factory.Create();
        var (patient, loc) = await SeedPatientAsync(c);
        var enc = await Encounters(c).CreateAsync(patient.Id, new CreateEncounterRequest(
            "VIS-CANCEL", EncounterType.Inpatient, EncounterStatus.Active, DateTime.UtcNow, null,
            null, null, null, null, null, null, null, null));
        var order = await Orders(c).CreateAsync(patient.Id, new CreateOrderRequest(
            enc.Value!.Id, loc.Id, $"ORD-CANCEL-{Guid.NewGuid():N}",
            [new OrderLineInputDto(OrderCategory.Test, "ABORH", null)],
            OrderPriority.Routine, DateTime.UtcNow, null, OrderSource.Manual, null, null));
        await Orders(c).CancelAsync(patient.Id, order.Value!.Id, new CancelOrderRequest("No longer needed"));

        var updated = await Orders(c).UpdateAsync(patient.Id, order.Value.Id, new UpdateOrderRequest(
            enc.Value.Id, loc.Id,
            [new OrderLineInputDto(OrderCategory.Test, "ABSC", null)],
            OrderPriority.Routine, null));
        Assert.False(updated.Succeeded);
        Assert.Contains("cannot be edited", updated.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProductOrder_RequiringCrossmatch_AutoAddsCrossmatchLine()
    {
        await using var c = _factory.Create();
        var (patient, loc) = await SeedPatientAsync(c);
        var enc = await Encounters(c).CreateAsync(patient.Id, new CreateEncounterRequest(
            "VIS-XM", EncounterType.Inpatient, EncounterStatus.Active, DateTime.UtcNow, null,
            null, null, null, null, null, null, null, null));
        var rbc = new ProductType
        {
            ProductCode = "RBC-XM",
            Name = "RBC",
            ComponentClass = ComponentClass.RedBloodCells,
            RequiresCrossmatch = true
        };
        var ffp = new ProductType
        {
            ProductCode = "FFP-NOXM",
            Name = "FFP",
            ComponentClass = ComponentClass.Plasma,
            RequiresCrossmatch = false
        };
        c.ProductTypes.AddRange(rbc, ffp);
        await c.SaveChangesAsync();

        var productOrder = await Orders(c).CreateAsync(patient.Id, new CreateOrderRequest(
            enc.Value!.Id, loc.Id, $"ORD-RBC-{Guid.NewGuid():N}",
            [new OrderLineInputDto(OrderCategory.Product, null, rbc.Id)],
            OrderPriority.Routine, DateTime.UtcNow, null, OrderSource.Manual, null, null));
        Assert.True(productOrder.Succeeded);

        var list = await Orders(c).ListByPatientAsync(patient.Id);
        Assert.Single(list);
        Assert.Equal(2, list[0].Lines.Count);
        Assert.Contains(list[0].Lines, l => l.TestCode == "XM");

        var ffpOrder = await Orders(c).CreateAsync(patient.Id, new CreateOrderRequest(
            enc.Value.Id, loc.Id, $"ORD-FFP-{Guid.NewGuid():N}",
            [new OrderLineInputDto(OrderCategory.Product, null, ffp.Id)],
            OrderPriority.Routine, DateTime.UtcNow, null, OrderSource.Manual, null, null));
        Assert.True(ffpOrder.Succeeded);

        list = await Orders(c).ListByPatientAsync(patient.Id);
        Assert.Equal(2, list.Count);
        Assert.Single(list.First(o => o.OrderNumber.StartsWith("ORD-FFP", StringComparison.Ordinal)).Lines);
    }

    [Fact]
    public async Task OrderCreate_ProducesAuditEvent()
    {
        await using var c = _factory.Create();
        var (patient, loc) = await SeedPatientAsync(c);
        var enc = await Encounters(c).CreateAsync(patient.Id, new CreateEncounterRequest(
            "VIS-AUDIT", EncounterType.Outpatient, EncounterStatus.Active, DateTime.UtcNow, null,
            null, null, null, null, null, null, null, null));
        var rbc = new ProductType { ProductCode = "RBC-A", Name = "RBC", ComponentClass = ComponentClass.RedBloodCells };
        c.ProductTypes.Add(rbc);
        await c.SaveChangesAsync();
        var beforeOrders = await c.AuditEvents.CountAsync(e => e.EntityType == nameof(Order));

        var order = await Orders(c).CreateAsync(patient.Id, new CreateOrderRequest(
            enc.Value!.Id, loc.Id, $"ORD-AUDIT-{Guid.NewGuid():N}",
            [new OrderLineInputDto(OrderCategory.Product, null, rbc.Id)],
            OrderPriority.Routine, DateTime.UtcNow, null, OrderSource.Manual, null, null));
        Assert.True(order.Succeeded);

        var afterOrders = await c.AuditEvents.CountAsync(e => e.EntityType == nameof(Order));
        Assert.True(afterOrders > beforeOrders);
    }
}
