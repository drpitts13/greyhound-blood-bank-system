using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class RushTurnaroundRuleTests
{
    private static readonly DateTime Now = new(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);

    // TEST-BB-1121: a rush test not yet verified is past the target only when it has waited longer than a target that is
    // set; routine, verified or corrected tests and a target of 0 are never marked (end-user testing pass 106).
    [Fact]
    public void Notice_OnlyForUnverifiedRushTestsPastASetTarget()
    {
        Assert.Equal("Past the 60 min target: waiting 1 h 30 min",
            RushTurnaroundRule.Notice(OrderPriority.Stat, Now.AddMinutes(-90), ResultStatus.Entered, 60, Now));
        Assert.Equal("Past the 30 min target: waiting 3 d 2 h",
            RushTurnaroundRule.Notice(OrderPriority.MassiveTransfusionProtocol, Now.AddHours(-74), null, 30, Now));
        Assert.Null(RushTurnaroundRule.Notice(OrderPriority.Stat, Now.AddMinutes(-60), null, 60, Now));
        Assert.Null(RushTurnaroundRule.Notice(OrderPriority.Stat, Now.AddMinutes(-90), null, 0, Now));
        Assert.Null(RushTurnaroundRule.Notice(OrderPriority.Routine, Now.AddDays(-2), null, 60, Now));
        Assert.Null(RushTurnaroundRule.Notice(OrderPriority.Urgent, Now.AddDays(-2), ResultStatus.Verified, 60, Now));
        Assert.Null(RushTurnaroundRule.Notice(OrderPriority.Urgent, Now.AddDays(-2), ResultStatus.Corrected, 60, Now));
        Assert.Equal("45 min", RushTurnaroundRule.Duration(TimeSpan.FromMinutes(45)));
    }
}
