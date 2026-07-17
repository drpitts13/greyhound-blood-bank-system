using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Audit;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class InventoryServiceTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public InventoryServiceTests(SqliteContextFactory factory) => _factory = factory;

    private InventoryService CreateService(BloodBankDbContext context)
    {
        var repository = new InventoryRepository(context);
        var audit = new AuditWriter(context, _factory.Clock, _factory.CurrentUser);
        return new InventoryService(
            repository,
            new EfRepository<UnitBloodAttribute>(context),
            new EfRepository<BloodAttributeDefinition>(context),
            context,
            _factory.Clock,
            _factory.CurrentUser,
            audit);
    }

    private async Task<long> EnsureProductTypeAsync()
    {
        await using var context = _factory.Create();
        var existing = await context.ProductTypes.FirstOrDefaultAsync(t => t.ProductCode == "RBC-TEST");
        if (existing is not null)
        {
            return existing.Id;
        }

        var type = new ProductType
        {
            ProductCode = "RBC-TEST",
            Name = "Test RBC",
            ComponentClass = ComponentClass.RedBloodCells,
            RequiresCrossmatch = true
        };
        context.ProductTypes.Add(type);
        await context.SaveChangesAsync();
        return type.Id;
    }

    private ReceiveUnitRequest NewUnitRequest(string unitNumber, long productTypeId, DateTime? expires = null) =>
        new(unitNumber, productTypeId, AboGroup.O, RhType.Positive,
            expires ?? _factory.Clock.UtcNow.AddDays(30));

    [Fact]
    public async Task ReceiveUnit_CreatesQuarantineUnit_WithInitialHistory()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;

        await using (var context = _factory.Create())
        {
            var service = CreateService(context);
            var result = await service.ReceiveUnitAsync(NewUnitRequest("U-INTAKE-1", productTypeId));

            Assert.True(result.Succeeded);
            Assert.Equal(UnitStatus.Quarantine, result.Unit!.Status);
            unitId = result.Unit.Id;
        }

        await using (var verify = _factory.Create())
        {
            var history = await verify.InventoryStatusHistory.Where(h => h.BloodProductId == unitId).ToListAsync();
            var initial = Assert.Single(history);
            Assert.Null(initial.FromStatus);
            Assert.Equal(UnitStatus.Quarantine, initial.ToStatus);
        }
    }

    [Fact]
    public async Task ReceiveUnit_DuplicateUnitNumber_Fails()
    {
        var productTypeId = await EnsureProductTypeAsync();

        await using var context = _factory.Create();
        var service = CreateService(context);
        await service.ReceiveUnitAsync(NewUnitRequest("U-DUP", productTypeId));

        var second = await service.ReceiveUnitAsync(NewUnitRequest("U-DUP", productTypeId));
        Assert.False(second.Succeeded);
        Assert.Contains("already exists", second.Error);
    }

    [Fact]
    public async Task ReceiveUnit_PastExpiration_Fails()
    {
        var productTypeId = await EnsureProductTypeAsync();

        await using var context = _factory.Create();
        var service = CreateService(context);
        var result = await service.ReceiveUnitAsync(
            NewUnitRequest("U-PASTEXP", productTypeId, _factory.Clock.UtcNow.AddHours(-1)));

        Assert.False(result.Succeeded);
        Assert.Contains("future", result.Error);
    }

    [Fact]
    public async Task Release_QuarantineToAvailable_AppendsHistory()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;

        await using (var context = _factory.Create())
        {
            var service = CreateService(context);
            var received = await service.ReceiveUnitAsync(NewUnitRequest("U-REL", productTypeId));
            unitId = received.Unit!.Id;
        }

        await using (var context = _factory.Create())
        {
            var service = CreateService(context);
            var result = await service.ReleaseFromQuarantineAsync(unitId);
            Assert.True(result.Succeeded);
            Assert.Equal(UnitStatus.Available, result.Unit!.Status);
        }

        await using (var verify = _factory.Create())
        {
            var history = await verify.InventoryStatusHistory.Where(h => h.BloodProductId == unitId).ToListAsync();
            Assert.Equal(2, history.Count);
            Assert.Contains(history, h => h.FromStatus == UnitStatus.Quarantine && h.ToStatus == UnitStatus.Available);
        }
    }

    [Fact]
    public async Task Discard_WithoutReason_Fails()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-DISC-NR", productTypeId))).Unit!.Id;
        }

        await using (var context = _factory.Create())
        {
            var result = await CreateService(context).DiscardAsync(unitId, "  ");
            Assert.False(result.Succeeded);
            Assert.Contains("reason is required", result.Error);
        }
    }

    [Fact]
    public async Task Discard_SetsStatus_AndWritesDiscardAudit()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-DISC", productTypeId))).Unit!.Id;
        }

        await using (var context = _factory.Create())
        {
            var result = await CreateService(context).DiscardAsync(unitId, "Bag integrity compromised");
            Assert.True(result.Succeeded);
            Assert.Equal(UnitStatus.Discarded, result.Unit!.Status);
        }

        await using (var verify = _factory.Create())
        {
            var discardAudit = await verify.AuditEvents
                .Where(a => a.EntityType == nameof(BloodUnit) && a.EntityId == unitId && a.EventType == AuditEventType.Discard)
                .SingleAsync();
            Assert.Equal("Bag integrity compromised", discardAudit.Reason);
        }
    }

    [Fact]
    public async Task Discard_TransfusedUnit_IsBlockedByTransitionGuard()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;

        await using (var context = _factory.Create())
        {
            var unit = new BloodUnit
            {
                UnitNumber = "U-TX",
                ProductTypeId = productTypeId,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                ExpiresUtc = _factory.Clock.UtcNow.AddDays(10),
                Status = UnitStatus.Transfused
            };
            context.BloodUnits.Add(unit);
            await context.SaveChangesAsync();
            unitId = unit.Id;
        }

        await using (var context = _factory.Create())
        {
            var result = await CreateService(context).DiscardAsync(unitId, "attempt");
            Assert.False(result.Succeeded);
            Assert.NotNull(result.Evaluation);
            Assert.True(result.Evaluation!.IsHardStopped);
        }
    }

    [Fact]
    public async Task Transfer_ChangesLocation_AndAppendsHistory()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        long toLocationId;

        await using (var context = _factory.Create())
        {
            var location = new InventoryLocation { Code = "LOC-XFER", Name = "Transfer Target", LocationType = LocationType.Refrigerator };
            context.InventoryLocations.Add(location);
            await context.SaveChangesAsync();
            toLocationId = location.Id;

            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-XFER", productTypeId))).Unit!.Id;
        }

        await using (var context = _factory.Create())
        {
            var result = await CreateService(context).TransferAsync(unitId, toLocationId, "Move to issue fridge");
            Assert.True(result.Succeeded);
            Assert.Equal(toLocationId, result.Unit!.CurrentLocationId);
        }

        await using (var verify = _factory.Create())
        {
            var history = await verify.InventoryStatusHistory.Where(h => h.BloodProductId == unitId).ToListAsync();
            Assert.Contains(history, h => h.ToLocationId == toLocationId && h.FromStatus == h.ToStatus);
        }
    }

    [Fact]
    public async Task ExpireDueUnits_MovesOnlyPastDueUnits()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long pastDueId;
        long futureId;

        await using (var context = _factory.Create())
        {
            var pastDue = new BloodUnit
            {
                UnitNumber = "U-PASTDUE",
                ProductTypeId = productTypeId,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                ExpiresUtc = _factory.Clock.UtcNow.AddHours(-1),
                Status = UnitStatus.Available
            };
            var future = new BloodUnit
            {
                UnitNumber = "U-FUTURE",
                ProductTypeId = productTypeId,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                ExpiresUtc = _factory.Clock.UtcNow.AddDays(5),
                Status = UnitStatus.Available
            };
            context.BloodUnits.AddRange(pastDue, future);
            await context.SaveChangesAsync();
            pastDueId = pastDue.Id;
            futureId = future.Id;
        }

        await using (var context = _factory.Create())
        {
            var expired = await CreateService(context).ExpireDueUnitsAsync();
            Assert.True(expired >= 1);
        }

        await using (var verify = _factory.Create())
        {
            Assert.Equal(UnitStatus.Expired, (await verify.BloodUnits.FindAsync(pastDueId))!.Status);
            Assert.Equal(UnitStatus.Available, (await verify.BloodUnits.FindAsync(futureId))!.Status);
        }
    }

    [Fact]
    public async Task Search_FiltersByStatus()
    {
        var productTypeId = await EnsureProductTypeAsync();

        await using (var context = _factory.Create())
        {
            var service = CreateService(context);
            await service.ReceiveUnitAsync(NewUnitRequest("U-SEARCH-Q", productTypeId)); // stays Quarantine
        }

        await using (var context = _factory.Create())
        {
            var service = CreateService(context);
            var quarantined = await service.SearchAsync(new InventorySearchCriteria(Status: UnitStatus.Quarantine));
            Assert.Contains(quarantined, u => u.UnitNumber == "U-SEARCH-Q");
            Assert.All(quarantined, u => Assert.Equal(UnitStatus.Quarantine, u.Status));
        }
    }
}
