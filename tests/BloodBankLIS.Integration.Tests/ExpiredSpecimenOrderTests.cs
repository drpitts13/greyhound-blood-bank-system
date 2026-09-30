using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class ExpiredSpecimenOrderTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public ExpiredSpecimenOrderTests(SqliteContextFactory factory) => _factory = factory;

    private ExpiredSpecimenOrderService Service(BloodBankDbContext c) =>
        new(new EfRepository<Specimen>(c),
            new EfRepository<Order>(c),
            new EfRepository<OrderLine>(c),
            new EfRepository<OrderSpecimen>(c),
            new EfRepository<OrderAction>(c),
            new EfRepository<TestResult>(c),
            new EfRepository<Allocation>(c),
            new EfRepository<ProductType>(c),
            new EfRepository<TestDefinition>(c),
            new EfRepository<OrderCancellationReason>(c),
            _factory.Clock,
            c,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser));

    [Fact]
    public async Task PendingCrossmatch_UnlinksExpiredSpecimen_AndKeepsTheOrder()
    {
        await using var c = _factory.Create();
        var chart = await SeedChartAsync(c);
        var specimen = await AddSpecimenAsync(c, chart, expired: true);
        var order = await AddOrderAsync(c, chart, "XM-1", OrderType.Crossmatch, OrderStatus.InProcess);
        await AddLineAsync(c, order, OrderCategory.Test, OrderType.Crossmatch, "XM", "Crossmatch");
        await LinkAsync(c, order, specimen, primary: true);

        var result = await Service(c).ProcessAsync();

        Assert.Equal(1, result.SpecimensMarkedExpired);
        Assert.Equal(1, result.CrossmatchOrdersUnlinked);
        Assert.Equal(0, result.OrdersCancelled);

        var savedOrder = await c.Orders.SingleAsync(o => o.Id == order.Id);
        Assert.Equal(OrderStatus.InProcess, savedOrder.Status);
        Assert.Equal(SpecimenStatus.Expired, (await c.Specimens.SingleAsync(s => s.Id == specimen.Id)).Status);
        Assert.False(await c.OrderSpecimens.AnyAsync(os => os.OrderId == order.Id));

        var stored = await c.OrderSpecimens.IgnoreQueryFilters().SingleAsync(os => os.OrderId == order.Id);
        Assert.Equal(_factory.Clock.UtcNow, stored.UnlinkedUtc);
        Assert.Equal(ExpiredSpecimenOrderDisposition.UnlinkReason, stored.UnlinkReason);
        Assert.True(await c.OrderLines.AnyAsync(l => l.OrderId == order.Id && l.IsActive));
        Assert.Contains(
            await c.AuditEvents.ToListAsync(),
            e => e.EventType == AuditEventType.OrderChange
                 && e.EntityId == order.Id
                 && e.Reason == ExpiredSpecimenOrderDisposition.UnlinkReason);
    }

    [Fact]
    public async Task OtherOpenOrder_IsCancelled_AndKeepsTheSpecimenLink()
    {
        await using var c = _factory.Create();
        var chart = await SeedChartAsync(c);
        c.OrderCancellationReasons.Add(new OrderCancellationReason
        {
            Code = ExpiredSpecimenOrderDisposition.CancellationReasonCode,
            Text = "Sample outdated",
            SortOrder = 50,
            IsActive = true
        });
        await c.SaveChangesAsync();

        var specimen = await AddSpecimenAsync(c, chart, expired: true);
        var order = await AddOrderAsync(c, chart, "TS-1", OrderType.TypeAndScreen, OrderStatus.New);
        var line = await AddLineAsync(c, order, OrderCategory.Test, OrderType.TypeAndScreen, "ABSC", "Antibody screen");
        await LinkAsync(c, order, specimen, primary: true);

        var result = await Service(c).ProcessAsync();

        Assert.Equal(1, result.OrdersCancelled);
        Assert.Equal(0, result.CrossmatchOrdersUnlinked);

        var saved = await c.Orders.SingleAsync(o => o.Id == order.Id);
        Assert.Equal(OrderStatus.Cancelled, saved.Status);
        Assert.Equal(ExpiredSpecimenOrderDisposition.CancellationReasonCode, saved.CancellationReasonCode);
        Assert.Equal("Sample outdated", saved.CancellationReason);
        Assert.False((await c.OrderLines.SingleAsync(l => l.Id == line.Id)).IsActive);
        Assert.True(await c.OrderSpecimens.AnyAsync(os => os.OrderId == order.Id && os.SpecimenId == specimen.Id));
    }

    [Fact]
    public async Task VerifiedLine_StaysActive_WhenTheOrderIsCancelled()
    {
        await using var c = _factory.Create();
        var chart = await SeedChartAsync(c);
        var specimen = await AddSpecimenAsync(c, chart, expired: true);
        var order = await AddOrderAsync(c, chart, "TS-2", OrderType.TypeAndScreen, OrderStatus.PartiallyComplete);
        var verified = await AddLineAsync(c, order, OrderCategory.Test, OrderType.AboRh, "ABORH", "ABO/Rh");
        verified.ResultStatus = ResultStatus.Verified;
        var pending = await AddLineAsync(c, order, OrderCategory.Test, OrderType.TypeAndScreen, "ABSC", "Antibody screen");
        c.TestResults.Add(new TestResult
        {
            SpecimenId = specimen.Id,
            PatientId = chart.Patient.Id,
            OrderId = order.Id,
            TestCode = "ABORH",
            Status = ResultStatus.Verified,
            Value = "O Pos"
        });
        await c.SaveChangesAsync();
        await LinkAsync(c, order, specimen, primary: true);

        await Service(c).ProcessAsync();

        Assert.True((await c.OrderLines.SingleAsync(l => l.Id == verified.Id)).IsActive);
        Assert.False((await c.OrderLines.SingleAsync(l => l.Id == pending.Id)).IsActive);
        Assert.Equal(OrderStatus.Cancelled, (await c.Orders.SingleAsync(o => o.Id == order.Id)).Status);
    }

    [Fact]
    public async Task ProductThatRequiresCrossmatch_IsUnlinked_NotCancelled()
    {
        await using var c = _factory.Create();
        var chart = await SeedChartAsync(c);
        var product = new ProductType
        {
            ProductCode = $"RBC-{Guid.NewGuid():N}"[..12],
            Name = "Red cells",
            RequiresCrossmatch = true,
            IsActive = true
        };
        c.ProductTypes.Add(product);
        await c.SaveChangesAsync();

        var specimen = await AddSpecimenAsync(c, chart, expired: true);
        var order = await AddOrderAsync(c, chart, "RBC-1", OrderType.Other, OrderStatus.New);
        await AddLineAsync(c, order, OrderCategory.Product, OrderType.Other, null, "Red cells", product.Id);
        await LinkAsync(c, order, specimen, primary: true);

        var result = await Service(c).ProcessAsync();

        Assert.Equal(1, result.CrossmatchOrdersUnlinked);
        Assert.Equal(0, result.OrdersCancelled);
        Assert.Equal(OrderStatus.New, (await c.Orders.SingleAsync(o => o.Id == order.Id)).Status);
        Assert.False(await c.OrderSpecimens.AnyAsync(os => os.OrderId == order.Id));
    }

    [Fact]
    public async Task CatalogCrossmatchTest_IsUnlinked()
    {
        await using var c = _factory.Create();
        var chart = await SeedChartAsync(c);
        c.TestDefinitions.Add(new TestDefinition
        {
            Code = "SER-XM",
            Name = "Serologic crossmatch",
            ResultValueType = ResultValueType.Crossmatch,
            IsActive = true
        });
        await c.SaveChangesAsync();

        var specimen = await AddSpecimenAsync(c, chart, expired: true);
        var order = await AddOrderAsync(c, chart, "SXM-1", OrderType.Other, OrderStatus.New);
        await AddLineAsync(c, order, OrderCategory.Test, OrderType.Other, "SER-XM", "Serologic crossmatch");
        await LinkAsync(c, order, specimen, primary: true);

        var result = await Service(c).ProcessAsync();

        Assert.Equal(1, result.CrossmatchOrdersUnlinked);
        Assert.Equal(OrderStatus.New, (await c.Orders.SingleAsync(o => o.Id == order.Id)).Status);
    }

    [Fact]
    public async Task CurrentSpecimen_OnTheSameCrossmatch_BecomesPrimary()
    {
        await using var c = _factory.Create();
        var chart = await SeedChartAsync(c);
        var expired = await AddSpecimenAsync(c, chart, expired: true, accession: "ACC-OLD");
        var current = await AddSpecimenAsync(c, chart, expired: false, accession: "ACC-NEW");
        var order = await AddOrderAsync(c, chart, "XM-2", OrderType.Crossmatch, OrderStatus.New);
        await AddLineAsync(c, order, OrderCategory.Test, OrderType.Crossmatch, "CXM", "Complex crossmatch");
        await LinkAsync(c, order, expired, primary: true);
        await LinkAsync(c, order, current, primary: false);

        await Service(c).ProcessAsync();

        var remaining = await c.OrderSpecimens.SingleAsync(os => os.OrderId == order.Id);
        Assert.Equal(current.Id, remaining.SpecimenId);
        Assert.True(remaining.IsPrimary);
        Assert.Equal(OrderStatus.New, (await c.Orders.SingleAsync(o => o.Id == order.Id)).Status);
    }

    [Fact]
    public async Task CompletedOrder_AndCurrentSpecimen_AreLeftAlone()
    {
        await using var c = _factory.Create();
        var chart = await SeedChartAsync(c);
        var expired = await AddSpecimenAsync(c, chart, expired: true, accession: "ACC-DONE");
        var current = await AddSpecimenAsync(c, chart, expired: false, accession: "ACC-LIVE");
        var completed = await AddOrderAsync(c, chart, "TS-DONE", OrderType.TypeAndScreen, OrderStatus.Completed);
        var live = await AddOrderAsync(c, chart, "TS-LIVE", OrderType.TypeAndScreen, OrderStatus.New);
        await AddLineAsync(c, completed, OrderCategory.Test, OrderType.TypeAndScreen, "ABSC", "Antibody screen");
        await AddLineAsync(c, live, OrderCategory.Test, OrderType.TypeAndScreen, "ABSC", "Antibody screen");
        await LinkAsync(c, completed, expired, primary: true);
        await LinkAsync(c, live, current, primary: true);

        var result = await Service(c).ProcessAsync();

        Assert.Equal(0, result.OrdersCancelled);
        Assert.Equal(0, result.CrossmatchOrdersUnlinked);
        Assert.Equal(OrderStatus.Completed, (await c.Orders.SingleAsync(o => o.Id == completed.Id)).Status);
        Assert.Equal(OrderStatus.New, (await c.Orders.SingleAsync(o => o.Id == live.Id)).Status);
        Assert.Equal(SpecimenStatus.Accepted, (await c.Specimens.SingleAsync(s => s.Id == current.Id)).Status);
        Assert.True(await c.OrderSpecimens.AnyAsync(os => os.OrderId == completed.Id && os.SpecimenId == expired.Id));
        Assert.True(await c.OrderSpecimens.AnyAsync(os => os.OrderId == live.Id && os.SpecimenId == current.Id));
    }

    [Fact]
    public async Task ReservedUnits_KeepANonCrossmatchOrderOpen()
    {
        await using var c = _factory.Create();
        var chart = await SeedChartAsync(c);
        var product = new ProductType
        {
            ProductCode = $"PLT-{Guid.NewGuid():N}"[..12],
            Name = "Platelets",
            RequiresCrossmatch = false,
            IsActive = true
        };
        c.ProductTypes.Add(product);
        await c.SaveChangesAsync();

        var specimen = await AddSpecimenAsync(c, chart, expired: true);
        var order = await AddOrderAsync(c, chart, "PLT-1", OrderType.Other, OrderStatus.InProcess);
        await AddLineAsync(c, order, OrderCategory.Product, OrderType.Other, null, "Platelets", product.Id);
        await LinkAsync(c, order, specimen, primary: true);

        var unit = new BloodUnit
        {
            UnitNumber = $"U-{Guid.NewGuid():N}"[..12],
            ProductTypeId = product.Id,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(5),
            Status = UnitStatus.Allocated
        };
        c.BloodUnits.Add(unit);
        await c.SaveChangesAsync();
        c.Allocations.Add(new Allocation
        {
            BloodProductId = unit.Id,
            PatientId = chart.Patient.Id,
            OrderId = order.Id,
            SpecimenId = specimen.Id,
            Status = AllocationStatus.Reserved,
            AllocatedUtc = _factory.Clock.UtcNow
        });
        await c.SaveChangesAsync();

        var result = await Service(c).ProcessAsync();

        Assert.Equal(0, result.OrdersCancelled);
        Assert.Equal(1, result.OrdersLeftOpen);
        Assert.Equal(OrderStatus.InProcess, (await c.Orders.SingleAsync(o => o.Id == order.Id)).Status);
        Assert.True(await c.OrderSpecimens.AnyAsync(os => os.OrderId == order.Id));
    }

    [Fact]
    public async Task SecondSweep_DoesNotRepeatTheWork()
    {
        await using var c = _factory.Create();
        var chart = await SeedChartAsync(c);
        var specimen = await AddSpecimenAsync(c, chart, expired: true);
        var crossmatch = await AddOrderAsync(c, chart, "XM-3", OrderType.Crossmatch, OrderStatus.New);
        var screen = await AddOrderAsync(c, chart, "TS-3", OrderType.TypeAndScreen, OrderStatus.New);
        await AddLineAsync(c, crossmatch, OrderCategory.Test, OrderType.Crossmatch, "EXM", "Electronic crossmatch");
        await AddLineAsync(c, screen, OrderCategory.Test, OrderType.TypeAndScreen, "ABSC", "Antibody screen");
        await LinkAsync(c, crossmatch, specimen, primary: true);
        await LinkAsync(c, screen, specimen, primary: true);

        var service = Service(c);
        await service.ProcessAsync();
        var again = await service.ProcessAsync();

        Assert.Equal(0, again.SpecimensMarkedExpired);
        Assert.Equal(0, again.CrossmatchOrdersUnlinked);
        Assert.Equal(0, again.OrdersCancelled);
        Assert.Equal(OrderStatus.Cancelled, (await c.Orders.SingleAsync(o => o.Id == screen.Id)).Status);
        Assert.Equal(OrderStatus.New, (await c.Orders.SingleAsync(o => o.Id == crossmatch.Id)).Status);
    }

    private async Task<(Patient Patient, Encounter Encounter, OrderingLocation Location)> SeedChartAsync(BloodBankDbContext c)
    {
        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-{Guid.NewGuid():N}"[..16],
            LastName = "Expire",
            FirstName = "Specimen",
            DateOfBirth = new DateOnly(1984, 6, 1),
            Sex = Sex.Male
        };
        var location = new OrderingLocation
        {
            Code = $"LOC-{Guid.NewGuid():N}"[..12],
            Name = "Lab",
            IsActive = true
        };
        c.Patients.Add(patient);
        c.OrderingLocations.Add(location);
        await c.SaveChangesAsync();

        var encounter = new Encounter
        {
            PatientId = patient.Id,
            VisitNumber = $"V-{Guid.NewGuid():N}"[..12],
            EncounterType = EncounterType.Inpatient,
            Status = EncounterStatus.Active,
            AdmitUtc = _factory.Clock.UtcNow.AddDays(-1)
        };
        c.Encounters.Add(encounter);
        await c.SaveChangesAsync();
        return (patient, encounter, location);
    }

    private async Task<Specimen> AddSpecimenAsync(
        BloodBankDbContext c,
        (Patient Patient, Encounter Encounter, OrderingLocation Location) chart,
        bool expired,
        string? accession = null)
    {
        var specimen = new Specimen
        {
            AccessionNumber = accession ?? $"ACC-{Guid.NewGuid():N}"[..16],
            PatientId = chart.Patient.Id,
            EncounterId = chart.Encounter.Id,
            SpecimenType = "EDTA",
            CollectedUtc = _factory.Clock.UtcNow.AddDays(-3),
            ExpiresUtc = expired ? _factory.Clock.UtcNow : _factory.Clock.UtcNow.AddDays(2),
            Status = SpecimenStatus.Accepted
        };
        c.Specimens.Add(specimen);
        await c.SaveChangesAsync();
        return specimen;
    }

    private async Task<Order> AddOrderAsync(
        BloodBankDbContext c,
        (Patient Patient, Encounter Encounter, OrderingLocation Location) chart,
        string orderNumber,
        OrderType orderType,
        OrderStatus status)
    {
        var order = new Order
        {
            OrderNumber = $"{orderNumber}-{Guid.NewGuid():N}"[..20],
            PatientId = chart.Patient.Id,
            EncounterId = chart.Encounter.Id,
            OrderingLocationId = chart.Location.Id,
            OrderName = orderNumber,
            OrderType = orderType,
            OrderCategory = OrderCategory.Test,
            Status = status,
            OrderedUtc = _factory.Clock.UtcNow.AddHours(-4)
        };
        c.Orders.Add(order);
        await c.SaveChangesAsync();
        return order;
    }

    private static async Task<OrderLine> AddLineAsync(
        BloodBankDbContext c,
        Order order,
        OrderCategory category,
        OrderType orderType,
        string? testCode,
        string name,
        long? productTypeId = null)
    {
        var count = await c.OrderLines.CountAsync(l => l.OrderId == order.Id);
        var line = new OrderLine
        {
            OrderId = order.Id,
            LineNumber = count + 1,
            LineCategory = category,
            LineName = name,
            TestCode = testCode,
            OrderType = orderType,
            ProductTypeId = productTypeId,
            IsActive = true
        };
        c.OrderLines.Add(line);
        await c.SaveChangesAsync();
        return line;
    }

    private static async Task LinkAsync(BloodBankDbContext c, Order order, Specimen specimen, bool primary)
    {
        c.OrderSpecimens.Add(new OrderSpecimen
        {
            OrderId = order.Id,
            SpecimenId = specimen.Id,
            IsPrimary = primary
        });
        await c.SaveChangesAsync();
    }
}
