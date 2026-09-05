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

public class ProductBillingAuthorizationTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public ProductBillingAuthorizationTests(SqliteContextFactory factory) => _factory = factory;

    private ProductBillingAdminService Rows(BloodBankDbContext c, IPermissionEvaluator? permissions = null)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new ProductBillingAdminService(
            new EfRepository<ProductBilling>(c),
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
            Code = $"P{Guid.NewGuid():N}"[..8].ToUpperInvariant(),
            Description = "Product charge",
            DefaultAmount = 1m
        };
        c.ChargeCodes.Add(code);
        await c.SaveChangesAsync();
        return code;
    }

    private static string UniqueIsbt() => $"T{Guid.NewGuid():N}"[..5].ToUpperInvariant();

    [Fact]
    public async Task Create_WithoutAdminConfigEdit_IsRejected()
    {
        await using var c = _factory.Create();
        var code = await SeedCodeAsync(c);
        var isbt = UniqueIsbt();
        var request = new SaveProductBillingRequest(code.Id, null, BillingTriggerType.UnitIssued, isbt);

        var denied = await Rows(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigView))
            .CreateAsync(request);
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == ProductBillingAuthorizationRule.CreateCode);
        Assert.False(await c.ProductBillings.AnyAsync(e => e.IsbtProductCode == isbt));

        var allowed = await Rows(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigEdit))
            .CreateAsync(request);
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(isbt, allowed.Value!.IsbtProductCode);
        Assert.True(allowed.Value.IsActive);
    }

    [Fact]
    public async Task Deactivate_WithoutAdminConfigActivate_IsRejected()
    {
        await using var c = _factory.Create();
        var code = await SeedCodeAsync(c);
        var created = await Rows(c).CreateAsync(
            new SaveProductBillingRequest(code.Id, null, BillingTriggerType.UnitIssued, UniqueIsbt()));
        Assert.True(created.Succeeded, created.Error);
        Assert.True(created.Value!.IsActive);

        var denied = await Rows(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigEdit))
            .SetActiveAsync(created.Value.Id, false);
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == ProductBillingAuthorizationRule.DeactivateCode);
        Assert.True((await c.ProductBillings.SingleAsync(e => e.Id == created.Value.Id)).IsActive);

        var allowed = await Rows(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigActivate))
            .SetActiveAsync(created.Value.Id, false);
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.False(allowed.Value!.IsActive);
    }

    [Fact]
    public async Task CreateAndUpdate_WriteConfigure()
    {
        await using var c = _factory.Create();
        var code = await SeedCodeAsync(c);
        var isbt = UniqueIsbt();
        var svc = Rows(c);

        var created = await svc.CreateAsync(
            new SaveProductBillingRequest(code.Id, null, BillingTriggerType.UnitIssued, isbt));
        Assert.True(created.Succeeded, created.Error ?? created.Evaluation?.HardStops.FirstOrDefault()?.Message);
        Assert.True(await c.AuditEvents.AnyAsync(a =>
            a.EntityType == nameof(ProductBilling)
            && a.EntityId == created.Value!.Id
            && a.EventType == AuditEventType.Configure));

        var updated = await svc.UpdateAsync(
            created.Value!.Id,
            new SaveProductBillingRequest(code.Id, "Issued RBC", BillingTriggerType.UnitIssued, isbt));
        Assert.True(updated.Succeeded, updated.Error ?? updated.Evaluation?.HardStops.FirstOrDefault()?.Message);

        var events = await c.AuditEvents
            .Where(a => a.EntityType == nameof(ProductBilling) && a.EntityId == created.Value.Id)
            .ToListAsync();
        Assert.Equal(2, events.Count(a => a.EventType == AuditEventType.Configure));
    }
}
