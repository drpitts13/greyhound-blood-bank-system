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
}
