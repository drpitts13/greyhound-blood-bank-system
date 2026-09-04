using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class ReflexRuleAuthorizationTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public ReflexRuleAuthorizationTests(SqliteContextFactory factory) => _factory = factory;

    private ReflexRuleAdminService Reflex(BloodBankDbContext c, IPermissionEvaluator? permissions = null)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new ReflexRuleAdminService(
            new EfRepository<ReflexRule>(c),
            new EfRepository<TestDefinition>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser, env),
            new ConfigurationHistoryWriter(c, _factory.Clock, _factory.CurrentUser, env),
            permissionEvaluator: permissions);
    }

    private static async Task<(string trigger, string reflex, SaveReflexRuleRequest request)> SeedPairAsync(
        BloodBankDbContext c)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var trigger = $"T{suffix}";
        var reflex = $"R{suffix}";
        c.TestDefinitions.AddRange(
            new TestDefinition { Code = trigger, Name = "Trigger", IsActive = true, IsDraft = false },
            new TestDefinition { Code = reflex, Name = "Reflex", IsActive = true, IsDraft = false });
        await c.SaveChangesAsync();
        return (trigger, reflex, new SaveReflexRuleRequest(
            $"RULE-{suffix}",
            "Trigger to reflex",
            trigger,
            "Positive",
            reflex,
            null));
    }

    [Fact]
    public async Task Create_WithoutAdminTestsManage_IsRejected()
    {
        await using var c = _factory.Create();
        var (_, _, request) = await SeedPairAsync(c);

        var denied = await Reflex(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigView))
            .CreateAsync(request);
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == ReflexRuleAuthorizationRule.CreateCode);
        Assert.False(await c.ReflexRules.AnyAsync(r => r.Code == request.Code));

        var allowed = await Reflex(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminTestsManage))
            .CreateAsync(request);
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(request.Code, allowed.Value!.Code);
    }

    [Fact]
    public async Task Activate_WithoutAdminConfigActivate_IsRejected()
    {
        await using var c = _factory.Create();
        var (_, _, request) = await SeedPairAsync(c);
        var created = await Reflex(c).CreateAsync(request);
        Assert.True(created.Succeeded, created.Error);

        var denied = await Reflex(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminTestsManage))
            .ActivateAsync(created.Value!.Id, "Go live.");
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == ReflexRuleAuthorizationRule.ActivateCode);
        Assert.False((await c.ReflexRules.SingleAsync(r => r.Id == created.Value.Id)).IsActive);

        var allowed = await Reflex(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigActivate))
            .ActivateAsync(created.Value.Id, "Go live.");
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.True(allowed.Value!.IsActive);
    }
}
