using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-559: a lot is not received already expired or under a number in use, not opened once expired or closed, and an
// expired lot takes no QC; the lot table shows days left.
public class ReagentLotRuleTests
{
    private static readonly DateTime Now = new(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Receive_RefusesAnExpiredLot_AndANumberInUse()
    {
        Assert.Equal(RuleSeverity.Pass, ReagentLotRule.EvaluateReceive(Now.AddDays(30), Now, numberInUse: false).Severity);

        var expired = ReagentLotRule.EvaluateReceive(Now.AddHours(-1), Now, numberInUse: false);
        Assert.Equal(RuleSeverity.HardStop, expired.Severity);
        Assert.Contains("expired 2026-11-07", expired.Message);

        var duplicate = ReagentLotRule.EvaluateReceive(Now.AddDays(30), Now, numberInUse: true);
        Assert.Equal(RuleSeverity.HardStop, duplicate.Severity);
        Assert.Contains("already has", duplicate.Message);
    }

    [Fact]
    public void OpenAndQc_RefuseExpiredOrClosedLots()
    {
        Assert.Equal(RuleSeverity.Pass, ReagentLotRule.EvaluateOpen(ReagentLotState.Received, Now.AddDays(1), Now).Severity);
        Assert.Equal(RuleSeverity.HardStop, ReagentLotRule.EvaluateOpen(ReagentLotState.Received, Now.AddDays(-1), Now).Severity);
        Assert.Equal(RuleSeverity.HardStop, ReagentLotRule.EvaluateOpen(ReagentLotState.Closed, Now.AddDays(1), Now).Severity);

        Assert.Equal(RuleSeverity.Pass, ReagentLotRule.EvaluateQc(ReagentLotState.Opened, Now.AddDays(1), Now).Severity);
        var expired = ReagentLotRule.EvaluateQc(ReagentLotState.Opened, Now.AddDays(-2), Now);
        Assert.Equal(RuleSeverity.HardStop, expired.Severity);
        Assert.Contains("close it", expired.Message);
        Assert.Equal(RuleSeverity.HardStop, ReagentLotRule.EvaluateQc(ReagentLotState.Closed, Now.AddDays(1), Now).Severity);
    }

    [Fact]
    public void ExpiryText_CountsDaysLeft_AndFlagsExpiringSoon()
    {
        Assert.Equal("expired 2026-11-05", ReagentLotRule.ExpiryText(Now.AddDays(-2), Now));
        Assert.Equal("expires today", ReagentLotRule.ExpiryText(Now.AddHours(6), Now));
        Assert.Equal("1 day left", ReagentLotRule.ExpiryText(Now.AddDays(1), Now));
        Assert.Equal("12 days left", ReagentLotRule.ExpiryText(Now.AddDays(12), Now));

        Assert.True(ReagentLotRule.ExpiringSoon(Now.AddDays(7), Now));
        Assert.False(ReagentLotRule.ExpiringSoon(Now.AddDays(8), Now));
        Assert.False(ReagentLotRule.ExpiringSoon(Now.AddDays(-1), Now));
    }

    // TEST-BB-560: an open lot within a week of expiry, with no later lot of its reagent on hand, is a Replace item.
    [Fact]
    public void Worklist_AsksToReplaceAnOpenLotNearExpiry_OnlyWhenNoLaterLotIsOnHand()
    {
        static ReagentQcLotFact Lot(long id, long reagentId, ReagentLotState state, DateTime expires) =>
            new(id, $"L-{id}", state, expires, reagentId, reagentId == 1 ? "AHG" : "ANTI-A");

        var items = ReagentQcWorklistRule.Evaluate(
        [
            Lot(1, 1, ReagentLotState.Opened, Now.AddDays(3)),   // AHG: nothing later -> Replace
            Lot(2, 2, ReagentLotState.Opened, Now.AddDays(3)),   // ANTI-A: next lot received -> no Replace
            Lot(3, 2, ReagentLotState.Received, Now.AddDays(60)),
            Lot(4, 1, ReagentLotState.Closed, Now.AddDays(90)),  // a closed later lot does not count
            Lot(5, 3, ReagentLotState.Opened, Now.AddDays(20)),  // not near expiry
            Lot(6, 3, ReagentLotState.Received, Now.AddDays(2))  // received, not opened: no Replace
        ], [], Now);

        var replace = Assert.Single(items, i => i.Kind == ReagentQcWorklistRule.Replace);
        Assert.Equal(1, replace.LotId);
        Assert.Equal("Lot L-1 3 days left (expires 2026-11-10) and no later lot of AHG is received: receive the next lot.", replace.Message);
        Assert.Contains(items, i => i.Kind == ReagentQcWorklistRule.Overdue && i.LotId == 1);
    }
}
