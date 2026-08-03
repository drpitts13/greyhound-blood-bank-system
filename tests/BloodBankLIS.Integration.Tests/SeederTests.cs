using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Rules;
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
            Assert.Equal(4, await verify.ProductTypes.CountAsync());
            Assert.True(await verify.ProductTypes.AnyAsync(p => p.ProductCode == "WB" && p.RequiresCrossmatch));
            Assert.Equal(3, await verify.InventoryLocations.CountAsync());
            Assert.Equal(1, await verify.Patients.CountAsync());
            Assert.Equal(3, await verify.BloodUnits.CountAsync());
            Assert.Equal(2, await verify.Encounters.CountAsync());
            Assert.Equal(4, await verify.Orders.CountAsync());

            Assert.True(await verify.ExceptionDefinitions.AnyAsync(e => e.RuleCode == AboCompatibilityRule.AboCode && !e.IsOverridable));
            Assert.True(await verify.ExceptionDefinitions.AnyAsync(e =>
                e.RuleCode == BloodAttributeCompatibilityRule.AntigenNegCode
                && e.IsOverridable
                && e.MinSecurityLevel == 2));
            Assert.True(await verify.ExceptionDefinitions.AnyAsync(e => e.RuleCode == CrossmatchValidityRule.Code && !e.IsOverridable));
            Assert.True(await verify.ExceptionDefinitions.AnyAsync(e => e.RuleCode == AntibodyHistoryCrossmatchRule.RuleCode && e.IsOverridable));

            // Seeding clinical/reference rows also produced audit events.
            Assert.True(await verify.AuditEvents.AnyAsync());

            Assert.True(await verify.IsbtProductCodes.CountAsync() >= 40);
            Assert.True(await verify.IsbtProductCodes.AnyAsync(p =>
                p.ProductDescriptionCode == "E0336"
                && p.Description.Contains("AS1")
                && p.StandardVersion == UsSupplierProductCodeSeed.StandardVersion));
            Assert.True(await verify.IsbtProductCodes.AnyAsync(p => p.ProductDescriptionCode == "E0206"));
            Assert.True(await verify.IsbtProductCodes.AnyAsync(p => p.ProductDescriptionCode == "E0701"));
            Assert.True(await verify.IsbtProductCodes.AnyAsync(p => p.ProductDescriptionCode == "E5165"));
        }
    }

    [Fact]
    public async Task Seed_UpsertsMissingProductCodes_WhenPlaceholderAlreadyPresent()
    {
        await using (var context = _factory.Create())
        {
            // Stale placeholder with a different StandardVersion than the US subset seed.
            if (!await context.IsbtProductCodes.AnyAsync(p =>
                    p.ProductDescriptionCode == "E0206"
                    && p.StandardVersion == "PLACEHOLDER-REQUIRES-ICCBBA"))
            {
                context.IsbtProductCodes.Add(new IsbtProductCode
                {
                    ProductDescriptionCode = "E0206",
                    Description = "PLACEHOLDER — Red Blood Cells",
                    ComponentClass = "RedBloodCells",
                    AttributesJson = "[]",
                    StandardVersion = "PLACEHOLDER-REQUIRES-ICCBBA",
                    IsPlaceholder = true
                });
                await context.SaveChangesAsync();
            }
        }

        await using (var context = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(context);
        }

        await using (var verify = _factory.Create())
        {
            Assert.True(await verify.IsbtProductCodes.CountAsync() >= 40);
            Assert.Equal(1, await verify.IsbtProductCodes.CountAsync(p => p.ProductDescriptionCode == "E0206"));
            var e0206 = await verify.IsbtProductCodes.SingleAsync(p => p.ProductDescriptionCode == "E0206");
            Assert.Equal("RED BLOOD CELLS|CPDA-1/450mL/refg|Irradiated", e0206.Description);
            Assert.Equal(UsSupplierProductCodeSeed.StandardVersion, e0206.StandardVersion);
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
