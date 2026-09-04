using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class SubtestDefinitionAuthorizationTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public SubtestDefinitionAuthorizationTests(SqliteContextFactory factory) => _factory = factory;

    private SubtestDefinitionAdminService Subtests(BloodBankDbContext c, IPermissionEvaluator? permissions = null)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new SubtestDefinitionAdminService(
            new EfRepository<SubtestDefinition>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser, env),
            new ConfigurationHistoryWriter(c, _factory.Clock, _factory.CurrentUser, env),
            permissionEvaluator: permissions);
    }

    private static SaveSubtestDefinitionRequest Request(string code) =>
        new(code, "Temp subtest", SubtestResultType.FreeText, null, "Catalog.");

    [Fact]
    public async Task Create_WithoutAdminTestsManage_IsRejected()
    {
        await using var c = _factory.Create();
        var code = $"ST-{Guid.NewGuid():N}"[..12];

        var denied = await Subtests(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigView))
            .CreateAsync(Request(code));
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == SubtestCatalogAuthorizationRule.CreateCode);
        Assert.False(await c.SubtestDefinitions.AnyAsync(s => s.Code == code));

        var allowed = await Subtests(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminTestsManage))
            .CreateAsync(Request(code));
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(code, allowed.Value!.Code);
    }

    [Fact]
    public async Task Activate_WithoutAdminConfigActivate_IsRejected()
    {
        await using var c = _factory.Create();
        var created = await Subtests(c).CreateAsync(Request($"ST-{Guid.NewGuid():N}"[..12]));
        Assert.True(created.Succeeded, created.Error);

        var denied = await Subtests(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminTestsManage))
            .ActivateAsync(created.Value!.Id, "Go live.");
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == SubtestCatalogAuthorizationRule.ActivateCode);
        Assert.False((await c.SubtestDefinitions.SingleAsync(s => s.Id == created.Value.Id)).IsActive);

        var allowed = await Subtests(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigActivate))
            .ActivateAsync(created.Value.Id, "Go live.");
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.True(allowed.Value!.IsActive);
    }
}
