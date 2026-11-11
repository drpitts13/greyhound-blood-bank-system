using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-601: the demo has one shipment of two units to St. Mary's Hospital (SHIP-DEMO-01), added once.
public class ShipOutDemoTests
{
    [Fact]
    public async Task Demo_HasOneShipmentOfTwoUnits()
    {
        using var factory = new SqliteContextFactory();
        await using (var db = factory.Create())
        {
            await DatabaseSeeder.SeedAsync(db);
            await DatabaseSeeder.SeedAsync(db);
        }

        await using var v = factory.Create();
        var shipped = await v.BloodUnits.AsNoTracking().Where(u => u.Status == UnitStatus.ShippedOut).ToListAsync();
        Assert.Equal(["W000123SHP0001", "W000123SHP0002"], shipped.Select(u => u.UnitNumber).Order());
        Assert.All(shipped, u => Assert.Equal(("St. Mary's Hospital", "SHIP-DEMO-01"), (u.ShippedToFacility, u.OutboundShipmentReference)));
    }

    // TEST-BB-921: the demo has one recalled unit to pull, with the status row a recall writes, added once.
    [Fact]
    public async Task Demo_HasOneRecalledUnitToPull()
    {
        using var factory = new SqliteContextFactory();
        await using (var db = factory.Create())
        {
            await DatabaseSeeder.SeedAsync(db);
            await DatabaseSeeder.SeedAsync(db);
        }

        await using var v = factory.Create();
        var recalled = Assert.Single(await v.BloodUnits.AsNoTracking().Where(u => u.Status == UnitStatus.Recalled).ToListAsync());
        Assert.Equal("W000123RCL0001", recalled.UnitNumber);
        Assert.Contains("R-DEMO-02", recalled.RecallReason);
        Assert.Equal(1, await v.InventoryStatusHistory.CountAsync(h => h.BloodProductId == recalled.Id && h.ToStatus == UnitStatus.Recalled));
    }

    // TEST-BB-910: the demo has one return of two units for recall R-DEMO-01, each with the status row a return writes,
    // added once.
    [Fact]
    public async Task Demo_HasOneSupplierReturnOfTwoUnits()
    {
        using var factory = new SqliteContextFactory();
        await using (var db = factory.Create())
        {
            await DatabaseSeeder.SeedAsync(db);
            await DatabaseSeeder.SeedAsync(db);
        }

        await using var v = factory.Create();
        var returned = await v.BloodUnits.AsNoTracking().Where(u => u.Status == UnitStatus.ReturnedToSupplier).ToListAsync();
        Assert.Equal(["W000123RTS0001", "W000123RTS0002"], returned.Select(u => u.UnitNumber).Order());
        Assert.All(returned, u => Assert.Equal("Supplier recall R-DEMO-01", u.SupplierReturnReason));
        var ids = returned.Select(u => u.Id).ToList();
        Assert.Equal(2, await v.InventoryStatusHistory.CountAsync(h => ids.Contains(h.BloodProductId) && h.ToStatus == UnitStatus.ReturnedToSupplier));
    }
}
