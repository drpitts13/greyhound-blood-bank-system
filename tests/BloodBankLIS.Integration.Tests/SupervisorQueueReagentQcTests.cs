using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;

namespace BloodBankLIS.Integration.Tests;

public class SupervisorQueueReagentQcTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    // TEST-BB-1200: failed and unreviewed reagent QC wait on the supervisor queue for a verifier, the failed one marked
    // first; overdue QC stays on Reagent QC; without result.verify none is listed (end-user testing pass 177).
    [Fact]
    public async Task FailedAndUnreviewedQc_WaitOnTheSupervisorQueue()
    {
        await using var context = _factory.Create();
        var now = _factory.Clock.UtcNow;
        var reagent = new Reagent { Code = $"SQ-{Guid.NewGuid():N}"[..10], Name = "Supervisor anti-D" };
        context.Reagents.Add(reagent);
        await context.SaveChangesAsync();
        var lot = new ReagentLot { ReagentId = reagent.Id, LotNumber = "SQ-1", ReceivedUtc = now.AddDays(-3), ExpiresUtc = now.AddDays(30), State = ReagentLotState.Opened };
        context.ReagentLots.Add(lot);
        await context.SaveChangesAsync();
        context.ReagentDailyQcs.Add(new ReagentDailyQc
        {
            ReagentLotId = lot.Id, QcDateUtc = now.Date, ExpectedResult = "3+", ObservedResult = "0", Passed = false,
            PerformedBy = "tech1", PerformedUtc = now.AddHours(-1)
        });
        // Pass 224: a QC the signed-in user performed.
        var ownLot = new ReagentLot { ReagentId = reagent.Id, LotNumber = "SQ-2", ReceivedUtc = now.AddDays(-3), ExpiresUtc = now.AddDays(30), State = ReagentLotState.Opened };
        context.ReagentLots.Add(ownLot);
        await context.SaveChangesAsync();
        context.ReagentDailyQcs.Add(new ReagentDailyQc
        {
            ReagentLotId = ownLot.Id, QcDateUtc = now.Date, ExpectedResult = "3+", ObservedResult = "3+", Passed = true,
            PerformedBy = _factory.CurrentUser.UserName, PerformedUtc = now.AddHours(-1)
        });
        await context.SaveChangesAsync();

        var audit = new AuditWriter(context, _factory.Clock, _factory.CurrentUser);
        var reagents = new ReagentQcService(
            new EfRepository<Reagent>(context), new EfRepository<ReagentLot>(context), new EfRepository<ReagentDailyQc>(context),
            context, _factory.Clock, _factory.CurrentUser, audit, new EfRepository<TestResultReagentLot>(context));

        var verifier = await new SupervisorQueueService(_factory.CurrentUser,
            permissions: new FixedPermissionEvaluator(1, PermissionCodes.ResultVerify), reagentQc: reagents).ListAsync();
        var qc = verifier.Value!.Where(i => i.Kind == SupervisorQueueRule.KindReagentQc).ToList();
        Assert.Contains(qc, i => i.Title == "Reagent QC failed — lot SQ-1" && i.DueSoon && i.Href == "/reagents");
        Assert.Contains(qc, i => i.Title == "Reagent QC unreviewed (failed) — lot SQ-1" && i.DueSoon);
        Assert.DoesNotContain(qc, i => i.Title.Contains("overdue", StringComparison.Ordinal));
        // TEST-BB-1246 (end-user testing pass 224): QC the signed-in user performed is not offered to them to review.
        var own = Assert.Single(qc, i => i.Title == "Reagent QC unreviewed — lot SQ-2");
        Assert.Equal("Open", own.NextAction);
        Assert.EndsWith("You performed it: another person reviews it.", own.Detail);
        Assert.Equal("Review", Assert.Single(qc, i => i.Title == "Reagent QC unreviewed (failed) — lot SQ-1").NextAction);

        var releaseOnly = await new SupervisorQueueService(_factory.CurrentUser,
            permissions: new FixedPermissionEvaluator(1, PermissionCodes.InventoryRelease), reagentQc: reagents).ListAsync();
        Assert.DoesNotContain(releaseOnly.Value!, i => i.Kind == SupervisorQueueRule.KindReagentQc);
    }
}
