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

public class ChargeRuleAuthorizationTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public ChargeRuleAuthorizationTests(SqliteContextFactory factory) => _factory = factory;

    private ChargeRuleAdminService Rules(BloodBankDbContext c, IPermissionEvaluator? permissions = null)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new ChargeRuleAdminService(
            new EfRepository<ChargeRule>(c),
            new EfRepository<ChargeCode>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser, env),
            new ConfigurationHistoryWriter(c, _factory.Clock, _factory.CurrentUser, env),
            permissionEvaluator: permissions);
    }

    private static async Task<ChargeCode> SeedCodeAsync(BloodBankDbContext c)
    {
        var code = new ChargeCode
        {
            Code = $"R{Guid.NewGuid():N}"[..8].ToUpperInvariant(),
            Description = "Rule charge",
            DefaultAmount = 1m
        };
        c.ChargeCodes.Add(code);
        await c.SaveChangesAsync();
        return code;
    }

    [Fact]
    public async Task Create_WithoutAdminConfigEdit_IsRejected()
    {
        await using var c = _factory.Create();
        var code = await SeedCodeAsync(c);
        var key = $"K{Guid.NewGuid():N}"[..8];
        var request = new SaveChargeRuleRequest(BillingTriggerType.TestVerified, key, code.Id);

        var denied = await Rules(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigView))
            .CreateAsync(request);
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == ChargeRuleAuthorizationRule.CreateCode);
        Assert.False(await c.ChargeRules.AnyAsync(e => e.TriggerKey == key));

        var allowed = await Rules(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigEdit))
            .CreateAsync(request);
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(key, allowed.Value!.TriggerKey);
        Assert.True(allowed.Value.IsActive);
    }

    [Fact]
    public async Task Deactivate_WithoutAdminConfigActivate_IsRejected()
    {
        await using var c = _factory.Create();
        var code = await SeedCodeAsync(c);
        var created = await Rules(c).CreateAsync(
            new SaveChargeRuleRequest(BillingTriggerType.UnitIssued, $"K{Guid.NewGuid():N}"[..8], code.Id));
        Assert.True(created.Succeeded, created.Error);
        Assert.True(created.Value!.IsActive);

        var denied = await Rules(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigEdit))
            .SetActiveAsync(created.Value.Id, false);
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == ChargeRuleAuthorizationRule.DeactivateCode);
        Assert.True((await c.ChargeRules.SingleAsync(e => e.Id == created.Value.Id)).IsActive);

        var allowed = await Rules(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigActivate))
            .SetActiveAsync(created.Value.Id, false);
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.False(allowed.Value!.IsActive);
    }
}
