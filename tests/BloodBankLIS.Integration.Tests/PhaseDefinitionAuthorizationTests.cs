using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class PhaseDefinitionAuthorizationTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public PhaseDefinitionAuthorizationTests(SqliteContextFactory factory) => _factory = factory;

    private PhaseDefinitionAdminService Phases(BloodBankDbContext c, IPermissionEvaluator? permissions = null)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new PhaseDefinitionAdminService(
            new EfRepository<PhaseDefinition>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser, env),
            new ConfigurationHistoryWriter(c, _factory.Clock, _factory.CurrentUser, env),
            permissionEvaluator: permissions);
    }

    private static SavePhaseDefinitionRequest Request(string code) =>
        new(code, "Temp phase", 90, true, false, null, "Catalog.");

    [Fact]
    public async Task Create_WithoutAdminTestsManage_IsRejected()
    {
        await using var c = _factory.Create();
        var code = $"PH-{Guid.NewGuid():N}"[..12];

        var denied = await Phases(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigView))
            .CreateAsync(Request(code));
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == PhaseCatalogAuthorizationRule.CreateCode);
        Assert.False(await c.PhaseDefinitions.AnyAsync(p => p.Code == code));

        var allowed = await Phases(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminTestsManage))
            .CreateAsync(Request(code));
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(code, allowed.Value!.Code);
    }

    [Fact]
    public async Task Activate_WithoutAdminConfigActivate_IsRejected()
    {
        await using var c = _factory.Create();
        var created = await Phases(c).CreateAsync(Request($"PH-{Guid.NewGuid():N}"[..12]));
        Assert.True(created.Succeeded, created.Error);

        var denied = await Phases(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminTestsManage))
            .ActivateAsync(created.Value!.Id, "Go live.");
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == PhaseCatalogAuthorizationRule.ActivateCode);
        Assert.False((await c.PhaseDefinitions.SingleAsync(p => p.Id == created.Value.Id)).IsActive);

        var allowed = await Phases(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigActivate))
            .ActivateAsync(created.Value.Id, "Go live.");
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.True(allowed.Value!.IsActive);
    }
}
