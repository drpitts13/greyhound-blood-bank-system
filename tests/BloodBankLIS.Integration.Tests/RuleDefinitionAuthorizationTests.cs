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

public class RuleDefinitionAuthorizationTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public RuleDefinitionAuthorizationTests(SqliteContextFactory factory) => _factory = factory;

    private RuleDefinitionAdminService Rules(BloodBankDbContext c, IPermissionEvaluator? permissions = null)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new RuleDefinitionAdminService(
            new EfRepository<RuleDefinition>(c),
            new EfRepository<TestDefinition>(c),
            new EfRepository<TestGrouper>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser, env),
            new ConfigurationHistoryWriter(c, _factory.Clock, _factory.CurrentUser, env),
            permissionEvaluator: permissions);
    }

    private static SaveRuleDefinitionRequest Request(string code) =>
        new(code, "Temp Weak D", null, RuleLevel.Test, 100, false,
            "test.code = 'ABORH' AND test.interpretation IN ('A Negative','B Negative','O Negative','AB Negative')",
            "addTest('WEAKD')",
            "Catalog.");

    [Fact]
    public async Task Create_WithoutAdminTestsManage_IsRejected()
    {
        await using var c = _factory.Create();
        var code = $"RD-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        var denied = await Rules(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigView))
            .CreateAsync(Request(code));
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == RuleDefinitionAuthorizationRule.CreateCode);
        Assert.False(await c.RuleDefinitions.AnyAsync(r => r.Code == code));

        var allowed = await Rules(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminTestsManage))
            .CreateAsync(Request(code));
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(code, allowed.Value!.Code);
    }

    [Fact]
    public async Task Activate_WithoutAdminConfigActivate_IsRejected()
    {
        await using var c = _factory.Create();
        var created = await Rules(c).CreateAsync(Request($"RD-{Guid.NewGuid():N}"[..12].ToUpperInvariant()));
        Assert.True(created.Succeeded, created.Error);

        var denied = await Rules(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminTestsManage))
            .ActivateAsync(created.Value!.Id, "Go live.");
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == RuleDefinitionAuthorizationRule.ActivateCode);
        Assert.False((await c.RuleDefinitions.SingleAsync(r => r.Id == created.Value.Id)).IsActive);

        var allowed = await Rules(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigActivate))
            .ActivateAsync(created.Value.Id, "Go live.");
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.True(allowed.Value!.IsActive);
    }
}
