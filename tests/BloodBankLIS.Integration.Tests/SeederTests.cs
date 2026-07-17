using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class SeederTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public SeederTests(SqliteContextFactory factory) => _factory = factory;

    [Fact]
    public async Task Seed_PopulatesReferenceAndDemoData_AndIsIdempotent()
    {
        await using (var context = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(context);
        }

        await using (var context = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(context); // second run should not duplicate
        }

        await using (var verify = _factory.Create())
        {
            Assert.Equal(3, await verify.ProductTypes.CountAsync());
            Assert.Equal(3, await verify.InventoryLocations.CountAsync());
            Assert.Equal(1, await verify.Patients.CountAsync());
            Assert.Equal(3, await verify.BloodUnits.CountAsync());
            Assert.Equal(2, await verify.Encounters.CountAsync());
            Assert.Equal(4, await verify.Orders.CountAsync());

            // Seeding clinical/reference rows also produced audit events.
            Assert.True(await verify.AuditEvents.AnyAsync());
        }
    }

    [Fact]
    public async Task Seed_AddsRequiredReferenceCodes_WhenTableHasPartialMigrationData()
    {
        await using (var context = _factory.Create())
        {
            context.OrderingLocations.Add(new OrderingLocation
            {
                Code = "CUSTOM",
                Name = "Custom location from SQLite migration",
                IsActive = true
            });
            await context.SaveChangesAsync();
        }

        await using (var context = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(context);
        }

        await using (var verify = _factory.Create())
        {
            Assert.True(await verify.OrderingLocations.AnyAsync(l => l.Code == "CUSTOM"));
            Assert.True(await verify.OrderingLocations.AnyAsync(l => l.Code == "OR"));
            Assert.True(await verify.OrderingLocations.AnyAsync(l => l.Code == "ED"));
            Assert.Equal(1, await verify.Patients.CountAsync());
        }
    }
}
