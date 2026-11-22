using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

public class InstrumentLastMessageTextTests
{
    // TEST-BB-1335 (end-user testing pass 308): the instrument status says when the instrument last sent a message and how
    // long ago, or that none has arrived yet.
    [Fact]
    public void For_SaysWhenTheLastMessageArrived()
    {
        var now = new DateTime(2026, 11, 19, 20, 0, 0, DateTimeKind.Utc);
        var last = now.AddDays(-2).AddHours(-3);
        Assert.Equal($"Last message {ClinicalTimeText.Local(last)} (2 d ago)", InstrumentLastMessageText.For(last, now));
        Assert.Equal($"Last message {ClinicalTimeText.Local(now.AddSeconds(-20))} (just now)", InstrumentLastMessageText.For(now.AddSeconds(-20), now));
        Assert.Equal("No message received yet", InstrumentLastMessageText.For(null, now));
    }
}
