using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class ChargeCodeAuthorizationTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public ChargeCodeAuthorizationTests(SqliteContextFactory factory) => _factory = factory;

    private ChargeCodeAdminService Codes(BloodBankDbContext c, IPermissionEvaluator? permissions = null)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new ChargeCodeAdminService(
            new EfRepository<ChargeCode>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser, env),
            new ConfigurationHistoryWriter(c, _factory.Clock, _factory.CurrentUser, env),
            permissionEvaluator: permissions);
    }

    private static SaveChargeCodeRequest Request(string code) =>
        new(code, "Temp charge", 1m, null);

    [Fact]
    public async Task Create_WithoutAdminConfigEdit_IsRejected()
    {
        await using var c = _factory.Create();
        var code = $"C{Guid.NewGuid():N}"[..8].ToUpperInvariant();

        var denied = await Codes(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigView))
            .CreateAsync(Request(code));
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == ChargeCodeAuthorizationRule.CreateCode);
        Assert.False(await c.ChargeCodes.AnyAsync(e => e.Code == code));

        var allowed = await Codes(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigEdit))
            .CreateAsync(Request(code));
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(code, allowed.Value!.Code);
    }

    [Fact]
    public async Task Deactivate_WithoutAdminConfigActivate_IsRejected()
    {
        await using var c = _factory.Create();
        var created = await Codes(c).CreateAsync(Request($"C{Guid.NewGuid():N}"[..8].ToUpperInvariant()));
        Assert.True(created.Succeeded, created.Error);
        Assert.True(created.Value!.IsActive);

        var denied = await Codes(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigEdit))
            .SetActiveAsync(created.Value.Id, false);
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == ChargeCodeAuthorizationRule.DeactivateCode);
        Assert.True((await c.ChargeCodes.SingleAsync(e => e.Id == created.Value.Id)).IsActive);

        var allowed = await Codes(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigActivate))
            .SetActiveAsync(created.Value.Id, false);
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.False(allowed.Value!.IsActive);
    }
}
