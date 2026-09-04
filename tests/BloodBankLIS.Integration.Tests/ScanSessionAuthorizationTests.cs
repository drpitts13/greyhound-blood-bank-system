using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Isbt128;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Isbt128;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class ScanSessionAuthorizationTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public ScanSessionAuthorizationTests(SqliteContextFactory factory) => _factory = factory;

    private ScanSessionService Sessions(BloodBankDbContext context, IPermissionEvaluator? permissions = null)
    {
        var inventoryRepo = new InventoryRepository(context);
        var lookups = new IsbtLookupCatalog(
            new EfRepository<IsbtAboRhdCode>(context),
            new EfRepository<IsbtProductCode>(context));
        var inventory = new InventoryService(
            inventoryRepo,
            new EfRepository<UnitBloodAttribute>(context),
            new EfRepository<BloodAttributeDefinition>(context),
            lookups,
            context,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(context, _factory.Clock, _factory.CurrentUser));

        return new ScanSessionService(
            new EfRepository<BloodComponentScanSession>(context),
            new EfRepository<BloodComponentScanSessionLine>(context),
            lookups,
            new PlaceholderDinCheckCharacterValidator(),
            inventoryRepo,
            inventory,
            context,
            _factory.Clock,
            _factory.CurrentUser,
            permissions: permissions);
    }

    [Fact]
    public async Task Start_WithoutInventoryReceive_IsRejected()
    {
        await using var c = _factory.Create();

        var denied = await Sessions(c, new FixedPermissionEvaluator(1, PermissionCodes.InventoryRelease))
            .StartAsync(new StartScanSessionRequest());
        Assert.False(denied.Succeeded);
        Assert.Contains("inventory.receive", denied.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(await c.BloodComponentScanSessions.AnyAsync());

        var allowed = await Sessions(c, new FixedPermissionEvaluator(1, PermissionCodes.InventoryReceive))
            .StartAsync(new StartScanSessionRequest());
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.True(await c.BloodComponentScanSessions.AnyAsync());
    }

    [Fact]
    public async Task AddScan_WithoutInventoryReceive_IsRejected()
    {
        await using var c = _factory.Create();
        var started = await Sessions(c).StartAsync(new StartScanSessionRequest());
        Assert.True(started.Succeeded, started.Error);

        var denied = await Sessions(c, new FixedPermissionEvaluator(1, PermissionCodes.InventoryRelease))
            .AddScanAsync(new AddScanRequest(started.Value!.SessionKey, "=W1234"));
        Assert.False(denied.Succeeded);
        Assert.Contains("inventory.receive", denied.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(await c.BloodComponentScanSessionLines.AnyAsync());
    }
}
