using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Billing;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class BillingAuthorizationTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public BillingAuthorizationTests(SqliteContextFactory factory) => _factory = factory;

    private BillingService Billing(BloodBankDbContext c, IPermissionEvaluator? permissions = null) =>
        new(
            new EfRepository<BillingEvent>(c),
            new EfRepository<ChargeRule>(c),
            new EfRepository<ChargeCode>(c),
            new EfRepository<TestServiceBilling>(c),
            new EfRepository<ProductBilling>(c),
            new EfRepository<TestResult>(c),
            new EfRepository<Issue>(c),
            new EfRepository<BloodUnit>(c),
            new EfRepository<ProductType>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            new NoOpPublisher(),
            permissionEvaluator: permissions);

    private static async Task<BillingEvent> SeedPendingAsync(BloodBankDbContext c)
    {
        var row = new BillingEvent
        {
            BillingCode = "BB-AUTH",
            TriggerType = BillingTriggerType.TestVerified,
            TriggerEntityType = nameof(TestResult),
            TriggerEntityId = 1,
            ServiceDateUtc = DateTime.UtcNow,
            Amount = 1m,
            SourceKind = BillingChargeSourceKind.ChargeRule,
            SourceId = 1,
            DedupeKey = Guid.NewGuid().ToString("N"),
            Status = BillingEventStatus.Pending
        };
        c.BillingEvents.Add(row);
        await c.SaveChangesAsync();
        return row;
    }

    [Fact]
    public async Task Review_WithoutBillingReview_IsRejected()
    {
        await using var c = _factory.Create();
        var pending = await SeedPendingAsync(c);

        var denied = await Billing(c, new FixedPermissionEvaluator(1, PermissionCodes.BillingCancel))
            .ReviewAsync(pending.Id);
        Assert.False(denied.Succeeded);
        Assert.Contains("billing.review", denied.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(BillingEventStatus.Pending, (await c.BillingEvents.SingleAsync(e => e.Id == pending.Id)).Status);

        var allowed = await Billing(c, new FixedPermissionEvaluator(1, PermissionCodes.BillingReview))
            .ReviewAsync(pending.Id);
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(BillingEventStatus.Reviewed, allowed.Value!.Status);
    }

    [Fact]
    public async Task Cancel_WithoutBillingCancel_IsRejected()
    {
        await using var c = _factory.Create();
        var pending = await SeedPendingAsync(c);

        var denied = await Billing(c, new FixedPermissionEvaluator(1, PermissionCodes.BillingReview))
            .CancelAsync(pending.Id, "Duplicate");
        Assert.False(denied.Succeeded);
        Assert.Contains("billing.cancel", denied.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(BillingEventStatus.Pending, (await c.BillingEvents.SingleAsync(e => e.Id == pending.Id)).Status);

        var allowed = await Billing(c, new FixedPermissionEvaluator(1, PermissionCodes.BillingCancel))
            .CancelAsync(pending.Id, "Duplicate");
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(BillingEventStatus.Cancelled, allowed.Value!.Status);
    }

    [Fact]
    public async Task ReviewAndCancel_WriteBilling()
    {
        await using var c = _factory.Create();
        var reviewRow = await SeedPendingAsync(c);
        var cancelRow = await SeedPendingAsync(c);
        var svc = Billing(c, new FixedPermissionEvaluator(
            1, PermissionCodes.BillingReview, PermissionCodes.BillingCancel));

        var reviewed = await svc.ReviewAsync(reviewRow.Id);
        Assert.True(reviewed.Succeeded, reviewed.Error);
        Assert.True(await c.AuditEvents.AnyAsync(a =>
            a.EntityType == nameof(BillingEvent)
            && a.EntityId == reviewRow.Id
            && a.EventType == AuditEventType.Billing));

        var cancelled = await svc.CancelAsync(cancelRow.Id, "Entered in error");
        Assert.True(cancelled.Succeeded, cancelled.Error);
        Assert.True(await c.AuditEvents.AnyAsync(a =>
            a.EntityType == nameof(BillingEvent)
            && a.EntityId == cancelRow.Id
            && a.EventType == AuditEventType.Billing
            && a.Reason == "Entered in error"));
    }

    [Fact]
    public async Task Export_WritesExport()
    {
        await using var c = _factory.Create();
        var pending = await SeedPendingAsync(c);
        var svc = Billing(c, new FixedPermissionEvaluator(
            1, PermissionCodes.BillingReview, PermissionCodes.BillingExport));

        var reviewed = await svc.ReviewAsync(pending.Id);
        Assert.True(reviewed.Succeeded, reviewed.Error);
        var exported = await svc.ExportAsync(pending.Id);
        Assert.True(exported.Succeeded, exported.Error);

        Assert.True(await c.AuditEvents.AnyAsync(a =>
            a.EntityType == nameof(BillingEvent)
            && a.EntityId == pending.Id
            && a.EventType == AuditEventType.Export));
    }

    private sealed class NoOpPublisher : IBillingInterfacePublisher
    {
        public Task<long?> PublishChargeAsync(BillingEvent billingEvent, CancellationToken ct = default) =>
            Task.FromResult<long?>(null);
    }
}
