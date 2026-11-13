using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class LookbackNotifyRuleTests
{
    // End-user testing pass 66: an open lookback notification's notify-by date is the day it was opened plus the
    // facility's days (OCD-089); none while not set, or once it is completed or not applicable.
    [Fact]
    public void NotifyBy_FollowsTheFacilityDays()
    {
        var opened = new DateTime(2026, 11, 6, 9, 50, 0, DateTimeKind.Utc);
        Assert.Equal(opened.AddDays(84), LookbackNotifyRule.NotifyBy(opened, 84, LookbackNotificationStatus.Pending));
        Assert.Equal(opened.AddDays(84), LookbackNotifyRule.NotifyBy(opened, 84, LookbackNotificationStatus.Attempted));
        Assert.Null(LookbackNotifyRule.NotifyBy(opened, 0, LookbackNotificationStatus.Pending));
        Assert.Null(LookbackNotifyRule.NotifyBy(opened, 84, LookbackNotificationStatus.Completed));
        Assert.Null(LookbackNotifyRule.NotifyBy(opened, 84, LookbackNotificationStatus.NotApplicable));
        Assert.Null(LookbackNotifyRule.NotifyBy(default, 84, LookbackNotificationStatus.Pending));
    }
}
