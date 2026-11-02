using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-332: overrides are reviewed after the fact by a second person with override.approve, from the supervisor queue.
public class OverrideReviewTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private OverrideReviewService Service(BloodBankDbContext c, string user, params string[] permissions)
    {
        var current = new TestCurrentUser(user, "WS-SUP");
        return new OverrideReviewService(new EfRepository<Override>(c), c, _factory.Clock, current,
            new AuditWriter(c, _factory.Clock, current), new FixedPermissionEvaluator(2, permissions));
    }

    private async Task<long> SeedOverrideAsync(BloodBankDbContext c)
    {
        // Stamped CreatedBy = the factory user ("tech-test"): the person who performed the override.
        var o = new Override
        {
            Action = OverrideAction.EmergencyRelease, ContextType = nameof(Issue), ContextId = 42,
            RuleCode = "ISS-XM-REQUIRED", Reason = "Massive hemorrhage", AuthorizedBy = "dr-lee",
            OverriddenUtc = _factory.Clock.UtcNow.AddHours(-1)
        };
        c.Overrides.Add(o);
        await c.SaveChangesAsync();
        return o.Id;
    }

    [Fact]
    public async Task Review_BySecondPerson_IsRecorded_Audited_AndLeavesTheQueue()
    {
        await using var c = _factory.Create();
        var id = await SeedOverrideAsync(c);
        var svc = Service(c, "supervisor1", PermissionCodes.OverrideApprove);
        var pending = Assert.Single(await svc.ListPendingAsync(), o => o.Id == id);
        Assert.Equal("tech-test", pending.PerformedBy);
        Assert.Equal("/inventory/units/42", pending.ContextHref);

        var reviewed = await svc.ReviewAsync(id, new ReviewOverrideRequest(OverrideReviewOutcome.FollowUpRequired, "Ask why O-neg stock was not used"));

        Assert.True(reviewed.Succeeded, reviewed.Error);
        Assert.Equal("supervisor1", reviewed.Value!.ReviewedBy);
        Assert.Equal(OverrideReviewOutcome.FollowUpRequired, reviewed.Value.ReviewOutcome);
        Assert.DoesNotContain(await svc.ListPendingAsync(), o => o.Id == id);
        Assert.True(await c.AuditEvents.AnyAsync(a => a.EventType == AuditEventType.Override && a.EntityType == nameof(Override)
            && a.EntityId == id && a.Reason == "Ask why O-neg stock was not used"));

        var again = await svc.ReviewAsync(id, new ReviewOverrideRequest(OverrideReviewOutcome.Acceptable, null));
        Assert.True(again.Evaluation!.IsHardStopped);
    }

    [Fact]
    public async Task Review_ByPerformerAuthorizerOrWithoutPermission_IsRefused()
    {
        await using var c = _factory.Create();
        var id = await SeedOverrideAsync(c);

        var performer = await Service(c, "tech-test", PermissionCodes.OverrideApprove)
            .ReviewAsync(id, new ReviewOverrideRequest(OverrideReviewOutcome.Acceptable, null));
        var authorizer = await Service(c, "DR-LEE", PermissionCodes.OverrideApprove)
            .ReviewAsync(id, new ReviewOverrideRequest(OverrideReviewOutcome.Acceptable, null));
        var noPermission = await Service(c, "supervisor1", PermissionCodes.IssueOverride)
            .ReviewAsync(id, new ReviewOverrideRequest(OverrideReviewOutcome.Acceptable, null));
        var noNote = await Service(c, "supervisor1", PermissionCodes.OverrideApprove)
            .ReviewAsync(id, new ReviewOverrideRequest(OverrideReviewOutcome.FollowUpRequired, null));

        Assert.All([performer, authorizer, noPermission, noNote], r => Assert.True(r.Evaluation!.IsHardStopped));
        Assert.Null((await c.Overrides.AsNoTracking().SingleAsync(o => o.Id == id)).ReviewedUtc);
    }

    [Fact]
    public async Task SupervisorQueue_ListsUnreviewedOverrides_OnlyForOverrideApprove()
    {
        await using var c = _factory.Create();
        var id = await SeedOverrideAsync(c);
        var reviewer = new TestCurrentUser("supervisor1", "WS-SUP");

        var withApprove = await new SupervisorQueueService(reviewer,
                permissions: new FixedPermissionEvaluator(2, PermissionCodes.OverrideApprove),
                overrides: Service(c, "supervisor1", PermissionCodes.OverrideApprove))
            .ListAsync();
        Assert.True(withApprove.Succeeded, withApprove.Error);
        var item = Assert.Single(withApprove.Value!, i => i.Kind == SupervisorQueueRule.KindOverrideReview);
        Assert.Equal(id, item.ContextId);
        Assert.Contains("Emergency release", item.Title);
        Assert.Contains("Massive hemorrhage", item.Detail);

        var withoutApprove = await new SupervisorQueueService(reviewer,
                permissions: new FixedPermissionEvaluator(2, PermissionCodes.ResultVerify),
                overrides: Service(c, "supervisor1", PermissionCodes.ResultVerify))
            .ListAsync();
        Assert.DoesNotContain(withoutApprove.Value!, i => i.Kind == SupervisorQueueRule.KindOverrideReview);
    }
}
