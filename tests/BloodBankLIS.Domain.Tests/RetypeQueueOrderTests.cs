using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class RetypeQueueOrderTests
{
    private static readonly DateTime Now = new(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);

    // TEST-BB-1154: the pending retype board puts units in date first, soonest-expiring first, then outdated units;
    // ties keep the received order (end-user testing pass 132: the board listed by received time only).
    [Fact]
    public void Sort_PutsSoonestExpiringFirst_AndOutdatedLast()
    {
        var items = new[]
        {
            (Id: "old-long", Expires: (DateTime?)Now.AddDays(30), Received: Now.AddDays(-5)),
            (Id: "outdated", Expires: (DateTime?)Now.AddHours(-1), Received: Now.AddDays(-6)),
            (Id: "new-short", Expires: (DateTime?)Now.AddHours(3), Received: Now.AddHours(-1)),
            (Id: "no-expiry", Expires: (DateTime?)null, Received: Now.AddDays(-7)),
        };

        var sorted = RetypeQueueOrder.Sort(items, i => i.Expires, i => i.Received, Now);

        Assert.Equal(["new-short", "old-long", "no-expiry", "outdated"], sorted.Select(i => i.Id));
    }

    [Fact]
    public void Notes_SayTimeLeftAndTimeWaiting()
    {
        Assert.Equal("expires in 3 h", RetypeQueueOrder.ExpiryNote(Now.AddHours(2.5), Now));
        Assert.Equal("expires in 5 d", RetypeQueueOrder.ExpiryNote(Now.AddDays(5.5), Now));
        Assert.Equal("outdated", RetypeQueueOrder.ExpiryNote(Now, Now));
        Assert.Equal(string.Empty, RetypeQueueOrder.ExpiryNote(null, Now));
        Assert.Equal("waiting 6 h", RetypeQueueOrder.WaitingNote(Now.AddHours(-6), Now));
        Assert.Equal("waiting 9 d", RetypeQueueOrder.WaitingNote(Now.AddDays(-9), Now));
    }
}
