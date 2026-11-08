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
}
