using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class BloodAttributeAuthorizationTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public BloodAttributeAuthorizationTests(SqliteContextFactory factory) => _factory = factory;

    private BloodAttributeAdminService Attrs(BloodBankDbContext c, IPermissionEvaluator? permissions = null)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new BloodAttributeAdminService(
            new EfRepository<BloodAttributeDefinition>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser, env),
            new ConfigurationHistoryWriter(c, _factory.Clock, _factory.CurrentUser, env),
            permissionEvaluator: permissions);
    }

    private static SaveBloodAttributeDefinitionRequest Request(string code) =>
        new(code, "Temp Kell", "anti-K", true, 1, "Catalog.");

    [Fact]
    public async Task Create_WithoutAdminConfigEdit_IsRejected()
    {
        await using var c = _factory.Create();
        var code = $"ZZ{Guid.NewGuid():N}"[..6];

        var denied = await Attrs(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigView))
            .CreateAsync(Request(code));
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == BloodAttributeAuthorizationRule.CreateCode);
        Assert.False(await c.BloodAttributeDefinitions.AnyAsync(d => d.Code == code));

        var allowed = await Attrs(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigEdit))
            .CreateAsync(Request(code));
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(code, allowed.Value!.Code);
    }

    [Fact]
    public async Task Activate_WithoutAdminConfigActivate_IsRejected()
    {
        await using var c = _factory.Create();
        var created = await Attrs(c).CreateAsync(Request($"ZZ{Guid.NewGuid():N}"[..6]));
        Assert.True(created.Succeeded, created.Error);

        var denied = await Attrs(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigEdit))
            .ActivateAsync(created.Value!.Id, "Go live.");
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == BloodAttributeAuthorizationRule.ActivateCode);
        Assert.False((await c.BloodAttributeDefinitions.SingleAsync(d => d.Id == created.Value.Id)).IsActive);

        var allowed = await Attrs(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigActivate))
            .ActivateAsync(created.Value.Id, "Go live.");
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.True(allowed.Value!.IsActive);
    }
}
