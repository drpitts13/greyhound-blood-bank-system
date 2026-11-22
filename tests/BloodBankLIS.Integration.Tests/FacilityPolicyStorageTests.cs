using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-398: an existing database is given every Facility Policies setting it lacks, at the catalog default and with
// configuration history, without touching a value the facility already stored.
public class FacilityPolicyStorageTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Seeding_AnUpgradedDatabase_StoresEveryMissingPolicy_AtItsDefault_WithHistory()
    {
        // Like an upgraded database: the settings table is not empty (the fixture stores the second verifier off and
        // this adds one more facility choice), so the full settings seed is skipped.
        await using (var c = _factory.Create())
        {
            c.SystemSettings.Add(new SystemSetting { Key = FacilityPolicyKeys.InTransitDueHours, Value = "6", Category = "Transfusion" });
            await c.SaveChangesAsync();
        }

        await using (var c = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(c);
        }

        await using var verify = _factory.Create();
        var settings = await verify.SystemSettings.AsNoTracking().ToListAsync();
        foreach (var definition in FacilityPolicyCatalog.All)
        {
            Assert.True(settings.Any(s => s.Key == definition.Key), $"{definition.Key} was not stored.");
        }

        // Values the facility already chose are kept.
        Assert.Equal("false", settings.Single(s => s.Key == FacilityPolicyKeys.RequireSecondVerifier).Value);
        Assert.Equal("6", settings.Single(s => s.Key == FacilityPolicyKeys.InTransitDueHours).Value);

        // A setting added only by this step carries the catalog default and a history row saying why.
        var scanOnly = settings.Single(s => s.Key == FacilityPolicyKeys.RequireSpecimenReceiptByScan);
        Assert.Equal(FacilityPolicyCatalog.Find(FacilityPolicyKeys.RequireSpecimenReceiptByScan)!.DefaultValue, scanOnly.Value);
        var history = await verify.ConfigurationChangeHistory.AsNoTracking()
            .SingleAsync(h => h.EntityType == nameof(SystemSetting) && h.EntityId == scanOnly.Id);
        Assert.Equal(ConfigChangeAction.Create, history.Action);
        Assert.Equal("system", history.ChangedBy);
        Assert.Contains("was not stored", history.ChangeReason);
        var keptId = settings.Single(s => s.Key == FacilityPolicyKeys.RequireSecondVerifier).Id;
        Assert.False(await verify.ConfigurationChangeHistory.AnyAsync(h => h.EntityType == nameof(SystemSetting) && h.EntityId == keptId));
    }

    [Fact]
    public async Task Seeding_Twice_AddsNothingMore()
    {
        await using (var c = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(c);
        }

        int settingsCount;
        int historyCount;
        await using (var c = _factory.Create())
        {
            settingsCount = await c.SystemSettings.CountAsync();
            historyCount = await c.ConfigurationChangeHistory.CountAsync();
            await DatabaseSeeder.SeedAsync(c);
        }

        await using var verify = _factory.Create();
        Assert.Equal(settingsCount, await verify.SystemSettings.CountAsync());
        Assert.Equal(historyCount, await verify.ConfigurationChangeHistory.CountAsync());
    }
}
