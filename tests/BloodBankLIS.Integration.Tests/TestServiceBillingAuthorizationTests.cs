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

public class TestServiceBillingAuthorizationTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public TestServiceBillingAuthorizationTests(SqliteContextFactory factory) => _factory = factory;

    private TestServiceBillingAdminService Rows(BloodBankDbContext c, IPermissionEvaluator? permissions = null)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new TestServiceBillingAdminService(
            new EfRepository<TestServiceBilling>(c),
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
            Code = $"S{Guid.NewGuid():N}"[..8].ToUpperInvariant(),
            Description = "Test service charge",
            DefaultAmount = 1m
        };
        c.ChargeCodes.Add(code);
        await c.SaveChangesAsync();
        return code;
    }

    private static string UniqueTest() => $"T{Guid.NewGuid():N}"[..8].ToUpperInvariant();

    [Fact]
    public async Task Create_WithoutAdminConfigEdit_IsRejected()
    {
        await using var c = _factory.Create();
        var code = await SeedCodeAsync(c);
        var test = UniqueTest();
        var request = new SaveTestServiceBillingRequest(code.Id, null, BillingTriggerType.TestVerified, test);

        var denied = await Rows(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigView))
            .CreateAsync(request);
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == TestServiceBillingAuthorizationRule.CreateCode);
        Assert.False(await c.TestServiceBillings.AnyAsync(e => e.TestCode == test));

        var allowed = await Rows(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigEdit))
            .CreateAsync(request);
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(test, allowed.Value!.TestCode);
        Assert.True(allowed.Value.IsActive);
    }

    [Fact]
    public async Task Deactivate_WithoutAdminConfigActivate_IsRejected()
    {
        await using var c = _factory.Create();
        var code = await SeedCodeAsync(c);
        var created = await Rows(c).CreateAsync(
            new SaveTestServiceBillingRequest(code.Id, null, BillingTriggerType.TestVerified, UniqueTest()));
        Assert.True(created.Succeeded, created.Error);
        Assert.True(created.Value!.IsActive);

        var denied = await Rows(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigEdit))
            .SetActiveAsync(created.Value.Id, false);
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == TestServiceBillingAuthorizationRule.DeactivateCode);
        Assert.True((await c.TestServiceBillings.SingleAsync(e => e.Id == created.Value.Id)).IsActive);

        var allowed = await Rows(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigActivate))
            .SetActiveAsync(created.Value.Id, false);
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.False(allowed.Value!.IsActive);
    }
}
