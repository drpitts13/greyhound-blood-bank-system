using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class MtpIdleRuleTests
{
    private static readonly DateTime Now = new(2026, 11, 9, 18, 0, 0, DateTimeKind.Utc);

    // TEST-BB-713: idle time runs from the last issue under the protocol (or its activation); 0 turns the reminder off;
    // the notice says how long and suggests closing it.
    [Fact]
    public void IdleTime_RunsFromTheLastIssue_AndZeroIsOff()
    {
        var activated = Now.AddHours(-6);
        Assert.Equal(activated, MtpIdleRule.LastActivityUtc(activated, null));
        Assert.Equal(Now.AddHours(-1), MtpIdleRule.LastActivityUtc(activated, Now.AddHours(-1)));

        Assert.True(MtpIdleRule.IsIdle(activated, Now, 240));
        Assert.False(MtpIdleRule.IsIdle(Now.AddHours(-1), Now, 240));
        Assert.False(MtpIdleRule.IsIdle(activated, Now, 0));

        Assert.Equal("No unit issued under it for 6 h. Close it if the bleeding is controlled.", MtpIdleRule.Notice(activated, Now, anyIssued: true));
        Assert.Equal("No unit issued since it was called 90 min ago. Close it if it is no longer needed.", MtpIdleRule.Notice(Now.AddMinutes(-90), Now, anyIssued: false));

        var alert = MtpIdleRule.Alert(7, " MRN0021 ", activated, Now, anyIssued: true);
        Assert.Equal(("MTPIDLE:7", AlertRule.MtpStillOpen, AlertSeverity.Warning), (alert.Key, alert.Source, alert.Severity));
        Assert.Equal("Massive transfusion protocol still open: MRN MRN0021", alert.Title);
    }
}
