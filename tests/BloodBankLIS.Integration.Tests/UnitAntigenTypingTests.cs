using BloodBankLIS.Application.Billing;
using BloodBankLIS.Application.Compatibility;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Application.Results;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class UnitAntigenTypingTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public UnitAntigenTypingTests(SqliteContextFactory factory) => _factory = factory;

    [Fact]
    public async Task Verify_OnAssignedUnit_WritesAttributesAndSendsBillingMessage()
    {
        await using var c = _factory.Create();
        var seed = await SeedAssignedUnitAsync(c, "UAG-OK");
        var publisher = new CapturingPublisher();
        var typing = Typing(c, publisher);

        var started = await typing.StartAsync(seed.PatientId, seed.AllocationId);
        Assert.True(started.Succeeded, started.Error);
        Assert.Equal(OrderActionStatus.Pending, started.Value!.ActionStatus);
        Assert.Equal(UnitAntigenTypingService.TestCode, started.Value.TestCode);
        Assert.True(await c.OrderLines.AnyAsync(l => l.Id == started.Value.OrderLineId && l.TestCode == "UAGTYPE"));

        var value = BloodAttributeResultValue.Serialize([
            new BloodAttributeResultRow("K", AntigenResult.Negative),
            new BloodAttributeResultRow("E", AntigenResult.Positive)
        ]);
        var saved = await Results(c).SaveTestResultAsync(new SaveTestResultRequest(
            started.Value.SpecimenId,
            started.Value.TestOrderId,
            started.Value.OrderLineId,
            started.Value.TestCode,
            value,
            null,
            null,
            null,
            null,
            null,
            MarkComplete: true,
            CorrectionReason: null,
            UnitNumber: started.Value.UnitNumber,
            CrossmatchMethod: null,
            CrossmatchResult: null,
            AntibodyScreenNegative: null,
            OrderActionId: started.Value.OrderActionId));
        Assert.True(saved.Succeeded, saved.Error);
        Assert.Equal(ResultStatus.Verified, saved.Value!.Status);

        var completed = await typing.CompleteAfterVerifiedResultAsync(started.Value.OrderActionId, saved.Value);
        Assert.True(completed.Succeeded, completed.Error);
        Assert.Equal(OrderActionStatus.Completed, completed.Value!.Status);

        var attrs = await c.UnitBloodAttributes
            .Where(a => a.BloodProductId == seed.UnitId)
            .Join(c.BloodAttributeDefinitions, a => a.BloodAttributeDefinitionId, d => d.Id, (a, d) => new { a, d.Code })
            .ToListAsync();
        Assert.Contains(attrs, a => a.Code == "K"
            && a.a.Result == AntigenResult.Negative
            && a.a.TypingSource == UnitAttributeTypingSource.TestResult);
        Assert.Contains(attrs, a => a.Code == "E"
            && a.a.Result == AntigenResult.Positive
            && a.a.TypingSource == UnitAttributeTypingSource.TestResult);
        Assert.DoesNotContain(attrs, a => a.Code == "M");

        var charges = await c.BillingEvents.Where(e => e.PatientId == seed.PatientId).ToListAsync();
        Assert.Single(charges);
        Assert.Equal("BB-UAG", charges[0].BillingCode);
        Assert.Equal(BillingTriggerType.ActionCompleted, charges[0].TriggerType);
        Assert.Single(publisher.Published);
        Assert.Equal(charges[0].Id, publisher.Published[0].Id);
    }

    [Fact]
    public async Task Start_UnitNotAssignedToPatient_Fails()
    {
        await using var c = _factory.Create();
        var seed = await SeedAssignedUnitAsync(c, "UAG-OTHER");
        var other = new Patient
        {
            MedicalRecordNumber = $"MRN-{Guid.NewGuid():N}",
            LastName = "Other",
            FirstName = "Patient",
            DateOfBirth = new DateOnly(1980, 1, 1),
            Sex = Sex.Male
        };
        c.Patients.Add(other);
        await c.SaveChangesAsync();

        var wrongPatient = await Typing(c).StartAsync(other.Id, seed.AllocationId);
        Assert.False(wrongPatient.Succeeded);
        Assert.Contains("not assigned", wrongPatient.Error!, StringComparison.OrdinalIgnoreCase);

        var unit = await c.BloodUnits.SingleAsync(u => u.Id == seed.UnitId);
        unit.Status = UnitStatus.Available;
        await c.SaveChangesAsync();

        var notAssigned = await Typing(c).StartAsync(seed.PatientId, seed.AllocationId);
        Assert.False(notAssigned.Succeeded);
        Assert.Contains("not assigned", notAssigned.Error!, StringComparison.OrdinalIgnoreCase);
    }

    private UnitAntigenTypingService Typing(BloodBankDbContext c, CapturingPublisher? publisher = null) =>
        new(
            new EfRepository<Allocation>(c),
            new EfRepository<BloodUnit>(c),
            new EfRepository<Order>(c),
            new EfRepository<OrderLine>(c),
            new EfRepository<OrderSpecimen>(c),
            new EfRepository<OrderAction>(c),
            new EfRepository<ActionDefinition>(c),
            new EfRepository<TestDefinition>(c),
            new EfRepository<Specimen>(c),
            _factory.Clock,
            _factory.CurrentUser,
            c,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            billing: Billing(c, publisher));

    private BillingService Billing(BloodBankDbContext c, CapturingPublisher? publisher = null) =>
        new(
            new EfRepository<BillingEvent>(c),
            new EfRepository<ChargeRule>(c),
            new EfRepository<ChargeCode>(c),
            new EfRepository<TestServiceBilling>(c),
            new EfRepository<ProductBilling>(c),
            new EfRepository<TestResult>(c),
            new EfRepository<Issue>(c),
            new EfRepository<BloodUnit>(c),
            new EfRepository<ProductType>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            publisher ?? new CapturingPublisher(),
            actionBillings: new EfRepository<ActionBilling>(c),
            orderActions: new EfRepository<OrderAction>(c),
            actionDefinitions: new EfRepository<ActionDefinition>(c),
            orders: new EfRepository<Order>(c));

    private ResultService Results(BloodBankDbContext c) =>
        new(
            new EfRepository<TestResult>(c),
            new EfRepository<Specimen>(c),
            new EfRepository<PatientBloodTypeHistory>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            new EfRepository<TestDefinition>(c),
            new EfRepository<SubtestDefinition>(c),
            new EfRepository<Order>(c),
            new EfRepository<OrderLine>(c),
            new InventoryRepository(c),
            new CompatibilityService(
                new InventoryRepository(c),
                new EfRepository<Crossmatch>(c),
                new EfRepository<Allocation>(c),
                new EfRepository<Patient>(c),
                new EfRepository<Specimen>(c),
                new EfRepository<ProductType>(c),
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
                c,
                _factory.Clock,
                _factory.CurrentUser),
            new EfRepository<AntibodyHistory>(c),
            new EfRepository<AntigenProfile>(c),
            new EfRepository<BloodAttributeDefinition>(c),
            new EfRepository<UnitBloodAttribute>(c));

    private async Task<(long PatientId, long AllocationId, long UnitId)> SeedAssignedUnitAsync(
        BloodBankDbContext c,
        string suffix)
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-{suffix}-{token}",
            LastName = "Kline",
            FirstName = "Nora",
            DateOfBirth = new DateOnly(1966, 9, 3),
            Sex = Sex.Female
        };
        var location = new OrderingLocation { Code = $"LOC-{token}", Name = "ICU", IsActive = true };
        var product = new ProductType
        {
            ProductCode = $"RBC-{token}",
            Name = "Red Blood Cells",
            ComponentClass = ComponentClass.RedBloodCells
        };
        c.Patients.Add(patient);
        c.OrderingLocations.Add(location);
        c.ProductTypes.Add(product);
        await c.SaveChangesAsync();

        var encounter = new Encounter
        {
            PatientId = patient.Id,
            VisitNumber = $"VIS-{token}",
            EncounterType = EncounterType.Inpatient,
            Status = EncounterStatus.Active,
            AdmitUtc = _factory.Clock.UtcNow.AddHours(-5)
        };
        c.Encounters.Add(encounter);
        await c.SaveChangesAsync();

        var specimen = new Specimen
        {
            AccessionNumber = $"ACC-{token}",
            PatientId = patient.Id,
            EncounterId = encounter.Id,
            SpecimenType = "EDTA",
            CollectedUtc = _factory.Clock.UtcNow.AddHours(-4),
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(2),
            Status = SpecimenStatus.Accepted
        };
        c.Specimens.Add(specimen);

        foreach (var (code, name) in new[] { ("K", "Kell"), ("E", "Rh E"), ("M", "MNS M") })
        {
            if (!await c.BloodAttributeDefinitions.AnyAsync(a => a.Code == code))
            {
                c.BloodAttributeDefinitions.Add(new BloodAttributeDefinition
                {
                    Code = code,
                    Name = name,
                    AntibodyName = $"anti-{code}",
                    IsClinicallySignificant = true,
                    IsActive = true,
                    IsDraft = false,
                    EffectiveUtc = _factory.Clock.UtcNow,
                    Version = 1
                });
            }
        }

        if (!await c.ActionDefinitions.AnyAsync(a => a.Code == "UAGTYPE"))
        {
            var charge = new ChargeCode
            {
                Code = "BB-UAG",
                Description = "Unit antigen typing",
                DefaultAmount = 48m,
                CptCode = "86905",
                RevenueCode = "0301"
            };
            c.ChargeCodes.Add(charge);
            var action = new ActionDefinition
            {
                Code = "UAGTYPE",
                Name = "Unit antigen typing",
                Category = ActionCategory.UnitAntigenTyping,
                IsActive = true
            };
            c.ActionDefinitions.Add(action);
            await c.SaveChangesAsync();
            c.ActionBillings.Add(new ActionBilling
            {
                ActionDefinitionId = action.Id,
                ChargeCodeId = charge.Id,
                Description = "Unit antigen typing",
                Trigger = BillingTriggerType.ActionCompleted,
                IsActive = true
            });
        }

        if (!await c.TestDefinitions.AnyAsync(t => t.Code == "UAGTYPE"))
        {
            c.TestDefinitions.Add(new TestDefinition
            {
                Code = "UAGTYPE",
                Name = "Unit Antigen Typing",
                Category = TestCategory.AntigenTyping,
                ResultValueType = ResultValueType.BloodAttribute,
                BloodAttributeScopeJson = BloodAttributeScope.Serialize([
                    new BloodAttributeScopeEntry("K"),
                    new BloodAttributeScopeEntry("E"),
                    new BloodAttributeScopeEntry("M")
                ]),
                BloodAttributeScopeKind = BloodAttributeKind.Antigen,
                ContributesToUnitBloodAttributes = true,
                VerificationRequired = true,
                IsActive = true,
                IsDraft = false,
                EffectiveUtc = _factory.Clock.UtcNow,
                Version = 1
            });
        }

        var order = new Order
        {
            OrderNumber = $"ORD-{token}",
            PatientId = patient.Id,
            EncounterId = encounter.Id,
            OrderingLocationId = location.Id,
            OrderCategory = OrderCategory.Product,
            OrderName = "Red Blood Cells",
            OrderType = OrderType.Other,
            ProductTypeId = product.Id,
            Status = OrderStatus.InProcess,
            OrderedUtc = _factory.Clock.UtcNow.AddHours(-1)
        };
        var unit = new BloodUnit
        {
            UnitNumber = $"W{token}",
            ProductTypeId = product.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            Status = UnitStatus.Assigned,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(20)
        };
        c.Orders.Add(order);
        c.BloodUnits.Add(unit);
        await c.SaveChangesAsync();

        c.OrderLines.Add(new OrderLine
        {
            OrderId = order.Id,
            LineNumber = 1,
            LineCategory = OrderCategory.Product,
            LineName = "Red Blood Cells",
            ProductTypeId = product.Id,
            OrderType = OrderType.Other
        });
        var allocation = new Allocation
        {
            BloodProductId = unit.Id,
            PatientId = patient.Id,
            EncounterId = encounter.Id,
            OrderId = order.Id,
            SpecimenId = specimen.Id,
            Status = AllocationStatus.Reserved,
            AllocatedUtc = _factory.Clock.UtcNow,
            AllocatedBy = "tech-test"
        };
        c.Allocations.Add(allocation);
        await c.SaveChangesAsync();
        return (patient.Id, allocation.Id, unit.Id);
    }
}
