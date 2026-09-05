using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class ExpirationModificationCodeAuthorizationTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public ExpirationModificationCodeAuthorizationTests(SqliteContextFactory factory) => _factory = factory;

    private ExpirationModificationCodeAdminService Codes(BloodBankDbContext c, IPermissionEvaluator? permissions = null)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new ExpirationModificationCodeAdminService(
            new EfRepository<ExpirationModificationCode>(c),
            new EfRepository<ModificationRule>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser, env),
            new ConfigurationHistoryWriter(c, _factory.Clock, _factory.CurrentUser, env),
            permissionEvaluator: permissions);
    }

    private static SaveExpirationModificationCodeRequest Request(string code) =>
        new(code, 24, ExpirationOffsetUnit.Hours, ExpirationRelativeTo.ModificationDateTime, "Temp offset", "Catalog.");

    [Fact]
    public async Task Create_WithoutAdminModificationRulesManage_IsRejected()
    {
        await using var c = _factory.Create();
        var code = $"X{Guid.NewGuid():N}"[..8].ToUpperInvariant();

        var denied = await Codes(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigView))
            .CreateAsync(Request(code));
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == ExpirationModificationCodeAuthorizationRule.CreateCode);
        Assert.False(await c.ExpirationModificationCodes.AnyAsync(e => e.Code == code));

        var allowed = await Codes(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminModificationRulesManage))
            .CreateAsync(Request(code));
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(code, allowed.Value!.Code);
    }

    [Fact]
    public async Task Activate_WithoutAdminConfigActivate_IsRejected()
    {
        await using var c = _factory.Create();
        var created = await Codes(c).CreateAsync(Request($"X{Guid.NewGuid():N}"[..8].ToUpperInvariant()));
        Assert.True(created.Succeeded, created.Error);

        var denied = await Codes(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminModificationRulesManage))
            .ActivateAsync(created.Value!.Id, "Go live.");
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == ExpirationModificationCodeAuthorizationRule.ActivateCode);
        Assert.False((await c.ExpirationModificationCodes.SingleAsync(e => e.Id == created.Value.Id)).IsActive);

        var allowed = await Codes(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigActivate))
            .ActivateAsync(created.Value.Id, "Go live.");
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.True(allowed.Value!.IsActive);
    }

    [Fact]
    public async Task CreateAndUpdate_WriteConfigure()
    {
        await using var c = _factory.Create();
        var code = $"X{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var svc = Codes(c);

        var created = await svc.CreateAsync(Request(code));
        Assert.True(created.Succeeded, created.Error ?? created.Evaluation?.HardStops.FirstOrDefault()?.Message);
        Assert.True(await c.AuditEvents.AnyAsync(a =>
            a.EntityType == nameof(ExpirationModificationCode)
            && a.EntityId == created.Value!.Id
            && a.EventType == AuditEventType.Configure));

        var updated = await svc.UpdateAsync(
            created.Value!.Id,
            Request(code) with { OffsetAmount = 12, ChangeReason = "Shorten modified-unit shelf life." });
        Assert.True(updated.Succeeded, updated.Error ?? updated.Evaluation?.HardStops.FirstOrDefault()?.Message);

        var events = await c.AuditEvents
            .Where(a => a.EntityType == nameof(ExpirationModificationCode) && a.EntityId == created.Value.Id)
            .ToListAsync();
        Assert.Equal(2, events.Count(a => a.EventType == AuditEventType.Configure));
    }
}
