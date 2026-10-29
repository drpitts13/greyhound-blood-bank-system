using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class ShiftHandoffServiceTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public ShiftHandoffServiceTests(SqliteContextFactory factory) => _factory = factory;

    private ShiftHandoffService CreateService(
        BloodBankDbContext context,
        ICurrentUser? user = null,
        IPermissionEvaluator? permissions = null) =>
        new(
            new EfRepository<ShiftHandoff>(context),
            context,
            _factory.Clock,
            user ?? _factory.CurrentUser,
            audit: new AuditWriter(context, _factory.Clock, user ?? _factory.CurrentUser),
            permissions: permissions);

    [Fact]
    public async Task Create_WithoutAuditRead_IsHardStopped()
    {
        await using var context = _factory.Create();
        var note = $"deny-perm-{Guid.NewGuid():N}";
        var denied = await CreateService(context, permissions: new FixedPermissionEvaluator(1, PermissionCodes.ResultEnter))
            .CreateAsync(new CreateShiftHandoffRequest(note));
        Assert.False(denied.Succeeded);
        Assert.Equal(ShiftHandoffRule.EvaluatePermission(false).Message, denied.Error);
        Assert.DoesNotContain(context.ShiftHandoffs, h => h.Notes == note);
    }

    [Fact]
    public async Task Create_WithoutNotes_IsHardStopped()
    {
        await using var context = _factory.Create();
        var before = await context.ShiftHandoffs.CountAsync();
        var denied = await CreateService(context, permissions: new FixedPermissionEvaluator(1, PermissionCodes.AuditRead))
            .CreateAsync(new CreateShiftHandoffRequest("  "));
        Assert.False(denied.Succeeded);
        Assert.Equal(ShiftHandoffRule.EvaluateNotes("  ").Message, denied.Error);
        Assert.Equal(before, await context.ShiftHandoffs.CountAsync());
    }

    [Fact]
    public async Task Create_WritesSnapshotAndAudit()
    {
        await using var context = _factory.Create();
        var note = $"Two units in transit; fatality follow-up open. {Guid.NewGuid():N}";
        var created = await CreateService(context, permissions: new FixedPermissionEvaluator(1, PermissionCodes.AuditRead))
            .CreateAsync(new CreateShiftHandoffRequest(note, "tech2"));
        Assert.True(created.Succeeded, created.Error);
        Assert.Equal(note, created.Value!.Notes);
        Assert.Equal("tech2", created.Value.ToUser);
        Assert.Equal(_factory.CurrentUser.UserName, created.Value.FromUser);
        Assert.Null(created.Value.AcknowledgedUtc);
        Assert.True(await context.AuditEvents.AnyAsync(a =>
            a.EventType == AuditEventType.ShiftHandoff
            && a.EntityId == created.Value.Id
            && a.Reason == "Shift handoff recorded."));
        var open = await CreateService(context).GetLatestOpenAsync();
        Assert.NotNull(open);
        Assert.Equal(created.Value.Id, open!.Id);
    }

    [Fact]
    public async Task Acknowledge_SameUser_IsHardStopped()
    {
        await using var context = _factory.Create();
        var created = await CreateService(context, permissions: new FixedPermissionEvaluator(1, PermissionCodes.AuditRead))
            .CreateAsync(new CreateShiftHandoffRequest($"Handoff note {Guid.NewGuid():N}"));
        Assert.True(created.Succeeded, created.Error);

        var denied = await CreateService(context, permissions: new FixedPermissionEvaluator(1, PermissionCodes.AuditRead))
            .AcknowledgeAsync(created.Value!.Id);
        Assert.False(denied.Succeeded);
        Assert.Equal(
            ShiftHandoffRule.EvaluateAcknowledge(
                created.Value.FromUser, _factory.CurrentUser.UserName, null).Message,
            denied.Error);
        Assert.Null((await context.ShiftHandoffs.FindAsync(created.Value.Id))!.AcknowledgedUtc);
    }

    [Fact]
    public async Task Acknowledge_DifferentUser_SucceedsOnce()
    {
        await using var context = _factory.Create();
        var created = await CreateService(context, permissions: new FixedPermissionEvaluator(1, PermissionCodes.AuditRead))
            .CreateAsync(new CreateShiftHandoffRequest($"Handoff note {Guid.NewGuid():N}"));
        Assert.True(created.Succeeded, created.Error);

        var incoming = new TestCurrentUser("tech-night", "WORKSTATION-N");
        var acked = await CreateService(
                context,
                user: incoming,
                permissions: new FixedPermissionEvaluator(1, PermissionCodes.AuditRead))
            .AcknowledgeAsync(created.Value!.Id);
        Assert.True(acked.Succeeded, acked.Error);
        Assert.Equal("tech-night", acked.Value!.AcknowledgedBy);
        Assert.NotNull(acked.Value.AcknowledgedUtc);
        Assert.NotNull((await context.ShiftHandoffs.FindAsync(created.Value.Id))!.AcknowledgedUtc);

        var again = await CreateService(
                context,
                user: incoming,
                permissions: new FixedPermissionEvaluator(1, PermissionCodes.AuditRead))
            .AcknowledgeAsync(created.Value.Id);
        Assert.False(again.Succeeded);
        Assert.Equal(
            ShiftHandoffRule.EvaluateAcknowledge(
                created.Value.FromUser, "tech-night", acked.Value.AcknowledgedUtc).Message,
            again.Error);
    }
}
