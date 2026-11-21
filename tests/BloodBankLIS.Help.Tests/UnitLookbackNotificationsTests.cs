using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

public class UnitLookbackNotificationsTests
{
    // TEST-BB-1240: a unit page lists the Pending or Attempted lookback notifications for its own DIN (end-user testing
    // pass 218); completed ones, other DINs, and a unit with no DIN get none.
    [Fact]
    public void OpenFor_ListsOpenNotificationsForTheUnitsDonation()
    {
        LookbackNotificationDto Note(long id, string din, LookbackNotificationStatus status) =>
            new(id, din, 1, null, null, status, null, null, null, null);
        var notes = new[]
        {
            Note(1, "W123426000001", LookbackNotificationStatus.Pending),
            Note(2, "W123426000001", LookbackNotificationStatus.Attempted),
            Note(3, "W123426000001", LookbackNotificationStatus.Completed),
            Note(4, "W999999000001", LookbackNotificationStatus.Pending)
        };

        Assert.Equal([1L, 2L], UnitLookbackNotifications.OpenFor(" w123426000001 ", notes).Select(n => n.Id));
        Assert.Empty(UnitLookbackNotifications.OpenFor(null, notes));
        Assert.Empty(UnitLookbackNotifications.OpenFor("W123426000001", null));
    }
}
