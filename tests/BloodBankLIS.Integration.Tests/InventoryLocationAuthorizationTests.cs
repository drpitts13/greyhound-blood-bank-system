using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class InventoryLocationAuthorizationTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public InventoryLocationAuthorizationTests(SqliteContextFactory factory) => _factory = factory;

    private InventoryLocationAdminService Locations(BloodBankDbContext c, IPermissionEvaluator? permissions = null)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new InventoryLocationAdminService(
            new EfRepository<InventoryLocation>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser, env),
            new ConfigurationHistoryWriter(c, _factory.Clock, _factory.CurrentUser, env),
            permissionEvaluator: permissions);
    }

    private static SaveInventoryLocationRequest Request(string code) =>
        new(code, "Temp fridge", LocationType.Refrigerator);

    [Fact]
    public async Task Create_WithoutAdminConfigEdit_IsRejected()
    {
        await using var c = _factory.Create();
        var code = $"F{Guid.NewGuid():N}"[..8].ToUpperInvariant();

        var denied = await Locations(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigView))
            .CreateAsync(Request(code));
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == InventoryLocationAuthorizationRule.CreateCode);
        Assert.False(await c.InventoryLocations.AnyAsync(e => e.Code == code));

        var allowed = await Locations(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigEdit))
            .CreateAsync(Request(code));
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(code, allowed.Value!.Code);
        Assert.True(allowed.Value.IsActive);
    }

    [Fact]
    public async Task Deactivate_WithoutAdminConfigActivate_IsRejected()
    {
        await using var c = _factory.Create();
        var created = await Locations(c).CreateAsync(Request($"F{Guid.NewGuid():N}"[..8].ToUpperInvariant()));
        Assert.True(created.Succeeded, created.Error);
        Assert.True(created.Value!.IsActive);

        var denied = await Locations(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigEdit))
            .SetActiveAsync(created.Value.Id, false);
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == InventoryLocationAuthorizationRule.DeactivateCode);
        Assert.True((await c.InventoryLocations.SingleAsync(e => e.Id == created.Value.Id)).IsActive);

        var allowed = await Locations(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigActivate))
            .SetActiveAsync(created.Value.Id, false);
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.False(allowed.Value!.IsActive);
    }
}
