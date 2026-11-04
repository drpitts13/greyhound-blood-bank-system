using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-392 (OCD-054)
public class DowntimeBackEntryRuleTests
{
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NotRequested_Passes_RequestedNeedsPermissionAndAnEarlierTime()
    {
        Assert.Equal(RuleSeverity.Pass, DowntimeBackEntryRule.Evaluate(false, false, Now, Now).Severity);
        Assert.Equal(DowntimeBackEntryRule.PermissionCode, DowntimeBackEntryRule.Evaluate(true, false, Now.AddHours(-3), Now).Code);
        Assert.Equal(RuleSeverity.HardStop, DowntimeBackEntryRule.Evaluate(true, false, Now.AddHours(-3), Now).Severity);
        var notEarlier = DowntimeBackEntryRule.Evaluate(true, true, Now, Now);
        Assert.Equal((DowntimeBackEntryRule.TimeCode, RuleSeverity.HardStop), (notEarlier.Code, notEarlier.Severity));
        Assert.Equal(RuleSeverity.Pass, DowntimeBackEntryRule.Evaluate(true, true, Now.AddHours(-3), Now).Severity);
    }

    [Fact]
    public void GateTime_IsThePaperTimeOnlyForABackEntry()
    {
        Assert.Equal(Now.AddHours(-3), DowntimeBackEntryRule.GateTime(true, Now.AddHours(-3), Now));
        Assert.Equal(Now, DowntimeBackEntryRule.GateTime(false, Now.AddHours(-3), Now));
    }

    [Fact]
    public void ExpiredStatus_CountsAsIssuable_OnlyIfInDateAtThePaperTime()
    {
        Assert.Equal(UnitStatus.Available, DowntimeBackEntryRule.StatusForGate(true, UnitStatus.Expired, Now.AddHours(-1), Now.AddHours(-3)));
        Assert.Equal(UnitStatus.Expired, DowntimeBackEntryRule.StatusForGate(true, UnitStatus.Expired, Now.AddHours(-4), Now.AddHours(-3)));
        Assert.Equal(UnitStatus.Expired, DowntimeBackEntryRule.StatusForGate(false, UnitStatus.Expired, Now.AddHours(-1), Now.AddHours(-3)));
        Assert.Equal(UnitStatus.Discarded, DowntimeBackEntryRule.StatusForGate(true, UnitStatus.Discarded, Now.AddHours(-1), Now.AddHours(-3)));
    }

    [Fact]
    public void ReservationEndedBySweep_CountsOnlyForABackEntry_MadeBeforeThePaperTime()
    {
        Assert.True(DowntimeBackEntryRule.ReservationCounts(false, AllocationStatus.Reserved, Now, Now));
        Assert.True(DowntimeBackEntryRule.ReservationCounts(true, AllocationStatus.Expired, Now.AddHours(-5), Now.AddHours(-3)));
        Assert.False(DowntimeBackEntryRule.ReservationCounts(true, AllocationStatus.Expired, Now.AddHours(-1), Now.AddHours(-3)));
        Assert.False(DowntimeBackEntryRule.ReservationCounts(false, AllocationStatus.Expired, Now.AddHours(-5), Now.AddHours(-3)));
        Assert.False(DowntimeBackEntryRule.ReservationCounts(true, AllocationStatus.Released, Now.AddHours(-5), Now.AddHours(-3)));
    }

    // TEST-BB-394
    [Fact]
    public void FollowOn_OnlyForABackEnteredIssue_WithThePermission_AndTheRightDin()
    {
        Assert.Equal(DowntimeBackEntryRule.NotBackEntryCode,
            DowntimeBackEntryRule.EvaluateFollowOn(false, true, "W1", "W1", null).Code);
        Assert.Equal(DowntimeBackEntryRule.PermissionCode,
            DowntimeBackEntryRule.EvaluateFollowOn(true, false, "W1", "W1", null).Code);
        Assert.Equal(RuleSeverity.HardStop, DowntimeBackEntryRule.EvaluateFollowOn(true, true, "W2", "W1", null).Severity);
        Assert.Equal(RuleSeverity.Pass, DowntimeBackEntryRule.EvaluateFollowOn(true, true, "w1", "W1", null).Severity);
    }

    [Theory]
    [InlineData("W0000 26 123456", true)]
    [InlineData("w000026123456", true)]
    [InlineData("W0000 26 999999", false)]
    [InlineData(" ", false)]
    public void ConfirmDin_MatchesTheUnit_IgnoringSpacesAndCase(string typed, bool ok) =>
        Assert.Equal(ok ? RuleSeverity.Pass : RuleSeverity.HardStop,
            DowntimeBackEntryRule.ConfirmDin(typed, "W0000 26 123456", null).Severity);
}
