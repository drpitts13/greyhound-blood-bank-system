using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Persistence;

namespace BloodBankLIS.Integration.Tests;

public class MinimumStockOpenOrdersTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public MinimumStockOpenOrdersTests(SqliteContextFactory factory) => _factory = factory;

    // TEST-BB-426: open product order lines for a product are counted beside its stock; filled, cancelled, test, and
    // closed-order lines are not.
    [Fact]
    public async Task OpenOrders_CountOnlyActiveProductLines_OnOpenOrders()
    {
        await using var c = _factory.Create();
        var product = new ProductType { ProductCode = $"FFP-{Guid.NewGuid():N}"[..12], Name = "Plasma" };
        var other = new ProductType { ProductCode = $"PLT-{Guid.NewGuid():N}"[..12], Name = "Platelets" };
        c.ProductTypes.AddRange(product, other);
        var patient = new Patient { MedicalRecordNumber = $"MSO{Guid.NewGuid():N}"[..10], LastName = "Stock", FirstName = "Open", DateOfBirth = new DateOnly(1970, 1, 1) };
        c.Patients.Add(patient);
        var location = new OrderingLocation { Code = $"MSO{Guid.NewGuid():N}"[..8], Name = "Stock ward" };
        c.OrderingLocations.Add(location);
        await c.SaveChangesAsync();
        var encounter = new Encounter { PatientId = patient.Id, VisitNumber = $"V-{Guid.NewGuid():N}"[..12], EncounterType = EncounterType.Inpatient, Status = EncounterStatus.Active };
        c.Encounters.Add(encounter);
        await c.SaveChangesAsync();
        c.BloodUnits.Add(new BloodUnit
        {
            UnitNumber = $"U-MSO-{Guid.NewGuid():N}"[..14], ProductTypeId = product.Id, Status = UnitStatus.Available,
            Abo = AboGroup.A, RhD = RhType.Positive, ExpiresUtc = _factory.Clock.UtcNow.AddDays(30)
        });

        Order NewOrder(OrderStatus status) => new()
        {
            OrderNumber = $"MSO-{Guid.NewGuid():N}"[..14], PatientId = patient.Id, EncounterId = encounter.Id,
            OrderingLocationId = location.Id, OrderCategory = OrderCategory.Product,
            OrderName = "Plasma", OrderType = OrderType.Other, Status = status, OrderedUtc = _factory.Clock.UtcNow
        };
        var open1 = NewOrder(OrderStatus.InProcess);
        var open2 = NewOrder(OrderStatus.New);
        var closed = NewOrder(OrderStatus.Completed);
        c.Orders.AddRange(open1, open2, closed);
        await c.SaveChangesAsync();

        OrderLine Line(Order order, long? productId, FulfillmentStatus status, OrderCategory category = OrderCategory.Product) => new()
        {
            OrderId = order.Id, LineNumber = 1, LineCategory = category, LineName = "line", ProductTypeId = productId,
            OrderType = OrderType.Other, FulfillmentStatus = status
        };
        c.OrderLines.AddRange(
            Line(open1, product.Id, FulfillmentStatus.Ordered),
            Line(open2, product.Id, FulfillmentStatus.PartiallyFulfilled),
            Line(open2, product.Id, FulfillmentStatus.Complete),
            Line(open2, product.Id, FulfillmentStatus.Cancelled),
            Line(closed, product.Id, FulfillmentStatus.Ordered),
            Line(open1, other.Id, FulfillmentStatus.Ordered),
            Line(open1, product.Id, FulfillmentStatus.Ordered, OrderCategory.Test));
        await c.SaveChangesAsync();

        var service = new MinimumStockService(
            new EfRepository<MinimumStockLevel>(c), new EfRepository<ProductType>(c), new InventoryRepository(c), c,
            new EfRepository<OrderLine>(c), new EfRepository<Order>(c));
        Assert.True((await service.SaveAsync(new SaveMinimumStockRequest(product.Id, null, 1))).Succeeded);

        var row = (await service.ListAsync()).Single(r => r.ProductTypeId == product.Id);
        Assert.Equal(1, row.OnHand);
        Assert.Equal(2, row.OpenOrders);
        Assert.True(row.OpenOrdersExceedOnHand);
        Assert.Equal(0, row.Shortfall);
    }

    // TEST-BB-1255 (end-user testing pass 231): an order line already covered by a unit reserved or issued on its order is
    // not counted as waiting, so the count matches Blood orders to prepare.
    [Fact]
    public async Task OpenOrders_LeaveOutLinesCoveredByReservedOrIssuedUnits()
    {
        await using var c = _factory.Create();
        var product = new ProductType { ProductCode = $"RCV-{Guid.NewGuid():N}"[..12], Name = "Red cells" };
        c.ProductTypes.Add(product);
        var patient = new Patient { MedicalRecordNumber = $"RCV{Guid.NewGuid():N}"[..10], LastName = "Covered", FirstName = "Order", DateOfBirth = new DateOnly(1970, 1, 1) };
        c.Patients.Add(patient);
        var location = new OrderingLocation { Code = $"RCV{Guid.NewGuid():N}"[..8], Name = "Ward" };
        c.OrderingLocations.Add(location);
        await c.SaveChangesAsync();
        var encounter = new Encounter { PatientId = patient.Id, VisitNumber = $"V-{Guid.NewGuid():N}"[..12], EncounterType = EncounterType.Inpatient, Status = EncounterStatus.Active };
        c.Encounters.Add(encounter);
        var unit = new BloodUnit
        {
            UnitNumber = $"U-RCV-{Guid.NewGuid():N}"[..14], ProductTypeId = product.Id, Status = UnitStatus.Crossmatched,
            Abo = AboGroup.O, RhD = RhType.Positive, ExpiresUtc = _factory.Clock.UtcNow.AddDays(30)
        };
        c.BloodUnits.Add(unit);
        await c.SaveChangesAsync();
        var order = new Order
        {
            OrderNumber = $"RCV-{Guid.NewGuid():N}"[..14], PatientId = patient.Id, EncounterId = encounter.Id,
            OrderingLocationId = location.Id, OrderCategory = OrderCategory.Product, OrderName = "Red cells",
            OrderType = OrderType.Other, Status = OrderStatus.InProcess, OrderedUtc = _factory.Clock.UtcNow
        };
        c.Orders.Add(order);
        await c.SaveChangesAsync();
        for (var n = 1; n <= 2; n++)
        {
            c.OrderLines.Add(new OrderLine
            {
                OrderId = order.Id, LineNumber = n, LineCategory = OrderCategory.Product, LineName = "Red cells", ProductTypeId = product.Id,
                OrderType = OrderType.Other, FulfillmentStatus = FulfillmentStatus.Ordered
            });
        }

        c.Allocations.Add(new Allocation { PatientId = patient.Id, BloodProductId = unit.Id, OrderId = order.Id, Status = AllocationStatus.Reserved, AllocatedUtc = _factory.Clock.UtcNow });
        await c.SaveChangesAsync();

        var service = new MinimumStockService(
            new EfRepository<MinimumStockLevel>(c), new EfRepository<ProductType>(c), new InventoryRepository(c), c,
            new EfRepository<OrderLine>(c), new EfRepository<Order>(c),
            allocations: new EfRepository<Allocation>(c), issues: new EfRepository<Issue>(c));
        Assert.True((await service.SaveAsync(new SaveMinimumStockRequest(product.Id, null, 1))).Succeeded);

        Assert.Equal(1, (await service.ListAsync()).Single(r => r.ProductTypeId == product.Id).OpenOrders);
    }

    // TEST-BB-1120: a minimum for O negative counts only O negative units, sits beside an O minimum for the same product,
    // and refuses an Unknown Rh (end-user testing pass 105).
    [Fact]
    public async Task Minimum_ForORhNegative_CountsOnlyThoseUnits()
    {
        await using var c = _factory.Create();
        var product = new ProductType { ProductCode = $"RBN-{Guid.NewGuid():N}"[..12], Name = "Red cells" };
        c.ProductTypes.Add(product);
        await c.SaveChangesAsync();
        foreach (var rh in new[] { RhType.Negative, RhType.Positive, RhType.Positive })
        {
            c.BloodUnits.Add(new BloodUnit
            {
                UnitNumber = $"U-RBN-{Guid.NewGuid():N}"[..14], ProductTypeId = product.Id, Status = UnitStatus.Available,
                Abo = AboGroup.O, RhD = rh, ExpiresUtc = _factory.Clock.UtcNow.AddDays(30)
            });
        }

        await c.SaveChangesAsync();
        var service = new MinimumStockService(new EfRepository<MinimumStockLevel>(c), new EfRepository<ProductType>(c), new InventoryRepository(c), c);
        var neg = await service.SaveAsync(new SaveMinimumStockRequest(product.Id, AboGroup.O, 4, RhType.Negative));
        Assert.True(neg.Succeeded);
        Assert.Equal(1, neg.Value!.OnHand);
        Assert.Equal(3, neg.Value.Shortfall);
        Assert.Equal("O Neg", neg.Value.GroupText);
        Assert.True((await service.SaveAsync(new SaveMinimumStockRequest(product.Id, AboGroup.O, 2))).Succeeded);
        Assert.False((await service.SaveAsync(new SaveMinimumStockRequest(product.Id, AboGroup.O, 2, RhType.Unknown))).Succeeded);

        var rows = (await service.ListAsync()).Where(r => r.ProductTypeId == product.Id).ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal(3, rows.Single(r => r.RhD is null).OnHand);
        Assert.Equal(1, rows.Single(r => r.RhD == RhType.Negative).OnHand);
    }
}
