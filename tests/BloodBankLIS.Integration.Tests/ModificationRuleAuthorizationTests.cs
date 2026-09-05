using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class ModificationRuleAuthorizationTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public ModificationRuleAuthorizationTests(SqliteContextFactory factory) => _factory = factory;

    private ModificationRuleAdminService Rules(BloodBankDbContext c, IPermissionEvaluator? permissions = null)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new ModificationRuleAdminService(
            new EfRepository<ModificationRule>(c),
            new EfRepository<ProductType>(c),
            new EfRepository<ExpirationModificationCode>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser, env),
            new ConfigurationHistoryWriter(c, _factory.Clock, _factory.CurrentUser, env),
            permissionEvaluator: permissions);
    }

    private static async Task<(long SourceId, long TargetId, long ExpId, string ModCode)> SeedAsync(BloodBankDbContext c)
    {
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var source = new ProductType { ProductCode = $"MRS-{suffix}", Name = "Source", IsActive = true };
        var target = new ProductType { ProductCode = $"MRT-{suffix}", Name = "Target", IsActive = true };
        var exp = new ExpirationModificationCode
        {
            Code = $"E{suffix}",
            OffsetAmount = 24,
            OffsetUnit = ExpirationOffsetUnit.Hours,
            RelativeTo = ExpirationRelativeTo.ModificationDateTime,
            IsActive = true,
            Version = 1
        };
        c.ProductTypes.AddRange(source, target);
        c.ExpirationModificationCodes.Add(exp);
        await c.SaveChangesAsync();
        return (source.Id, target.Id, exp.Id, $"MR{suffix}");
    }

    private static SaveModificationRuleRequest Request(long sourceId, long targetId, long expId, string code) =>
        new(code, sourceId, ModificationType.Irradiate, targetId, expId, "Temp rule", "Catalog.");

    [Fact]
    public async Task Create_WithoutAdminModificationRulesManage_IsRejected()
    {
        await using var c = _factory.Create();
        var (sourceId, targetId, expId, code) = await SeedAsync(c);

        var denied = await Rules(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigView))
            .CreateAsync(Request(sourceId, targetId, expId, code));
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == ModificationRuleAuthorizationRule.CreateCode);
        Assert.False(await c.ModificationRules.AnyAsync(r => r.ModificationCode == code));

        var allowed = await Rules(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminModificationRulesManage))
            .CreateAsync(Request(sourceId, targetId, expId, code));
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(code, allowed.Value!.ModificationCode);
    }

    [Fact]
    public async Task Activate_WithoutAdminConfigActivate_IsRejected()
    {
        await using var c = _factory.Create();
        var (sourceId, targetId, expId, code) = await SeedAsync(c);
        var created = await Rules(c).CreateAsync(Request(sourceId, targetId, expId, code));
        Assert.True(created.Succeeded, created.Error);

        var denied = await Rules(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminModificationRulesManage))
            .ActivateAsync(created.Value!.Id, "Go live.");
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == ModificationRuleAuthorizationRule.ActivateCode);
        Assert.False((await c.ModificationRules.SingleAsync(r => r.Id == created.Value.Id)).IsActive);

        var allowed = await Rules(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigActivate))
            .ActivateAsync(created.Value.Id, "Go live.");
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.True(allowed.Value!.IsActive);
    }

    [Fact]
    public async Task CreateAndUpdate_WriteConfigure()
    {
        await using var c = _factory.Create();
        var (sourceId, targetId, expId, code) = await SeedAsync(c);
        var svc = Rules(c);

        var created = await svc.CreateAsync(Request(sourceId, targetId, expId, code));
        Assert.True(created.Succeeded, created.Error ?? created.Evaluation?.HardStops.FirstOrDefault()?.Message);
        Assert.True(await c.AuditEvents.AnyAsync(a =>
            a.EntityType == nameof(ModificationRule)
            && a.EntityId == created.Value!.Id
            && a.EventType == AuditEventType.Configure));

        var updated = await svc.UpdateAsync(
            created.Value!.Id,
            Request(sourceId, targetId, expId, code) with { Description = "Allow irradiate path.", ChangeReason = "Document allowed source-to-target path." });
        Assert.True(updated.Succeeded, updated.Error ?? updated.Evaluation?.HardStops.FirstOrDefault()?.Message);

        var events = await c.AuditEvents
            .Where(a => a.EntityType == nameof(ModificationRule) && a.EntityId == created.Value.Id)
            .ToListAsync();
        Assert.Equal(2, events.Count(a => a.EventType == AuditEventType.Configure));
    }
}
