using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class OrderServiceAuthorizationTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public OrderServiceAuthorizationTests(SqliteContextFactory factory) => _factory = factory;

    private OrderService Orders(BloodBankDbContext c, IPermissionEvaluator? permissions = null) =>
        new(new EfRepository<Order>(c), new EfRepository<OrderLine>(c), new EfRepository<OrderSpecimen>(c),
            new EfRepository<Encounter>(c), new EfRepository<OrderingLocation>(c), new EfRepository<Patient>(c),
            new EfRepository<Specimen>(c), new EfRepository<OrderingProvider>(c), new EfRepository<ProductType>(c),
            new EfRepository<TestDefinition>(c), new EfRepository<TestGrouper>(c),
            new FixedClock(DateTime.UtcNow), c,
            ruleEngine: null,
            permissions: permissions,
            currentUser: _factory.CurrentUser);

    private async Task<(Patient patient, Encounter encounter, OrderingLocation location)> SeedContextAsync(
        BloodBankDbContext c)
    {
        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-{Guid.NewGuid():N}",
            LastName = "Order",
            FirstName = "Auth",
            DateOfBirth = new DateOnly(1990, 1, 1),
            Sex = Sex.Female
        };
        c.Patients.Add(patient);
        var location = new OrderingLocation { Code = $"LOC-{Guid.NewGuid():N}", Name = "Auth OR", IsActive = true };
        c.OrderingLocations.Add(location);
        await c.SaveChangesAsync();

        var encounter = new Encounter
        {
            PatientId = patient.Id,
            VisitNumber = $"VIS-{Guid.NewGuid():N}"[..16],
            EncounterType = EncounterType.Inpatient,
            Status = EncounterStatus.Active,
            AdmitUtc = DateTime.UtcNow
        };
        c.Encounters.Add(encounter);
        await c.SaveChangesAsync();
        return (patient, encounter, location);
    }

    private async Task<(Patient patient, Encounter encounter, OrderingLocation location, Order order)> SeedOrderAsync(
        BloodBankDbContext c)
    {
        var (patient, encounter, location) = await SeedContextAsync(c);

        var created = await Orders(c).CreateAsync(patient.Id, new CreateOrderRequest(
            encounter.Id, location.Id, $"ORD-{Guid.NewGuid():N}"[..16],
            [new OrderLineInputDto(OrderCategory.Test, "ABORH", null)],
            OrderPriority.Routine, DateTime.UtcNow, null, OrderSource.Manual, null, null));
        Assert.True(created.Succeeded);
        return (patient, encounter, location, created.Value!);
    }

    [Fact]
    public async Task Create_WithoutPatientWrite_IsRejected()
    {
        await using var c = _factory.Create();
        var (patient, encounter, location) = await SeedContextAsync(c);
        var request = new CreateOrderRequest(
            encounter.Id, location.Id, $"ORD-{Guid.NewGuid():N}"[..16],
            [new OrderLineInputDto(OrderCategory.Test, "ABORH", null)],
            OrderPriority.Routine, DateTime.UtcNow, null, OrderSource.Manual, null, null);

        var denied = await Orders(c, new FixedPermissionEvaluator(1, PermissionCodes.LookbackManage))
            .CreateAsync(patient.Id, request);
        Assert.False(denied.Succeeded);
        Assert.Contains("patient.write", denied.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(await c.Orders.AnyAsync(o => o.OrderNumber == request.OrderNumber));

        var allowed = await Orders(c, new FixedPermissionEvaluator(1, PermissionCodes.PatientWrite))
            .CreateAsync(patient.Id, request);
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(request.OrderNumber, allowed.Value!.OrderNumber);
    }

    [Fact]
    public async Task CreateFromHl7_DoesNotRequirePatientWrite()
    {
        await using var c = _factory.Create();
        var (patient, encounter, location) = await SeedContextAsync(c);
        var request = new CreateOrderRequest(
            encounter.Id, location.Id, $"ORD-{Guid.NewGuid():N}"[..16],
            [new OrderLineInputDto(OrderCategory.Test, "ABORH", null)],
            OrderPriority.Routine, DateTime.UtcNow, null, OrderSource.Hl7, "EHR", null);

        var result = await Orders(c, new FixedPermissionEvaluator(1, PermissionCodes.LookbackManage))
            .CreateFromHl7Async(patient.Id, request);
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(OrderSource.Hl7, result.Value!.Source);
    }

    [Fact]
    public async Task CreateForAllocation_DoesNotRequirePatientWrite()
    {
        await using var c = _factory.Create();
        var (patient, encounter, location) = await SeedContextAsync(c);
        var request = new CreateOrderRequest(
            encounter.Id, location.Id, $"XM-{Guid.NewGuid():N}"[..16],
            [new OrderLineInputDto(OrderCategory.Test, "ABORH", null)],
            OrderPriority.Stat, DateTime.UtcNow, null, OrderSource.Manual, null, "tech-test");

        var result = await Orders(c, new FixedPermissionEvaluator(1, PermissionCodes.CompatibilityAllocate))
            .CreateForAllocationAsync(patient.Id, request);
        Assert.True(result.Succeeded, result.Error);
    }

    [Fact]
    public async Task Update_WithoutPatientWrite_IsRejected()
    {
        await using var c = _factory.Create();
        var (patient, encounter, location, order) = await SeedOrderAsync(c);
        var request = new UpdateOrderRequest(
            encounter.Id, location.Id,
            [new OrderLineInputDto(OrderCategory.Test, "ABORH", null), new OrderLineInputDto(OrderCategory.Test, "ABSC", null)],
            OrderPriority.Stat, null);

        var denied = await Orders(c, new FixedPermissionEvaluator(1, PermissionCodes.LookbackManage))
            .UpdateAsync(patient.Id, order.Id, request);
        Assert.False(denied.Succeeded);
        Assert.Contains("patient.write", denied.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(OrderPriority.Routine, (await c.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id)).Priority);

        var allowed = await Orders(c, new FixedPermissionEvaluator(1, PermissionCodes.PatientWrite))
            .UpdateAsync(patient.Id, order.Id, request);
        Assert.True(allowed.Succeeded);
        Assert.Equal(OrderPriority.Stat, allowed.Value!.Priority);
    }

    [Fact]
    public async Task Cancel_WithoutPatientWrite_IsRejected()
    {
        await using var c = _factory.Create();
        var (patient, _, _, order) = await SeedOrderAsync(c);

        var denied = await Orders(c, new FixedPermissionEvaluator(1, PermissionCodes.LookbackManage))
            .CancelAsync(patient.Id, order.Id, new CancelOrderRequest("No longer needed"));
        Assert.False(denied.Succeeded);
        Assert.Contains("patient.write", denied.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(OrderStatus.New, (await c.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id)).Status);

        var allowed = await Orders(c, new FixedPermissionEvaluator(1, PermissionCodes.PatientWrite))
            .CancelAsync(patient.Id, order.Id, new CancelOrderRequest("No longer needed"));
        Assert.True(allowed.Succeeded);
        Assert.Equal(OrderStatus.Cancelled, allowed.Value!.Status);
    }

    [Fact]
    public async Task LinkSpecimen_WithoutPatientWrite_IsRejected()
    {
        await using var c = _factory.Create();
        var (patient, encounter, _, order) = await SeedOrderAsync(c);
        var specimen = new Specimen
        {
            AccessionNumber = $"ACC-{Guid.NewGuid():N}",
            PatientId = patient.Id,
            EncounterId = encounter.Id,
            SpecimenType = "EDTA",
            CollectedUtc = DateTime.UtcNow.AddHours(-1),
            ExpiresUtc = DateTime.UtcNow.AddDays(2),
            Status = SpecimenStatus.Accepted
        };
        c.Specimens.Add(specimen);
        await c.SaveChangesAsync();
        var request = new LinkOrderSpecimenRequest(specimen.Id);

        var denied = await Orders(c, new FixedPermissionEvaluator(1, PermissionCodes.LookbackManage))
            .LinkSpecimenAsync(patient.Id, order.Id, request);
        Assert.False(denied.Succeeded);
        Assert.Contains("patient.write", denied.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(await c.OrderSpecimens.AnyAsync(s => s.OrderId == order.Id));

        var allowed = await Orders(c, new FixedPermissionEvaluator(1, PermissionCodes.PatientWrite))
            .LinkSpecimenAsync(patient.Id, order.Id, request);
        Assert.True(allowed.Succeeded);
        Assert.Equal(specimen.Id, allowed.Value!.SpecimenId);
    }
}
